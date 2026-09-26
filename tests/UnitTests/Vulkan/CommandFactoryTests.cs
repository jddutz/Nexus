namespace Tests;

using Microsoft.Extensions.Logging;
using Nexus.Core;
using Nexus.Graphics;
using Nexus.Graphics.Geometry;
using Nexus.Graphics.Shaders;
using Nexus.Graphics.Textures;
using Nexus.Graphics.Vulkan;
using Nexus.Graphics.Vulkan.Commands;
using Nexus.Graphics.Vulkan.Descriptors;
using Nexus.Graphics.Vulkan.Geometry;
using Nexus.Graphics.Vulkan.Pipelines;
using Nexus.Graphics.Vulkan.Textures;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using VkBuffer = Silk.NET.Vulkan.Buffer;
using VkSampler = Silk.NET.Vulkan.Sampler;

public class CommandFactoryTests
{
    [Fact]
    public void ToVulkanAttributes_maps_msdf_distance_range_as_scalar_at_expected_offset()
    {
        var attributes = new ShaderInput(InputSemantics.MsdfDistanceRange, 4)
            .ToVulkanAttributes(1, 8, 96);

        var attribute = Assert.Single(attributes);
        Assert.Equal(1u, attribute.Binding);
        Assert.Equal(8u, attribute.Location);
        Assert.Equal(Format.R32Sfloat, attribute.Format);
        Assert.Equal(96u, attribute.Offset);
    }

    [Fact]
    public void Create_requires_a_vertex_shader()
    {
        var drawable = CreateDrawable(includeVertexShader: false);
        var factory = CreateFactory();

        Assert.Throws<InvalidOperationException>(() => factory.Create(drawable).ToList());
    }

    [Fact]
    public void Create_requires_a_fragment_shader()
    {
        var drawable = CreateDrawable(includeFragmentShader: false);
        var factory = CreateFactory();

        Assert.Throws<InvalidOperationException>(() => factory.Create(drawable).ToList());
    }

    [Fact]
    public void Create_builds_commands_and_writes_uniform_and_texture_descriptors()
    {
        var dependencies = new TestDependencies();
        var factory = CreateFactory(dependencies);
        var drawable = CreateDrawable(
            tessellationControlShader: CreateShader("control", ShaderStageEnum.TessellationControl),
            tessellationEvalShader: CreateShader("eval", ShaderStageEnum.TessellationEval),
            geometryShader: CreateShader("geometry", ShaderStageEnum.Geometry)
        );

        var commands = factory.Create(drawable).ToArray();

        Assert.Collection(
            commands,
            command => Assert.IsType<BindPipelineCommand>(command),
            command => Assert.IsType<BindDescriptorSetsCommand>(command),
            command => Assert.IsType<BindVertexBufferCommand>(command),
            command => Assert.IsType<DrawCommand>(command)
        );
        Assert.Equal(1, dependencies.Geometry.CreateCount);
        Assert.Equal(1, dependencies.Texture.CreateCount);
        Assert.Equal(1, dependencies.Pipelines.GetOrCreateCount);
        Assert.Equal(2, dependencies.Descriptors.AllocateCount);
        Assert.Equal(1, dependencies.Descriptors.UniformWriteCount);
        Assert.Equal(1, dependencies.Descriptors.ImageSamplerWriteCount);
        Assert.Equal(1, dependencies.Buffers.CreateUniformBufferCount);
        Assert.Equal(1, dependencies.Buffers.UpdateBufferCount);
        Assert.Equal(1, dependencies.Samplers.CreateCount);
    }

    [Fact]
    public void Create_rejects_unsupported_descriptor_types()
    {
        var dependencies = new TestDependencies
        {
            PipelineDefinition = CreatePipelineDefinition(
                new DescriptorSchema([
                    new DescriptorSetSchema(
                        0,
                        [
                            new DescriptorBinding(
                                0,
                                DescriptorType.Sampler,
                                1,
                                ShaderStageFlags.VertexBit
                            ),
                        ]
                    ),
                ])
            ),
        };
        var factory = CreateFactory(dependencies);

        Assert.Throws<NotSupportedException>(() => factory.Create(CreateDrawable()).ToList());
    }

