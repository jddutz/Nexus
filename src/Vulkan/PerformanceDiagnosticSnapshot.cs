namespace Nexus.Graphics.Vulkan;

/// <summary>Immutable diagnostic evidence captured for one drawable.</summary>
public sealed record PerformanceDiagnosticSnapshot
{
    /// <summary>Initializes a diagnostic snapshot with copied values and bytes.</summary>
    /// <param name="drawableId">The drawable associated with the evidence.</param>
    /// <param name="category">The diagnostic category, such as View, Pipeline, or Vertex Buffer.</param>
    /// <param name="label">The stable label identifying this item within its category.</param>
    /// <param name="values">The scalar values captured at the observation point.</param>
    /// <param name="rawBytes">The resource bytes copied at the observation point.</param>
    /// <param name="decodedValues">Readable values decoded from the copied bytes.</param>
    public PerformanceDiagnosticSnapshot(
        DrawableId drawableId,
        string category,
        string label,
        IEnumerable<KeyValuePair<string, string>> values,
        ReadOnlySpan<byte> rawBytes = default,
        IEnumerable<string>? decodedValues = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(values);

        DrawableId = drawableId;
        Category = category;
        Label = label;
        Values = values.ToImmutableDictionary();
        RawBytes = ImmutableArray.CreateRange(rawBytes.ToArray());
        DecodedValues = decodedValues is null ? [] : [.. decodedValues];
    }

    /// <summary>Gets the drawable associated with this diagnostic evidence.</summary>
    public DrawableId DrawableId { get; }

    /// <summary>Gets the diagnostic category.</summary>
    public string Category { get; }

    /// <summary>Gets the label identifying this item within its category.</summary>
    public string Label { get; }

    /// <summary>Gets the immutable scalar values captured at the observation point.</summary>
    public ImmutableDictionary<string, string> Values { get; }

    /// <summary>Gets a copied immutable byte snapshot.</summary>
    public ImmutableArray<byte> RawBytes { get; }

    /// <summary>Gets readable values decoded from the copied bytes.</summary>
    public ImmutableArray<string> DecodedValues { get; }
}
