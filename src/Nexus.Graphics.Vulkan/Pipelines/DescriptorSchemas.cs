namespace Nexus.Graphics.Vulkan.Pipelines;

public static class DescriptorSchemas
{
    public static DescriptorSchema Basic { get; } =
        new SchemaBuilder()
            .AddDescriptorSet(set => set.AddUniformBuffer(ShaderStageFlags.VertexBit))
            .Build();

    /// <summary>
    /// Set 0 only: the camera view-projection uniform buffer shared by every pipeline that
    /// consumes camera state.
    /// </summary>
    public static DescriptorSchema Camera { get; } =
        new SchemaBuilder()
            .AddDescriptorSet(set => set.AddUniformBuffer(ShaderStageFlags.VertexBit))
            .Build();

    /// <summary>
    /// Set 0 - camera view-projection uniform buffer, set 1 - material combined image sampler.
    /// Kept as separate sets so the camera descriptor set can be shared across render items
    /// while each item's material descriptor set stays independent.
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