    [Fact]
    public void UpdateInstanceData_refreshes_instance_binding_and_draw_count()
    {
        var dependencies = new TestDependencies();
        var factory = CreateFactory(dependencies);
        var drawable = CreateDrawable(includeInstanceLayout: true);
        factory.Create(drawable).ToArray();
        drawable.InstanceCount = 3;

        var commands = factory.UpdateInstanceData(drawable).ToArray();

        Assert.Equal(2, dependencies.Geometry.InstanceCreateCount);
        Assert.Equal(1, dependencies.Geometry.CreateCount);
        Assert.Equal(1, dependencies.Texture.CreateCount);
        Assert.Equal(1, dependencies.Pipelines.GetOrCreateCount);
        Assert.Equal((uint)3, Assert.IsType<DrawCommand>(commands[^1]).InstanceCount);
        Assert.Contains(commands, command => command is BindVertexBufferCommand { Binding: 1 });
    }

    [Fact]
    public void UpdateUniformData_updates_the_existing_uniform_buffer()
    {
        var dependencies = new TestDependencies();
        var factory = CreateFactory(dependencies);
        var drawable = CreateDrawable(includeInstanceLayout: true);
        factory.Create(drawable).ToArray();

        factory.UpdateUniformData(drawable);

        Assert.Equal(2, dependencies.Buffers.UpdateBufferCount);
        Assert.Equal(2, dependencies.Descriptors.UniformWriteCount);
        Assert.Equal(1, dependencies.Buffers.CreateUniformBufferCount);
    }

    [Fact]
    public void UpdateTexture_updates_image_and_descriptor_without_recreating_pipeline()
    {
        var dependencies = new TestDependencies();
        var factory = CreateFactory(dependencies);
        var drawable = CreateDrawable();
        factory.Create(drawable).ToArray();
        drawable.Texture = new TestTexture();

        factory.UpdateTexture(drawable).ToArray();

        Assert.Equal(1, dependencies.Texture.UpdateCount);
        Assert.Equal(2, dependencies.Descriptors.ImageSamplerWriteCount);
        Assert.Equal(1, dependencies.Pipelines.GetOrCreateCount);
    }

    [Fact]
    public void UpdateMesh_updates_vertex_buffer_and_refreshes_its_binding()
    {
        var dependencies = new TestDependencies();
        var factory = CreateFactory(dependencies);
        var drawable = CreateDrawable();
        factory.Create(drawable).ToArray();

        var commands = factory.UpdateMesh(drawable).ToArray();

        Assert.Equal(1, dependencies.Geometry.UpdateCount);
        Assert.Contains(commands, command => command is BindVertexBufferCommand { Binding: 0 });
        Assert.Equal(1, dependencies.Pipelines.GetOrCreateCount);
    }

    [Fact]
    public void UpdateShaders_acquires_new_pipeline_and_releases_the_old_reference()
    {
        var dependencies = new TestDependencies();
        var factory = CreateFactory(dependencies);
        var drawable = CreateDrawable();
        factory.Create(drawable).ToArray();
        factory.PipelineDefinition = new PipelineDefinition(
            "updated",
            null,
            null,
            null,
            null,
            null,
            new RenderPass(1),
            descriptorSchema: DescriptorSchemas.Textured
        );

        factory.UpdateShaders(drawable).ToArray();

        Assert.Equal(2, dependencies.Pipelines.GetOrCreateCount);
        Assert.Equal(1, dependencies.Pipelines.ReleaseCount);
    }

    [Fact]
    public void Release_releases_drawable_owned_resources()
    {
        var dependencies = new TestDependencies();
        var factory = CreateFactory(dependencies);
        var drawable = CreateDrawable(includeInstanceLayout: true);
        factory.Create(drawable).ToArray();

        factory.Release(drawable).ToArray();

        Assert.Equal(2, dependencies.Descriptors.ReleaseCount);
        Assert.Equal(1, dependencies.Buffers.DestroyBufferCount);
        Assert.Equal(1, dependencies.Samplers.ReleaseCount);
        Assert.Equal(1, dependencies.Pipelines.ReleaseCount);
        Assert.Equal(1, dependencies.Geometry.GeometryReleaseCount);
        Assert.Equal(1, dependencies.Geometry.InstanceReleaseCount);
        Assert.Equal(1, dependencies.Texture.ReleaseCount);
    }

