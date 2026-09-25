using Nexus.Assets.Typography.FontReader;
using Nexus.Assets.Typography.FontReader.TrueType;

namespace Nexus.Assets.Typography.FontReader.TrueType.Tables.Kern;

/// <summary>
/// Parses supported legacy horizontal pair kerning subtables.
/// </summary>
public sealed class KernTable
{
    private readonly IReadOnlyList<GlyphKerningPair> _pairs;

    /// <summary>
    /// Initializes a parsed legacy kerning table.
    /// </summary>
    /// <param name="pairs">The merged glyph-index adjustments.</param>
    private KernTable(GlyphKerningPair[] pairs)
    {
        _pairs = Array.AsReadOnly(pairs);
    }

    /// <summary>
    /// Gets the merged kerning pairs in glyph-index order.
    /// </summary>
    public IReadOnlyList<GlyphKerningPair> Pairs => _pairs;

    /// <summary>
    /// Parses version-zero `kern` tables containing horizontal format-zero subtables.
    /// </summary>
    /// <param name="reader">A reader bounded to the complete `kern` table.</param>
    /// <param name="glyphCount">The number of glyphs declared by the font.</param>
    /// <returns>The supported legacy kerning pairs.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="InvalidDataException">The table or a supported horizontal subtable is malformed or unsupported.</exception>
    public static KernTable Parse(TrueTypeReader reader, ushort glyphCount)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (reader.Length < 4)
            throw new InvalidDataException("The 'kern' table is too short.");

        if (reader.ReadUInt16() != 0)
            throw new InvalidDataException("The 'kern' table has an unsupported version.");

        var subtableCount = reader.ReadUInt16();
        var mergedPairs = new Dictionary<(ushort Left, ushort Right), int>();
        for (var index = 0; index < subtableCount; index++)
        {
            var subtableStart = reader.Position;
            if (reader.Length - subtableStart < 6)
                throw new InvalidDataException("A 'kern' subtable header is truncated.");

            var version = reader.ReadUInt16();
            var subtableLength = reader.ReadUInt16();
            var coverage = reader.ReadUInt16();
            if (subtableLength < 6 || subtableLength > reader.Length - subtableStart)
                throw new InvalidDataException("A 'kern' subtable has an invalid length.");

            var subtableReader = new TrueTypeReader(reader.Slice(subtableStart, subtableLength));

            var format = (byte)(coverage >> 8);
            var isHorizontal = (coverage & 0x0001) != 0;
            var isMinimum = (coverage & 0x0002) != 0;
            var isCrossStream = (coverage & 0x0004) != 0;
            var isOverride = (coverage & 0x0008) != 0;
            if (version != 0)
                throw new InvalidDataException("A 'kern' subtable has an unsupported version.");

            if (!isHorizontal || isMinimum || isCrossStream)
            {
                var nextSubtableOffset = subtableStart + subtableLength;
                reader = new TrueTypeReader(
                    reader.Slice(nextSubtableOffset, reader.Length - nextSubtableOffset)
                );
                continue;
            }

            if (format != 0)
                throw new InvalidDataException(
                    $"The horizontal 'kern' subtable format {format} is not supported."
                );

            ParseFormatZero(subtableReader, glyphCount, isOverride, mergedPairs);
            var nextOffset = subtableStart + subtableLength;
            reader = new TrueTypeReader(reader.Slice(nextOffset, reader.Length - nextOffset));
        }

        var pairs = mergedPairs
            .Where(pair => pair.Value != 0)
            .OrderBy(pair => pair.Key.Left)
            .ThenBy(pair => pair.Key.Right)
            .Select(pair => new GlyphKerningPair(pair.Key.Left, pair.Key.Right, pair.Value))
            .ToArray();
        return new KernTable(pairs);
    }

    /// <summary>
    /// Reads a format-zero pair array and merges its values into the table result.
    /// </summary>
    /// <param name="reader">The reader positioned at the format-zero subtable header.</param>
    /// <param name="glyphCount">The number of glyphs declared by the font.</param>
    /// <param name="isOverride">Whether this subtable replaces earlier values for matching pairs.</param>
    /// <param name="pairs">The merged pair values under construction.</param>
    /// <exception cref="InvalidDataException">The pair array is truncated, unsorted, duplicated, or references an invalid glyph.</exception>
    private static void ParseFormatZero(
        TrueTypeReader reader,
        ushort glyphCount,
        bool isOverride,
        Dictionary<(ushort Left, ushort Right), int> pairs
    )
    {
        if (reader.Length < 14)
            throw new InvalidDataException("A format-zero 'kern' subtable is too short.");

        _ = reader.ReadUInt16();
        _ = reader.ReadUInt16();
        _ = reader.ReadUInt16();
        var pairCount = reader.ReadUInt16();
        _ = reader.ReadUInt16();
        _ = reader.ReadUInt16();
        _ = reader.ReadUInt16();

        if (pairCount > (reader.Length - reader.Position) / 6)
            throw new InvalidDataException("A format-zero 'kern' pair array is truncated.");

        (ushort Left, ushort Right)? previousPair = null;
        for (var index = 0; index < pairCount; index++)
        {
            var leftGlyphIndex = reader.ReadUInt16();
            var rightGlyphIndex = reader.ReadUInt16();
            var adjustment = reader.ReadInt16();
            var currentPair = (leftGlyphIndex, rightGlyphIndex);
            if (leftGlyphIndex >= glyphCount || rightGlyphIndex >= glyphCount)
                throw new InvalidDataException(
                    "A 'kern' pair references a glyph outside the font."
                );

            if (previousPair is { } previous && Compare(previous, currentPair) >= 0)
                throw new InvalidDataException(
                    "A format-zero 'kern' pair array is not strictly sorted."
                );

            if (isOverride || !pairs.TryGetValue(currentPair, out var previousAdjustment))
                pairs[currentPair] = adjustment;
            else
                pairs[currentPair] = checked(previousAdjustment + adjustment);
            previousPair = currentPair;
        }
    }

    /// <summary>
    /// Compares two glyph-index pair keys in their required lexicographic order.
    /// </summary>
    /// <param name="left">The first pair.</param>
    /// <param name="right">The second pair.</param>
    /// <returns>A negative number, zero, or a positive number according to pair ordering.</returns>
    private static int Compare((ushort Left, ushort Right) left, (ushort Left, ushort Right) right)
    {
        var leftComparison = left.Left.CompareTo(right.Left);
        return leftComparison != 0 ? leftComparison : left.Right.CompareTo(right.Right);
    }
}
