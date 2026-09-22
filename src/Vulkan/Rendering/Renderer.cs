namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Vulkan renderer implementation that orchestrates frame rendering.
/// Manages image acquisition, command recording, submission, and presentation.
/// </summary>
/// <param name="context">The Vulkan context used for device and command recording operations.</param>
/// <param name="swapChain">The swap chain that supplies render targets and presentation.</param>
/// <param name="syncManager">The synchronization manager for frames and swap-chain images.</param>
/// <param name="logger">The logger used to record rendering failures.</param>
public unsafe class Renderer(
    Context context,
    ISwapChain swapChain,
    ISyncManager syncManager,
    ILogger<Renderer> logger
) : IRenderer, IDisposable
{
    private Context _context = context;
    private ISwapChain _swapChain = swapChain;
    private ISyncManager _syncManager = syncManager;
    private readonly ILogger<Renderer> _logger = logger;
    private CommandBufferPool _commandPool = CommandBufferPool.ForGraphics(context, 2);
    private FrameSync? _frameSync;
    private ImageSync? _imageSync;
    private uint _imageIndex;
    private CommandBuffer _commandBuffer;
    private bool _disposed;

    /// <summary>Occurs after a frame is acquired and before command recording begins.</summary>
    public event EventHandler<RenderEventArgs>? BeforeRendering;

    /// <summary>Occurs after the frame has been submitted and presented.</summary>
    public event EventHandler<RenderEventArgs>? AfterRendering;

    /// <summary>Determines whether the renderer has a usable swap chain and render layer.</summary>
    /// <returns><see langword="true"/> when rendering can begin; otherwise, <see langword="false"/>.</returns>
    public bool CanRender() =>
        _context != null
        && _swapChain != null
        && _swapChain.Extent.Width > 0
        && _swapChain.Extent.Height > 0;

    /// <summary>Acquires the next swap-chain image and begins command recording.</summary>
    /// <returns><see langword="true"/> when recording can begin; otherwise, <see langword="false"/>.</returns>
    public bool Begin()
    {
        if (!CanRender())
            return false;

        if (!PrepareFrame())
            return false;

        BeforeRendering?.Invoke(this, new RenderEventArgs(_imageIndex));

        return BeginCommandBuffer();
    }

    /// <summary>Records a render batch into the current command buffer.</summary>
    /// <param name="batch">The render batch to record.</param>
    public void Record(IRenderBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        foreach (var command in batch.Commands)
        {
            if (command.RefCount <= 0)
                continue;

            if (command.IsSticky || !command.IsRecorded)
                command.Record(_context.VulkanApi, _commandBuffer);

            command.IsRecorded = true;
        }
    }

    /// <summary>Prepares frame synchronization and acquires the next swap-chain image.</summary>
    /// <returns>Frame sync, image index, and image sync objects.</returns>
    private bool PrepareFrame()
    {
        _frameSync = _syncManager.WaitForFrame(_syncManager.CurrentFrameIndex);
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
        if (result != Result.Success)
            throw new InvalidOperationException(
                $"Failed to begin command buffer recording: {result}"
            );

        return true;
    }

    /// <summary>Ends command recording, submits the frame, and presents the rendered image.</summary>
    public void Submit()
    {
        if (_context.VulkanApi.EndCommandBuffer(_commandBuffer) != Result.Success)
            throw new InvalidOperationException("Failed to end command buffer recording.");

        SubmitFrame();
        PresentFrame();
        AfterRendering?.Invoke(this, new RenderEventArgs(_imageIndex));
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
    public void PresentFrame()
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

    /// <summary>
    /// Releases the persistently mapped instance upload buffers owned by this renderer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _context.VulkanApi.DeviceWaitIdle(_context.Device);

        _commandPool.Dispose();

        _disposed = true;
    }
}
