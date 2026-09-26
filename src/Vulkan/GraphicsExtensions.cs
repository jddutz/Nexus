using VkPrimitiveTopology = Silk.NET.Vulkan.PrimitiveTopology;

namespace Nexus.Graphics.Vulkan;

public static class GraphicsExtensions
{
    /// <summary>Converts a shader stage to its Vulkan stage flags.</summary>
    /// <param name="stage">The Nexus shader stage.</param>
    /// <returns>The corresponding Vulkan stage flags.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for an unsupported stage.</exception>
    public static ShaderStageFlags ToVulkanStageFlags(this ShaderStageEnum stage) =>
        stage switch
        {
            ShaderStageEnum.Vertex => ShaderStageFlags.VertexBit,
            ShaderStageEnum.TessellationControl => ShaderStageFlags.TessellationControlBit,
            ShaderStageEnum.TessellationEval => ShaderStageFlags.TessellationEvaluationBit,
            ShaderStageEnum.Geometry => ShaderStageFlags.GeometryBit,
            ShaderStageEnum.Fragment => ShaderStageFlags.FragmentBit,
            ShaderStageEnum.Compute => ShaderStageFlags.ComputeBit,
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null),
        };

    /// <summary>Gets the number of Vulkan attributes emitted for a shader input.</summary>
    /// <param name="input">The shader input.</param>
    /// <returns>The number of vertex attributes.</returns>
    public static uint GetVulkanAttributeCount(this ShaderInput input) =>
        input.Semantic == InputSemantics.Transform ? 4u : 1u;

    /// <summary>Converts an instance shader input to Vulkan vertex attributes.</summary>
    /// <param name="input">The shader input.</param>
    /// <param name="binding">The Vulkan vertex binding.</param>
    /// <param name="location">The first Vulkan attribute location.</param>
    /// <param name="offset">The byte offset in the packed instance record.</param>
    /// <returns>The Vulkan attributes represented by the input.</returns>
    /// <exception cref="NotSupportedException">Thrown for an unsupported semantic.</exception>
    /// <exception cref="ArgumentException">Thrown when the input size is incompatible.</exception>
    public static VertexInputAttributeDescription[] ToVulkanAttributes(
        this ShaderInput input,
        uint binding,
        uint location,
        uint offset
    )
    {
        var format = input.Semantic switch
        {
            InputSemantics.Transform when input.Size == 64 => Format.R32G32B32A32Sfloat,
            InputSemantics.Color or InputSemantics.TextureRegion when input.Size == 16 =>
                Format.R32G32B32A32Sfloat,
            InputSemantics.MsdfDistanceRange when input.Size == sizeof(float) =>
                Format.R32Sfloat,
            InputSemantics.Transform
                or InputSemantics.Color
                or InputSemantics.TextureRegion
                or InputSemantics.MsdfDistanceRange =>
                throw new ArgumentException(
                    $"Shader input semantic {input.Semantic} requires a supported packed size, but received {input.Size} bytes.",
                    nameof(input)
                ),
            _ => throw new NotSupportedException(
                $"Instance shader input semantic {input.Semantic} is not supported by Vulkan."
            ),
        };

        var count = input.GetVulkanAttributeCount();
        var attributes = new VertexInputAttributeDescription[count];
        for (var index = 0u; index < count; index++)
        {
            attributes[index] = new VertexInputAttributeDescription
            {
                Location = location + index,
                Binding = binding,
                Format = format,
                Offset = offset + index * 16,
            };
        }

        return attributes;
    }

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

    public static Filter ToVulkanFilter(this MagFilterEnum filter) =>
        filter switch
        {
            MagFilterEnum.Nearest => Filter.Nearest,
            MagFilterEnum.Linear => Filter.Linear,
            _ => throw new ArgumentOutOfRangeException(nameof(filter)),
        };

    public static Filter ToVulkanFilter(this MinFilterEnum filter) =>
        filter switch
        {
            MinFilterEnum.Nearest => Filter.Nearest,
            MinFilterEnum.Linear => Filter.Linear,
            MinFilterEnum.NearestMipmapNearest => Filter.Nearest,
            MinFilterEnum.NearestMipmapLinear => Filter.Nearest,
            MinFilterEnum.LinearMipmapNearest => Filter.Linear,
            MinFilterEnum.LinearMipmapLinear => Filter.Linear,
            _ => throw new ArgumentOutOfRangeException(nameof(filter)),
        };

    public static SamplerMipmapMode ToVulkanMipmapMode(this MinFilterEnum filter) =>
        filter switch
        {
            MinFilterEnum.Nearest => SamplerMipmapMode.Nearest,
            MinFilterEnum.Linear => SamplerMipmapMode.Nearest,
            MinFilterEnum.NearestMipmapNearest => SamplerMipmapMode.Nearest,
            MinFilterEnum.LinearMipmapNearest => SamplerMipmapMode.Nearest,
            MinFilterEnum.NearestMipmapLinear => SamplerMipmapMode.Linear,
            MinFilterEnum.LinearMipmapLinear => SamplerMipmapMode.Linear,
            _ => throw new ArgumentOutOfRangeException(nameof(filter)),
        };

    public static SamplerAddressMode ToVulkanAddressMode(this WrapModeEnum mode) =>
        mode switch
        {
            WrapModeEnum.ClampToEdge => SamplerAddressMode.ClampToEdge,

            WrapModeEnum.Repeat => SamplerAddressMode.Repeat,

            WrapModeEnum.MirroredRepeat => SamplerAddressMode.MirroredRepeat,

            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

    public static bool UsesMipmaps(this MinFilterEnum filter) =>
        filter
            is MinFilterEnum.NearestMipmapNearest
                or MinFilterEnum.LinearMipmapNearest
                or MinFilterEnum.NearestMipmapLinear
                or MinFilterEnum.LinearMipmapLinear;
}
