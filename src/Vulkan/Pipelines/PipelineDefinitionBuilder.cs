namespace Nexus.Graphics.Vulkan.Pipelines;

/// <summary>
/// Builds immutable graphics pipeline descriptions from configured pipeline state.
/// </summary>
public sealed unsafe class PipelineDefinitionBuilder : IPipelineDefinitionBuilder
{
    private readonly Context _context;
    private readonly List<Shader> _shaders = [];
    private readonly List<VertexInputBindingDescription> _vertexBindings = [];
    private readonly List<VertexInputAttributeDescription> _vertexAttributes = [];
    private readonly List<PushConstantRange> _pushConstantRanges = [];
    private DescriptorSchema? _descriptorSchemaOverride;

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
    public PipelineDefinitionBuilder(string name, Context context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(context);
        _name = name;
        _context = context;
    }

    /// <summary>Adds a shader stage to the pipeline.</summary>
    /// <param name="shader">The shader stage description.</param>
    /// <returns>This builder.</returns>
    public PipelineDefinitionBuilder WithShader(Shader shader)
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

    /// <summary>Adds the vertex-buffer layout described by <paramref name="format"/>.</summary>
    /// <param name="format">The vertex buffer layout.</param>
    /// <returns>This builder.</returns>
    public PipelineDefinitionBuilder WithVertexFormat(VertexFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);

        WithVertexBinding(
            new VertexInputBindingDescription
            {
                Binding = 0,
                Stride = format.Stride,
                InputRate = VertexInputRate.Vertex,
            }
        );

        var offset = 0u;
        for (var location = 0u; location < format.Inputs.Length; location++)
        {
            var semantic = format.Inputs[(int)location];
            WithVertexAttribute(
                new VertexInputAttributeDescription
                {
                    Location = location,
                    Binding = 0,
                    Format = format.ToVulkanFormat(semantic),
                    Offset = offset,
                }
            );
            offset += GetAttributeSize(format, semantic);
        }

