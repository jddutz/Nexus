using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Nexus.Graphics.Vulkan;

public unsafe class VulkanGraphicsSystem(
    Context context,
    IRenderer renderer,
    IGraphicsResourceManager resourceManager,
    IPipelineRegistry pipelineRegistry,
    IPipelineDefinitionBuilder pipelineDefinitionBuilder
) : IGraphicsSystem, IDisposable
{
    private const string DEFAULT_PIPELINE_NAME = "DefaultPipeline";

    private readonly Context _context = context;
    private readonly IRenderer _renderer = renderer;
    private readonly IGraphicsResourceManager _resourceManager = resourceManager;
    private readonly IPipelineRegistry _pipelineManager = pipelineRegistry;
    private readonly IPipelineDefinitionBuilder _piplineDefinitionBuilder =
        pipelineDefinitionBuilder;

    private VkBuffer _vertexBuffer;
    private RenderBatch? _renderBatch;
    private bool disposedValue;

    public IGraphicsResourceManager ResourceManager => _resourceManager;

    public void Configure()
    {
        foreach (var resource in VulkanResources.ShaderDefinitions)
        {
            _resourceManager.Register(resource);
        }
    }

    public void Initialize()
    {
        var (pipeline, layout) = _pipelineManager.GetOrCreate(
            new PipelineDefinitionBuilder(DEFAULT_PIPELINE_NAME)
            // TODO: define the pipeline
            .Build()
        );

        Vertex[] vertices = [new(-1f, -1f), new(3f, -1f), new(-1f, 3f)];
        ulong size = (ulong)(vertices.Length * Unsafe.SizeOf<Vertex>());

        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = BufferUsageFlags.VertexBufferBit,
            SharingMode = SharingMode.Exclusive,
        };

        var result = _context.VulkanApi.CreateBuffer(
            _context.Device,
            in bufferInfo,
            null,
            out _vertexBuffer
        );

        if (result != Result.Success)
            throw new InvalidOperationException($"Unable to create vertex buffer: {result}");

        _renderBatch = new()
        {
            Items =
            [
                new RenderItem
                {
                    RenderMask = 1,
                    Pipeline = pipeline,
                    Layout = layout,
                    VertexBuffer = _vertexBuffer,
                    VertexCount = 3,
                },
            ],
        };
    }

    public void Render()
    {
        _renderer.Batches = [_renderBatch!];

        _renderer.Render();

        // TODO: handle rendering failure
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

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
