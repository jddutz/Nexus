namespace Nexus.Graphics.Abstractions;

/// <summary>
/// Interface for fluent pipeline configuration.
/// Implement this interface to create custom pipeline builders.
/// </summary>
public interface IPipelineBuilder
{
    /// <summary>Sets the shader resource for this pipeline.</summary>
    IPipelineBuilder WithShader(
        ShaderResource shader,
        ShaderStageFlags flags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit
    );

    /// <summary>Sets the shader definition to be loaded from the resource manager on Build().</summary>
    IPipelineBuilder WithShader(ShaderDefinition shaderDefinition);

    /// <summary>Sets the primitive topology (default: TriangleList).</summary>
    IPipelineBuilder WithTopology(PrimitiveTopology topology);

    /// <summary>Sets the face culling mode (default: BackBit).</summary>
    IPipelineBuilder WithCullMode(CullModeFlags cullMode);

    /// <summary>Sets the front face winding order (default: CounterClockwise).</summary>
    IPipelineBuilder WithFrontFace(FrontFace frontFace);

    /// <summary>Enables or disables depth testing (default: enabled).</summary>
    IPipelineBuilder WithDepthTest(bool enable = true);

    /// <summary>Enables or disables depth writes (default: enabled).</summary>
    IPipelineBuilder WithDepthWrite(bool enable = true);

    /// <summary>Enables or disables blending (default: disabled).</summary>
    IPipelineBuilder WithBlending(bool enable = true);

    /// <summary>Sets the subpass index within the render pass (default: 0).</summary>
    IPipelineBuilder WithSubpass(uint subpass);

    /// <summary>
    /// Sets the target render pass for this pipeline using a render pass mask
    /// (e.g. RenderPasses.Main). Supports only single-pass masks.
    /// </summary>
    /// <exception cref="ArgumentException">If mask contains multiple bits set or is zero.</exception>
    IPipelineBuilder WithRenderPasses(uint renderPassMask);

    /// <summary>
    /// Builds the pipeline using the configured settings and returns the pipeline handle.
    /// The pipeline is registered with the pipeline manager and cached for reuse.
    /// </summary>
    /// <returns>Pipeline handle, ready for use in draw commands.</returns>
    /// <exception cref="InvalidOperationException">Thrown if required fields are missing.</exception>
    PipelineHandle Build(string name);
}
