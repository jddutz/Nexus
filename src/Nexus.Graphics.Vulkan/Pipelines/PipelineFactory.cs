namespace Nexus.Graphics.Vulkan.Pipelines;

/// <summary>
/// Creates Vulkan graphics pipelines and their associated pipeline layouts.
/// </summary>
public unsafe class PipelineFactory(
    Context context,
    IDescriptorSetLayoutFactory descriptorSetLayoutFactory
) : IPipelineFactory
{
    private readonly Context _context = context;
    private readonly IDescriptorSetLayoutFactory _descriptorSetLayoutFactory =
        descriptorSetLayoutFactory;

    /// <inheritdoc />
    public (
        Pipeline Pipeline,
        PipelineLayout Layout,
        DescriptorSetLayout[] DescriptorSetLayouts
    ) Create(PipelineDefinition definition)
    {
        var shaders = GetShaders(definition);
        var shaderModules = CreateShaderModules(shaders);
        PipelineShaderStageCreateInfo[]? shaderStages = null;
        Pipeline pipeline = default;
        PipelineLayout pipelineLayout = default;
        var descriptorSetLayouts = Array.Empty<DescriptorSetLayout>();

        try
        {
            shaderStages = CreateShaderStages(shaders, shaderModules);
            descriptorSetLayouts = CreateDescriptorSetLayouts(definition);
            pipelineLayout = CreatePipelineLayout(descriptorSetLayouts, definition);
            pipeline = CreatePipeline(definition, shaderStages, pipelineLayout);

            return (pipeline, pipelineLayout, descriptorSetLayouts);
        }
        catch
        {
            if (pipeline.Handle != 0)
                _context.VulkanApi.DestroyPipeline(_context.Device, pipeline, null);

            if (pipelineLayout.Handle != 0)
                _context.VulkanApi.DestroyPipelineLayout(_context.Device, pipelineLayout, null);

            _descriptorSetLayoutFactory.Destroy(descriptorSetLayouts);

            throw;
        }
        finally
        {
            if (shaderStages is not null)
                DestroyShaderStages(shaderStages);

            foreach (var module in shaderModules)
            {
                if (module.Handle != 0)
                    _context.VulkanApi.DestroyShaderModule(_context.Device, module, null);
            }
        }
    }

    /// <summary>
    /// Gets the shader definitions in the order required by the graphics pipeline.
    /// </summary>
    /// <param name="definition">The pipeline definition.</param>
    /// <returns>The shader definitions included in the pipeline.</returns>
    private static ImmutableArray<ShaderDescription> GetShaders(PipelineDefinition definition)
    {
        var shaders = new List<ShaderDescription>();

        if (definition.VertexShader is not null)
            shaders.Add(definition.VertexShader);
        if (definition.TessellationControlShader is not null)
            shaders.Add(definition.TessellationControlShader);
        if (definition.TessellationEvalShader is not null)
            shaders.Add(definition.TessellationEvalShader);
        if (definition.GeometryShader is not null)
            shaders.Add(definition.GeometryShader);
        if (definition.FragmentShader is not null)
            shaders.Add(definition.FragmentShader);

        return [.. shaders];
    }

    /// <summary>
    /// Creates Vulkan shader modules for the supplied shader definitions.
    /// </summary>
    /// <param name="shaders">The shader definitions to load.</param>
    /// <returns>The created shader modules.</returns>
    private ShaderModule[] CreateShaderModules(ImmutableArray<ShaderDescription> shaders)
    {
        var modules = new ShaderModule[shaders.Length];

        for (var i = 0; i < shaders.Length; i++)
        {
            var shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", shaders[i].Source);
            var code = File.ReadAllBytes(shaderPath);

            fixed (byte* codePtr = code)
            {
                var createInfo = new ShaderModuleCreateInfo
                {
                    SType = StructureType.ShaderModuleCreateInfo,
                    CodeSize = (nuint)code.Length,
                    PCode = (uint*)codePtr,
                };

                var result = _context.VulkanApi.CreateShaderModule(
                    _context.Device,
                    &createInfo,
                    null,
                    out modules[i]
                );

                if (result != Result.Success)
                    throw new InvalidOperationException(
                        $"Failed to create shader module: {result}"
                    );
            }
        }

        return modules;
    }

    /// <summary>
    /// Creates shader-stage descriptions for the supplied shader modules.
    /// </summary>
    /// <param name="shaders">The shader definitions.</param>
    /// <param name="shaderModules">The corresponding shader modules.</param>
    /// <returns>The shader-stage descriptions.</returns>
    private static PipelineShaderStageCreateInfo[] CreateShaderStages(
        ImmutableArray<ShaderDescription> shaders,
        ShaderModule[] shaderModules
    )
    {
        var stages = new PipelineShaderStageCreateInfo[shaders.Length];

        for (var i = 0; i < shaders.Length; i++)
        {
            stages[i] = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = shaders[i].Stages switch
                {
                    ShaderStageEnum.Vertex => ShaderStageFlags.VertexBit,
                    ShaderStageEnum.TessellationControl => ShaderStageFlags.TessellationControlBit,
                    ShaderStageEnum.TessellationEval => ShaderStageFlags.TessellationEvaluationBit,
                    ShaderStageEnum.Geometry => ShaderStageFlags.GeometryBit,
                    ShaderStageEnum.Fragment => ShaderStageFlags.FragmentBit,
                    ShaderStageEnum.Compute => ShaderStageFlags.ComputeBit,
                    _ => throw new InvalidOperationException("Unsupported shader stage."),
                },
                Module = shaderModules[i],
                PName = (byte*)SilkMarshal.StringToPtr("main"),
            };
        }

        return stages;
    }

    /// <summary>
    /// Realizes the pipeline's descriptor schema into Vulkan descriptor-set layouts.
    /// </summary>
    /// <param name="definition">The pipeline definition.</param>
    /// <returns>The created descriptor-set layouts, ordered by set index.</returns>
    private DescriptorSetLayout[] CreateDescriptorSetLayouts(PipelineDefinition definition) =>
        definition.DescriptorSchema is not { } schema
            ? []
            : _descriptorSetLayoutFactory.Create(schema);

    /// <summary>
    /// Creates a Vulkan pipeline layout from a pipeline definition.
    /// </summary>
    /// <param name="descriptorSetLayouts">The realized descriptor-set layouts for the pipeline.</param>
    /// <param name="definition">The pipeline definition.</param>
    /// <returns>The created pipeline layout.</returns>
    private PipelineLayout CreatePipelineLayout(
        DescriptorSetLayout[] descriptorSetLayouts,
        PipelineDefinition definition
    )
    {
        var pushConstantRanges = definition.PushConstantRanges.ToArray();

        fixed (DescriptorSetLayout* descriptorSetLayoutsPointer = descriptorSetLayouts)
        fixed (PushConstantRange* pushConstantRangesPointer = pushConstantRanges)
        {
            var createInfo = new PipelineLayoutCreateInfo
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = (uint)descriptorSetLayouts.Length,
                PSetLayouts = descriptorSetLayoutsPointer,
                PushConstantRangeCount = (uint)pushConstantRanges.Length,
                PPushConstantRanges = pushConstantRangesPointer,
            };

            var result = _context.VulkanApi.CreatePipelineLayout(
                _context.Device,
                &createInfo,
                null,
                out PipelineLayout pipelineLayout
            );

            if (result != Result.Success)
                throw new InvalidOperationException($"Failed to create pipeline layout: {result}");

            return pipelineLayout;
        }
    }

    /// <summary>
    /// Creates a Vulkan graphics pipeline from its definition and layout.
    /// </summary>
    /// <param name="definition">The pipeline definition.</param>
    /// <param name="shaderStages">The shader-stage descriptions.</param>
    /// <param name="pipelineLayout">The pipeline layout.</param>
    /// <returns>The created graphics pipeline.</returns>
    private Pipeline CreatePipeline(
        PipelineDefinition definition,
        PipelineShaderStageCreateInfo[] shaderStages,
        PipelineLayout pipelineLayout
    )
    {
        var vertexBindings = definition.VertexBindings.ToArray();
        var vertexAttributes = definition.VertexAttributes.ToArray();

        fixed (PipelineShaderStageCreateInfo* pShaderStages = shaderStages)
        fixed (VertexInputBindingDescription* pVertexBindings = vertexBindings)
        fixed (VertexInputAttributeDescription* pVertexAttributes = vertexAttributes)
        {
            var vertexInput = new PipelineVertexInputStateCreateInfo
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = (uint)definition.VertexBindings.Length,
                PVertexBindingDescriptions = pVertexBindings,
                VertexAttributeDescriptionCount = (uint)definition.VertexAttributes.Length,
                PVertexAttributeDescriptions = pVertexAttributes,
            };

            var inputAssembly = new PipelineInputAssemblyStateCreateInfo
            {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = definition.Topology,
                PrimitiveRestartEnable = false,
            };

            var viewportState = new PipelineViewportStateCreateInfo
            {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                ScissorCount = 1,
            };

            var rasterization = new PipelineRasterizationStateCreateInfo
            {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                DepthClampEnable = false,
                RasterizerDiscardEnable = false,
                PolygonMode = definition.PolygonMode,
                LineWidth = definition.LineWidth,
                CullMode = definition.CullMode,
                FrontFace = definition.FrontFace,
                DepthBiasEnable = false,
            };

            var multisample = new PipelineMultisampleStateCreateInfo
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                SampleShadingEnable = false,
                RasterizationSamples = SampleCountFlags.Count1Bit,
            };

            var depthStencil = new PipelineDepthStencilStateCreateInfo
            {
                SType = StructureType.PipelineDepthStencilStateCreateInfo,
                DepthTestEnable = definition.EnableDepthTest,
                DepthWriteEnable = definition.EnableDepthWrite,
                DepthCompareOp = definition.DepthCompareOp,
                DepthBoundsTestEnable = false,
                StencilTestEnable = false,
            };

            var colorBlendAttachment = new PipelineColorBlendAttachmentState
            {
                ColorWriteMask =
                    ColorComponentFlags.RBit
                    | ColorComponentFlags.GBit
                    | ColorComponentFlags.BBit
                    | ColorComponentFlags.ABit,
                BlendEnable = definition.EnableBlending,
                SrcColorBlendFactor = definition.SrcBlendFactor,
                DstColorBlendFactor = definition.DstBlendFactor,
                ColorBlendOp = definition.BlendOp,
                SrcAlphaBlendFactor = definition.SrcBlendFactor,
                DstAlphaBlendFactor = definition.DstBlendFactor,
                AlphaBlendOp = definition.BlendOp,
            };

            var colorBlend = new PipelineColorBlendStateCreateInfo
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                LogicOpEnable = false,
                AttachmentCount = 1,
                PAttachments = &colorBlendAttachment,
            };

            var dynamicStates = stackalloc DynamicState[]
            {
                DynamicState.Viewport,
                DynamicState.Scissor,
            };

            var dynamicState = new PipelineDynamicStateCreateInfo
            {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = 2,
                PDynamicStates = dynamicStates,
            };

            var pipelineInfo = new GraphicsPipelineCreateInfo
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = (uint)shaderStages.Length,
                PStages = pShaderStages,
                PVertexInputState = &vertexInput,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterization,
                PMultisampleState = &multisample,
                PDepthStencilState = &depthStencil,
                PColorBlendState = &colorBlend,
                PDynamicState = &dynamicState,
                Layout = pipelineLayout,
                RenderPass = definition.RenderPass,
                Subpass = definition.Subpass,
            };

            var result = _context.VulkanApi.CreateGraphicsPipelines(
                _context.Device,
                default,
                1,
                &pipelineInfo,
                null,
                out Pipeline pipeline
            );

            if (result != Result.Success)
                throw new InvalidOperationException(
                    $"Failed to create graphics pipeline: {result}"
                );

            return pipeline;
        }
    }

    /// <summary>
    /// Releases unmanaged name strings allocated for shader stages.
    /// </summary>
    /// <param name="stages">The shader stages to clean up.</param>
    private static void DestroyShaderStages(PipelineShaderStageCreateInfo[] stages)
    {
        foreach (var stage in stages)
        {
            if (stage.PName is not null)
                SilkMarshal.Free((nint)stage.PName);
        }
    }
}
