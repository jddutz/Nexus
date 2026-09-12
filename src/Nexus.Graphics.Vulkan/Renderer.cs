using System.Formats.Asn1;
using CommandBufferPool = Nexus.Graphics.Vulkan.Commands.CommandBufferPool;
using VkViewport = Silk.NET.Vulkan.Viewport;

namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Vulkan renderer implementation that orchestrates frame rendering.
/// Manages image acquisition, command recording, and presentation.
/// Uses ContentManager to get active cameras for rendering.
/// </summary>
public unsafe class Renderer : IRenderer
{
    private const string VK_CONTEXT_NULL = "Vulkan _context has not been initialized yet.";

    private Context _context;
    private ISwapChain _swapChain;
    private CommandBufferPool _commandPool;
    private ISyncManager _syncManager;
    private PipelineManager _pipelineManager;
    private FrameSync? _frameSync;
    private ImageSync? _imageSync;
    private uint _imageIndex;
    private CommandBuffer _commandBuffer;
    private AttachmentDescription _colorAttachment;
    private ClearValue _clearValue;
    private IProfiler? _profiler;
    private uint _currentFrameIndex = 0;

    public event EventHandler<RenderEventArgs>? BeforeRendering;
    public event EventHandler<RenderEventArgs>? AfterRendering;

    internal Renderer(
        Context context,
        ISwapChain swapChain,
        ISyncManager syncManager,
        PipelineManager pipelineManager,
        CommandBufferPool commandPool
    )
    {
        _context = context;
        _swapChain = swapChain;
        _syncManager = syncManager;
        _pipelineManager = pipelineManager;
        _commandPool = commandPool;
    }

    public RenderDefinition[] Definitions { get; set; } = [new()];

    public bool CanRender() =>
        _context != null
        && _swapChain != null
        && Definitions.Length > 0
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

            BeginCommandBuffer();

            foreach (var definition in Definitions)
            {
                var viewport = definition.Viewport;
                _context.VulkanApi.CmdSetViewport(_commandBuffer, 0, 1, &viewport);

                var scissor = definition.Scissor;
                _context.VulkanApi.CmdSetScissor(_commandBuffer, 0, 1, &scissor);

                foreach (var pass in definition.RenderPassDefinitions)
                {
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

                    foreach (var command in definition.DrawCommands)
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

            _currentFrameIndex = (_currentFrameIndex + 1) % _syncManager.MaxFramesInFlight;
        }
        catch (Exception)
        {
            _context.Window.Close();
            return false;
        }

        return true;
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

    private void Draw(
        DrawCommand drawCommand,
        ref ulong lastPipelineId,
        ref ulong lastDescriptorSetHandle
    )
    {
        var pipeline = _pipelineManager.Get(drawCommand.PipelineId);

        if (drawCommand.PipelineId != lastPipelineId)
        {
            _context.VulkanApi.CmdBindPipeline(
                _commandBuffer,
                PipelineBindPoint.Graphics,
                pipeline.Pipeline
            );

            lastPipelineId = drawCommand.PipelineId;
            lastDescriptorSetHandle = 0;
        }

        if (
            drawCommand.DescriptorSet.Handle != 0
            && drawCommand.DescriptorSet.Handle != lastDescriptorSetHandle
        )
        {
            var descriptorSet = drawCommand.DescriptorSet;

            _context.VulkanApi.CmdBindDescriptorSets(
                _commandBuffer,
                PipelineBindPoint.Graphics,
                pipeline.Layout,
                0,
                1,
                &descriptorSet,
                0,
                null
            );

            lastDescriptorSetHandle = descriptorSet.Handle;
        }

        if (drawCommand.PushConstants != null && pipeline.Layout.Handle != 0)
        {
            var handle = GCHandle.Alloc(drawCommand.PushConstants, GCHandleType.Pinned);

            try
            {
                _context.VulkanApi.CmdPushConstants(
                    _commandBuffer,
                    pipeline.Layout,
                    pipeline.ShaderStageFlags,
                    0,
                    (uint)Marshal.SizeOf(drawCommand.PushConstants),
                    handle.AddrOfPinnedObject().ToPointer()
                );
            }
            finally
            {
                handle.Free();
            }
        }

        var vertexBuffer = drawCommand.VertexBuffer;
        ulong offset = 0;

        _context.VulkanApi.CmdBindVertexBuffers(_commandBuffer, 0, 1, &vertexBuffer, &offset);

        _context.VulkanApi.CmdDraw(
            _commandBuffer,
            drawCommand.VertexCount,
            drawCommand.InstanceCount,
            drawCommand.FirstVertex,
            0
        );

        var vertexBuffers = stackalloc Silk.NET.Vulkan.Buffer[] { drawCommand.VertexBuffer };
        var offsets = stackalloc ulong[] { 0 };
        _context.VulkanApi.CmdBindVertexBuffers(_commandBuffer, 0, 1, vertexBuffers, offsets);

        _context.VulkanApi.CmdDraw(
            _commandBuffer,
            drawCommand.VertexCount,
            drawCommand.InstanceCount,
            drawCommand.FirstVertex,
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
