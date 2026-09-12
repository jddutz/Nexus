namespace Nexus.Graphics.Vulkan.Pipelines;

public static class DescriptorSchemas
{
    public static DescriptorSchema Basic { get; } =
        new SchemaBuilder()
            .AddDescriptorSet(set => set.AddUniformBuffer(ShaderStageFlags.VertexBit))
            .Build();

    public static DescriptorSchema Textured { get; } =
        new SchemaBuilder()
            .AddDescriptorSet(set =>
                set.AddUniformBuffer(ShaderStageFlags.VertexBit)
                    .AddCombinedImageSampler(ShaderStageFlags.FragmentBit)
            )
            .Build();

    public static DescriptorSchema Text { get; } =
        new SchemaBuilder()
            .AddDescriptorSet(set =>
                set.AddUniformBuffer(ShaderStageFlags.VertexBit)
                    .AddCombinedImageSampler(ShaderStageFlags.FragmentBit)
            )
            .Build();
}
