namespace Nexus.Graphics.Vulkan.Pipelines;

/// <summary>
/// Contains the Vulkan handles and shader stages associated with a pipeline.
/// </summary>
public readonly record struct PipelineRecord(
    Pipeline Pipeline,
    PipelineLayout Layout,
    ShaderStageFlags ShaderStageFlags
);

public unsafe class PipelineManager(Context context) : IPipelineManager
{
    private readonly Context _context = context;

    private readonly Dictionary<ulong, PipelineRecord> _pipelines = [];

    public ulong Create(PipelineDefinition description)
    {
        var shaderModules = CreateShaderModules(description.Shaders);
        PipelineShaderStageCreateInfo[]? shaderStages = null;
        Pipeline pipeline = default;
        PipelineLayout pipelineLayout = default;

        try
        {
            shaderStages = CreateShaderStages(description.Shaders, shaderModules);

            pipelineLayout = CreatePipelineLayout(description);
            pipeline = CreatePipeline(description, shaderStages, pipelineLayout);

            var id = pipeline.Handle;

            _pipelines.Add(
                id,
                new(pipeline, pipelineLayout, GetShaderStageFlags(description.Shaders))
            );

            pipeline = default;
            pipelineLayout = default;

            return id;
        }
        finally
        {
            if (pipeline.Handle != 0)
                _context.VulkanApi.DestroyPipeline(_context.Device, pipeline, null);

            if (pipelineLayout.Handle != 0)
                _context.VulkanApi.DestroyPipelineLayout(_context.Device, pipelineLayout, null);

            if (shaderStages is not null)
                DestroyShaderStages(shaderStages);

            foreach (var module in shaderModules)
                _context.VulkanApi.DestroyShaderModule(_context.Device, module, null);
        }
    }

    public PipelineRecord Get(ulong id)
    {
        if (!_pipelines.TryGetValue(id, out var record))
            throw new KeyNotFoundException($"Pipeline with ID {id} was not found.");

        return record;
    }

    private ShaderModule[] CreateShaderModules(ShaderDefinition[] shaders)
    {
        var modules = new ShaderModule[shaders.Length];

        try
        {
            for (var i = 0; i < shaders.Length; i++)
            {
                var code = shaders[i].Code;

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
        catch
        {
            foreach (var module in modules)
            {
                if (module.Handle != 0)
                    _context.VulkanApi.DestroyShaderModule(_context.Device, module, null);
            }

            throw;
        }
    }

    private static PipelineShaderStageCreateInfo[] CreateShaderStages(
        ShaderDescription[] shaders,
        ShaderModule[] shaderModules
    )
    {
        var stages = new PipelineShaderStageCreateInfo[shaders.Length];

        try
        {
            for (var i = 0; i < shaders.Length; i++)
            {
                stages[i] = new PipelineShaderStageCreateInfo
                {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = GetShaderStage(shaders[i].Stage),
                    Module = shaderModules[i],
                    PName = (byte*)SilkMarshal.StringToPtr(shaders[i].EntryPoint),
                };
            }

            return stages;
        }
        catch
        {
            DestroyShaderStages(stages);
            throw;
        }
    }

    private static ShaderStageFlags GetShaderStage(ShaderStageEnum stage) =>
        stage switch
        {
            ShaderStageEnum.Vertex => ShaderStageFlags.VertexBit,
            ShaderStageEnum.Fragment => ShaderStageFlags.FragmentBit,
            ShaderStageEnum.Geometry => ShaderStageFlags.GeometryBit,
            ShaderStageEnum.Compute => ShaderStageFlags.ComputeBit,
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null),
        };

    private static ShaderStageFlags GetShaderStageFlags(ShaderDefinition[] shaders)
    {
        var flags = ShaderStageFlags.None;

        foreach (var shader in shaders)
            flags |= GetShaderStage(shader.Stage);

        return flags;
    }

    private PipelineLayout CreatePipelineLayout(PipelineDefinition description)
    {
        fixed (DescriptorSetLayout* descriptorSetLayouts = description.DescriptorSetLayouts)
        fixed (PushConstantRange* pushConstantRanges = description.PushConstantRanges)
        {
            var createInfo = new PipelineLayoutCreateInfo
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = (uint)(description.DescriptorSetLayouts?.Length ?? 0),
                PSetLayouts = descriptorSetLayouts,
                PushConstantRangeCount = (uint)(description.PushConstantRanges?.Length ?? 0),
                PPushConstantRanges = pushConstantRanges,
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

    private Pipeline CreatePipeline(
        PipelineDefinition description,
        PipelineShaderStageCreateInfo[] shaderStages,
        PipelineLayout pipelineLayout
    )
    {
        fixed (PipelineShaderStageCreateInfo* pShaderStages = shaderStages)
        fixed (VertexInputBindingDescription* pVertexBindings = description.VertexBindings)
        fixed (VertexInputAttributeDescription* pVertexAttributes = description.VertexAttributes)
        {
            var vertexInput = new PipelineVertexInputStateCreateInfo
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = (uint)description.VertexBindings.Length,
                PVertexBindingDescriptions = pVertexBindings,
                VertexAttributeDescriptionCount = (uint)description.VertexAttributes.Length,
                PVertexAttributeDescriptions = pVertexAttributes,
            };

            var inputAssembly = new PipelineInputAssemblyStateCreateInfo
            {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = description.Topology,
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
                PolygonMode = description.PolygonMode,
                LineWidth = description.LineWidth,
                CullMode = description.CullMode,
                FrontFace = description.FrontFace,
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
                DepthTestEnable = description.EnableDepthTest,
                DepthWriteEnable = description.EnableDepthWrite,
                DepthCompareOp = description.DepthCompareOp,
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

                BlendEnable = description.EnableBlending,
                SrcColorBlendFactor = description.SrcBlendFactor,
                DstColorBlendFactor = description.DstBlendFactor,
                ColorBlendOp = description.BlendOp,
                SrcAlphaBlendFactor = description.SrcBlendFactor,
                DstAlphaBlendFactor = description.DstBlendFactor,
                AlphaBlendOp = description.BlendOp,
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
                RenderPass = description.RenderPass,
                Subpass = description.Subpass,
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

    private static void DestroyShaderStages(PipelineShaderStageCreateInfo[] stages)
    {
        foreach (var stage in stages)
        {
            if (stage.PName is not null)
                SilkMarshal.Free((nint)stage.PName);
        }
    }

    public void Delete(ulong id)
    {
        if (!_pipelines.Remove(id, out var record))
            return;

        _context.VulkanApi.DestroyPipeline(_context.Device, record.Pipeline, null);
        _context.VulkanApi.DestroyPipelineLayout(_context.Device, record.Layout, null);
    }

    public void Dispose()
    {
        foreach (var record in _pipelines.Values)
        {
            _context.VulkanApi.DestroyPipeline(_context.Device, record.Pipeline, null);
            _context.VulkanApi.DestroyPipelineLayout(_context.Device, record.Layout, null);
        }

        _pipelines.Clear();

        GC.SuppressFinalize(this);
    }
}
