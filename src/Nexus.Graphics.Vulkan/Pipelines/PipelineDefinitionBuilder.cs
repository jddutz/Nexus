namespace Nexus.Graphics.Vulkan.Pipelines;

/// <summary>
/// Builds immutable graphics pipeline descriptions from configured pipeline state.
/// </summary>
public sealed class PipelineDefinitionBuilder : IPipelineDefinitionBuilder
{
    private readonly List<ShaderDefinition> _shaders = [];
    private readonly List<VertexInputBindingDescription> _vertexBindings = [];
    private readonly List<VertexInputAttributeDescription> _vertexAttributes = [];
    private readonly List<PushConstantRange> _pushConstantRanges = [];
    private readonly List<DescriptorSetLayout> _descriptorSetLayouts = [];

    private string? _name;
    private RenderPass? _renderPass;
    private uint _subpass;
    private PrimitiveTopology _topology = PrimitiveTopology.TriangleList;
    private bool _enableDepthTest = true;
    private bool _enableDepthWrite = true;
    private CompareOp _depthCompareOp = CompareOp.Less;
    private bool _enableBlending;
    private BlendFactor _srcBlendFactor = BlendFactor.SrcAlpha;
    private BlendFactor _dstBlendFactor = BlendFactor.OneMinusSrcAlpha;
    private BlendOp _blendOp = BlendOp.Add;
    private PolygonMode _polygonMode = PolygonMode.Fill;
    private CullModeFlags _cullMode = CullModeFlags.BackBit;
    private FrontFace _frontFace = FrontFace.Clockwise;
    private float _lineWidth = 1.0f;

