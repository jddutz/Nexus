using Nexus.Graphics.Vulkan;

namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Coordinates Vulkan resource loading, render-layer preparation, and frame rendering.
/// </summary>
/// <param name="context">The Vulkan context that owns graphics resources.</param>
/// <param name="swapChain">The swap chain used for presentation.</param>
/// <param name="renderer">The renderer used to record and submit render batches.</param>
/// <param name="geometryRegistry">The registry that creates commands for drawables.</param>
/// <param name="renderPassConfig">The shared render-pass configurations.</param>
/// <param name="eventHub">The event hub used to register this graphics system.</param>
/// <param name="logger">The logger used to record graphics system activity.</param>
public unsafe class VulkanGraphicsSystem(
    Context context,
    ISwapChain swapChain,
    IRenderer renderer,
    RenderPassConfigurations renderPassConfig,
    IEventHub eventHub,
    IGeometryRegistry geometryRegistry,
    ITextureRegistry textureRegistry,
    ILogger<VulkanGraphicsSystem> logger
) : IGraphicsSystem, IDisposable
{
    private IRenderBatch[] _batches = [];

    /// <summary>
    /// Initializes the graphics system and records the current Vulkan state.
    /// </summary>
    public void Initialize()
    {
        eventHub.Register(this);

        logger.LogInformation(
            "Vulkan graphics system initialized. DeviceHandle={DeviceHandle}, "
                + "SwapchainExtent={Width}x{Height}",
            context.Device.Handle,
            swapChain.Extent.Width,
            swapChain.Extent.Height
        );
    }

    /// <summary>
    /// Creates render batches for the specified view configuration.
    /// </summary>
    /// <param name="view">The activated view configuration.</param>
    private void Configure(ViewComponent view)
    {
        var viewport = new VkViewport
        {
            X = 0,
            Y = 0,
            Width = swapChain.Extent.Width,
            Height = swapChain.Extent.Height,
            MinDepth = 0.0f,
            MaxDepth = 1.0f,
        };
        var scissor = new Rect2D { Offset = new Offset2D(0, 0), Extent = swapChain.Extent };
        var batches = new IRenderBatch[view.RenderLayers.Count];

        for (var layerIndex = 0; layerIndex < view.RenderLayers.Count; layerIndex++)
        {
            var batch = new RenderBatch(new DefaultBatchStrategy());
            foreach (var config in renderPassConfig.Configurations.Values)
            {
                batch.Add(new SetViewportCommand(config.RenderPassBit, viewport));
                batch.Add(new SetScissorCommand(config.RenderPassBit, scissor));
            }

            batches[layerIndex] = batch;
        }

        _batches = batches;
        logger.LogDebug(
            "Configured Vulkan graphics view. RenderLayerCount={RenderLayerCount}",
            view.RenderLayers.Count
        );
    }

    private void Activate(IDrawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);

        if (_batches.Length == 0)
            return;

        var batch = _batches[0];

        var vertexShader =
            drawable.VertexShader
            ?? throw new InvalidOperationException("Drawables must define a vertex shader.");
        geometryRegistry.Create(drawable.Mesh, vertexShader.VertexFormat);

        foreach (var cmd in textureRegistry.Create(drawable.Texture))
        {
            batch.Add(cmd);
        }

        logger.LogDebug(
            "Activated drawable. DrawableType={DrawableType}, DrawableId={DrawableId}",
            drawable.GetType().Name,
            drawable.Id
        );
    }

    public void Handle(ComponentActivatedEvent e)
    {
        if (e.Component is ViewComponent view)
        {
            Configure(view);
            return;
        }

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

        if (_batches.Length == 0)
            return;

        var batch = _batches[0];

        var vertexShader =
            drawable.VertexShader
            ?? throw new InvalidOperationException("Drawables must define a vertex shader.");
        geometryRegistry.Release(drawable.Mesh, vertexShader.VertexFormat);

        foreach (var cmd in textureRegistry.Release(drawable.Texture))
        {
            batch.Add(cmd);
        }

        logger.LogDebug(
            "Deactivated drawable. DrawableType={DrawableType}, DrawableId={DrawableId}",
            drawable.GetType().Name,
            drawable.Id
        );
    }

    public void Handle(ComponentDeactivatedEvent e)
    {
        if (e.Component is ViewComponent)
        {
            _batches = [];
            logger.LogDebug("Cleared Vulkan graphics view configuration.");
            return;
        }

        if (e.Component is not IGraphicsComponent component)
            return;

        logger.LogDebug(
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
            if (!renderer.Begin())
                return;

            foreach (var batch in _batches)
                renderer.Record(batch);

            renderer.Submit();

            foreach (var batch in _batches)
                batch.Clean();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Vulkan graphics system rendering failed.");
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
            logger.LogDebug("Vulkan graphics system disposal requested more than once.");
            return;
        }

        context.VulkanApi.DeviceWaitIdle(context.Device);

        if (disposing)
        {
            // TODO: dispose managed state (managed objects)
        }

        disposedValue = true;
        logger.LogInformation("Vulkan graphics system disposed.");
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
