namespace Nexus.Graphics.Vulkan.Commands;

public unsafe class CommandFactory(
    Context context,
    ISwapChain swapChain,
    IVertexBufferRegistry geometryRegistry,
    IImageRegistry textureRegistry,
    IPipelineRegistry pipelineRegistry,
    IDescriptorSetPool descriptorSetPool,
    ILogger<CommandFactory> logger
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

        var renderPass = RenderPasses.Main;

        geometryRegistry.Create(drawable.Mesh, vertexShader.VertexFormat);

        foreach (var command in textureRegistry.Create(drawable.Texture, colorFormat))
            yield return command;

        var pipelineDefinition = CreatePipelineDefinition(drawable, renderPass, vertexShader);

        var (pipeline, pipelineLayout) = pipelineRegistry.GetOrCreate(pipelineDefinition);

        var vertexBuffer = geometryRegistry.Get(drawable.Mesh.Id, vertexShader.VertexFormat.Id);

        yield return new BindPipelineCommand(renderPass, pipelineDefinition.Id, drawable, pipeline);

        var schema = pipelineDefinition.DescriptorSchema.GetValueOrDefault();
        var descriptorSets = new VkDescriptorSet[schema.Sets.Length];

        foreach (var setSchema in schema.Sets)
        {
            var layout = pipelineRegistry.GetDescriptorSetLayout(
                pipelineDefinition.Id,
                setSchema.Set
            );

            descriptorSets[setSchema.Set] = descriptorSetPool.Allocate(layout);
        }

        yield return new BindDescriptorSetsCommand(
            renderPass,
            pipelineDefinition.Id,
            drawable,
            pipelineLayout,
            descriptorSets
        );

        yield return new BindVertexBufferCommand(
            renderPass,
            pipelineDefinition.Id,
            drawable,
            vertexBuffer
        );

        yield return new DrawCommand(
            renderPass,
            pipelineDefinition.Id,
            drawable,
            checked((uint)drawable.Mesh.Count)
        );
    }

    /// <summary>
    /// Reconstructs the pipeline definition used by a drawable.
    /// </summary>
    /// <param name="drawable">The drawable whose shader state defines the pipeline.</param>
    /// <param name="renderPass">The render pass used by the drawable.</param>
    /// <param name="vertexShader">The drawable's required vertex shader.</param>
    /// <returns>The pipeline definition for the drawable.</returns>
    private PipelineDefinition CreatePipelineDefinition(
        IDrawable drawable,
        uint renderPass,
        VertexShader vertexShader
    )
    {
        var pipelineDefinitionBuilder = new PipelineDefinitionBuilder(
            drawable.GetType().Name,
            context
        )
            .WithShader(vertexShader)
            .WithRenderPass(swapChain.Passes[RenderPasses.GetIndex(renderPass)]);

        if (drawable.TessellationControlShader is not null)
            pipelineDefinitionBuilder.WithShader(drawable.TessellationControlShader);
        if (drawable.TessellationEvalShader is not null)
            pipelineDefinitionBuilder.WithShader(drawable.TessellationEvalShader);
        if (drawable.GeometryShader is not null)
            pipelineDefinitionBuilder.WithShader(drawable.GeometryShader);
        if (drawable.FragmentShader is not null)
            pipelineDefinitionBuilder.WithShader(drawable.FragmentShader);

        return pipelineDefinitionBuilder.Build();
    }
}
