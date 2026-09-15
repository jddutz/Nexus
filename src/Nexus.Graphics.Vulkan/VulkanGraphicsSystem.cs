using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Nexus.Graphics.Vulkan;

public unsafe class VulkanGraphicsSystem(
    Context context,
    ISwapChain swapChain,
    IRenderer renderer,
    IEnumerable<IResourceRegistry> registries,
    IPipelineRegistry pipelineRegistry
) : IGraphicsSystem, IDisposable
{
    private const string DEFAULT_PIPELINE_NAME = "DefaultPipeline";

    private readonly Context _context = context;
    private readonly ISwapChain _swapChain = swapChain;
    private readonly IRenderer _renderer = renderer;
    private readonly IPipelineRegistry _pipelineManager = pipelineRegistry;
    private IEnumerable<IResourceRegistry> _registries = registries;
    private VkBuffer _vertexBuffer;
    private DeviceMemory _vertexBufferMemory;

    private bool disposedValue;

    public void Initialize() { }

    public ResourceId Load(IResourceDescription resource)
    {
        foreach (var registry in _registries)
        {
            if (registry.CanLoad(resource))
                return registry.Load(resource);
        }

        throw new NotSupportedException(
            $"No registry supports resource description: {resource.GetType().Name}"
        );
    }

    // TODO: Reconcile requested graphics state with the current GPU/render state.
    //
    // GameSystem may create, modify, or remove graphics instances during its update.
    // Those requests should not immediately mutate Vulkan resources or RenderBatches.
    // Instead, affected instances are marked dirty and processed here after the
    // GameSystem has finished submitting changes for the frame.
    //
    // For each dirty instance:
    // - Retrieve its current graphics-instance state.
    // - Resolve the ResourceIds referenced by the instance through their registries.
    // - Ensure the required resources have been created and are available to the GPU.
    // - Allocate, upload, reallocate, or otherwise update GPU resources as required.
    // - Resolve the Vulkan handles and other concrete state required for rendering.
    // - Create, replace, update, or remove the corresponding RenderBatch data.
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
    // Invariant: when Update completes, RenderBatches contain fully resolved, valid
    // Vulkan state and Renderer.Render() can execute them without performing resource
    // lookup, state reconciliation, residency management, or garbage collection.
    public void Update(double deltaTime)
    {
        if (_renderer.Batches.Any()) { }
        else
        {
            var extent = _swapChain.SwapchainExtent;

            var batch = new RenderBatch()
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

            _renderer.Batches = [batch];
        }
    }

    public void Render()
    {
        _renderer.Render();
    }

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
                _context.VulkanApi.DestroyBuffer(_context.Device, _vertexBuffer, null);
                _vertexBuffer = default;
            }

            disposedValue = true;
        }
    }

    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags requiredProperties)
    {
        _context.VulkanApi.GetPhysicalDeviceMemoryProperties(
            _context.PhysicalDevice,
            out var memoryProperties
        );

        for (uint i = 0; i < memoryProperties.MemoryTypeCount; i++)
        {
            var supported = (typeFilter & (1u << (int)i)) != 0;

            var properties = memoryProperties.MemoryTypes[(int)i].PropertyFlags;

            var suitable = (properties & requiredProperties) == requiredProperties;

            if (supported && suitable)
                return i;
        }

        throw new InvalidOperationException(
            $"Unable to find suitable Vulkan memory type for {requiredProperties}."
        );
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
