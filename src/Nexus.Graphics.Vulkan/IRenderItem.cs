using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Nexus.Graphics.Vulkan;

public interface IRenderItem
{
    Pipeline Pipeline { get; }

    PipelineLayout Layout { get; }

    VkBuffer VertexBuffer { get; }

    ulong DescriptorSetId { get; }

    ulong IndexBufferId { get; }

    object? PushConstants { get; }

    int RenderPriority { get; }

    float DepthSortKey { get; }
}
