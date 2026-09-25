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
            new[]
            {
                "null",
                "bind-1",
                "vertex-1",
                "descriptor-1",
                "draw-1",
                "vertex-2",
                "descriptor-2",
                "draw-2",
            },
            ordered.Select(command => command.Name)
        );
    }

    [Fact]
    public void RenderBatch_retainsCommandsWithMatchingSortKeysAndDistinctIds()
    {
        var drawable = new TestDrawable(1);
        var pipelineId = new PipelineId(7);
        var first = new TestCommand("first", drawable, pipelineId, int.MaxValue);
        var second = new TestCommand("second", drawable, pipelineId, int.MaxValue);
        var batch = new RenderBatch(new DefaultBatchStrategy());

        Assert.True(batch.Add(first));
        Assert.True(batch.Add(second));

        Assert.Equal(2, batch.Commands.Count());
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

        public uint RenderPassMask => RenderPasses.Main;

        public PipelineId? PipelineId => pipelineId;

        public IDrawable? Drawable => drawable;

        public int RenderPriority => renderPriority;

        public void Record(Vk vk, CommandBuffer commandBuffer) { }
    }

    private sealed class TestDrawable(ulong id) : IDrawable
    {
        event EventHandler? IDrawable.RenderLayerChanged { add { } remove { } }
        event EventHandler? IDrawable.MeshChanged { add { } remove { } }
        event EventHandler? IDrawable.TextureChanged { add { } remove { } }
        event EventHandler? IDrawable.InstanceDataChanged { add { } remove { } }
        event EventHandler? IDrawable.UniformDataChanged { add { } remove { } }
        event EventHandler? IDrawable.ShaderChanged { add { } remove { } }

        public DrawableId Id => id;

        public ulong RenderLayerMask => 0;

        public Mesh Mesh => null!;

        public ITexture Texture => null!;

        public ulong InstanceCount => 1;

        public ISamplingBehavior SamplingBehavior => null!;

        public ReadOnlyMemory<byte> GetInstanceData(ShaderInput[] layout) =>
            ReadOnlyMemory<byte>.Empty;

        public ReadOnlyMemory<byte> GetUniformData(ShaderInput[] layout) =>
            ReadOnlyMemory<byte>.Empty;

        public VertexShader? VertexShader => null;

        public IShaderContract? TessellationControlShader => null;

        public IShaderContract? TessellationEvalShader => null;

        public IShaderContract? GeometryShader => null;

        public FragmentShader? FragmentShader => null;
    }
}