        return this;
    }

    /// <summary>Adds the instance-buffer layout described by <paramref name="layout"/>.</summary>
    /// <param name="layout">The instance buffer layout.</param>
    /// <returns>This builder.</returns>
    public PipelineDefinitionBuilder WithInstanceLayout(InstanceLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        const uint binding = 1;

        WithVertexBinding(
            new VertexInputBindingDescription
            {
                Binding = binding,
                Stride = layout.Stride,
                InputRate = VertexInputRate.Instance,
            }
        );

        foreach (var input in layout.Inputs)
        {
            WithVertexAttribute(
                new VertexInputAttributeDescription
                {
                    Location = input.Location,
                    Binding = binding,
                    Format = ToVulkanFormat(input.Format),
                    Offset = input.Offset,
                }
            );
        }

        return this;
    }

    private static uint GetAttributeSize(VertexFormat format, VertexSemanticEnum semantic) =>
        semantic switch
        {
            VertexSemanticEnum.Position => format.PositionFormat == VectorFormatEnum.Float2D
                ? sizeof(float) * 2u
                : sizeof(float) * 3u,
            VertexSemanticEnum.Normal => sizeof(float) * 3u,
            VertexSemanticEnum.Color => (uint)format.ColorFormat.GetBytesPerPixel(),
            VertexSemanticEnum.TexCoord => sizeof(float) * 2u,
            _ => throw new ArgumentOutOfRangeException(nameof(semantic), semantic, null),
        };

    /// <summary>Converts an instance input format to its Vulkan equivalent.</summary>
    /// <param name="format">The instance input format.</param>
    /// <returns>The corresponding Vulkan format.</returns>
    private static Format ToVulkanFormat(InstanceInputFormatEnum format) =>
        format switch
        {
            InstanceInputFormatEnum.Float4 => Format.R32G32B32A32Sfloat,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };

    /// <summary>Sets the primitive topology.</summary>
    /// <param name="topology">The primitive topology.</param>
    /// <returns>This builder.</returns>
    public PipelineDefinitionBuilder WithTopology(PrimitiveTopologyEnum topology)
    {
        _topology = topology.ToVulkanTopology();
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

    /// <summary>Sets the descriptor schema describing all descriptor sets used by the pipeline.</summary>
    /// <param name="schema">The descriptor schema.</param>
    /// <returns>This builder.</returns>
    public PipelineDefinitionBuilder WithDescriptorSchema(DescriptorSchema schema)
    {
        _descriptorSchemaOverride = schema;
        return this;
    }

    /// <summary>Builds the descriptor schema declared by the pipeline shaders.</summary>
    /// <param name="vertexShader">The pipeline vertex shader.</param>
    /// <param name="fragmentShader">The optional pipeline fragment shader.</param>
    /// <returns>The derived descriptor schema.</returns>
    private static DescriptorSchema BuildDescriptorSchema(
        VertexShader vertexShader,
        FragmentShader? fragmentShader
    )
    {
        var schema = new SchemaBuilder();
        var set = 0u;

        if (vertexShader.Resources.HasFlag(ShaderResourceFlags.Camera))
        {
            schema.AddDescriptorSet(
                set++,
                descriptorSet => descriptorSet.AddUniformBuffer(0, ShaderStageFlags.VertexBit)
            );
        }

        if (fragmentShader?.Resources.HasFlag(ShaderResourceFlags.SampledColor) == true)
        {
            schema.AddDescriptorSet(
                set,
                descriptorSet =>
                    descriptorSet.AddCombinedImageSampler(0, ShaderStageFlags.FragmentBit)
            );
        }

        return schema.Build();
    }

    /// <summary>Builds an immutable snapshot of the configured pipeline description.</summary>
    /// <returns>The pipeline description.</returns>
    public PipelineDefinition Build()
    {
        if (_name is null)
            throw new InvalidOperationException("A pipeline name is required.");
        if (_shaders.Count == 0)
            throw new InvalidOperationException("At least one shader is required.");
        if (_renderPass is null)
            throw new InvalidOperationException("A render pass is required.");

        VertexShader? vertexShader = null;
        Shader? tessellationControlShader = null;
        Shader? tessellationEvalShader = null;
        Shader? geometryShader = null;
        FragmentShader? fragmentShader = null;

        foreach (var shader in _shaders)
        {
            switch (shader.Stage)
            {
                case ShaderStageEnum.Vertex
                    when shader is VertexShader typedVertexShader && vertexShader is null:
                    vertexShader = typedVertexShader;
                    break;
                case ShaderStageEnum.Vertex:
                    throw new InvalidOperationException(
                        "The vertex stage must use a VertexShader."
                    );
                case ShaderStageEnum.TessellationControl when tessellationControlShader is null:
                    tessellationControlShader = shader;
                    break;
                case ShaderStageEnum.TessellationEval when tessellationEvalShader is null:
                    tessellationEvalShader = shader;
                    break;
                case ShaderStageEnum.Geometry when geometryShader is null:
                    geometryShader = shader;
                    break;
                case ShaderStageEnum.Fragment
                    when shader is FragmentShader typedFragmentShader && fragmentShader is null:
                    fragmentShader = typedFragmentShader;
                    break;
                case ShaderStageEnum.Fragment:
                    throw new InvalidOperationException(
                        "The fragment stage must use a FragmentShader."
                    );
                case ShaderStageEnum.Compute:
                    throw new InvalidOperationException(
                        "Compute shaders cannot be used in a graphics pipeline."
                    );
                default:
                    throw new InvalidOperationException(
                        "A graphics pipeline cannot contain duplicate or unsupported shader stages."
                    );
            }
        }

        if (vertexShader is null)
            throw new InvalidOperationException("A graphics pipeline requires a vertex shader.");

        ValidateShaderInterfaces(
            vertexShader,
            tessellationControlShader,
            tessellationEvalShader,
            geometryShader,
            fragmentShader
        );
        ValidateFragmentFormat(fragmentShader);

        WithVertexFormat(vertexShader.VertexFormat);
        WithInstanceLayout(vertexShader.InstanceLayout);

        var descriptorSchema =
            _descriptorSchemaOverride ?? BuildDescriptorSchema(vertexShader, fragmentShader);

        return new PipelineDefinition(
            _name,
            vertexShader,
            tessellationControlShader,
            tessellationEvalShader,
            geometryShader,
            fragmentShader,
            _renderPass.Value,
            _vertexBindings,
            _vertexAttributes,
            _topology,
            _subpass,
            _enableDepthTest,
            _enableDepthWrite,
            _depthCompareOp,
            _enableBlending,
            _srcBlendFactor,
            _dstBlendFactor,
            _blendOp,
            _polygonMode,
            _cullMode,
            _frontFace,
            _lineWidth,
            _pushConstantRanges,
            descriptorSchema
        );
    }

    /// <summary>Validates that each adjacent graphics stage exposes the same vertex interface.</summary>
    /// <param name="vertexShader">The pipeline vertex shader.</param>
    /// <param name="tessellationControlShader">The optional tessellation-control shader.</param>
    /// <param name="tessellationEvalShader">The optional tessellation-evaluation shader.</param>
    /// <param name="geometryShader">The optional geometry shader.</param>
    /// <param name="fragmentShader">The optional fragment shader.</param>
    /// <exception cref="InvalidOperationException">Thrown when adjacent shader interfaces differ.</exception>
    private static void ValidateShaderInterfaces(
        VertexShader vertexShader,
        Shader? tessellationControlShader,
        Shader? tessellationEvalShader,
        Shader? geometryShader,
        FragmentShader? fragmentShader
    )
    {
        Shader? previous = vertexShader;
        foreach (
            var current in new[]
            {
                tessellationControlShader,
                tessellationEvalShader,
                geometryShader,
                fragmentShader,
            }
        )
        {
            if (current is null)
                continue;

            if (previous!.VertexFormat.Id != current.VertexFormat.Id)
            {
                throw new InvalidOperationException(
                    $"Shader stages '{previous.Name}' and '{current.Name}' have incompatible vertex formats."
                );
            }

            previous = current;
        }
    }

    /// <summary>Validates the fragment shader output format against the selected physical device.</summary>
    /// <param name="fragmentShader">The optional fragment shader.</param>
    /// <exception cref="NotSupportedException">Thrown when the format is unsupported for sampled images.</exception>
    private void ValidateFragmentFormat(FragmentShader? fragmentShader)
    {
        if (fragmentShader is null)
            return;

        var format = fragmentShader.ColorFormat.ToVulkanFormat();
        _context.VulkanApi.GetPhysicalDeviceFormatProperties(
            _context.PhysicalDevice,
            format,
            out var properties
        );

        if ((properties.OptimalTilingFeatures & FormatFeatureFlags.SampledImageBit) == 0)
        {
            throw new NotSupportedException(
                $"Fragment color format '{fragmentShader.ColorFormat}' ({format}) is not supported for sampled images by the selected Vulkan device."
            );
        }
    }
}
