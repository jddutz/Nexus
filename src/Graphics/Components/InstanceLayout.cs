namespace Nexus.Graphics.Components;

/// <summary>
/// Identifies the scalar layout of an instance input attribute.
/// </summary>
public enum InstanceInputFormatEnum
{
    /// <summary>
    /// A vector containing four 32-bit floating-point values.
    /// </summary>
    Float4,
}

/// <summary>
/// Describes one attribute in a packed instance record.
/// </summary>
/// <param name="Location">The shader input location.</param>
/// <param name="Offset">The byte offset within the instance record.</param>
/// <param name="Format">The scalar layout of the attribute.</param>
public sealed record InstanceInput(uint Location, uint Offset, InstanceInputFormatEnum Format);

/// <summary>
/// Describes the packed per-instance records consumed by a graphics pipeline.
/// </summary>
public sealed class InstanceLayout
{
    /// <summary>Gets the identity of this instance layout.</summary>
    public ResourceId Id { get; }

    /// <summary>
    /// Gets the byte size of one instance record.
    /// </summary>
    public uint Stride { get; }

    /// <summary>
    /// Gets the attributes in the instance record.
    /// </summary>
    public IReadOnlyList<InstanceInput> Inputs { get; }

    /// <summary>
    /// Initializes an instance-record layout.
    /// </summary>
    /// <param name="stride">The byte size of one instance record.</param>
    /// <param name="inputs">The shader input attributes in the record.</param>
    public InstanceLayout(uint stride, IEnumerable<InstanceInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        Stride = stride;
        Inputs = [.. inputs];

        if (Inputs.Count == 0)
            throw new ArgumentException(
                "An instance layout requires at least one input.",
                nameof(inputs)
            );
        if (Inputs.Any(input => input.Offset + GetSize(input.Format) > Stride))
            throw new ArgumentException(
                "An instance input exceeds the record stride.",
                nameof(inputs)
            );
        if (Inputs.GroupBy(input => input.Location).Any(group => group.Count() > 1))
            throw new ArgumentException("Instance input locations must be unique.", nameof(inputs));
    }

    /// <summary>
    /// Gets the byte size of an instance input format.
    /// </summary>
    /// <param name="format">The instance input format.</param>
    /// <returns>The format size in bytes.</returns>
    public static uint GetSize(InstanceInputFormatEnum format) =>
        format switch
        {
            InstanceInputFormatEnum.Float4 => sizeof(float) * 4u,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };
}
