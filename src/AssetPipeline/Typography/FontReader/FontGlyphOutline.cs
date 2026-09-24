using System.Collections.ObjectModel;

namespace Nexus.AssetPipeline.Typography.FontReader;

/// <summary>
/// Contains the contours decoded from one simple TrueType glyph.
/// </summary>
public sealed class FontGlyphOutline
{
    private readonly ReadOnlyCollection<FontContour> _contours;

    /// <summary>
    /// Initializes an outline with its decoded contours.
    /// </summary>
    /// <param name="contours">The contours in the glyph.</param>
    internal FontGlyphOutline(FontContour[] contours)
    {
        _contours = Array.AsReadOnly(contours);
    }

    /// <summary>
    /// Gets the contours in the glyph.
    /// </summary>
    public IReadOnlyList<FontContour> Contours => _contours;
}
