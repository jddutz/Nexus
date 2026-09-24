namespace Nexus.Graphics.Vulkan.Commands;

/// <summary>
/// Creates the Vulkan commands required to render a drawable.
/// </summary>
public unsafe class CommandFactory(
    Context context,
    ISwapChain swapChain,
    IVertexBufferRegistry vertexBufferRegistry,
    IInstanceBufferRegistry instanceBufferRegistry,
    IImageRegistry imageRegistry,
    IPipelineRegistry pipelineRegistry,
    IDescriptorSetPool descriptorSetPool,
    IBufferManager bufferManager,
    ISamplerRegistry samplerRegistry
) : ICommandFactory
{
    public IEnumerable<IVulkanCommand> Create(IDrawable drawable)
    {
        /*
        // TODO: Can we get the input semantics from schema?
        foreach (var semantic in InputSemantics.All)
        {
            switch (semantic)
            {
                case InputSemantics.Transform:
                    throw new NotImplementedException();
                case InputSemantics.View:
                    throw new NotImplementedException();
                case InputSemantics.Projection:
                    throw new NotImplementedException();
                case InputSemantics.Color:
                    throw new NotImplementedException();
                case InputSemantics.TextureRegion:
                    throw new NotImplementedException();
                default:
                    throw new NotImplementedException();
            }
        }
        */
        var vertexShader =
            drawable.VertexShader
            ?? throw new InvalidOperationException("Drawables must define a vertex shader.");

        var colorFormat =
            drawable.FragmentShader?.ColorFormat
            ?? throw new InvalidOperationException("Drawables must define a fragment shader.");

        var renderPassMask = RenderPasses.Main;

        vertexBufferRegistry.Create(drawable.Mesh, vertexShader.VertexFormat);
        if (vertexShader.InstanceLayout.Length > 0)
            instanceBufferRegistry.Create(drawable, vertexShader.InstanceLayout);

        foreach (var command in imageRegistry.Create(drawable.Texture, colorFormat))
            yield return command;

        var pipelineDefinition = CreatePipelineDefinition(drawable, renderPassMask, vertexShader);

        var (pipeline, pipelineLayout) = pipelineRegistry.GetOrCreate(pipelineDefinition);

        Debug.WriteLine(
            $"Prepared Vulkan drawable resources. DrawableId={drawable.Id}, PipelineId={pipelineDefinition.Id}"
        );

        var vertexBuffer = vertexBufferRegistry.Get(drawable.Mesh.Id, vertexShader.VertexFormat.Id);

        yield return new BindPipelineCommand(
            renderPassMask,
            pipelineDefinition.Id,
            drawable,
            pipeline
        );

        var schema = pipelineDefinition.DescriptorSchema.GetValueOrDefault();
        var descriptorSets = new VkDescriptorSet[schema.Sets.Length];

        foreach (var setSchema in schema.Sets)
        {
            var layout = pipelineRegistry.GetDescriptorSetLayout(
                pipelineDefinition.Id,
                setSchema.Set
            );

            var descriptorSet = descriptorSetPool.Allocate(layout);
            descriptorSets[setSchema.Set] = descriptorSet;

            Debug.WriteLine(
                $"Allocated descriptor set. DrawableId={drawable.Id}, PipelineId={pipelineDefinition.Id}, Set={setSchema.Set}"
            );

            foreach (var binding in setSchema.Bindings)
            {
                switch (binding.DescriptorType)
                {
                    case DescriptorType.UniformBuffer:
                        var uniformData = drawable.GetUniformData(vertexShader.UniformLayout);
                        var uniformBuffer = bufferManager.CreateUniformBuffer(
                            checked((ulong)uniformData.Length)
                        );
                        bufferManager.UpdateBuffer(uniformBuffer, uniformData.Span);
                        descriptorSetPool.WriteUniformBuffer(
                            descriptorSet,
                            binding.Binding,
                            uniformBuffer,
                            0,
                            checked((ulong)uniformData.Length)
                        );
                        break;

                    case DescriptorType.CombinedImageSampler:
                        samplerRegistry.Create(drawable.SamplingBehavior);
                        descriptorSetPool.WriteCombinedImageSampler(
                            descriptorSet,
                            binding.Binding,
                            imageRegistry.Get(drawable.Texture, colorFormat),
                            samplerRegistry.Get(drawable.SamplingBehavior.Id)
                        );
                        break;

                    default:
                        throw new NotSupportedException(
                            $"Descriptor type {binding.DescriptorType} is not supported."
                        );
                }
            }
        }

        yield return new BindDescriptorSetsCommand(
            renderPassMask,
            pipelineDefinition.Id,
            drawable,
            pipelineLayout,
            descriptorSets
        );

        yield return new BindVertexBufferCommand(
            renderPassMask,
            pipelineDefinition.Id,
            drawable,
            0,
            vertexBuffer
        );

        if (vertexShader.InstanceLayout.Length > 0)
        {
            var instanceBuffer = instanceBufferRegistry.Get(drawable.Id);
            yield return new BindVertexBufferCommand(
                renderPassMask,
                pipelineDefinition.Id,
                drawable,
                1,
                instanceBuffer
            );
        }

        yield return new DrawCommand(
            renderPassMask,
            pipelineDefinition.Id,
            drawable,
            checked((uint)drawable.Mesh.Count),
            checked((uint)drawable.InstanceCount)
        );

        Debug.WriteLine(
            $"Created Vulkan commands. DrawableId={drawable.Id}, PipelineId={pipelineDefinition.Id}, VertexCount={drawable.Mesh.Count}"
        );
    }

    /// <summary>
    /// Creates the pipeline definition used by a drawable.
    /// </summary>
    /// <param name="drawable">The drawable whose shader state defines the pipeline.</param>
    /// <param name="renderPassMask">The render-pass mask used by the drawable.</param>
    /// <param name="vertexShader">The drawable's required vertex shader.</param>
    /// <returns>The pipeline definition for the drawable.</returns>
    protected virtual PipelineDefinition CreatePipelineDefinition(
        IDrawable drawable,
        uint renderPassMask,
        VertexShader vertexShader
    )
    {
        var pipelineDefinitionBuilder = new PipelineDefinitionBuilder(
            drawable.GetType().Name,
            context
        )
            .WithShader(vertexShader)
            .WithDescriptorSchema(DescriptorSchemas.Textured)
            .WithRenderPass(swapChain.Passes[RenderPasses.GetIndex(renderPassMask)]);

        if (drawable.TessellationControlShader is not null)
            pipelineDefinitionBuilder.WithShader(drawable.TessellationControlShader);
        if (drawable.TessellationEvalShader is not null)
            pipelineDefinitionBuilder.WithShader(drawable.TessellationEvalShader);
        if (drawable.GeometryShader is not null)
            pipelineDefinitionBuilder.WithShader(drawable.GeometryShader);
        if (drawable.FragmentShader is not null)
            pipelineDefinitionBuilder.WithShader(drawable.FragmentShader);

        return BuildPipelineDefinition(pipelineDefinitionBuilder);
    }

    /// <summary>
    /// Builds a configured pipeline definition.
    /// </summary>
    /// <param name="builder">The configured pipeline definition builder.</param>
    /// <returns>The immutable pipeline definition.</returns>
    protected virtual PipelineDefinition BuildPipelineDefinition(
        PipelineDefinitionBuilder builder
    ) => builder.Build();
}
