using VkPrimitiveTopology = Silk.NET.Vulkan.PrimitiveTopology;

namespace Nexus.Graphics.Vulkan;

public static class GraphicsExtensions
{
    public static Format ToVulkanFormat(this VertexFormatEnum format) =>
        format switch
        {
            VertexFormatEnum.Float => Format.R32Sfloat,
            VertexFormatEnum.Float2 => Format.R32G32Sfloat,
            VertexFormatEnum.Float3 => Format.R32G32B32Sfloat,
            VertexFormatEnum.Float4 => Format.R32G32B32A32Sfloat,

            VertexFormatEnum.Int => Format.R32Sint,
            VertexFormatEnum.Int2 => Format.R32G32Sint,
            VertexFormatEnum.Int3 => Format.R32G32B32Sint,
            VertexFormatEnum.Int4 => Format.R32G32B32A32Sint,

            VertexFormatEnum.UInt => Format.R32Uint,
            VertexFormatEnum.UInt2 => Format.R32G32Uint,
            VertexFormatEnum.UInt3 => Format.R32G32B32Uint,
            VertexFormatEnum.UInt4 => Format.R32G32B32A32Uint,

            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };

    public static VkPrimitiveTopology ToVulkanTopology(this PrimitiveTopologyEnum topology) =>
        topology switch
        {
            PrimitiveTopologyEnum.TriangleList => VkPrimitiveTopology.TriangleList,

            PrimitiveTopologyEnum.TriangleStrip => VkPrimitiveTopology.TriangleStrip,

            PrimitiveTopologyEnum.LineList => VkPrimitiveTopology.LineList,

            PrimitiveTopologyEnum.LineStrip => VkPrimitiveTopology.LineStrip,

            PrimitiveTopologyEnum.PointList => VkPrimitiveTopology.PointList,

            _ => throw new ArgumentOutOfRangeException(
                nameof(topology),
                topology,
                "Unsupported primitive topology."
            ),
        };
}
