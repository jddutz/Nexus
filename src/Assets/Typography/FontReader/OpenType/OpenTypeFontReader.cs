using Nexus.Assets.Fonts;
using Nexus.Assets.Typography.FontReader.TrueType;
using Nexus.Assets.Typography.FontReader.TrueType.Tables;
using Nexus.Assets.Typography.Geometry;

namespace Nexus.Assets.Typography.FontReader.OpenType;

/// <summary>Reads OpenType fonts with TrueType or CFF Type 2 outlines.</summary>
public sealed class OpenTypeFontReader
{
    private readonly TrueTypeFontReader _sfnt;
    private CffFont? _cff;

    /// <summary>Initializes a reader over a complete SFNT font.</summary>
    public OpenTypeFontReader(ReadOnlyMemory<byte> data)
    {
        _sfnt = new TrueTypeFontReader(data);
    }

    /// <summary>Gets the shared SFNT table directory.</summary>
    public TableDirectory TableDirectory => _sfnt.TableDirectory;

    /// <summary>Gets font metrics in font units.</summary>
    public FontFace FontFace => _sfnt.FontFace;

    /// <summary>Opens a TrueType or OpenType file.</summary>
    public static OpenTypeFontReader Open(string path) => new(File.ReadAllBytes(path));

    /// <summary>Resolves a Unicode codepoint to a glyph index.</summary>
    public ushort GetGlyphIndex(int codepoint) => _sfnt.GetGlyphIndex(codepoint);

    /// <summary>Gets horizontal glyph metrics.</summary>
    public GlyphHorizontalMetrics GetHorizontalMetrics(ushort glyphIndex) =>
        _sfnt.GetHorizontalMetrics(glyphIndex);

    /// <summary>Gets codepoint kerning in em units.</summary>
    public IReadOnlyList<TextKerningPair> GetKerningPairs(
        IEnumerable<int> codepoints, string scriptTag = "latn", string? languageTag = null
    ) => _sfnt.GetKerningPairs(codepoints, scriptTag, languageTag);

    /// <summary>Decodes a glyph into geometry accepted by the distance-field generator.</summary>
    /// <param name="glyphIndex">The glyph to decode.</param>
    /// <param name="cubicApproximationTolerance">Positive, finite CFF approximation error bound in font units.
    /// The default preserves the original precision; TrueType outlines are unchanged.</param>
    /// <remarks>The bound excludes floating-point rounding. Exported outlines retain this approximation
    /// at all raster sizes, so choose a tolerance for the largest intended em size.</remarks>
    public IReadOnlyList<Contour> GetGlyphContours(ushort glyphIndex, float cubicApproximationTolerance = .01f)
    {
        if (!float.IsFinite(cubicApproximationTolerance) || cubicApproximationTolerance <= 0f)
            throw new ArgumentOutOfRangeException(nameof(cubicApproximationTolerance));
        if (glyphIndex >= FontFace.GlyphCount)
            throw new ArgumentOutOfRangeException(nameof(glyphIndex));
        if (TableDirectory.TryGetTable("CFF2", out _))
            throw new InvalidDataException("OpenType CFF2 variable outlines are not supported.");
        if (TableDirectory.TryGetTable("CFF ", out _))
        {
            _cff ??= new CffFont(_sfnt.GetTable("CFF "), FontFace.GlyphCount);
            return _cff.GetGlyphContours(glyphIndex, cubicApproximationTolerance);
        }
        if (!TableDirectory.TryGetTable("glyf", out _))
            throw new InvalidDataException("The OpenType font has no supported outline table (glyf or CFF).");
        return _sfnt.GetGlyphOutline(glyphIndex).Contours.Select(FontContourConverter.Convert).ToArray();
    }
}
