namespace Nexus.Graphics.Vulkan;

public interface IDrawCommand
{
    Pipeline Pipeline { get; }

    PipelineLayout Layout { get; }

    ulong DescriptorSetId { get; }

    ulong VertexBufferId { get; }

    ulong IndexBufferId { get; }

    object? PushConstants { get; }

    int RenderPriority { get; }

    float DepthSortKey { get; }
}
