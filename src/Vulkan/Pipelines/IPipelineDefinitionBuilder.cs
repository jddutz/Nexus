namespace Nexus.Graphics.Vulkan.Pipelines;

public interface IPipelineDefinitionBuilder
{
    PipelineDefinitionBuilder WithShader(IShaderContract shader);
    PipelineDefinitionBuilder WithVertexBinding(VertexInputBindingDescription binding);
    PipelineDefinitionBuilder WithVertexAttribute(VertexInputAttributeDescription attribute);

    /// <summary>Adds the vertex-buffer layout described by <paramref name="format"/>.</summary>
    /// <param name="format">The vertex buffer layout.</param>
    /// <returns>This builder.</returns>
    PipelineDefinitionBuilder WithVertexFormat(VertexFormat format);

    PipelineDefinitionBuilder WithTopology(PrimitiveTopologyEnum topology);
    PipelineDefinitionBuilder WithRenderPass(RenderPass renderPass, uint subpass = 0);
    PipelineDefinitionBuilder WithDepthTest(bool enabled = true);
    PipelineDefinitionBuilder WithDepthWrite(bool enabled = true);
    PipelineDefinitionBuilder WithDepthCompare(CompareOp compareOp);
    PipelineDefinitionBuilder WithBlending(bool enabled = true);
    PipelineDefinitionBuilder WithBlendFactors(
        BlendFactor source,
        BlendFactor destination,
        BlendOp operation = BlendOp.Add
    );
    PipelineDefinitionBuilder WithPolygonMode(PolygonMode polygonMode);
    PipelineDefinitionBuilder WithCullMode(CullModeFlags cullMode);
    PipelineDefinitionBuilder WithFrontFace(FrontFace frontFace);
    PipelineDefinitionBuilder WithLineWidth(float lineWidth);
    PipelineDefinitionBuilder WithPushConstant(PushConstantRange range);
    PipelineDefinitionBuilder WithDescriptorSchema(DescriptorSchema schema);
    PipelineDefinition Build();
}