    private static TestCommandFactory CreateFactory(TestDependencies? dependencies = null)
    {
        dependencies ??= new TestDependencies();

        return new TestCommandFactory(
            (Context)
                System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
                    typeof(Context)
                ),
            dependencies.SwapChain,
            dependencies.Geometry,
            dependencies.Geometry,
            dependencies.Texture,
            dependencies.Pipelines,
            dependencies.Descriptors,
            dependencies.Buffers,
            dependencies.Samplers
        )
        {
            PipelineDefinition = dependencies.PipelineDefinition,
        };
    }

    private static TestDrawable CreateDrawable(
        bool includeVertexShader = true,
        bool includeFragmentShader = true,
        IShaderContract? tessellationControlShader = null,
        IShaderContract? tessellationEvalShader = null,
        IShaderContract? geometryShader = null,
        bool includeInstanceLayout = false
    )
    {
        var format = new VertexFormat([VertexSemanticEnum.Position]);
        return new TestDrawable(
            new Mesh(
                "mesh",
                PrimitiveTopologyEnum.TriangleList,
                [new Vertex(new Vector3D<float>(0, 0, 0))]
            ),
            new TestTexture(),
            SamplingBehaviors.PixelPerfect,
            includeVertexShader
                ? new VertexShader(
                    "vertex",
                    "vertex.spv",
                    PrimitiveTopologyEnum.TriangleList,
                    format,
                    [new ShaderInput(0, 16)],
                    includeInstanceLayout ? [new ShaderInput(0, 16)] : []
                )
                : null,
            includeFragmentShader
                ? new FragmentShader(
                    "fragment",
                    "fragment.spv",
                    format,
                    ColorFormatEnum.RGBA8UNorm,
                    []
                )
                : null,
            tessellationControlShader,
            tessellationEvalShader,
            geometryShader
        );
    }

    private static IShaderContract CreateShader(string name, ShaderStageEnum stage) =>
        new TestShader(name, stage, new VertexFormat([VertexSemanticEnum.Position]));

    private static PipelineDefinition CreatePipelineDefinition(DescriptorSchema schema) =>
        new("test", null, null, null, null, null, new RenderPass(1), descriptorSchema: schema);

    private sealed class TestCommandFactory(
        Context context,
        ISwapChain swapChain,
        IVertexBufferRegistry geometryRegistry,
        IInstanceBufferRegistry instanceBufferRegistry,
        IImageRegistry textureRegistry,
        IPipelineRegistry pipelineRegistry,
        IDescriptorSetPool descriptorSetPool,
        IBufferManager bufferManager,
        ISamplerRegistry samplerRegistry
    )
        : CommandFactory(
            context,
            swapChain,
            geometryRegistry,
            instanceBufferRegistry,
            textureRegistry,
            pipelineRegistry,
            descriptorSetPool,
            bufferManager,
            samplerRegistry
        )
    {
        public PipelineDefinition PipelineDefinition { get; set; } =
            CommandFactoryTests.CreatePipelineDefinition(DescriptorSchemas.Textured);

        protected override PipelineDefinition BuildPipelineDefinition(
            PipelineDefinitionBuilder builder
        ) => PipelineDefinition;
    }

    private sealed class TestDependencies
    {
        public TestSwapChain SwapChain { get; } = new();
        public TestGeometryRegistry Geometry { get; } = new();
        public TestImageRegistry Texture { get; } = new();
        public TestPipelineRegistry Pipelines { get; } = new();
        public TestDescriptorSetPool Descriptors { get; } = new();
        public TestBufferManager Buffers { get; } = new();
        public TestSamplerRegistry Samplers { get; } = new();
        public PipelineDefinition PipelineDefinition { get; set; } =
            CreatePipelineDefinition(DescriptorSchemas.Textured);
    }

    private sealed class TestSwapChain : ISwapChain
    {
        public RenderPass[] Passes { get; } =
            Enumerable.Repeat(new RenderPass(1), RenderPasses.Count).ToArray();
        public SwapchainKHR Swapchain => default;
        public Format Format => default;
        public Extent2D Extent => default;
        public Image[] Images => [];
        public ImageView[] ImageViews => [];
        public Framebuffer[][] Framebuffers => [];
        public Image DepthImage => default;
        public bool HasDepthAttachment => false;
        public event EventHandler<PresentEventArgs>? BeforePresent
        {
            add { }
            remove { }
        }

        public void Recreate() { }

        public uint AcquireNextImage(Semaphore imageAvailableSemaphore, out Result result)
        {
            result = Result.Success;
            return 0;
        }

        public void Present(uint imageIndex, Semaphore renderFinishedSemaphore) { }

        public void Dispose() { }
    }

    private sealed class TestGeometryRegistry : IVertexBufferRegistry, IInstanceBufferRegistry
    {
        public int CreateCount { get; private set; }
        public int InstanceCreateCount { get; private set; }
        public int UpdateCount { get; private set; }
        public int GeometryReleaseCount { get; private set; }
        public int InstanceReleaseCount { get; private set; }

        public IEnumerable<IVulkanCommand> Create(IDrawable drawable, ShaderInput[] layout)
        {
            InstanceCreateCount++;
            return [];
        }

        public IEnumerable<IVulkanCommand> Create(IGeometry geometry, VertexFormat format)
        {
            CreateCount++;
            return [];
        }

        public IEnumerable<IVulkanCommand> Update(IGeometry geometry, VertexFormat format)
        {
            UpdateCount++;
            return [];
        }

        public VkBuffer Get(MeshId meshId, VertexFormatId formatId) => new(11);

        public VkBuffer Get(DrawableId drawableId) => new(12);

        public IEnumerable<IVulkanCommand> Release(IGeometry geometry, VertexFormat format)
        {
            GeometryReleaseCount++;
            return [];
        }

        public IEnumerable<IVulkanCommand> Release(DrawableId drawableId)
        {
            InstanceReleaseCount++;
            return [];
        }

        public void Reset() { }

        public void Dispose() { }
    }

    private sealed class TestImageRegistry : IImageRegistry
    {
        public int CreateCount { get; private set; }
        public int UpdateCount { get; private set; }
        public int ReleaseCount { get; private set; }

        public IEnumerable<IVulkanCommand> Create(ITexture texture, ColorFormatEnum format)
        {
            CreateCount++;
            return [];
        }

        public ImageView Get(ITexture texture, ColorFormatEnum format) => new(12);

        public IEnumerable<IVulkanCommand> Update(ITexture texture, ColorFormatEnum format)
        {
            UpdateCount++;
            return [];
        }

        public IEnumerable<IVulkanCommand> Release(ITexture texture, ColorFormatEnum format)
        {
            ReleaseCount++;
            return [];
        }

        public void Reset() { }

        public void Dispose() { }
    }

    private sealed class TestPipelineRegistry : IPipelineRegistry
    {
        public int GetOrCreateCount { get; private set; }
        public int ReleaseCount { get; private set; }

        public (Pipeline pipeline, PipelineLayout layout) GetOrCreate(
            PipelineDefinition description
        )
        {
            GetOrCreateCount++;
            return (new(20), new(21));
        }

        public Pipeline Get(PipelineId id) => new(20);

        public PipelineLayout GetLayout(PipelineId id) => new(21);

        public DescriptorSetLayout GetDescriptorSetLayout(PipelineId pipelineId, uint set) =>
            new(30 + set);

        public int GetDescriptorSetLayoutCount(PipelineId pipelineId) => 2;

        public void Release(PipelineId id) => ReleaseCount++;

        public void Dispose() { }
    }

    private sealed class TestDescriptorSetPool : IDescriptorSetPool
    {
        public int AllocateCount { get; private set; }
        public int UniformWriteCount { get; private set; }
        public int ImageSamplerWriteCount { get; private set; }
        public int ReleaseCount { get; private set; }

        public DescriptorSet Allocate(DescriptorSetLayout layout)
        {
            AllocateCount++;
            return new((ulong)(40 + AllocateCount));
        }

        public void WriteUniformBuffer(
            DescriptorSet descriptorSet,
            uint binding,
            VkBuffer buffer,
            ulong offset,
            ulong range
        ) => UniformWriteCount++;

        public void WriteCombinedImageSampler(
            DescriptorSet descriptorSet,
            uint binding,
            ImageView imageView,
            VkSampler sampler
        ) => ImageSamplerWriteCount++;

        public void Release(DescriptorSet descriptorSet) => ReleaseCount++;

        public void Dispose() { }
    }

    private sealed class TestBufferManager : IBufferManager
    {
        public int CreateUniformBufferCount { get; private set; }
        public int UpdateBufferCount { get; private set; }
        public int DestroyBufferCount { get; private set; }

        public VkBuffer CreateVertexBuffer(ReadOnlySpan<byte> data) => new(1);

        public VkBuffer CreateIndexBuffer(ReadOnlySpan<byte> data) => new(2);

        public VkBuffer CreateUniformBuffer(ulong size)
        {
            CreateUniformBufferCount++;
            return new(3);
        }

        public VkBuffer CreateStorageBuffer(ulong size) => new(4);

        public void UpdateBuffer(VkBuffer buffer, ReadOnlySpan<byte> data) => UpdateBufferCount++;

        public void DestroyBuffer(VkBuffer buffer) => DestroyBufferCount++;
    }

    private sealed class TestSamplerRegistry : ISamplerRegistry
    {
        public int CreateCount { get; private set; }
        public int ReleaseCount { get; private set; }

        public void Create(ISamplingBehavior behavior) => CreateCount++;

        public VkSampler Get(SamplingBehaviorId id) => new(13);

        public void Release(ISamplingBehavior behavior) => ReleaseCount++;

        public void Reset() { }

        public void Dispose() { }
    }

    private sealed class TestTexture : ITexture
    {
        public TextureId Id => 1;
        public ContentId ContentId => "test";
        public uint Width => 1;
        public uint Height => 1;
        public ulong Count => 1;

        public void WriteTo(ulong start, ulong count, ColorFormatEnum format, Span<byte> target) { }
    }

    private sealed class TestDrawable(
        Mesh mesh,
        ITexture texture,
        ISamplingBehavior samplingBehavior,
        VertexShader? vertexShader,
        FragmentShader? fragmentShader,
        IShaderContract? tessellationControlShader,
        IShaderContract? tessellationEvalShader,
        IShaderContract? geometryShader
    ) : IDrawable
    {
        event EventHandler? IDrawable.RenderLayerChanged
        {
            add { }
            remove { }
        }
        event EventHandler? IDrawable.MeshChanged
        {
            add { }
            remove { }
        }
        event EventHandler? IDrawable.TextureChanged
        {
            add { }
            remove { }
        }
        event EventHandler? IDrawable.InstanceDataChanged
        {
            add { }
            remove { }
        }
        event EventHandler? IDrawable.UniformDataChanged
        {
            add { }
            remove { }
        }
        event EventHandler? IDrawable.ShaderChanged
        {
            add { }
            remove { }
        }

        public DrawableId Id => new(1);
        public ulong RenderLayerMask => ulong.MaxValue;
        public Mesh Mesh => mesh;
        public ITexture Texture { get; set; } = texture;
        public ulong InstanceCount { get; set; } = 1;
        public ISamplingBehavior SamplingBehavior => samplingBehavior;

        public ReadOnlyMemory<byte> GetInstanceData(ShaderInput[] layout) =>
            ReadOnlyMemory<byte>.Empty;

        public ReadOnlyMemory<byte> GetUniformData(ShaderInput[] layout) => new byte[16];

        public VertexShader? VertexShader => vertexShader;
        public IShaderContract? TessellationControlShader => tessellationControlShader;
        public IShaderContract? TessellationEvalShader => tessellationEvalShader;
        public IShaderContract? GeometryShader => geometryShader;
        public FragmentShader? FragmentShader => fragmentShader;
    }

    private sealed class TestShader(string name, ShaderStageEnum stage, VertexFormat vertexFormat)
        : IShaderContract
    {
        public DrawableId Id => new((ulong)name.GetHashCode());
        public string Name => name;
        public string SourceFileName => name + ".spv";
        public ShaderStageEnum Stage => stage;
        public VertexFormat VertexFormat => vertexFormat;
        public ShaderInput[] UniformLayout => [];
    }
}
