using Nexus.Assets.Typography.FontReader.TrueType;

namespace Nexus.Assets.Typography.FontReader.TrueType.Tables;

/// <summary>
/// Contains the maximum profile's global glyph count.
/// </summary>
public sealed class MaxpTable
{
    private const uint TrueTypeVersion = 0x00010000;
    private const uint CffVersion = 0x00005000;

    /// <summary>
    /// Initializes the parsed maximum profile metrics.
    /// </summary>
    /// <param name="glyphCount">The number of glyphs in the font.</param>
    private MaxpTable(ushort glyphCount)
    {
        GlyphCount = glyphCount;
    }

    /// <summary>
    /// Gets the number of glyphs in the font.
    /// </summary>
    public ushort GlyphCount { get; }

    /// <summary>
    /// Parses the maximum profile table.
    /// </summary>
    /// <param name="reader">A reader bounded to the maxp table data.</param>
    /// <returns>The parsed maximum profile metrics.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="InvalidDataException">The table header or its glyph count is invalid.</exception>
    public static MaxpTable Parse(TrueTypeReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (reader.Length < 6)
            throw new InvalidDataException("The 'maxp' table is too short.");

        var version = reader.ReadUInt32();
        if (version is not (TrueTypeVersion or CffVersion))
            throw new InvalidDataException("The 'maxp' table has an unsupported version.");

        var glyphCount = reader.ReadUInt16();
        if (glyphCount == 0)
            throw new InvalidDataException("The 'maxp' table has an invalid glyph count.");

        return new MaxpTable(glyphCount);
    }
}
