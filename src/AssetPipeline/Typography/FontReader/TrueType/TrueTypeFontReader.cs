using Nexus.AssetPipeline.Typography.FontReader;
using Nexus.AssetPipeline.Typography.FontReader.TrueType.Tables;

namespace Nexus.AssetPipeline.Typography.FontReader.TrueType;

/// <summary>
/// Opens an SFNT font and exposes its validated table directory and table data.
/// </summary>
public sealed class TrueTypeFontReader
{
    private readonly ReadOnlyMemory<byte> _data;
    private FontFace? _fontFace;
    private CmapTable? _cmapTable;
    private HmtxTable? _hmtxTable;

    /// <summary>
    /// Initializes a font reader from in-memory font data.
    /// </summary>
    /// <param name="data">The complete font file contents.</param>
    public TrueTypeFontReader(ReadOnlyMemory<byte> data)
    {
        _data = data;
        TableDirectory = TableDirectory.Parse(new TrueTypeReader(data));
    }

    /// <summary>
    /// Gets the parsed SFNT table directory.
    /// </summary>
    public TableDirectory TableDirectory { get; }

    /// <summary>
    /// Gets the font-wide metrics parsed from the required TrueType tables.
    /// </summary>
    public FontFace FontFace => _fontFace ??= ParseFontFace();

    /// <summary>
    /// Gets the glyph index representing a Unicode codepoint, or zero when it is absent.
    /// </summary>
    /// <param name="codepoint">The Unicode codepoint to resolve.</param>
    /// <returns>The glyph index, or zero if no mapping exists.</returns>
    public ushort GetGlyphIndex(int codepoint)
    {
        if (codepoint is < 0 or > 0x10FFFF)
            return 0;

        var glyphCount = MaxpTable.Parse(new TrueTypeReader(GetTable("maxp"))).GlyphCount;
        _cmapTable ??= CmapTable.Parse(new TrueTypeReader(GetTable("cmap")), glyphCount);
        return _cmapTable.GetGlyphIndex(codepoint);
    }

    /// <summary>
    /// Gets the horizontal metrics for a glyph index.
    /// </summary>
    /// <param name="glyphIndex">The zero-based glyph index, typically resolved from a codepoint.</param>
    /// <returns>The glyph's advance width and left side bearing in font units.</returns>
    public GlyphHorizontalMetrics GetHorizontalMetrics(ushort glyphIndex)
    {
        var fontFace = FontFace;
        _hmtxTable ??= HmtxTable.Parse(
            new TrueTypeReader(GetTable("hmtx")),
            fontFace.GlyphCount,
            fontFace.NumberOfHorizontalMetrics
        );
        return _hmtxTable.GetMetrics(glyphIndex);
    }

    /// <summary>
    /// Opens a font file from disk.
    /// </summary>
    /// <param name="path">The path to a TrueType or OpenType font file.</param>
    /// <returns>A reader for the font.</returns>
    public static TrueTypeFontReader Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new TrueTypeFontReader(File.ReadAllBytes(path));
    }

    /// <summary>
    /// Gets the bytes for a table declared in the font directory.
    /// </summary>
    /// <param name="tag">The exact four-character table tag.</param>
    /// <returns>A bounded memory view of the table.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tag"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">The directory does not contain the tag.</exception>
    public ReadOnlyMemory<byte> GetTable(string tag)
    {
        if (!TableDirectory.TryGetTable(tag, out var table))
            throw new KeyNotFoundException($"The font does not contain the '{tag}' table.");

        return _data.Slice(checked((int)table.Offset), checked((int)table.Length));
    }

    /// <summary>
    /// Parses the required global metric tables into a format-neutral font face.
    /// </summary>
    /// <returns>The parsed font face.</returns>
    private FontFace ParseFontFace()
    {
        var head = HeadTable.Parse(new TrueTypeReader(GetTable("head")));
        var maxp = MaxpTable.Parse(new TrueTypeReader(GetTable("maxp")));
        var hhea = HheaTable.Parse(new TrueTypeReader(GetTable("hhea")), maxp.GlyphCount);
        return new FontFace(head, hhea, maxp);
    }
}