    /// <summary>Sets the pipeline name.</summary>
    /// <param name="name">The non-empty pipeline name.</param>
    /// <returns>This builder.</returns>
    public PipelineDefinitionBuilder WithName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
        return this;
    }

    /// <summary>Adds a shader stage to the pipeline.</summary>
    /// <param name="shader">The shader stage description.</param>
    /// <returns>This builder.</returns>
    public PipelineDefinitionBuilder WithShader(ShaderDefinition shader)
    {
        ArgumentNullException.ThrowIfNull(shader);
        _shaders.Add(shader);
        return this;
    }

    /// <summary>Adds a vertex buffer binding description.</summary>
    /// <param name="binding">The vertex binding description.</param>
    /// <returns>This builder.</returns>
    public PipelineDefinitionBuilder WithVertexBinding(VertexInputBindingDescription binding)
    {
        if (_vertexBindings.Any(x => x.Binding == binding.Binding))
            throw new ArgumentException(
                $"Vertex binding {binding.Binding} has already been defined.",
                nameof(binding)
            );

        _vertexBindings.Add(binding);
        return this;
    }

    /// <summary>Adds a vertex attribute description.</summary>
    /// <param name="attribute">The vertex attribute description.</param>
    /// <returns>This builder.</returns>
    public PipelineDefinitionBuilder WithVertexAttribute(VertexInputAttributeDescription attribute)
    {
        if (_vertexAttributes.Any(x => x.Location == attribute.Location))
            throw new ArgumentException(
                $"Vertex attribute location {attribute.Location} has already been defined.",
                nameof(attribute)
            );

        _vertexAttributes.Add(attribute);
        return this;
    }

    /// <summary>Sets the primitive topology.</summary>
    /// <param name="topology">The primitive topology.</param>
    /// <returns>This builder.</returns>
    public PipelineDefinitionBuilder WithTopology(PrimitiveTopology topology)
    {
        _topology = topology;
        return this;
    }

    /// <summary>Sets the target render pass and subpass.</summary>
    /// <param name="renderPass">The target render pass.</param>
    /// <param name="subpass">The target subpass index.</param>
    /// <returns>This builder.</returns>
    public PipelineDefinitionBuilder WithRenderPass(RenderPass renderPass, uint subpass = 0)
    {
        if (renderPass.Handle == 0)
            throw new ArgumentException(
                "The render pass handle must be valid.",
                nameof(renderPass)
            );

        _renderPass = renderPass;
        _subpass = subpass;
        return this;
    }

    /// <summary>Enables or disables depth testing.</summary>
    /// <param name="enabled">Whether depth testing is enabled.</param>
    /// <returns>This builder.</returns>
    public PipelineDefinitionBuilder WithDepthTest(bool enabled = true)
    {
        _enableDepthTest = enabled;
        return this;
    }

    /// <summary>Enables or disables depth writes.</summary>
    /// <param name="enabled">Whether depth writes are enabled.</param>
    /// <returns>This builder.</returns>
    public PipelineDefinitionBuilder WithDepthWrite(bool enabled = true)
    {
        _enableDepthWrite = enabled;
        return this;
    }

    /// <summary>Sets the depth comparison operation.</summary>
    /// <param name="compareOp">The depth comparison operation.</param>
    /// <returns>This builder.</returns>
    public PipelineDefinitionBuilder WithDepthCompare(CompareOp compareOp)
    {
        _depthCompareOp = compareOp;
        return this;
    }

    /// <summary>Enables or disables color blending.</summary>
    /// <param name="enabled">Whether color blending is enabled.</param>
    /// <returns>This builder.</returns>
    public PipelineDefinitionBuilder WithBlending(bool enabled = true)
    {
        _enableBlending = enabled;
        return this;
    }

    /// <summary>Sets the color and alpha blend factors.</summary>
    /// <param name="source">The source blend factor.</param>
    /// <param name="destination">The destination blend factor.</param>
    /// <param name="operation">The blend operation.</param>
    /// <returns>This builder.</returns>
    public PipelineDefinitionBuilder WithBlendFactors(
        BlendFactor source,
        BlendFactor destination,
        BlendOp operation = BlendOp.Add
    )
    {
        _srcBlendFactor = source;
        _dstBlendFactor = destination;
        _blendOp = operation;
        return this;
    }

    /// <summary>Sets the polygon rasterization mode.</summary>
    /// <param name="polygonMode">The polygon mode.</param>
    /// <returns>This builder.</returns>
    public PipelineDefinitionBuilder WithPolygonMode(PolygonMode polygonMode)
    {
        _polygonMode = polygonMode;
        return this;
    }

    /// <summary>Sets the face culling mode.</summary>
    /// <param name="cullMode">The culling mode.</param>
    /// <returns>This builder.</returns>
    public PipelineDefinitionBuilder WithCullMode(CullModeFlags cullMode)
    {
        _cullMode = cullMode;
        return this;
    }

    /// <summary>Sets the front-face winding order.</summary>
    /// <param name="frontFace">The front-face winding order.</param>
    /// <returns>This builder.</returns>
    public PipelineDefinitionBuilder WithFrontFace(FrontFace frontFace)
    {
        _frontFace = frontFace;
        return this;
    }

    /// <summary>Sets the rasterizer line width.</summary>
    /// <param name="lineWidth">The positive line width.</param>
    /// <returns>This builder.</returns>
    public PipelineDefinitionBuilder WithLineWidth(float lineWidth)
    {
        if (!float.IsFinite(lineWidth) || lineWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(lineWidth));

        _lineWidth = lineWidth;
        return this;
    }

    /// <summary>Adds a push-constant range.</summary>
    /// <param name="range">The push-constant range.</param>
    /// <returns>This builder.</returns>
    public PipelineDefinitionBuilder WithPushConstant(PushConstantRange range)
    {
        if (range.Size == 0)
            throw new ArgumentException(
                "The push-constant range size must be greater than zero.",
                nameof(range)
            );
        if (range.StageFlags == 0)
            throw new ArgumentException(
                "The push-constant range must specify at least one shader stage.",
                nameof(range)
            );

        _pushConstantRanges.Add(range);
        return this;
    }

    /// <summary>Adds a descriptor-set layout.</summary>
    /// <param name="layout">The descriptor-set layout.</param>
    /// <returns>This builder.</returns>
    public PipelineDefinitionBuilder WithDescriptorSetLayout(DescriptorSetLayout layout)
    {
        if (layout.Handle == 0)
            throw new ArgumentException(
                "The descriptor set layout handle must be valid.",
                nameof(layout)
            );

        _descriptorSetLayouts.Add(layout);
        return this;
    }

    /// <summary>Builds an immutable snapshot of the configured pipeline description.</summary>
    /// <returns>The pipeline description.</returns>
    public PipelineDefinition Build()
    {
        if (_name is null)
            throw new InvalidOperationException("A pipeline name is required.");
        if (_shaders.Count == 0)
            throw new InvalidOperationException("At least one shader is required.");
        if (_shaders.Any(x => x.Stage == ShaderStageEnum.Compute))
            throw new InvalidOperationException(
                "Compute shaders cannot be used in a graphics pipeline."
            );
        if (_shaders.GroupBy(x => x.Stage).Any(x => x.Count() > 1))
            throw new InvalidOperationException(
                "A graphics pipeline cannot contain duplicate shader stages."
            );
        if (!_shaders.Any(x => x.Stage == ShaderStageEnum.Vertex))
            throw new InvalidOperationException("A graphics pipeline requires a vertex shader.");
        if (_renderPass is null)
            throw new InvalidOperationException("A render pass is required.");

        return new PipelineDefinition
        {
            Name = _name,
            Shaders = [.. _shaders],
            VertexBindings = [.. _vertexBindings],
            VertexAttributes = [.. _vertexAttributes],
            Topology = _topology,
            RenderPass = _renderPass.Value,
            Subpass = _subpass,
            EnableDepthTest = _enableDepthTest,
            EnableDepthWrite = _enableDepthWrite,
            DepthCompareOp = _depthCompareOp,
            EnableBlending = _enableBlending,
            SrcBlendFactor = _srcBlendFactor,
            DstBlendFactor = _dstBlendFactor,
            BlendOp = _blendOp,
            PolygonMode = _polygonMode,
            CullMode = _cullMode,
            FrontFace = _frontFace,
            LineWidth = _lineWidth,
            PushConstantRanges = _pushConstantRanges.Count == 0 ? null : [.. _pushConstantRanges],
            DescriptorSetLayouts =
                _descriptorSetLayouts.Count == 0 ? null : [.. _descriptorSetLayouts],
        };
    }
}
