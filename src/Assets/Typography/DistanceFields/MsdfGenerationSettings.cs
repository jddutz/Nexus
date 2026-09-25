namespace Nexus.Assets.Typography.DistanceFields;

/// <summary>
/// Controls the resolution, encoded distance range, and empty border of one generated glyph.
/// </summary>
public sealed record MsdfGenerationSettings
{
    /// <summary>Gets or sets the number of output pixels per geometry unit.</summary>
    public float PixelsPerUnit { get; init; } = 1f;

    /// <summary>Gets or sets the encoded signed-distance range in output pixels.</summary>
    public float DistanceRange { get; init; } = 4f;

    /// <summary>Gets or sets the border around the geometry in output pixels.</summary>
    public int Padding { get; init; } = 2;
}
