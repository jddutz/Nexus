using Nexus.AssetPipeline.Typography.FontReader.TrueType;

namespace Nexus.AssetPipeline.Typography.FontReader.TrueType.Tables;

/// <summary>
/// Contains the TrueType font-wide header metrics used by the glyph coordinate system.
/// </summary>
public sealed class HeadTable
{
    private const uint ExpectedVersion = 0x00010000;
    private const uint ExpectedMagicNumber = 0x5F0F3CF5;

    /// <summary>
    /// Initializes the parsed font header metrics.
    /// </summary>
    /// <param name="unitsPerEm">The number of font units per em.</param>
    /// <param name="indexToLocFormat">The glyph offset format used by the loca table.</param>
    private HeadTable(ushort unitsPerEm, short indexToLocFormat)
    {
        UnitsPerEm = unitsPerEm;
        IndexToLocFormat = indexToLocFormat;
    }

    /// <summary>
    /// Gets the number of font units per em.
    /// </summary>
    public ushort UnitsPerEm { get; }

    /// <summary>
    /// Gets the format of glyph offsets in the loca table.
    /// </summary>
    public short IndexToLocFormat { get; }

    /// <summary>
    /// Parses the font header table.
    /// </summary>
    /// <param name="reader">A reader bounded to the head table data.</param>
    /// <returns>The parsed header metrics.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="InvalidDataException">The table header or its metrics are invalid.</exception>
    public static HeadTable Parse(TrueTypeReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (reader.Length < 54)
            throw new InvalidDataException("The 'head' table is too short.");

        if (reader.ReadUInt32() != ExpectedVersion)
            throw new InvalidDataException("The 'head' table has an unsupported version.");

        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        if (reader.ReadUInt32() != ExpectedMagicNumber)
            throw new InvalidDataException("The 'head' table has an invalid magic number.");

        _ = reader.ReadUInt16();
        var unitsPerEm = reader.ReadUInt16();
        if (unitsPerEm is < 16 or > 16384)
            throw new InvalidDataException("The 'head' table has an invalid units-per-em value.");

        for (var index = 0; index < 4; index++)
            _ = reader.ReadUInt32();
        for (var index = 0; index < 4; index++)
            _ = reader.ReadInt16();
        _ = reader.ReadUInt16();
        _ = reader.ReadUInt16();
        _ = reader.ReadInt16();
        var indexToLocFormat = reader.ReadInt16();
        if (indexToLocFormat is not (0 or 1))
            throw new InvalidDataException(
                "The 'head' table has an invalid index-to-location format."
            );

        return new HeadTable(unitsPerEm, indexToLocFormat);
    }
}
