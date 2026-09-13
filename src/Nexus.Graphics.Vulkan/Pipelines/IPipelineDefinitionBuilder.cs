namespace Nexus.Graphics.Vulkan.Pipelines;

public interface IPipelineDefinitionBuilder
{
    IPipelineDefinitionBuilder WithName(string name);

    IPipelineDefinitionBuilder WithShader(ShaderDescription shader);

    IPipelineDefinitionBuilder WithVertexBinding(VertexInputBindingDescription binding);

    IPipelineDefinitionBuilder WithVertexAttribute(VertexInputAttributeDescription attribute);

    IPipelineDefinitionBuilder WithTopology(PrimitiveTopology topology);

    IPipelineDefinitionBuilder WithRenderPass(RenderPass renderPass, uint subpass = 0);

    IPipelineDefinitionBuilder WithDepthTest(bool enabled = true);

    IPipelineDefinitionBuilder WithDepthWrite(bool enabled = true);

    IPipelineDefinitionBuilder WithDepthCompare(CompareOp compareOp);

    IPipelineDefinitionBuilder WithBlending(bool enabled = true);

    IPipelineDefinitionBuilder WithBlendFactors(
        BlendFactor source,
        BlendFactor destination,
        BlendOp operation = BlendOp.Add
    );

    IPipelineDefinitionBuilder WithPolygonMode(PolygonMode polygonMode);

    IPipelineDefinitionBuilder WithCullMode(CullModeFlags cullMode);

    IPipelineDefinitionBuilder WithFrontFace(FrontFace frontFace);

    IPipelineDefinitionBuilder WithLineWidth(float lineWidth);

    IPipelineDefinitionBuilder WithPushConstant(PushConstantRange range);

    IPipelineDefinitionBuilder WithDescriptorSetLayout(DescriptorSetLayout layout);

    PipelineDefinition Build();
}
