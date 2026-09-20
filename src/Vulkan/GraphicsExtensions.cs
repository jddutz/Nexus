using VkPrimitiveTopology = Silk.NET.Vulkan.PrimitiveTopology;

namespace Nexus.Graphics.Vulkan;

public static class GraphicsExtensions
{
    /// <summary>Converts a Nexus color format to its Vulkan image format.</summary>
    /// <param name="format">The Nexus color format.</param>
    /// <returns>The corresponding Vulkan format.</returns>
    /// <exception cref="NotSupportedException">Thrown when Vulkan has no direct format for the channel order.</exception>
    public static Format ToVulkanFormat(this ColorFormatEnum format) =>
        format switch
        {
            ColorFormatEnum.RGB8UNorm => Format.R8G8B8Unorm,
            ColorFormatEnum.RGBA8UNorm => Format.R8G8B8A8Unorm,
            ColorFormatEnum.RGB16UNorm => Format.R16G16B16Unorm,
            ColorFormatEnum.RGBA16UNorm => Format.R16G16B16A16Unorm,
            ColorFormatEnum.RGB16Float => Format.R16G16B16Sfloat,
            ColorFormatEnum.RGBA16Float => Format.R16G16B16A16Sfloat,
            ColorFormatEnum.RGB32UInt => Format.R32G32B32Uint,
            ColorFormatEnum.RGBA32UInt => Format.R32G32B32A32Uint,
            ColorFormatEnum.RGB32Float => Format.R32G32B32Sfloat,
            ColorFormatEnum.RGBA32Float => Format.R32G32B32A32Sfloat,
            _ => throw new NotSupportedException(
                $"Color format '{format}' has no corresponding Vulkan format."
            ),
        };

    /// <summary>Converts a semantic in a vertex format to its Vulkan attribute format.</summary>
    /// <param name="vertexFormat">The vertex buffer layout.</param>
    /// <param name="semantic">The semantic whose Vulkan format should be returned.</param>
    /// <returns>The Vulkan vertex attribute format.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a format or semantic is unsupported.</exception>
    /// <exception cref="NotSupportedException">Thrown when Vulkan cannot represent the channel order.</exception>
    public static Format ToVulkanFormat(
        this VertexFormat vertexFormat,
        VertexSemanticEnum semantic
    ) =>
        semantic switch
        {
            VertexSemanticEnum.Position => vertexFormat.PositionFormat switch
            {
                VectorFormatEnum.Float2D => Format.R32G32Sfloat,
                VectorFormatEnum.Float3D => Format.R32G32B32Sfloat,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(vertexFormat),
                    vertexFormat.PositionFormat,
                    "Unsupported position format."
                ),
            },
            VertexSemanticEnum.Normal => Format.R32G32B32Sfloat,
            VertexSemanticEnum.TexCoord => Format.R32G32Sfloat,
            VertexSemanticEnum.Color => vertexFormat.ColorFormat.ToVulkanFormat(),
            _ => throw new ArgumentOutOfRangeException(nameof(semantic), semantic, null),
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
