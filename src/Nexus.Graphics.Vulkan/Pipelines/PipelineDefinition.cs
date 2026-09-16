public sealed record PipelineDefinition
{
    public PipelineId Id { get; }

    public string Name { get; }

    public ShaderDescription? VertexShader { get; }
    public ShaderDescription? TessellationControlShader { get; }
    public ShaderDescription? TessellationEvalShader { get; }
    public ShaderDescription? GeometryShader { get; }
    public ShaderDescription? FragmentShader { get; }

    public ImmutableArray<VertexInputBindingDescription> VertexBindings { get; }

    public ImmutableArray<VertexInputAttributeDescription> VertexAttributes { get; }

    public PrimitiveTopology Topology { get; }

    public RenderPass RenderPass { get; }

    public uint Subpass { get; }

    public bool EnableDepthTest { get; }

    public bool EnableDepthWrite { get; }

    public CompareOp DepthCompareOp { get; }

    public bool EnableBlending { get; }

    public BlendFactor SrcBlendFactor { get; }

    public BlendFactor DstBlendFactor { get; }

    public BlendOp BlendOp { get; }

    public PolygonMode PolygonMode { get; }

    public CullModeFlags CullMode { get; }

    public FrontFace FrontFace { get; }

    public float LineWidth { get; }

    public ImmutableArray<PushConstantRange> PushConstantRanges { get; }

    public ImmutableArray<DescriptorSetLayout> DescriptorSetLayouts { get; }

    public PipelineDefinition(
        string name,
        ShaderDescription? vertexShader,
        ShaderDescription? tessellationControlShader,
        ShaderDescription? tessellationEvalShader,
        ShaderDescription? geometryShader,
        ShaderDescription? fragmentShader,
        RenderPass renderPass,
        IEnumerable<VertexInputBindingDescription>? vertexBindings = null,
        IEnumerable<VertexInputAttributeDescription>? vertexAttributes = null,
        PrimitiveTopology topology = PrimitiveTopology.TriangleList,
        uint subpass = 0,
        bool enableDepthTest = true,
        bool enableDepthWrite = true,
        CompareOp depthCompareOp = CompareOp.Less,
        bool enableBlending = false,
        BlendFactor srcBlendFactor = BlendFactor.SrcAlpha,
        BlendFactor dstBlendFactor = BlendFactor.OneMinusSrcAlpha,
        BlendOp blendOp = BlendOp.Add,
        PolygonMode polygonMode = PolygonMode.Fill,
        CullModeFlags cullMode = CullModeFlags.BackBit,
        FrontFace frontFace = FrontFace.Clockwise,
        float lineWidth = 1.0f,
        IEnumerable<PushConstantRange>? pushConstantRanges = null,
        IEnumerable<DescriptorSetLayout>? descriptorSetLayouts = null
    )
    {
        Name = name;

        VertexShader = vertexShader;
        TessellationControlShader = tessellationControlShader;
        TessellationEvalShader = tessellationEvalShader;
        GeometryShader = geometryShader;
        FragmentShader = fragmentShader;

        RenderPass = renderPass;

        VertexBindings = [.. vertexBindings ?? []];
        VertexAttributes = [.. vertexAttributes ?? []];

        Topology = topology;
        Subpass = subpass;

        EnableDepthTest = enableDepthTest;
        EnableDepthWrite = enableDepthWrite;
        DepthCompareOp = depthCompareOp;

        EnableBlending = enableBlending;
        SrcBlendFactor = srcBlendFactor;
        DstBlendFactor = dstBlendFactor;
        BlendOp = blendOp;

        PolygonMode = polygonMode;
        CullMode = cullMode;
        FrontFace = frontFace;
        LineWidth = lineWidth;

        PushConstantRanges = [.. pushConstantRanges ?? []];
        DescriptorSetLayouts = [.. descriptorSetLayouts ?? []];

        var hash = new IdentityHashBuilder(nameof(PipelineDefinition));

        hash.Add(Name);

        hash.Add(VertexShader is not null);
        if (VertexShader != null)
            hash.Add(VertexShader.Id);

        hash.Add(TessellationControlShader is not null);
        if (TessellationControlShader != null)
            hash.Add(TessellationControlShader.Id);

        hash.Add(TessellationEvalShader is not null);
        if (TessellationEvalShader != null)
            hash.Add(TessellationEvalShader.Id);

        hash.Add(GeometryShader is not null);
        if (GeometryShader != null)
            hash.Add(GeometryShader.Id);

        hash.Add(FragmentShader is not null);
        if (FragmentShader != null)
            hash.Add(FragmentShader.Id);

        hash.Add((uint)Topology)
            .Add(RenderPass.Handle)
            .Add(Subpass)
            .Add(EnableDepthTest)
            .Add(EnableDepthWrite)
            .Add((uint)DepthCompareOp)
            .Add(EnableBlending)
            .Add((uint)SrcBlendFactor)
            .Add((uint)DstBlendFactor)
            .Add((uint)BlendOp)
            .Add((uint)PolygonMode)
            .Add((uint)CullMode)
            .Add((uint)FrontFace)
            .Add(LineWidth);

        foreach (var binding in VertexBindings)
        {
            hash.Add(binding.Binding).Add(binding.Stride).Add((uint)binding.InputRate);
        }

        foreach (var attribute in VertexAttributes)
        {
            hash.Add(attribute.Location)
                .Add(attribute.Binding)
                .Add((uint)attribute.Format)
                .Add(attribute.Offset);
        }

        Id = hash.Compute();
    }
}
