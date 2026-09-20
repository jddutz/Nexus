namespace Nexus.Graphics.Vulkan.Pipelines;

public static class DescriptorSchemas
{
    public static DescriptorSchema Basic { get; } =
        new SchemaBuilder()
            .AddDescriptorSet(set => set.AddUniformBuffer(ShaderStageFlags.VertexBit))
            .Build();

    /// <summary>
    /// Set 0 only: one vertex-stage uniform buffer.
    /// </summary>
    public static DescriptorSchema Camera { get; } =
        new SchemaBuilder()
            .AddDescriptorSet(set => set.AddUniformBuffer(ShaderStageFlags.VertexBit))
            .Build();

    /// <summary>
    /// Set 0 - vertex-stage uniform buffer, set 1 - explicit combined image sampler.
    /// </summary>
    public static DescriptorSchema Textured { get; } =
        new SchemaBuilder()
            .AddDescriptorSet(set => set.AddUniformBuffer(ShaderStageFlags.VertexBit))
            .AddDescriptorSet(set => set.AddCombinedImageSampler(ShaderStageFlags.FragmentBit))
            .Build();

    public static DescriptorSchema Text { get; } =
        new SchemaBuilder()
            .AddDescriptorSet(set =>
                set.AddUniformBuffer(ShaderStageFlags.VertexBit)
                    .AddCombinedImageSampler(ShaderStageFlags.FragmentBit)
            )
            .Build();
}
