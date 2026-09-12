namespace Nexus.Graphics.Vulkan.Pipelines;

/// <summary>
/// Describes the configuration used to create a graphics pipeline.
/// Used as a key for pipeline caching and as input for pipeline creation.
/// </summary>
/// <remarks>
/// <para>This record includes the shader stages, vertex input, render-pass
/// compatibility, and fixed-function state required by a Vulkan graphics pipeline.</para>
/// <para><strong>Design notes:</strong></para>
/// <list type="bullet">
/// <item>The record provides value-based equality for pipeline caching.</item>
/// <item>Properties use init-only setters so a description cannot be changed after initialization.</item>
/// <item>Default values provide a conventional triangle-list pipeline with depth testing enabled.</item>
/// </list>
///
/// <para><strong>Usage:</strong></para>
/// <code>
/// var description = new PipelineDescription
/// {
///     Name = "MyPipeline",
///     Shaders = shaders,
///     VertexInputDescription = MyVertex.GetDescription(),
///     Topology = PrimitiveTopology.TriangleList,
///     RenderPass = renderPass,
///     EnableDepthTest = true
/// };
/// </code>
/// </remarks>
public record PipelineDescription
{
    /// <summary>
    /// Unique name for this pipeline (used for debugging and caching).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Shader resource containing compiled shader modules.
    /// If provided, takes precedence over shader paths.
    /// </summary>
    public required ShaderDescription[] Shaders { get; init; }

    /// <summary>
    /// Describes the vertex buffer bindings used by the pipeline.
    /// </summary>
    public VertexInputBindingDescription[] VertexBindings { get; init; } = [];

    /// <summary>
    /// Describes how vertex attributes are mapped from vertex buffers to shader inputs.
    /// </summary>
    public VertexInputAttributeDescription[] VertexAttributes { get; init; } = [];

    /// <summary>
    /// Primitive topology (point list, line list, triangle list, etc.).
    /// </summary>
    public PrimitiveTopology Topology { get; init; } = PrimitiveTopology.TriangleList;

    /// <summary>
    /// Target render pass this pipeline will be used with.
    /// Pipeline must be compatible with this render pass.
    /// </summary>
    public required RenderPass RenderPass { get; init; }

    /// <summary>
    /// Subpass index within the render pass.
    /// </summary>
    public uint Subpass { get; init; } = 0;

    /// <summary>
    /// Enable depth testing (write and compare against depth buffer).
    /// </summary>
    public bool EnableDepthTest { get; init; } = true;

    /// <summary>
    /// Enable depth writes (update depth buffer).
    /// </summary>
    public bool EnableDepthWrite { get; init; } = true;

    /// <summary>
    /// Depth comparison operation (Less, LessOrEqual, Greater, etc.).
    /// </summary>
    public CompareOp DepthCompareOp { get; init; } = CompareOp.Less;

    /// <summary>
    /// Enable alpha blending for color attachments.
    /// </summary>
    public bool EnableBlending { get; init; } = false;

    /// <summary>
    /// Source blend factor (for color blending).
    /// </summary>
    public BlendFactor SrcBlendFactor { get; init; } = BlendFactor.SrcAlpha;

    /// <summary>
    /// Destination blend factor (for color blending).
    /// </summary>
    public BlendFactor DstBlendFactor { get; init; } = BlendFactor.OneMinusSrcAlpha;

    /// <summary>
    /// Blend operation (Add, Subtract, etc.).
    /// </summary>
    public BlendOp BlendOp { get; init; } = BlendOp.Add;

    /// <summary>
    /// Polygon rasterization mode (Fill, Line, Point).
    /// </summary>
    public PolygonMode PolygonMode { get; init; } = PolygonMode.Fill;

    /// <summary>
    /// Face culling mode (None, Front, Back, FrontAndBack).
    /// </summary>
    public CullModeFlags CullMode { get; init; } = CullModeFlags.BackBit;

    /// <summary>
    /// Front face winding order (Clockwise, CounterClockwise).
    /// </summary>
    public FrontFace FrontFace { get; init; } = FrontFace.Clockwise;

    /// <summary>
    /// Line width (for line rendering).
    /// Must be 1.0 unless wideLines feature is enabled.
    /// </summary>
    public float LineWidth { get; init; } = 1.0f;

    /// <summary>
    /// Push constant ranges for shader uniforms.
    /// </summary>
    public PushConstantRange[]? PushConstantRanges { get; init; }

    /// <summary>
    /// Descriptor set layouts for shader resources (textures, buffers, etc.).
    /// </summary>
    public DescriptorSetLayout[]? DescriptorSetLayouts { get; init; }
}
