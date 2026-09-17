namespace Nexus.Graphics.Vulkan.Pipelines;

/// <inheritdoc cref="IDescriptorSetLayoutFactory" />
public unsafe class DescriptorSetLayoutFactory(Context context) : IDescriptorSetLayoutFactory
{
    private readonly Context _context = context;

    /// <inheritdoc />
    public DescriptorSetLayout[] Create(DescriptorSchema schema)
    {
        var layouts = new DescriptorSetLayout[schema.Sets.Length];

        try
        {
            for (var i = 0; i < schema.Sets.Length; i++)
                layouts[i] = CreateDescriptorSetLayout(schema.Sets[i]);

            return layouts;
        }
        catch
        {
            Destroy(layouts);
            throw;
        }
    }

    /// <inheritdoc />
    public void Destroy(DescriptorSetLayout[] descriptorSetLayouts)
    {
        foreach (var layout in descriptorSetLayouts)
        {
            if (layout.Handle != 0)
                _context.VulkanApi.DestroyDescriptorSetLayout(_context.Device, layout, null);
        }
    }

    /// <summary>
    /// Creates a single Vulkan descriptor-set layout from a descriptor set schema.
    /// </summary>
    /// <param name="setSchema">The descriptor set schema.</param>
    /// <returns>The created descriptor-set layout.</returns>
    private DescriptorSetLayout CreateDescriptorSetLayout(DescriptorSetSchema setSchema)
    {
        var bindings = new DescriptorSetLayoutBinding[setSchema.Bindings.Length];

        for (var i = 0; i < setSchema.Bindings.Length; i++)
        {
            var binding = setSchema.Bindings[i];

            bindings[i] = new DescriptorSetLayoutBinding
            {
                Binding = binding.Binding,
                DescriptorType = binding.DescriptorType,
                DescriptorCount = binding.DescriptorCount,
                StageFlags = binding.StageFlags,
            };
        }

        fixed (DescriptorSetLayoutBinding* pBindings = bindings)
        {
            var createInfo = new DescriptorSetLayoutCreateInfo
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = (uint)bindings.Length,
                PBindings = pBindings,
            };

            var result = _context.VulkanApi.CreateDescriptorSetLayout(
                _context.Device,
                &createInfo,
                null,
                out DescriptorSetLayout layout
            );

            if (result != Result.Success)
                throw new InvalidOperationException(
                    $"Failed to create descriptor set layout: {result}"
                );

            return layout;
        }
    }
}
