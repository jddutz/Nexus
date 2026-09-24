using Nexus.AssetPipeline.Typography.FontReader.TrueType;

namespace Nexus.AssetPipeline.Typography.FontReader.TrueType.Tables;

/// <summary>
/// Contains per-glyph horizontal advance widths and left side bearings.
/// </summary>
public sealed class HmtxTable
{
    private readonly ushort[] _advanceWidths;
    private readonly short[] _leftSideBearings;

    /// <summary>
    /// Initializes the parsed horizontal metrics.
    /// </summary>
    /// <param name="advanceWidths">The advances stored in full horizontal metric records.</param>
    /// <param name="leftSideBearings">The bearings for every glyph.</param>
    private HmtxTable(ushort[] advanceWidths, short[] leftSideBearings)
    {
        _advanceWidths = advanceWidths;
        _leftSideBearings = leftSideBearings;
    }

    /// <summary>
    /// Parses the horizontal metrics table.
    /// </summary>
    /// <param name="reader">A reader bounded to the hmtx table data.</param>
    /// <param name="glyphCount">The number of glyphs from the maxp table.</param>
    /// <param name="numberOfHorizontalMetrics">The number of full records from the hhea table.</param>
    /// <returns>The parsed horizontal metrics.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="InvalidDataException">The table is too short or its metric counts are invalid.</exception>
    public static HmtxTable Parse(
        TrueTypeReader reader,
        ushort glyphCount,
        ushort numberOfHorizontalMetrics
    )
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (
            glyphCount == 0
            || numberOfHorizontalMetrics == 0
            || numberOfHorizontalMetrics > glyphCount
        )
            throw new InvalidDataException("The 'hmtx' table has invalid metric counts.");

        var requiredLength =
            (long)numberOfHorizontalMetrics * 4
            + (long)(glyphCount - numberOfHorizontalMetrics) * 2;
        if (reader.Length < requiredLength)
            throw new InvalidDataException("The 'hmtx' table is too short.");

        var advanceWidths = new ushort[numberOfHorizontalMetrics];
        var leftSideBearings = new short[glyphCount];
        for (var index = 0; index < numberOfHorizontalMetrics; index++)
        {
            advanceWidths[index] = reader.ReadUInt16();
            leftSideBearings[index] = reader.ReadInt16();
        }

        for (var index = numberOfHorizontalMetrics; index < glyphCount; index++)
            leftSideBearings[index] = reader.ReadInt16();

        return new HmtxTable(advanceWidths, leftSideBearings);
    }

    /// <summary>
    /// Gets the horizontal metrics for a glyph index.
    /// </summary>
    /// <param name="glyphIndex">The zero-based glyph index.</param>
    /// <returns>The glyph's advance width and left side bearing in font units.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The glyph index is outside this font.</exception>
    public GlyphHorizontalMetrics GetMetrics(ushort glyphIndex)
    {
        if (glyphIndex >= _leftSideBearings.Length)
            throw new ArgumentOutOfRangeException(nameof(glyphIndex));

        var advanceIndex = Math.Min(glyphIndex, _advanceWidths.Length - 1);
        return new GlyphHorizontalMetrics(
            _advanceWidths[advanceIndex],
            _leftSideBearings[glyphIndex]
        );
    }
}

/// <summary>
/// Describes one glyph's horizontal layout metrics in font units.
/// </summary>
/// <param name="AdvanceWidth">The horizontal advance width.</param>
/// <param name="LeftSideBearing">The left side bearing.</param>
public readonly record struct GlyphHorizontalMetrics(ushort AdvanceWidth, short LeftSideBearing);
