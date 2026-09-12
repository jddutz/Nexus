namespace Nexus.Graphics.Vulkan.Pipelines;

public readonly record struct DescriptorBinding(
    uint Binding,
    DescriptorType DescriptorType,
    uint DescriptorCount,
    ShaderStageFlags StageFlags
);

public readonly record struct DescriptorSetSchema(uint Set, DescriptorBinding[] Bindings);

public readonly record struct DescriptorSchema(DescriptorSetSchema[] Sets);

/// <summary>
/// Builds a descriptor schema from one or more descriptor sets.
/// </summary>
public interface ISchemaBuilder
{
    /// <summary>Adds a descriptor set at the next available index.</summary>
    /// <param name="configure">Configures the descriptor set.</param>
    /// <returns>The builder.</returns>
    ISchemaBuilder AddDescriptorSet(Action<IDescriptorSetSchemaBuilder> configure);

    /// <summary>Adds a descriptor set at the specified index.</summary>
    /// <param name="set">The descriptor set index.</param>
    /// <param name="configure">Configures the descriptor set.</param>
    /// <returns>The builder.</returns>
    ISchemaBuilder AddDescriptorSet(uint set, Action<IDescriptorSetSchemaBuilder> configure);

    /// <summary>Builds the descriptor schema.</summary>
    /// <returns>The configured descriptor schema.</returns>
    DescriptorSchema Build();
}

/// <summary>
/// Builds the bindings in a descriptor set schema.
/// </summary>
public interface IDescriptorSetSchemaBuilder
{
    /// <summary>Adds a uniform buffer at the next available binding index.</summary>
    /// <param name="stageFlags">The shader stages that access the binding.</param>
    /// <param name="descriptorCount">The number of descriptors.</param>
    /// <returns>The builder.</returns>
    IDescriptorSetSchemaBuilder AddUniformBuffer(
        ShaderStageFlags stageFlags,
        uint descriptorCount = 1
    );

    /// <summary>Adds a uniform buffer at the specified binding index.</summary>
    /// <param name="binding">The binding index.</param>
    /// <param name="stageFlags">The shader stages that access the binding.</param>
    /// <param name="descriptorCount">The number of descriptors.</param>
    /// <returns>The builder.</returns>
    IDescriptorSetSchemaBuilder AddUniformBuffer(
        uint binding,
        ShaderStageFlags stageFlags,
        uint descriptorCount = 1
    );

    /// <summary>Adds a combined image sampler at the next available binding index.</summary>
    /// <param name="stageFlags">The shader stages that access the binding.</param>
    /// <param name="descriptorCount">The number of descriptors.</param>
    /// <returns>The builder.</returns>
    IDescriptorSetSchemaBuilder AddCombinedImageSampler(
        ShaderStageFlags stageFlags,
        uint descriptorCount = 1
    );

    /// <summary>Adds a combined image sampler at the specified binding index.</summary>
    /// <param name="binding">The binding index.</param>
    /// <param name="stageFlags">The shader stages that access the binding.</param>
    /// <param name="descriptorCount">The number of descriptors.</param>
    /// <returns>The builder.</returns>
    IDescriptorSetSchemaBuilder AddCombinedImageSampler(
        uint binding,
        ShaderStageFlags stageFlags,
        uint descriptorCount = 1
    );
}

public class SchemaBuilder() : ISchemaBuilder
{
    private readonly List<DescriptorSetSchema> _sets = [];

    ISchemaBuilder ISchemaBuilder.AddDescriptorSet(Action<IDescriptorSetSchemaBuilder> configure) =>
        AddDescriptorSet(configure);

    ISchemaBuilder ISchemaBuilder.AddDescriptorSet(
        uint set,
        Action<IDescriptorSetSchemaBuilder> configure
    ) => AddDescriptorSet(set, configure);

    DescriptorSchema ISchemaBuilder.Build() => Build();

    public SchemaBuilder AddDescriptorSet(Action<IDescriptorSetSchemaBuilder> configure)
    {
        return AddDescriptorSet(GetNextSetIndex(), configure);
    }

