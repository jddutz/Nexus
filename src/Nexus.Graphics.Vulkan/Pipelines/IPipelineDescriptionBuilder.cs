namespace Nexus.Graphics.Vulkan.Pipelines;

public interface IPipelineDescriptionBuilder
{
    IPipelineDescriptionBuilder WithName(string name);

    IPipelineDescriptionBuilder WithShader(ShaderDescription shader);

    IPipelineDescriptionBuilder WithVertexBinding(VertexInputBindingDescription binding);

    IPipelineDescriptionBuilder WithVertexAttribute(VertexInputAttributeDescription attribute);

    IPipelineDescriptionBuilder WithTopology(PrimitiveTopology topology);

    IPipelineDescriptionBuilder WithRenderPass(RenderPass renderPass, uint subpass = 0);

    IPipelineDescriptionBuilder WithDepthTest(bool enabled = true);

    IPipelineDescriptionBuilder WithDepthWrite(bool enabled = true);

    IPipelineDescriptionBuilder WithDepthCompare(CompareOp compareOp);

    IPipelineDescriptionBuilder WithBlending(bool enabled = true);

    IPipelineDescriptionBuilder WithBlendFactors(
        BlendFactor source,
        BlendFactor destination,
        BlendOp operation = BlendOp.Add
    );

    IPipelineDescriptionBuilder WithPolygonMode(PolygonMode polygonMode);

    IPipelineDescriptionBuilder WithCullMode(CullModeFlags cullMode);

    IPipelineDescriptionBuilder WithFrontFace(FrontFace frontFace);

    IPipelineDescriptionBuilder WithLineWidth(float lineWidth);

    IPipelineDescriptionBuilder WithPushConstant(PushConstantRange range);

    IPipelineDescriptionBuilder WithDescriptorSetLayout(DescriptorSetLayout layout);

    PipelineDescription Build();
}
