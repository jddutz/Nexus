namespace Nexus.Graphics.Vulkan.Pipelines;

/// <summary>
/// Fluent builder for creating graphics pipelines.
/// Provides a readable API for configuring pipeline state.
/// </summary>
public class PipelineBuilder(
    PipelineManager manager,
    ISwapChain swapChain,
    IResourceManager resources,
    IDescriptorManager descriptorManager
) : IPipelineBuilder
{
    private ShaderResource? _shader;
    private ShaderDefinition? _shaderDefinition;
    private ShaderStageFlags _shaderStages =
        ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit;
    private RenderPass? _renderPass;
    private PrimitiveTopology _topology = PrimitiveTopology.TriangleList;
    private CullModeFlags _cullMode = CullModeFlags.BackBit;
    private FrontFace _frontFace = FrontFace.CounterClockwise;
    private bool _enableDepthTest = true;
    private bool _enableDepthWrite = true;
    private bool _enableBlending;
    private uint _subpass;

    /// <inheritdoc/>
    public IPipelineBuilder WithShader(
        ShaderResource shader,
        ShaderStageFlags flags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit
    )
    {
        _shader = shader ?? throw new ArgumentNullException(nameof(shader));
        _shaderStages = flags;
        return this;
    }

    /// <inheritdoc/>
    public IPipelineBuilder WithShader(ShaderDefinition shaderDefinition)
    {
        _shaderDefinition =
            shaderDefinition ?? throw new ArgumentNullException(nameof(shaderDefinition));
        return this;
    }

    /// <inheritdoc/>
    public IPipelineBuilder WithTopology(PrimitiveTopology topology)
    {
        _topology = topology;
        return this;
    }

    /// <inheritdoc/>
    public IPipelineBuilder WithCullMode(CullModeFlags cullMode)
    {
        _cullMode = cullMode;
        return this;
    }

    /// <inheritdoc/>
    public IPipelineBuilder WithFrontFace(FrontFace frontFace)
    {
        _frontFace = frontFace;
        return this;
    }

    /// <inheritdoc/>
    public IPipelineBuilder WithDepthTest(bool enable = true)
    {
        _enableDepthTest = enable;
        return this;
    }

    /// <inheritdoc/>
    public IPipelineBuilder WithDepthWrite(bool enable = true)
    {
        _enableDepthWrite = enable;
        return this;
    }

    /// <inheritdoc/>
    public IPipelineBuilder WithBlending(bool enable = true)
    {
        _enableBlending = enable;
        return this;
    }

    /// <inheritdoc/>
    public IPipelineBuilder WithSubpass(uint subpass)
    {
        _subpass = subpass;
        return this;
    }

    /// <inheritdoc/>
    public IPipelineBuilder WithRenderPasses(uint renderPassMask)
    {
        // Ensure only a single bit is set
        if (renderPassMask == 0 || (renderPassMask & (renderPassMask - 1)) != 0)
        {
            throw new ArgumentException(
                "WithRenderPasses only supports a single render pass.",
                nameof(renderPassMask)
            );
        }

        // Convert bit flag to array index using Log2
        int passIndex = (int)Math.Log2(renderPassMask);
        _renderPass = swapChain.Passes[passIndex];
        return this;
    }

    /// <inheritdoc/>
    public PipelineHandle Build(string? name)
    {
        // Get shader resource from definition if needed
        if (_shader == null && _shaderDefinition != null)
        {
            _shader = resources.Shaders.GetOrCreate(_shaderDefinition);
        }

        // Validate required fields
        if (_shader == null)
            throw new InvalidOperationException(
                "Shader is required. Call WithShader() before Build()."
            );
        if (_renderPass == null)
            throw new InvalidOperationException(
                "RenderPass is required. Call WithRenderPasses() before Build()."
            );

        // Create descriptor set layouts if shader uses descriptor sets
        GpuHandle[]? descriptorSetLayouts = null;

        if (
            _shader.Definition.DescriptorSetLayouts != null
            && _shader.Definition.DescriptorSetLayouts.Count > 0
        )
        {
            // Create one descriptor set layout per set index
            var maxSetIndex = _shader.Definition.DescriptorSetLayouts.Keys.Max();
            descriptorSetLayouts = new GpuHandle[maxSetIndex + 1];

            foreach (var (setIndex, bindings) in _shader.Definition.DescriptorSetLayouts)
            {
                descriptorSetLayouts[setIndex] = descriptorManager.CreateDescriptorSetLayout(
                    bindings
                );
            }
        }

        // Create descriptor from builder configuration
        var descriptor = new PipelineDescriptor
        {
            Name = name ?? GenerateAutomaticName(),
            ShaderResource = _shader, // Pass the loaded shader resource with compiled modules
            VertexShaderPath = _shader.Definition.Name + ".vert", // Legacy fallback for tracking
            FragmentShaderPath = _shader.Definition.Name + ".frag", // Legacy fallback for tracking
            VertexInputDescription = _shader.Definition.InputDescription,
            PushConstantRanges = _shader.Definition.PushConstantRanges,
            ShaderStageFlags = _shaderStages,
            DescriptorSetLayouts = descriptorSetLayouts
                ?.Select(h => new DescriptorSetLayout(h.Value))
                .ToArray(),
            RenderPass = _renderPass.Value,
            Topology = _topology,
            CullMode = _cullMode,
            FrontFace = _frontFace,
            EnableDepthTest = _enableDepthTest,
            EnableDepthWrite = _enableDepthWrite,
            EnableBlending = _enableBlending,
            Subpass = _subpass,
        };

        // Let the manager create and cache the pipeline, then expose the opaque handle
        return manager.GetOrCreatePipeline(descriptor).ToPipelineHandle();
    }

    private string GenerateAutomaticName()
    {
        // Generate a deterministic name from configuration
        var hash = ComputeConfigurationHash();
        return $"Pipeline_{_shader!.Name}_{hash:X8}";
    }

    private int ComputeConfigurationHash()
    {
        var hashCode = new HashCode();
        hashCode.Add(_shader?.Name);
        hashCode.Add(_renderPass);
        hashCode.Add(_topology);
        hashCode.Add(_cullMode);
        hashCode.Add(_frontFace);
        hashCode.Add(_enableDepthTest);
        hashCode.Add(_enableDepthWrite);
        hashCode.Add(_enableBlending);
        hashCode.Add(_subpass);
        return hashCode.ToHashCode();
    }
}
