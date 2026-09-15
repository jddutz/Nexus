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

    // TODO: Reconcile requested graphics state with the current GPU/render state.
    //
    // GameSystem may create, modify, or remove graphics instances during its update.
    // Those requests should not immediately mutate Vulkan resources or RenderLayers.
    // Instead, affected instances are marked dirty and processed here after the
    // GameSystem has finished submitting changes for the frame.
    //
    // For each dirty instance:
    // - Retrieve its current graphics-instance state.
    // - Resolve the ResourceIds referenced by the instance through their registries.
    // - Ensure the required resources have been created and are available to the GPU.
    // - Allocate, upload, reallocate, or otherwise update GPU resources as required.
    // - Resolve the Vulkan handles and other concrete state required for rendering.
    // - Create, replace, update, or remove the corresponding RenderLayer data.
    // - Clear the instance's dirty state once reconciliation succeeds.
    //
    // GPU memory management also belongs here. Resource registration does not imply
    // that a resource must remain resident indefinitely. As memory management evolves,
    // this update may determine residency, perform uploads/re-buffering, relocate
    // resources, and evict resources that are no longer required.
    //
    // Finally, perform deferred resource cleanup and garbage collection. Vulkan
    // resources must not be destroyed while they may still be referenced by in-flight
    // GPU work, so destruction may need to be deferred until the relevant frame/fence
    // guarantees that the resource is no longer in use.
    //
    // Invariant: when Update completes, RenderLayers contain fully resolved, valid
    // Vulkan state and Renderer.Render() can execute them without performing resource
    // lookup, state reconciliation, residency management, or garbage collection.
    /// <summary>
    /// Reconciles graphics state and ensures the renderer has a default render layer.
    /// </summary>
    /// <param name="deltaTime">The elapsed time in seconds since the previous update.</param>
    public void Update(double deltaTime)
    {
        if (!_renderer.Layers.Any())
        {
            var extent = _swapChain.SwapchainExtent;

            _logger.LogInformation(
                $"Renderer has no render layers; creating the default Vulkan render layer. "
                    + $"SwapchainExtent={extent.Width}x{extent.Height}, "
                    + $"RenderPassName={RenderPasses.GetName(RenderPasses.Main)}"
            );

            var layer = new VulkanRenderLayer()
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
                RenderPasses =
                [
                    new RenderPassDefinition
                    {
                        RenderPass = RenderPasses.Main,
                        ClearValues = [new Vector4D<float>(0.02f, 0.02f, 0.02f, 1.0f).ClearValue()],
                        ShouldRender = true,
                    },
                ],
                Items = [],
            };

            _renderer.Layers = [layer];

            _logger.LogInformation(
                "Default Vulkan render layer created. LayerCount={LayerCount}, "
                    + "RenderPassCount={RenderPassCount}, RenderItemCount={RenderItemCount}",
                _renderer.Layers.Count(),
                layer.RenderPasses.Count(),
                layer.Items.Count()
            );
        }
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

    /// <summary>
    /// Determines whether the graphics system can activate a component.
    /// </summary>
    /// <typeparam name="TComponent">The component type to evaluate.</typeparam>
    /// <param name="component">The component to evaluate.</param>
    /// <returns><see langword="false"/> because activation is not implemented yet.</returns>
    public bool CanActivate<TComponent>(TComponent component)
        where TComponent : class, IComponent
    {
        var componentType = component.GetType().Name;
        var canActivate = _registries.Any(registry => registry.CanLoad(component));

        _logger.LogDebug(
            "Graphics component activation support checked. ComponentType={ComponentType}, "
                + "CanActivate={CanActivate}",
            componentType,
            canActivate
        );

        return canActivate;
    }

    /// <summary>
    /// Activates a component in the graphics system.
    /// </summary>
    /// <typeparam name="TComponent">The component type to activate.</typeparam>
    /// <param name="component">The component to activate.</param>
    /// <returns><see langword="false"/> because activation is not implemented yet.</returns>
    public bool Activate<TComponent>(TComponent component)
        where TComponent : class, IComponent
    {
        var componentType = component.GetType().Name;

        foreach (var registry in _registries)
        {
            if (!registry.CanLoad(component))
                continue;

            registry.Load(component);
            _logger.LogDebug(
                "Graphics component activated. ComponentType={ComponentType}, "
                    + "RegistryType={RegistryType}",
                componentType,
                registry.GetType().Name
            );
            return true;
        }

        _logger.LogDebug(
            "Graphics component activation skipped because no registry can load it. "
                + "ComponentType={ComponentType}",
            componentType
        );
        return false;
    }

    /// <summary>
    /// Deactivates a component in the graphics system.
    /// </summary>
    /// <typeparam name="TComponent">The component type to deactivate.</typeparam>
    /// <param name="component">The component to deactivate.</param>
    /// <returns><see langword="false"/> because deactivation is not implemented yet.</returns>
    public void Deactivate<TComponent>(TComponent component)
        where TComponent : class, IComponent
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
}
