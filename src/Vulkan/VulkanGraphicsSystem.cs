namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Coordinates Vulkan resource loading, render-layer preparation, and frame rendering.
/// </summary>
public unsafe class VulkanGraphicsSystem(
    Context context,
    ISwapChain swapChain,
    IRenderer renderer,
    IEventHub eventHub,
    ILogger<VulkanGraphicsSystem> logger
) : IGraphicsSystem, IDisposable
{
    private readonly Context _context = context;
    private readonly ISwapChain _swapChain = swapChain;
    private readonly IRenderer _renderer = renderer;
    private readonly ILogger<VulkanGraphicsSystem> _logger = logger;

    private bool disposedValue;

    /// <summary>
    /// Initializes the graphics system and records the current Vulkan state.
    /// </summary>
    public void Initialize()
    {
        eventHub.Register(this);

        _logger.LogInformation(
            "Vulkan graphics system initialized. DeviceHandle={DeviceHandle}, "
                + "SwapchainExtent={Width}x{Height}",
            _context.Device.Handle,
            _swapChain.SwapchainExtent.Width,
            _swapChain.SwapchainExtent.Height
        );
    }

    private void Activate(IRenderable renderable)
    {
        // Resolve Vulkan resources from the renderable.
    }

    private void OnComponentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not IGraphicsComponent component)
            return;

        // synchronize renderables
    }

    private void Deactivate(IRenderable renderable)
    {
        // Release Vulkan resources allocated to the renderable.
    }

    public void Handle(ComponentActivatedEvent e)
    {
        if (e.Component is not IGraphicsComponent component)
            return;

        foreach (var renderable in component.Renderables)
            Activate(renderable);

        component.PropertyChanged += OnComponentPropertyChanged;
    }

    public void Handle(ComponentDeactivatedEvent e)
    {
        if (e.Component is not IGraphicsComponent component)
            return;

        component.PropertyChanged -= OnComponentPropertyChanged;

        foreach (var renderable in component.Renderables)
            Deactivate(renderable);
    }

    /// <summary>
    /// Creates the default Vulkan render layer covering the full swapchain extent, if one does not already exist.
    /// </summary>
    private void EnsureDefaultRenderLayer()
    {
        if (_renderer.Layers.Any())
            return;

        var extent = _swapChain.SwapchainExtent;

        _logger.LogInformation(
            "Renderer has no render layers; creating the default Vulkan render layer. "
                + "SwapchainExtent={Width}x{Height}, RenderPassName={RenderPassName}",
            extent.Width,
            extent.Height,
            RenderPasses.GetName(RenderPasses.Main)
        );

        var layer = new VulkanRenderLayer
        {
            LoadOp = AttachmentLoadOp.Load,
            Viewport = new()
            {
                X = 0,
                Y = 0,
                Width = extent.Width,
                Height = extent.Height,
                MinDepth = 0f,
                MaxDepth = 1f,
            },
            Scissor = new Rect2D { Offset = new Offset2D(0, 0), Extent = extent },
            RenderPasses =
            [
                new RenderPassDefinition
                {
                    RenderPass = RenderPasses.Main,
                    ClearValues = [new Color(0.02f, 0.02f, 0.02f, 1.0f).ClearValue()],
                    ShouldRender = true,
                },
            ],
            Items = [],
        };

        _renderer.Layers = [layer];

        _logger.LogInformation(
            $"Default Vulkan render layer created. "
                + $"LayerCount={_renderer.Layers.Count()}, "
                + $"RenderPassCount={layer.RenderPasses.Length}, "
                + $"RenderItemCount={layer.Items.Count}"
        );
    }

    /// <summary>
    /// Renders the current frame through the Vulkan renderer.
    /// </summary>
    public void Render()
    {
        try
        {
            _renderer.Render();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Vulkan graphics system failed to render a frame.");
            throw;
        }
    }

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
