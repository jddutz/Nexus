namespace Nexus.AssetPipeline.Typography.FontReader;

/// <summary>
/// Represents a point in a glyph outline, in font units.
/// </summary>
public readonly struct FontPoint
{
    /// <summary>
    /// Initializes a glyph point.
    /// </summary>
    /// <param name="x">The horizontal coordinate in font units.</param>
    /// <param name="y">The vertical coordinate in font units.</param>
    /// <param name="onCurve">Whether the point lies on the outline curve.</param>
    public FontPoint(int x, int y, bool onCurve)
    {
        X = x;
        Y = y;
        OnCurve = onCurve;
    }

    /// <summary>
    /// Gets the horizontal coordinate in font units.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Gets the vertical coordinate in font units.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// Gets whether the point lies on the outline curve.
    /// </summary>
    public bool OnCurve { get; }
}
