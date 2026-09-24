using System.Collections.ObjectModel;

namespace Nexus.AssetPipeline.Typography.FontReader;

/// <summary>
/// Contains the ordered points that form one glyph contour.
/// </summary>
public sealed class FontContour
{
    private readonly ReadOnlyCollection<FontPoint> _points;

    /// <summary>
    /// Initializes a contour with its ordered points.
    /// </summary>
    /// <param name="points">The points in the contour.</param>
    internal FontContour(FontPoint[] points)
    {
        _points = Array.AsReadOnly(points);
    }

    /// <summary>
    /// Gets the ordered points in the contour.
    /// </summary>
    public IReadOnlyList<FontPoint> Points => _points;
}