    public SchemaBuilder AddDescriptorSet(uint set, Action<IDescriptorSetSchemaBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        if (_sets.Any(x => x.Set == set))
            throw new InvalidOperationException($"Descriptor set {set} has already been defined.");

        var builder = new DescriptorSetSchemaBuilder(set);

        configure(builder);

        _sets.Add(builder.Build());

        return this;
    }

    public DescriptorSchema Build()
    {
        return new DescriptorSchema(_sets.OrderBy(x => x.Set).ToArray());
    }

    private uint GetNextSetIndex()
    {
        uint set = 0;

        while (_sets.Any(x => x.Set == set))
            set++;

        return set;
    }
}

public class DescriptorSetSchemaBuilder(uint set) : IDescriptorSetSchemaBuilder
{
    private readonly List<DescriptorBinding> _bindings = [];

    IDescriptorSetSchemaBuilder IDescriptorSetSchemaBuilder.AddUniformBuffer(
        ShaderStageFlags stageFlags,
        uint descriptorCount
    ) => AddUniformBuffer(stageFlags, descriptorCount);

    IDescriptorSetSchemaBuilder IDescriptorSetSchemaBuilder.AddUniformBuffer(
        uint binding,
        ShaderStageFlags stageFlags,
        uint descriptorCount
    ) => AddUniformBuffer(binding, stageFlags, descriptorCount);

    IDescriptorSetSchemaBuilder IDescriptorSetSchemaBuilder.AddCombinedImageSampler(
        ShaderStageFlags stageFlags,
        uint descriptorCount
    ) => AddCombinedImageSampler(stageFlags, descriptorCount);

    IDescriptorSetSchemaBuilder IDescriptorSetSchemaBuilder.AddCombinedImageSampler(
        uint binding,
        ShaderStageFlags stageFlags,
        uint descriptorCount
    ) => AddCombinedImageSampler(binding, stageFlags, descriptorCount);

    public DescriptorSetSchemaBuilder AddUniformBuffer(
        ShaderStageFlags stageFlags,
        uint descriptorCount = 1
    )
    {
        return AddUniformBuffer(GetNextBindingIndex(), stageFlags, descriptorCount);
    }

    public DescriptorSetSchemaBuilder AddUniformBuffer(
        uint binding,
        ShaderStageFlags stageFlags,
        uint descriptorCount = 1
    )
    {
        AddBinding(binding, DescriptorType.UniformBuffer, descriptorCount, stageFlags);

        return this;
    }

    public DescriptorSetSchemaBuilder AddCombinedImageSampler(
        ShaderStageFlags stageFlags,
        uint descriptorCount = 1
    )
    {
        return AddCombinedImageSampler(GetNextBindingIndex(), stageFlags, descriptorCount);
    }

    public DescriptorSetSchemaBuilder AddCombinedImageSampler(
        uint binding,
        ShaderStageFlags stageFlags,
        uint descriptorCount = 1
    )
    {
        AddBinding(binding, DescriptorType.CombinedImageSampler, descriptorCount, stageFlags);

        return this;
    }

    internal DescriptorSetSchema Build()
    {
        return new DescriptorSetSchema(set, _bindings.OrderBy(x => x.Binding).ToArray());
    }

    private void AddBinding(
        uint binding,
        DescriptorType descriptorType,
        uint descriptorCount,
        ShaderStageFlags stageFlags
    )
    {
        if (_bindings.Any(x => x.Binding == binding))
            throw new InvalidOperationException(
                $"Binding {binding} has already been defined in descriptor set {set}."
            );

        if (descriptorCount == 0)
            throw new ArgumentOutOfRangeException(
                nameof(descriptorCount),
                "Descriptor count must be greater than zero."
            );

        if (stageFlags == 0)
            throw new ArgumentException(
                "At least one shader stage must be specified.",
                nameof(stageFlags)
            );

        _bindings.Add(new DescriptorBinding(binding, descriptorType, descriptorCount, stageFlags));
    }

    private uint GetNextBindingIndex()
    {
        uint binding = 0;

        while (_bindings.Any(x => x.Binding == binding))
            binding++;

        return binding;
    }
}
