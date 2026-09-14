using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Nexus.Graphics.Vulkan;

public unsafe class VulkanGraphicsSystem(
    Context context,
    ISwapChain swapChain,
    IRenderer renderer,
    IGraphicsResourceManager resourceManager,
    IPipelineRegistry pipelineRegistry
) : IGraphicsSystem, IDisposable
{
    private const string DEFAULT_PIPELINE_NAME = "DefaultPipeline";

    private readonly Context _context = context;
    private readonly ISwapChain _swapChain = swapChain;
    private readonly IRenderer _renderer = renderer;
    private readonly IGraphicsResourceManager _resources = resourceManager;
    private readonly IPipelineRegistry _pipelineManager = pipelineRegistry;

    private VkBuffer _vertexBuffer;
    private DeviceMemory _vertexBufferMemory;
    private RenderBatch? _renderBatch;
    private bool disposedValue;

    public IGraphicsResourceManager ResourceManager => _resources;

    public void Configure()
    {
        foreach (var resource in VulkanResources.ShaderDefinitions)
        {
            _resources.Register(resource);
        }
    }

    public void Initialize()
    {
        var (pipeline, layout) = _pipelineManager.GetOrCreate(
            new PipelineDefinitionBuilder(DEFAULT_PIPELINE_NAME)
                .WithShader(VulkanResources.UniformColorVertShader)
                .WithShader(VulkanResources.UniformColorFragShader)
                .WithRenderPass(_swapChain.Passes[0])
                .WithVertexBinding(
                    new VertexInputBindingDescription
                    {
                        Binding = 0,
                        Stride = (uint)Unsafe.SizeOf<Vertex>(),
                        InputRate = VertexInputRate.Vertex,
                    }
                )
                .WithVertexAttribute(
                    new VertexInputAttributeDescription
                    {
                        Location = 0,
                        Binding = 0,
                        Format = Format.R32G32Sfloat,
                        Offset = 0,
                    }
                )
                .WithDepthTest(false)
                .WithDepthWrite(false)
                .WithCullMode(CullModeFlags.None)
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

        _context.VulkanApi.GetBufferMemoryRequirements(
            _context.Device,
            _vertexBuffer,
            out var requirements
        );

        var memoryTypeIndex = FindMemoryType(
            requirements.MemoryTypeBits,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit
        );

        var memoryAllocation = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = memoryTypeIndex,
        };

        result = _context.VulkanApi.AllocateMemory(
            _context.Device,
            in memoryAllocation,
            null,
            out _vertexBufferMemory
        );

        if (result != Result.Success)
            throw new InvalidOperationException(
                $"Unable to allocate memory for vertex buffer: {result}"
            );

        result = _context.VulkanApi.BindBufferMemory(
            _context.Device,
            _vertexBuffer,
            _vertexBufferMemory,
            0
        );

        if (result != Result.Success)
            throw new InvalidOperationException(
                $"Unable to bind memory to vertex buffer: {result}"
            );

        void* mapped;

        _context.VulkanApi.MapMemory(_context.Device, _vertexBufferMemory, 0, size, 0, &mapped);

        fixed (Vertex* source = vertices)
        {
            System.Buffer.MemoryCopy(source, mapped, size, size);
        }

        _context.VulkanApi.UnmapMemory(_context.Device, _vertexBufferMemory);

        _renderBatch = new()
        {
            RenderPasses =
            [
                new RenderPassDefinition
                {
                    RenderPass = RenderPasses.Main,
                    ShouldRender = true,
                    ClearValues = [Colors.Black.ClearValue()],
                },
            ],
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

    private RenderPass CreateRenderPass()
    {
        var colorAttachment = new AttachmentDescription
        {
            Format = _swapChain.SwapchainFormat,
            Samples = SampleCountFlags.Count1Bit,

            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,

            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,

            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr,
        };

        var colorAttachmentReference = new AttachmentReference
        {
            Attachment = 0,
            Layout = ImageLayout.ColorAttachmentOptimal,
        };

        var subpass = new SubpassDescription
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachmentReference,
        };

        var dependency = new SubpassDependency
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,

            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = 0,

            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit,
        };

        var renderPassInfo = new RenderPassCreateInfo
        {
            SType = StructureType.RenderPassCreateInfo,

            AttachmentCount = 1,
            PAttachments = &colorAttachment,

            SubpassCount = 1,
            PSubpasses = &subpass,

            DependencyCount = 1,
            PDependencies = &dependency,
        };

        var result = _context.VulkanApi.CreateRenderPass(
            _context.Device,
            in renderPassInfo,
            null,
            out var renderPass
        );

        if (result != Result.Success)
            throw new InvalidOperationException($"Unable to create render pass: {result}");

        return renderPass;
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
