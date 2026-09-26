namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Vulkan renderer implementation that orchestrates frame rendering.
/// Manages image acquisition, command recording, submission, and presentation.
/// </summary>
/// <param name="context">The Vulkan context used for device and command recording operations.</param>
/// <param name="swapChain">The swap chain that supplies render targets and presentation.</param>
/// <param name="syncManager">The synchronization manager for frames and swap-chain images.</param>
/// <param name="renderPasses">The configured render passes.</param>
/// <param name="performanceMetrics">Optional performance counter collector.</param>
/// <param name="diagnostics">Optional immutable Vulkan diagnostic collector.</param>
public unsafe class Renderer(
    Context context,
    ISwapChain swapChain,
    ISyncManager syncManager,
    RenderPassConfigurations renderPasses,
    PerformanceMetrics? performanceMetrics = null,
    PerformanceDiagnostics? diagnostics = null
) : IRenderer, IDisposable
{
    private readonly Context _context = context;
    private readonly ISwapChain _swapChain = swapChain;
    private readonly ISyncManager _syncManager = syncManager;
    private readonly RenderPassConfigurations _renderPasses = renderPasses;
    private readonly PerformanceMetrics? _performanceMetrics = performanceMetrics;
    private readonly PerformanceDiagnostics? _diagnostics = diagnostics;

    private readonly CommandBufferPool _commandPool = CommandBufferPool.ForGraphics(context, 2);

    private FrameSync? _frameSync;
    private ImageSync? _imageSync;
    private uint _imageIndex;
    private CommandBuffer _commandBuffer;
    private Rect2D _renderArea;
    private bool _disposed;

    /// <summary>
    /// Occurs after a frame is acquired and before command recording begins.
    /// </summary>
    public event EventHandler<RenderEventArgs>? BeforeRendering;

    /// <summary>
    /// Occurs after the frame has been submitted and presented.
    /// </summary>
    public event EventHandler<RenderEventArgs>? AfterRendering;

    /// <summary>
    /// Determines whether the renderer has a usable swap chain.
    /// </summary>
    public bool CanRender() => _swapChain.Extent.Width > 0 && _swapChain.Extent.Height > 0;

    /// <summary>
    /// Acquires the next swap-chain image and begins command recording using the available frame slot.
    /// </summary>
    /// <param name="frameSync">The completed frame synchronization slot to reuse for this submission.</param>
    /// <returns>The acquired frame and image indices, or <see langword="null"/> when rendering cannot begin.</returns>
    public RenderFrameResult? PrepareFrame(FrameSync frameSync)
    {
        ArgumentNullException.ThrowIfNull(frameSync);

        if (!CanRender())
            return null;

        if (!AcquireFrame(frameSync))
            return null;

        if (!BeginCommandBuffer())
            return null;

        _diagnostics?.BeginFrame();
        TransitionToColorAttachment();

        BeforeRendering?.Invoke(this, new RenderEventArgs(_imageIndex));

        return new RenderFrameResult(_frameSync!.FrameIndex, _imageIndex);
    }

    /// <summary>
    /// Begins processing a render layer.
    /// </summary>
    public void Begin(IRenderBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        _renderArea = new Rect2D { Offset = new Offset2D(0, 0), Extent = _swapChain.Extent };

        foreach (var command in batch.Commands)
        {
            if (command is not SetViewportScissorCommand viewState)
                continue;

            _renderArea = viewState.RenderArea;
            break;
        }

        RecordCommands(batch);
    }

    /// <summary>
    /// Records a compute workload.
    /// </summary>
    public void Compute(IRenderBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        throw new NotImplementedException();
    }

    /// <summary>
    /// Records a render pass.
    /// </summary>
    public void Record(int renderPassIndex, IRenderBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (!_renderPasses.Configurations.TryGetValue(renderPassIndex, out var configuration))
        {
            throw new ArgumentOutOfRangeException(
                nameof(renderPassIndex),
                $"Render pass {renderPassIndex} is not configured."
            );
        }

        if (!configuration.ShouldRender)
            return;

        BeginRendering(configuration);

        RecordCommands(batch);

        EndRendering();
    }

    /// <summary>
    /// Finalizes processing of a render layer.
    /// </summary>
    public void Finalize(IRenderBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        RecordCommands(batch);
    }

    /// <summary>
    /// Records all commands in a batch into the current command buffer.
    /// </summary>
    private void RecordCommands(IRenderBatch batch)
    {
        foreach (var command in batch.Commands)
        {
            command.Record(_context.VulkanApi, _commandBuffer);
            _performanceMetrics?.Record(command);
            _diagnostics?.RecordCommand(command);
        }
    }

    /// <summary>
    /// Begins dynamic rendering for the specified render pass.
    /// </summary>
    private void BeginRendering(RenderPassConfiguration configuration)
    {
        var clearValue =
            configuration.ClearValues.Length > 0 ? configuration.ClearValues[0] : default;

        var colorAttachment = new RenderingAttachmentInfo
        {
            SType = StructureType.RenderingAttachmentInfo,
            ImageView = _swapChain.ImageViews[_imageIndex],
            ImageLayout = ImageLayout.ColorAttachmentOptimal,
            LoadOp = configuration.ColorLoadOp,
            StoreOp = configuration.ColorStoreOp,
            ClearValue = clearValue,
        };

        var renderingInfo = new RenderingInfo
        {
            SType = StructureType.RenderingInfo,
            RenderArea = _renderArea,
            LayerCount = 1,
            ViewMask = 0,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachment,
            PDepthAttachment = null,
            PStencilAttachment = null,
        };

        _context.VulkanApi.CmdBeginRendering(_commandBuffer, &renderingInfo);
    }

    /// <summary>
    /// Ends the current dynamic rendering operation.
    /// </summary>
    private void EndRendering()
    {
        _context.VulkanApi.CmdEndRendering(_commandBuffer);
    }

    /// <summary>
    /// Prepares frame synchronization and acquires the next swap-chain image.
    /// </summary>
    /// <param name="frameSync">The completed frame synchronization slot to reuse for this submission.</param>
    /// <returns><see langword="true"/> when an image was acquired successfully; otherwise, <see langword="false"/>.</returns>
    private bool AcquireFrame(FrameSync frameSync)
    {
        _frameSync = frameSync;

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

        return true;
    }

    /// <summary>
    /// Begins recording the command buffer for the current frame.
    /// </summary>
    private bool BeginCommandBuffer()
    {
        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };

        var result = _context.VulkanApi.BeginCommandBuffer(_commandBuffer, &beginInfo);

        if (result != Result.Success)
        {
            throw new InvalidOperationException(
                $"Failed to begin command buffer recording: {result}"
            );
        }

        return true;
    }

    /// <summary>
    /// Ends command recording, submits the frame, and presents the rendered image.
    /// </summary>
    public void Submit()
    {
        TransitionToPresent();

        var result = _context.VulkanApi.EndCommandBuffer(_commandBuffer);

        if (result != Result.Success)
        {
            throw new InvalidOperationException(
                $"Failed to end command buffer recording: {result}"
            );
        }

        SubmitFrame();
        PresentFrame();

        AfterRendering?.Invoke(this, new RenderEventArgs(_imageIndex));
    }

    private void TransitionToColorAttachment()
    {
        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = ImageLayout.Undefined,
            NewLayout = ImageLayout.ColorAttachmentOptimal,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = _swapChain.Images[_imageIndex],
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
            SrcAccessMask = 0,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit,
        };

        _context.VulkanApi.CmdPipelineBarrier(
            _commandBuffer,
            PipelineStageFlags.TopOfPipeBit | PipelineStageFlags.ColorAttachmentOutputBit,
            PipelineStageFlags.ColorAttachmentOutputBit,
            0,
            0,
            null,
            0,
            null,
            1,
            in barrier
        );
    }

    /// <summary>
    /// Records the swap-chain image transition required before presentation.
    /// </summary>
    private void TransitionToPresent()
    {
        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = ImageLayout.ColorAttachmentOptimal,
            NewLayout = ImageLayout.PresentSrcKhr,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = _swapChain.Images[_imageIndex],

            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },

            SrcAccessMask = AccessFlags.ColorAttachmentWriteBit,

            DstAccessMask = 0,
        };

        _context.VulkanApi.CmdPipelineBarrier(
            _commandBuffer,
            PipelineStageFlags.ColorAttachmentOutputBit,
            PipelineStageFlags.BottomOfPipeBit,
            0,
            0,
            null,
            0,
            null,
            1,
            in barrier
        );
    }

    /// <summary>
    /// Submits the recorded command buffer to the graphics queue.
    /// </summary>
    private void SubmitFrame()
    {
        if (_frameSync == null || _imageSync == null || _commandBuffer.Handle == 0)
        {
            return;
        }

        var waitStages = PipelineStageFlags.ColorAttachmentOutputBit;

        var imageAvailableSemaphore = _frameSync.ImageAvailable;

        var renderFinishedSemaphore = _imageSync.RenderFinished;

        var commandBuffer = _commandBuffer;

        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,

            WaitSemaphoreCount = 1,
            PWaitSemaphores = &imageAvailableSemaphore,
            PWaitDstStageMask = &waitStages,

            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer,

            SignalSemaphoreCount = 1,
            PSignalSemaphores = &renderFinishedSemaphore,
        };

        _syncManager.ResetFence(_frameSync.InFlightFence);

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

        _syncManager.IncrementFrameCounter();
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

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _context.VulkanApi.DeviceWaitIdle(_context.Device);

        _commandPool.Dispose();

        _disposed = true;
    }
}
