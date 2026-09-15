using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Coordinates Vulkan resource loading, render-layer preparation, and frame rendering.
/// </summary>
public unsafe class VulkanGraphicsSystem(
    Context context,
    ISwapChain swapChain,
    IRenderer renderer,
    IEnumerable<IComponentRegistry> registries,
    IPipelineRegistry pipelineRegistry,
    ILogger<VulkanGraphicsSystem> logger
) : IGraphicsSystem, IDisposable
{
    private const string DEFAULT_PIPELINE_NAME = "DefaultPipeline";

    private readonly Context _context = context;
    private readonly ISwapChain _swapChain = swapChain;
    private readonly IRenderer _renderer = renderer;
    private readonly IPipelineRegistry _pipelineManager = pipelineRegistry;
    private readonly ILogger<VulkanGraphicsSystem> _logger = logger;
    private IEnumerable<IComponentRegistry> _registries = registries;
    private VkBuffer _vertexBuffer;

    private bool disposedValue;

    /// <summary>
    /// Gets the render layers prepared by this graphics system.
    /// </summary>
    public RenderLayers RenderLayers { get; } = new();

    /// <summary>
    /// Initializes the graphics system and records the current Vulkan state.
    /// </summary>
    public void Initialize()
    {
        _logger.LogInformation(
            "Vulkan graphics system initialized. DeviceHandle={DeviceHandle}, "
                + "SwapchainExtent={Width}x{Height}, ComponentRegistryCount={RegistryCount}",
            _context.Device.Handle,
            _swapChain.SwapchainExtent.Width,
            _swapChain.SwapchainExtent.Height,
            _registries.Count()
        );
    }

    public bool Activate<TComponent>(TComponent component)
        where TComponent : class, IGraphicsComponent
    {
        _logger.LogDebug(
            "Activating graphics component. "
                + "ComponentId={ComponentId}, ComponentType={ComponentType}",
            component.Id,
            component.GetType().Name
        );

        foreach (var registry in _registries)
        {
            if (!registry.CanLoad(component))
                continue;

            RenderItem[] renderItems;
            try
            {
                renderItems = registry.Load(component);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to activate graphics component. "
                        + "ComponentId={ComponentId}, ComponentType={ComponentType}, "
                        + "RegistryType={RegistryType}",
                    component.Id,
                    component.GetType().Name,
                    registry.GetType().Name
                );
                throw;
            }

            EnsureDefaultRenderLayer();

            if (renderItems.Length == 0)
            {
                _logger.LogDebug(
                    "No RenderItems were added to Vulkan render layer. "
                        + "ComponentId={ComponentId}",
                    component.Id
                );
                return false;
            }

            var layer = _renderer.Layers[0];

            layer.Items = [.. layer.Items, .. renderItems];

            _logger.LogDebug(
                "Graphics component activated. "
                    + "ComponentId={ComponentId}, ComponentType={ComponentType}, "
                    + "RegistryType={RegistryType}, RenderItemCount={RenderItemCount}",
                component.Id,
                component.GetType().Name,
                registry.GetType().Name,
                renderItems.Length
            );

            return true;
        }

        _logger.LogWarning(
            "Graphics component activation skipped because no registry can load it. "
                + "ComponentId={ComponentId}, ComponentType={ComponentType}",
            component.Id,
            component.GetType().Name
        );

        return false;
    }

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
    /// Deactivates a component in the graphics system.
    /// </summary>
    /// <typeparam name="TComponent">The component type to deactivate.</typeparam>
    /// <param name="component">The component to deactivate.</param>
    /// <returns><see langword="false"/> because deactivation is not implemented yet.</returns>
    public void Deactivate<TComponent>(TComponent component)
        where TComponent : class, IGraphicsComponent
    {
        var componentType = component.GetType().Name;
        var unloadedRegistryCount = 0;

        foreach (var registry in _registries)
        {
            if (registry.CanUnload(component.Id))
            {
                registry.Unload(component.Id);
                unloadedRegistryCount++;
            }
        }

        _logger.LogDebug(
            "Graphics component deactivation completed. ComponentType={ComponentType}, "
                + "ComponentId={ComponentId}, UnloadedRegistryCount={UnloadedRegistryCount}",
            componentType,
            component.Id,
            unloadedRegistryCount
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
        _context.VulkanApi.DeviceWaitIdle(_context.Device);
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            if (_vertexBuffer.Handle != 0 && _context.Device.Handle != 0)
            {
                _logger.LogDebug(
                    "Destroying Vulkan graphics system vertex buffer. BufferHandle={BufferHandle}",
                    _vertexBuffer.Handle
                );
                _context.VulkanApi.DestroyBuffer(_context.Device, _vertexBuffer, null);
                _vertexBuffer = default;
            }

            disposedValue = true;
            _logger.LogInformation("Vulkan graphics system disposed.");
        }
        else
        {
            _logger.LogDebug("Vulkan graphics system disposal requested more than once.");
        }
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
