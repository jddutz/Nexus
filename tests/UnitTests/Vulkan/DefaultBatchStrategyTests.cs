namespace Tests;

using Nexus.Graphics;
using Nexus.Graphics.Geometry;
using Nexus.Graphics.Shaders;
using Nexus.Graphics.Textures;
using Nexus.Graphics.Vulkan.Commands;
using Nexus.Graphics.Vulkan.Pipelines;
using Nexus.Graphics.Vulkan.Rendering;
using Silk.NET.Vulkan;

public class DefaultBatchStrategyTests
{
    [Fact]
    public void Compare_groupsRenderPassCommandsByDrawableBeforePriority()
    {
        var drawable1 = new TestDrawable(1);
        var drawable2 = new TestDrawable(2);
        var pipelineId = new PipelineId(7);
        var commands = new[]
        {
            new TestCommand("draw-2", drawable2, pipelineId, int.MaxValue),
            new TestCommand("bind-1", drawable1, pipelineId, 0),
            new TestCommand("null", null, pipelineId, 0),
            new TestCommand("descriptor-2", drawable2, pipelineId, 2),
            new TestCommand("draw-1", drawable1, pipelineId, int.MaxValue),
            new TestCommand("vertex-2", drawable2, pipelineId, 1),
            new TestCommand("descriptor-1", drawable1, pipelineId, 2),
            new TestCommand("vertex-1", drawable1, pipelineId, 1),
        };

        var ordered = commands.OrderBy(command => command, new DefaultBatchStrategy());

        Assert.Equal(
            new[] { "null", "bind-1", "vertex-1", "descriptor-1", "draw-1", "vertex-2", "descriptor-2", "draw-2" },
            ordered.Select(command => command.Name)
        );
    }

    private sealed class TestCommand(
        string name,
        IDrawable? drawable,
        PipelineId pipelineId,
        int renderPriority
    ) : IVulkanCommand
    {
        public string Name { get; } = name;

        public Guid Id { get; } = Guid.NewGuid();

        public bool IsSticky => true;

        public RenderPhase Phase => RenderPhase.RenderPass;

        public uint RenderPass => 1;

        public PipelineId? PipelineId => pipelineId;

        public IDrawable? Drawable => drawable;

        public int RenderPriority => renderPriority;

        public void Record(Vk vk, CommandBuffer commandBuffer) { }
    }

    private sealed class TestDrawable(ulong id) : IDrawable
    {
        public DrawableId Id => id;

        public IEnumerable<RenderLayer> RenderLayers => [];

        public Mesh Mesh => null!;

        public ITexture Texture => null!;

        public ISamplingBehavior SamplingBehavior => null!;

        public IInstanceDataSource Instances => null!;

        public ReadOnlyMemory<byte> GetUniformData(ShaderInput[] layout) => ReadOnlyMemory<byte>.Empty;

        public VertexShader? VertexShader => null;

        public IShaderContract? TessellationControlShader => null;

        public IShaderContract? TessellationEvalShader => null;

        public IShaderContract? GeometryShader => null;

        public FragmentShader? FragmentShader => null;
    }
}