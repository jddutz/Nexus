namespace Nexus.Graphics;

public interface IDrawCommand
{
    ulong PipelineId { get; }

    ulong DescriptorSetId { get; }

    ulong VertexBufferId { get; }

    ulong IndexBufferId { get; }

    object? PushConstants { get; }

    int RenderPriority { get; }

    float DepthSortKey { get; }
}
