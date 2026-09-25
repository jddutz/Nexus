using Nexus.Assets.Typography.FontReader.TrueType;

namespace Nexus.Assets.Typography.FontReader.TrueType.Tables;

/// <summary>
/// Contains the font's global horizontal layout metrics.
/// </summary>
public sealed class HheaTable
{
    private const uint ExpectedVersion = 0x00010000;

    /// <summary>
    /// Initializes the parsed horizontal header metrics.
    /// </summary>
    /// <param name="ascender">The typographic ascender in font units.</param>
    /// <param name="descender">The typographic descender in font units.</param>
    /// <param name="lineGap">The typographic line gap in font units.</param>
    /// <param name="numberOfHorizontalMetrics">The number of full horizontal metric records.</param>
    private HheaTable(
        short ascender,
        short descender,
        short lineGap,
        ushort numberOfHorizontalMetrics
    )
    {
        Ascender = ascender;
        Descender = descender;
        LineGap = lineGap;
        NumberOfHorizontalMetrics = numberOfHorizontalMetrics;
    }

    /// <summary>
    /// Gets the typographic ascender in font units.
    /// </summary>
    public short Ascender { get; }

    /// <summary>
    /// Gets the typographic descender in font units.
    /// </summary>
    public short Descender { get; }

    /// <summary>
    /// Gets the typographic line gap in font units.
    /// </summary>
    public short LineGap { get; }

    /// <summary>
    /// Gets the number of full horizontal metric records in the font.
    /// </summary>
    public ushort NumberOfHorizontalMetrics { get; }

    /// <summary>
    /// Parses the horizontal header table.
    /// </summary>
    /// <param name="reader">A reader bounded to the hhea table data.</param>
    /// <param name="glyphCount">The glyph count from the maxp table.</param>
    /// <returns>The parsed horizontal metrics.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="InvalidDataException">The table header or its metrics are invalid.</exception>
    public static HheaTable Parse(TrueTypeReader reader, ushort glyphCount)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (reader.Length < 36)
            throw new InvalidDataException("The 'hhea' table is too short.");

        if (reader.ReadUInt32() != ExpectedVersion)
            throw new InvalidDataException("The 'hhea' table has an unsupported version.");

        var ascender = reader.ReadInt16();
        var descender = reader.ReadInt16();
        var lineGap = reader.ReadInt16();
        for (var index = 0; index < 12; index++)
            _ = reader.ReadInt16();
        var numberOfHorizontalMetrics = reader.ReadUInt16();
        if (numberOfHorizontalMetrics == 0 || numberOfHorizontalMetrics > glyphCount)
            throw new InvalidDataException(
                "The 'hhea' table has an invalid number of horizontal metrics."
            );

        return new HheaTable(ascender, descender, lineGap, numberOfHorizontalMetrics);
    }
}
