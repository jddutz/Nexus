using Nexus.AssetPipeline.Typography.FontReader.TrueType;

namespace Nexus.AssetPipeline.Typography.FontReader.TrueType.Tables;

/// <summary>
/// Contains the byte offsets of glyph data in the TrueType glyf table.
/// </summary>
public sealed class LocaTable
{
    private readonly int[] _offsets;

    /// <summary>
    /// Initializes the validated glyph offsets.
    /// </summary>
    /// <param name="offsets">The offsets for each glyph and the terminal offset.</param>
    private LocaTable(int[] offsets)
    {
        _offsets = offsets;
    }

    /// <summary>
    /// Gets the number of glyphs represented by the offset table.
    /// </summary>
    public int GlyphCount => _offsets.Length - 1;

    /// <summary>
    /// Parses short or long glyph offsets and validates them against the glyf table length.
    /// </summary>
    /// <param name="reader">A reader bounded to the loca table data.</param>
    /// <param name="glyphCount">The number of glyphs declared by the maxp table.</param>
    /// <param name="indexToLocFormat">The offset format declared by the head table.</param>
    /// <param name="glyfLength">The byte length of the glyf table.</param>
    /// <returns>The validated glyph offsets.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="glyfLength"/> is negative.</exception>
    /// <exception cref="InvalidDataException">The loca data or one of its offsets is invalid.</exception>
    public static LocaTable Parse(
        TrueTypeReader reader,
        ushort glyphCount,
        short indexToLocFormat,
        int glyfLength
    )
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentOutOfRangeException.ThrowIfNegative(glyfLength);
        if (indexToLocFormat is not (0 or 1))
            throw new InvalidDataException(
                "The 'head' table has an invalid index-to-location format."
            );

        var bytesPerOffset = indexToLocFormat == 0 ? sizeof(ushort) : sizeof(uint);
        var requiredLength = checked((glyphCount + 1) * bytesPerOffset);
        if (reader.Length < requiredLength)
            throw new InvalidDataException("The 'loca' table is too short.");

        var offsets = new int[glyphCount + 1];
        for (var index = 0; index < offsets.Length; index++)
        {
            var offset =
                indexToLocFormat == 0 ? (uint)reader.ReadUInt16() * 2 : reader.ReadUInt32();
            if (offset > glyfLength || (index > 0 && offset < offsets[index - 1]))
                throw new InvalidDataException(
                    "The 'loca' table contains an invalid glyph offset."
                );

            offsets[index] = checked((int)offset);
        }

        return new LocaTable(offsets);
    }

    /// <summary>
    /// Gets the byte range for a glyph index.
    /// </summary>
    /// <param name="glyphIndex">The zero-based glyph index.</param>
    /// <returns>The starting offset and byte length of the glyph data.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="glyphIndex"/> is outside the font.</exception>
    internal (int Offset, int Length) GetGlyphRange(ushort glyphIndex)
    {
        if (glyphIndex >= GlyphCount)
            throw new ArgumentOutOfRangeException(nameof(glyphIndex));

        var start = _offsets[glyphIndex];
        var end = _offsets[glyphIndex + 1];
        return (start, end - start);
    }
}
