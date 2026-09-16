using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Vulkan renderer implementation that orchestrates frame rendering.
/// Manages image acquisition, command recording, and presentation.
/// Uses ContentManager to get active cameras for rendering.
/// </summary>
public unsafe class Renderer(
    Context context,
    ISwapChain swapChain,
    ISyncManager syncManager,
    IPipelineRegistry pipelineManager,
    ILogger<Renderer> logger
) : IRenderer, IDisposable
{
    private const string VK_CONTEXT_NULL = "Vulkan _context has not been initialized yet.";

    private Context _context = context;
    private ISwapChain _swapChain = swapChain;
    private ISyncManager _syncManager = syncManager;
    private IPipelineRegistry _pipelineManager = pipelineManager;
    private readonly ILogger<Renderer> _logger = logger;
    private CommandBufferPool _commandPool = CommandBufferPool.ForGraphics(context, 2);
    private FrameSync? _frameSync;
    private ImageSync? _imageSync;
    private uint _imageIndex;
    private CommandBuffer _commandBuffer;
    private uint _currentFrameIndex = 0;
    private bool _disposed;
    private readonly InstanceBuffer[] _instanceBuffers = Enumerable
        .Range(0, checked((int)syncManager.MaxFramesInFlight))
        .Select(_ => new InstanceBuffer(context))
        .ToArray();

    public event EventHandler<RenderEventArgs>? BeforeRendering;
    public event EventHandler<RenderEventArgs>? AfterRendering;

    public VulkanRenderLayer[] Layers { get; set; } = [];

    public bool CanRender() =>
        _context != null
        && _swapChain != null
        && Layers.Length > 0
        && _swapChain.SwapchainExtent.Width > 0
        && _swapChain.SwapchainExtent.Height > 0;

    public bool Render()
    {
        try
        {
            if (!CanRender())
                return false;

            if (!PrepareFrame())
                return false;

            BeforeRendering?.Invoke(this, new RenderEventArgs(_imageIndex));

            if (!BeginCommandBuffer())
                return false;

            foreach (var layer in Layers)
            {
                ValidateRenderPasses(layer);

                var viewport = layer.Viewport;
                _context.VulkanApi.CmdSetViewport(_commandBuffer, 0, 1, &viewport);

                var scissor = layer.Scissor;
                _context.VulkanApi.CmdSetScissor(_commandBuffer, 0, 1, &scissor);

                foreach (var pass in layer.RenderPasses)
                {
                    if (!pass.ShouldRender)
                        continue;

                    var clearValues = pass.ClearValues;

                    fixed (ClearValue* clearValuesPointer = clearValues)
                    {
                        BeginRenderPass(
                            RenderPasses.GetIndex(pass.RenderPass),
                            (uint)clearValues.Length,
                            clearValuesPointer
                        );
                    }

                    ulong lastPipelineId = 0;
                    ulong lastDescriptorSetHandle = 0;

                    foreach (var command in layer.Items)
                    {
                        if ((command.RenderMask & pass.RenderPass) != 0)
                            Draw(command, ref lastPipelineId, ref lastDescriptorSetHandle);
                    }

                    _context.VulkanApi.CmdEndRenderPass(_commandBuffer);
                }
            }

            if (_context.VulkanApi.EndCommandBuffer(_commandBuffer) != Result.Success)
            {
                // TODO: Clean up allocated resources before exiting
                // so the CommandBuffer and sync fence can be released
                return false;
            }

            SubmitFrame();
            PresentFrame();
            AfterRendering?.Invoke(this, new RenderEventArgs(_imageIndex));

            _currentFrameIndex = (_currentFrameIndex + 1) % _syncManager.MaxFramesInFlight;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Vulkan rendering failed; closing the window.");
            _context.Window.Close();
            throw;
        }

        return true;
    }

    /// <summary>
    /// Releases the persistently mapped instance upload buffers owned by this renderer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _context.VulkanApi.DeviceWaitIdle(_context.Device);

        foreach (var instanceBuffer in _instanceBuffers)
        {
            instanceBuffer.Dispose();
        }

        _disposed = true;
    }

    private void ValidateRenderPasses(VulkanRenderLayer definition)
    {
        if (definition.RenderPasses.Length == 0)
            throw new InvalidOperationException(
                "A render layer must define at least one render pass."
            );

        foreach (var pass in definition.RenderPasses)
        {
            if (pass.RenderPass == 0)
                throw new InvalidOperationException(
                    "A render pass definition must specify a render pass."
                );

            if (RenderPasses.GetIndex(pass.RenderPass) < 0)
                throw new InvalidOperationException(
                    $"Render pass mask 0x{pass.RenderPass:X} must specify exactly one render pass."
                );

            if (pass.ClearValues.Length == 0)
                throw new InvalidOperationException(
                    $"Render pass {RenderPasses.GetName(pass.RenderPass)} must define clear values."
                );
        }
    }

    /// <summary>
    /// Prepares frame synchronization and acquires the next _swapChain image.
    /// </summary>
    /// <returns>Frame sync, image index, and image sync objects.</returns>
    private bool PrepareFrame()
    {
        _frameSync = _syncManager.GetFrameSync(_currentFrameIndex);

        // This frame slot is no longer being used by the GPU.
        _syncManager.WaitForFence(_frameSync.InFlightFence);
        _instanceBuffers[_currentFrameIndex].Reset();

        if (!_commandPool.TryGetCommandBuffer(_frameSync.InFlightFence, out _commandBuffer))
            return false;

        _imageIndex = _swapChain.AcquireNextImage(_frameSync.ImageAvailable, out var result);

        if (result == Result.ErrorOutOfDateKhr)
        {
            _swapChain.Recreate();
            return false;
        }

        if (result != Result.Success && result != Result.SuboptimalKhr)
        {
            throw new InvalidOperationException($"Failed to acquire swapchain image: {result}");
        }

        _imageSync = _syncManager.GetImageSync(_imageIndex);

        _syncManager.ResetFence(_frameSync.InFlightFence);
        return true;
    }

    private bool BeginCommandBuffer()
    {
        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };

        var result = _context.VulkanApi.BeginCommandBuffer(_commandBuffer, &beginInfo);

        return result == Result.Success;
    }

    private void BeginRenderPass(int index, uint clearValueCount, ClearValue* passClearValues)
    {
        var renderPassInfo = new RenderPassBeginInfo
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _swapChain.Passes[index],
            Framebuffer = _swapChain.Framebuffers[index][_imageIndex],
            RenderArea = new Rect2D
            {
                Offset = new Offset2D(0, 0),
                Extent = _swapChain.SwapchainExtent,
            },
            ClearValueCount = clearValueCount,
            PClearValues = passClearValues,
        };

        _context.VulkanApi.CmdBeginRenderPass(
            _commandBuffer,
            &renderPassInfo,
            SubpassContents.Inline
        );
    }

    private void Draw(RenderItem cmd, ref ulong lastPipelineId, ref ulong lastDescriptorSetHandle)
    {
        if (cmd.InstanceCount == 0)
            return;

        _context.VulkanApi.CmdBindPipeline(
            _commandBuffer,
            PipelineBindPoint.Graphics,
            cmd.Pipeline
        );

        if (cmd.DescriptorSet.Handle != 0 && cmd.DescriptorSet.Handle != lastDescriptorSetHandle)
        {
            var descriptorSet = cmd.DescriptorSet;

            _context.VulkanApi.CmdBindDescriptorSets(
                _commandBuffer,
                PipelineBindPoint.Graphics,
                cmd.Layout,
                0,
                1,
                &descriptorSet,
                0,
                null
            );

            lastDescriptorSetHandle = descriptorSet.Handle;
        }

        if (cmd.PushConstants != null && cmd.Layout.Handle != 0)
        {
            var handle = GCHandle.Alloc(cmd.PushConstants, GCHandleType.Pinned);

            try
            {
                _context.VulkanApi.CmdPushConstants(
                    _commandBuffer,
                    cmd.Layout,
                    cmd.ShaderStageFlags,
                    0,
                    (uint)Marshal.SizeOf(cmd.PushConstants),
                    handle.AddrOfPinnedObject().ToPointer()
                );
            }
            finally
            {
                handle.Free();
            }
        }

        var instanceBuffer = _instanceBuffers[_currentFrameIndex];
        var instanceOffset = instanceBuffer.Write(cmd.InstanceData);

        VkBuffer* buffers = stackalloc VkBuffer[2] { cmd.VertexBuffer, instanceBuffer.Buffer };
        ulong* offsets = stackalloc ulong[2] { 0, instanceOffset };

        _context.VulkanApi.CmdBindVertexBuffers(_commandBuffer, 0, 2, buffers, offsets);

        _context.VulkanApi.CmdDraw(
            _commandBuffer,
            cmd.VertexCount,
            cmd.InstanceCount,
            cmd.FirstVertex,
            0
        );
    }

    /// <summary>
    /// Submits the recorded command buffer to the GPU queue.
    /// </summary>
    private void SubmitFrame()
    {
        if (_frameSync == null || _imageSync == null || _commandBuffer.Handle == 0)
            return;

        var waitStages = PipelineStageFlags.ColorAttachmentOutputBit;
        var imageAvailableSemaphore = _frameSync.ImageAvailable; // Per-frame acquire semaphore
        var renderFinishedSemaphore = _imageSync.RenderFinished; // Per-image render semaphore
        var cmdBuffer = (CommandBuffer)_commandBuffer;

        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &imageAvailableSemaphore,
            PWaitDstStageMask = &waitStages,
            CommandBufferCount = 1,
            PCommandBuffers = &cmdBuffer,
            SignalSemaphoreCount = 1,
            PSignalSemaphores = &renderFinishedSemaphore,
        };

        var result = _context.VulkanApi.QueueSubmit(
            _context.GraphicsQueue,
            1,
            &submitInfo,
            _frameSync.InFlightFence
        );
        if (result != Result.Success)
        {
            throw new InvalidOperationException($"Failed to submit queue: {result}");
        }
    }

    /// <summary>
    /// Presents the rendered image to the screen.
    /// </summary>
    private void PresentFrame()
    {
        if (_imageSync == null)
            return;

        try
        {
            _swapChain.Present(_imageIndex, _imageSync.RenderFinished);
        }
        catch (Exception ex)
            when (ex.Message.Contains("out of date") || ex.Message.Contains("suboptimal"))
        {
            _swapChain.Recreate();
        }
    }
}
