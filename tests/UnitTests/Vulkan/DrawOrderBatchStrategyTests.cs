namespace Tests;

using Nexus.Core;
using Nexus.Graphics;
using Nexus.Graphics.Geometry;
using Nexus.Graphics.Shaders;
using Nexus.Graphics.Textures;
using Nexus.Graphics.Vulkan.Commands;
using Nexus.Graphics.Vulkan.Pipelines;
using Nexus.Graphics.Vulkan.Rendering;
using Silk.NET.Vulkan;

/// <summary>Verifies draw-order batch command ordering.</summary>
public sealed class DrawOrderBatchStrategyTests
{
    [Fact]
    public void Batch_reorders_changed_drawables_and_replaces_old_instance_buffer_bindings()
    {
        var first = new TestDrawable(1) { DrawOrder = 0 };
        var second = new TestDrawable(2) { DrawOrder = 1 };
        var batch = new RenderBatch(new DrawOrderBatchStrategy());
        var pipeline = new PipelineId(1);
        var mesh = new BindVertexBufferCommand(RenderPasses.Main, pipeline, first, 0, new Buffer(10));
        var oldInstances = new BindVertexBufferCommand(RenderPasses.Main, pipeline, first, 1, new Buffer(11));
        var firstDraw = new TestCommand("first", first, pipeline, long.MaxValue / 2);
        var secondDraw = new TestCommand("second", second, pipeline, long.MaxValue / 2);
        batch.Add(mesh);
        batch.Add(oldInstances);
        batch.Add(firstDraw);
        batch.Add(secondDraw);
        first.DrawOrder = 2;
        var replacement = new BindVertexBufferCommand(RenderPasses.Main, pipeline, first, 1, new Buffer(12));
        batch.Add(replacement);
        var ordered = batch.Commands.ToArray();
        Assert.Same(secondDraw, ordered[0]);
        Assert.Same(firstDraw, ordered[^1]);
        Assert.DoesNotContain(oldInstances, ordered);
        Assert.Contains(mesh, ordered);
        Assert.Contains(replacement, ordered);
        batch.RemoveCommand(replacement.Id);
        Assert.DoesNotContain(replacement, batch.Commands);
        batch.Remove(first.Id);
        Assert.Equal(new[] { secondDraw }, batch.Commands);
    }

    /// <summary>Verifies draw order precedes pipeline identity in the sort keys.</summary>
    [Fact]
    public void Compare_orders_draw_order_before_pipeline_id()
    {
        var background = new TestDrawable(1) { DrawOrder = -1 };
        var text = new TestDrawable(2) { DrawOrder = 1 };
        var commands = new[]
        {
            new TestCommand("text", text, new PipelineId(1), 10),
            new TestCommand("background", background, new PipelineId(2), 0),
        };

        var ordered = commands.OrderBy(command => command, new DrawOrderBatchStrategy());

        Assert.Equal(["background", "text"], ordered.Select(command => command.Name));
    }

    private sealed class TestCommand(
        string name,
        IDrawable drawable,
        PipelineId pipelineId,
        long renderPriority
    ) : IVulkanCommand
    {
        /// <summary>Gets the command name used by the test.</summary>
        public string Name { get; } = name;

        /// <inheritdoc />
        public Guid Id { get; } = Guid.NewGuid();

        /// <inheritdoc />
        public bool IsSticky => true;

        /// <inheritdoc />
        public uint RenderPassMask => RenderPasses.Main;

        /// <inheritdoc />
        public PipelineId? PipelineId => pipelineId;

        /// <inheritdoc />
        public IDrawable Drawable => drawable;

        /// <inheritdoc />
        public long RenderPriority => renderPriority;

        /// <inheritdoc />
        public void Record(Vk vk, CommandBuffer commandBuffer) { }
    }

    private sealed class TestDrawable(ulong id) : IDrawable
    {
        /// <inheritdoc />
        event Action<string>? IObservable.PropertyChanged
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        event EventHandler? IDrawable.InstanceDataChanged
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        event EventHandler? IDrawable.UniformDataChanged
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        public DrawableId Id => id;

        /// <inheritdoc />
        public ulong RenderLayerMask => 0;

        /// <inheritdoc />
        public int DrawOrder { get; set; }

        /// <inheritdoc />
        public Mesh Mesh => null!;

        /// <inheritdoc />
        public ITexture Texture => null!;

        /// <inheritdoc />
        public ulong InstanceCount => 1;

        /// <inheritdoc />
        public ISamplingBehavior SamplingBehavior => null!;

        /// <inheritdoc />
        public VertexShader? VertexShader => null;

        /// <inheritdoc />
        public IShaderContract? TessellationControlShader => null;

        /// <inheritdoc />
        public IShaderContract? TessellationEvalShader => null;

        /// <inheritdoc />
        public IShaderContract? GeometryShader => null;

        /// <inheritdoc />
        public FragmentShader? FragmentShader => null;

        /// <inheritdoc />
        public void WriteInstanceDataTo(
            ulong start,
            ulong count,
            ShaderInput[] layout,
            Span<byte> target
        ) { }

        /// <inheritdoc />
        public void WriteUniformDataTo(
            ulong start,
            ulong count,
            ShaderInput[] layout,
            Span<byte> target
        ) { }
    }
}
