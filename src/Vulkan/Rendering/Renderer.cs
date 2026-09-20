namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Vulkan renderer implementation that orchestrates frame rendering.
/// Manages image acquisition, command recording, and presentation.
/// Binds the resources already resolved on each render item.
/// </summary>
/// <param name="context">The Vulkan context used for device and command recording operations.</param>
/// <param name="swapChain">The swap chain that supplies render targets and presentation.</param>
/// <param name="syncManager">The synchronization manager for frames and swap-chain images.</param>
/// <param name="pipelineManager">The pipeline registry used to resolve pipeline state.</param>
/// <param name="logger">The logger used to record rendering failures.</param>
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

    /// <summary>Occurs after a frame is acquired and before command recording begins.</summary>
    public event EventHandler<RenderEventArgs>? BeforeRendering;

    /// <summary>Occurs after the frame has been submitted and presented.</summary>
    public event EventHandler<RenderEventArgs>? AfterRendering;

    /// <summary>Gets or sets the render layers processed for each frame.</summary>
    public VulkanRenderLayer[] Layers { get; set; } = [];

    /// <summary>Determines whether the renderer has a usable swap chain and render layer.</summary>
    /// <returns><see langword="true"/> when rendering can begin; otherwise, <see langword="false"/>.</returns>
    public bool CanRender() =>
        _context != null
        && _swapChain != null
        && Layers.Length > 0
        && _swapChain.SwapchainExtent.Width > 0
        && _swapChain.SwapchainExtent.Height > 0;

    /// <summary>Acquires, records, submits, and presents one frame.</summary>
    /// <returns><see langword="true"/> when the frame was rendered; otherwise, <see langword="false"/>.</returns>
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

                    foreach (var command in layer.Items)
                    {
                        if ((command.RenderPassMask & pass.RenderPass) != 0)
                            Draw(
                                command,
                                RenderPasses.GetIndex(pass.RenderPass),
                                ref lastPipelineId
                            );
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

    /// <summary>Validates the render-pass definitions associated with a layer.</summary>
    /// <param name="definition">The layer whose passes are validated.</param>
    /// <exception cref="InvalidOperationException">Thrown when the layer or one of its passes is invalid.</exception>
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

    /// <summary>Begins recording the command buffer for the current frame.</summary>
    /// <returns><see langword="true"/> when command recording begins successfully.</returns>
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

    /// <summary>Begins the selected swap-chain render pass.</summary>
    /// <param name="index">The zero-based render-pass index.</param>
    /// <param name="clearValueCount">The number of clear values supplied.</param>
    /// <param name="passClearValues">A pointer to the pass clear values.</param>
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

    /// <summary>Records the draw commands for one render item and pass.</summary>
    /// <param name="cmd">The render item to draw.</param>
    /// <param name="passIndex">The zero-based pass index used to select resources.</param>
    /// <param name="lastPipelineId">The pipeline handle most recently bound in this pass.</param>
    private void Draw(RenderItem cmd, int passIndex, ref ulong lastPipelineId)
    {
        if (cmd.InstanceCount == 0)
            return;

        var pipeline = cmd.Pipelines[passIndex];
        var layout = cmd.Layouts[passIndex];
        var descriptorSets = cmd.DescriptorSets[passIndex];
        var vertexBuffer = cmd.VertexBuffers[passIndex];

        if (pipeline.Handle != lastPipelineId)
        {
            _context.VulkanApi.CmdBindPipeline(
                _commandBuffer,
                PipelineBindPoint.Graphics,
                pipeline
            );

            lastPipelineId = pipeline.Handle;
        }

        if (descriptorSets.Length > 0)
        {
            fixed (DescriptorSet* descriptorSetsPointer = descriptorSets)
            {
                _context.VulkanApi.CmdBindDescriptorSets(
                    _commandBuffer,
                    PipelineBindPoint.Graphics,
                    layout,
                    0,
                    (uint)descriptorSets.Length,
                    descriptorSetsPointer,
                    0,
                    null
                );
            }
        }

        if (cmd.PushConstants != null && layout.Handle != 0)
        {
            var handle = GCHandle.Alloc(cmd.PushConstants, GCHandleType.Pinned);

            try
            {
                _context.VulkanApi.CmdPushConstants(
                    _commandBuffer,
                    layout,
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

        VkBuffer* buffers = stackalloc VkBuffer[2] { vertexBuffer, instanceBuffer.Buffer };
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
