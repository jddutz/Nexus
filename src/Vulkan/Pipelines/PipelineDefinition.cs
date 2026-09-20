public sealed record PipelineDefinition
{
    public PipelineId Id { get; }

    public string Name { get; }

    public VertexShader? VertexShader { get; }
    public Shader? TessellationControlShader { get; }
    public Shader? TessellationEvalShader { get; }
    public Shader? GeometryShader { get; }
    public FragmentShader? FragmentShader { get; }

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

    public DescriptorSchema? DescriptorSchema { get; }

    public PipelineDefinition(
        string name,
        VertexShader? vertexShader,
        Shader? tessellationControlShader,
        Shader? tessellationEvalShader,
        Shader? geometryShader,
        FragmentShader? fragmentShader,
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
        DescriptorSchema? descriptorSchema = null
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
        DescriptorSchema = descriptorSchema;

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

        foreach (var range in PushConstantRanges)
        {
            hash.Add((uint)range.StageFlags).Add(range.Offset).Add(range.Size);
        }

        hash.Add(DescriptorSchema is not null);
        if (DescriptorSchema is not null)
        {
            foreach (var set in DescriptorSchema.Value.Sets)
            {
                hash.Add(set.Set);

                foreach (var binding in set.Bindings)
                {
                    hash.Add(binding.Binding)
                        .Add((uint)binding.DescriptorType)
                        .Add(binding.DescriptorCount)
                        .Add((uint)binding.StageFlags);
                }
            }
        }

        Id = hash.Compute();
    }
}
