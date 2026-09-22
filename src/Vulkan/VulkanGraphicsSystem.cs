using Nexus.Graphics.Vulkan.Drawables;

namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Coordinates Vulkan resource loading, render-layer preparation, and frame rendering.
/// </summary>
/// <param name="context">The Vulkan context that owns graphics resources.</param>
/// <param name="swapChain">The swap chain used for presentation.</param>
/// <param name="renderer">The renderer used to record and submit render batches.</param>
/// <param name="drawableRegistry">The registry that creates commands for drawables.</param>
/// <param name="renderPassConfigurations">The shared render-pass configurations.</param>
/// <param name="eventHub">The event hub used to register this graphics system.</param>
/// <param name="logger">The logger used to record graphics system activity.</param>
public unsafe class VulkanGraphicsSystem(
    Context context,
    ISwapChain swapChain,
    IRenderer renderer,
    IDrawableRegistry drawableRegistry,
    RenderPassConfigurations renderPassConfigurations,
    IEventHub eventHub,
    ILogger<VulkanGraphicsSystem> logger
) : IGraphicsSystem, IDisposable
{
    private readonly Context _context = context;
    private readonly ISwapChain _swapChain = swapChain;
    private readonly IRenderer _renderer = renderer;
    private readonly IDrawableRegistry _drawables = drawableRegistry;
    private readonly RenderPassConfigurations _renderPassConfigurations = renderPassConfigurations;
    private readonly ILogger<VulkanGraphicsSystem> _logger = logger;

    private IRenderBatch[] _batches = [];

    /// <summary>
    /// Initializes the graphics system and records the current Vulkan state.
    /// </summary>
    public void Initialize()
    {
        eventHub.Register(this);

        _logger.LogDebug(
            "Initializing Vulkan graphics system. ExistingRenderLayerCount={RenderLayerCount}",
            _batches.Length
        );

        _logger.LogInformation(
            "Vulkan graphics system initialized. DeviceHandle={DeviceHandle}, "
                + "SwapchainExtent={Width}x{Height}",
            _context.Device.Handle,
            _swapChain.Extent.Width,
            _swapChain.Extent.Height
        );

        var batchStrategy = new RenderItemBatchStrategy();

        var batch = new RenderBatch(batchStrategy);

        var viewport = new VkViewport
        {
            X = 0,
            Y = 0,
            Width = _swapChain.Extent.Width,
            Height = _swapChain.Extent.Height,
            MinDepth = 0.0f,
            MaxDepth = 1.0f,
        };

        var scissor = new Rect2D { Offset = new Offset2D(0, 0), Extent = _swapChain.Extent };

        foreach (var renderPass in _renderPassConfigurations.Configurations)
        {
            batch.Add(new SetViewportCommand(viewport));
            batch.Add(new SetScissorCommand(scissor));
        }

        _batches = [batch];
    }

    private void Activate(IDrawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);

        var batch = _batches[0];

        foreach (var cmd in _drawables.Create(drawable))
        {
            batch.Add(cmd);
        }

        _logger.LogDebug(
            "Activated drawable. DrawableType={DrawableType}, DrawableId={DrawableId}",
            drawable.GetType().Name,
            drawable.Id
        );
    }

    public void Handle(ComponentActivatedEvent e)
    {
        if (e.Component is not IGraphicsComponent component)
            return;

        foreach (var drawable in component.Drawables)
            Activate(drawable);

        component.PropertyChanged += OnDrawablePropertyChanged;
    }

    private void OnDrawablePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        throw new NotImplementedException();
    }

    private void Deactivate(IDrawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);

        var batch = _batches[0];

        foreach (var cmd in _drawables.Release(drawable))
        {
            batch.Add(cmd);
        }

        _logger.LogDebug(
            "Deactivated drawable. DrawableType={DrawableType}, DrawableId={DrawableId}",
            drawable.GetType().Name,
            drawable.Id
        );
    }

    public void Handle(ComponentDeactivatedEvent e)
    {
        if (e.Component is not IGraphicsComponent component)
            return;

        _logger.LogDebug(
            "Graphics component deactivated. ComponentType={ComponentType}, DrawableCount={DrawableCount}",
            component.GetType().Name,
            component.Drawables.Count()
        );

        component.PropertyChanged -= OnDrawablePropertyChanged;

        foreach (var drawable in component.Drawables)
            Deactivate(drawable);
    }

    /// <summary>
    /// Renders the current frame through the Vulkan renderer.
    /// </summary>
    public void Render()
    {
        try
        {
            if (!_renderer.Begin())
                return;

            foreach (var batch in _batches)
                _renderer.Record(batch);

            _renderer.Submit();

            foreach (var batch in _batches)
                batch.Clean();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Vulkan graphics system rendering failed.");
            throw;
        }
    }

    private bool disposedValue;

    /// <summary>
    /// Releases resources owned directly by the graphics system.
    /// </summary>
    /// <param name="disposing">Whether managed resources should also be released.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposedValue)
        {
            _logger.LogDebug("Vulkan graphics system disposal requested more than once.");
            return;
        }

        _context.VulkanApi.DeviceWaitIdle(_context.Device);

        if (disposing)
        {
            // TODO: dispose managed state (managed objects)
        }

        disposedValue = true;
        _logger.LogInformation("Vulkan graphics system disposed.");
    }

    /// <summary>
    /// Releases resources owned by the graphics system.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
