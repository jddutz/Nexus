using Nexus.Assets.Fonts;
using Nexus.Assets.Typography.FontReader;
using Nexus.Assets.Typography.FontReader.TrueType.Tables;
using Nexus.Assets.Typography.FontReader.TrueType.Tables.Gpos;
using Nexus.Assets.Typography.FontReader.TrueType.Tables.Kern;

namespace Nexus.Assets.Typography.FontReader.TrueType;

/// <summary>
/// Opens an SFNT font and exposes its validated table directory and table data.
/// </summary>
public sealed class TrueTypeFontReader
{
    private readonly ReadOnlyMemory<byte> _data;
    private FontFace? _fontFace;
    private CmapTable? _cmapTable;
    private HmtxTable? _hmtxTable;
    private LocaTable? _locaTable;
    private KernTable? _kernTable;

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

        _cmapTable ??= CmapTable.Parse(new TrueTypeReader(GetTable("cmap")), FontFace.GlyphCount);
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
    /// Gets kerning adjustments for selected glyphs, preferring supported GPOS data over legacy `kern` data.
    /// </summary>
    /// <param name="glyphIndices">The glyph indices needed by the current font build.</param>
    /// <param name="scriptTag">The four-character script tag used to select GPOS features.</param>
    /// <param name="languageTag">An optional four-character GPOS language-system tag.</param>
    /// <returns>Nonzero adjustments keyed by ordered glyph-index pairs.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="glyphIndices"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A glyph index is outside the font.</exception>
    /// <exception cref="InvalidDataException">A selected kerning table is malformed or unsupported.</exception>
    public IReadOnlyList<GlyphKerningPair> GetGlyphKerningPairs(
        IEnumerable<ushort> glyphIndices,
        string scriptTag = "latn",
        string? languageTag = null
    )
    {
        ArgumentNullException.ThrowIfNull(glyphIndices);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptTag);
        if (scriptTag.Length != 4)
            throw new ArgumentException(
                "A script tag must contain four characters.",
                nameof(scriptTag)
            );
        if (languageTag is not null && languageTag.Length != 4)
            throw new ArgumentException(
                "A language tag must contain four characters.",
                nameof(languageTag)
            );
        var glyphCount = FontFace.GlyphCount;
        var candidates = glyphIndices.Distinct().ToArray();
        if (candidates.Any(glyphIndex => glyphIndex >= glyphCount))
            throw new ArgumentOutOfRangeException(nameof(glyphIndices));

        if (TableDirectory.TryGetTable("GPOS", out _))
        {
            var gposTable = GposTable.Parse(
                new TrueTypeReader(GetTable("GPOS")),
                glyphCount,
                candidates,
                scriptTag,
                languageTag
            );
            if (gposTable.HasSupportedKerning)
                return gposTable.Pairs;
        }

        if (!TableDirectory.TryGetTable("kern", out _))
            return [];

        _kernTable ??= KernTable.Parse(new TrueTypeReader(GetTable("kern")), glyphCount);
        var candidateSet = candidates.ToHashSet();
        return _kernTable
            .Pairs.Where(pair =>
                candidateSet.Contains(pair.LeftGlyphIndex)
                && candidateSet.Contains(pair.RightGlyphIndex)
            )
            .ToArray();
    }

    /// <summary>
    /// Gets kerning pairs as codepoint adjustments for the selected repertoire.
    /// </summary>
    /// <param name="codepoints">The codepoints included in the current font build.</param>
    /// <param name="scriptTag">The four-character script tag used to select GPOS features.</param>
    /// <param name="languageTag">An optional four-character GPOS language-system tag.</param>
    /// <returns>Kerning adjustments keyed by ordered codepoint pairs, in em units.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="codepoints"/> is null.</exception>
    /// <exception cref="InvalidDataException">A selected kerning table is malformed or unsupported.</exception>
    public IReadOnlyList<TextKerningPair> GetKerningPairs(
        IEnumerable<int> codepoints,
        string scriptTag = "latn",
        string? languageTag = null
    )
    {
        ArgumentNullException.ThrowIfNull(codepoints);
        var codepointGlyphs = codepoints
            .Distinct()
            .Where(codepoint => codepoint is >= 0 and <= 0x10FFFF)
            .Select(codepoint => (Codepoint: codepoint, GlyphIndex: GetGlyphIndex(codepoint)))
            .ToArray();
        var glyphPairs = GetGlyphKerningPairs(
            codepointGlyphs.Select(mapping => mapping.GlyphIndex),
            scriptTag,
            languageTag
        );
        var unitsPerEm = FontFace.UnitsPerEm;
        var codepointsByGlyph = codepointGlyphs
            .GroupBy(mapping => mapping.GlyphIndex)
            .ToDictionary(
                group => group.Key,
                group => group.Select(mapping => mapping.Codepoint).Order().ToArray()
            );
        var result = new List<TextKerningPair>();
        foreach (var pair in glyphPairs)
        {
            foreach (var leftCodepoint in codepointsByGlyph[pair.LeftGlyphIndex])
            {
                foreach (var rightCodepoint in codepointsByGlyph[pair.RightGlyphIndex])
                {
                    result.Add(
                        new TextKerningPair(
                            leftCodepoint,
                            rightCodepoint,
                            (double)pair.AdvanceAdjustment / unitsPerEm
                        )
                    );
                }
            }
        }

        return result
            .OrderBy(pair => pair.LeftCodepoint)
            .ThenBy(pair => pair.RightCodepoint)
            .ToArray();
    }

    /// <summary>
    /// Gets the decoded outline for a simple or composite TrueType glyph.
    /// </summary>
    /// <param name="glyphIndex">The zero-based glyph index, typically resolved from a codepoint.</param>
    /// <returns>The glyph's contours and points in font units.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="glyphIndex"/> is outside the font.</exception>
    /// <exception cref="InvalidDataException">The loca or glyph data is malformed.</exception>
    /// <exception cref="InvalidDataException">The glyph data is malformed or contains cyclic or excessively deep composites.</exception>
    public FontGlyphOutline GetGlyphOutline(ushort glyphIndex)
    {
        var glyfData = GetTable("glyf");
        if (_locaTable is null)
        {
            var fontFace = FontFace;
            _locaTable = LocaTable.Parse(
                new TrueTypeReader(GetTable("loca")),
                fontFace.GlyphCount,
                fontFace.IndexToLocFormat,
                glyfData.Length
            );
        }

        return GlyfTable.ParseGlyph(new TrueTypeReader(glyfData), _locaTable, glyphIndex);
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
