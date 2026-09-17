namespace Nexus.Graphics.Vulkan.Pipelines;

public readonly record struct PipelineId(ulong Value) : IEquatable<PipelineId>, IUniqueId
{
    /// <summary>
    /// Creates an PipelineId from a ulong value.
    /// </summary>
    public static implicit operator PipelineId(ulong value) => new(value);

    /// <summary>
    /// Converts PipelineId to its underlying ulong value.
    /// </summary>
    public static implicit operator ulong(PipelineId id) => id.Value;

    public override string ToString() => Value.ToString();

    public bool Equals(IUniqueId? other) => other != null && Value == other.Value;
}
