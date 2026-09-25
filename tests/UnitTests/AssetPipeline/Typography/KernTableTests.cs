using System.Buffers.Binary;
using Nexus.Assets.Typography.FontReader;
using Nexus.Assets.Typography.FontReader.TrueType;
using Nexus.Assets.Typography.FontReader.TrueType.Tables.Kern;

namespace Nexus.AssetPipeline.Tests;

/// <summary>
/// Verifies supported legacy horizontal kern table behavior.
/// </summary>
public sealed class KernTableTests
{
    /// <summary>
    /// Verifies format-zero pairs merge additively and honor override subtables.
    /// </summary>
    [Fact]
    public void KernTable_parsesAndMergesFormatZeroPairs()
    {
        var table = BuildTable(
            BuildFormatZeroSubtable(0x0001, [(1, 2, -80), (2, 1, 20)]),
            BuildFormatZeroSubtable(0x0001, [(1, 2, -10)]),
            BuildFormatZeroSubtable(0x0009, [(1, 2, -40)])
        );

        var result = KernTable.Parse(new TrueTypeReader(table), glyphCount: 3);

        Assert.Collection(
            result.Pairs,
            pair => Assert.Equal(new GlyphKerningPair(1, 2, -40), pair),
            pair => Assert.Equal(new GlyphKerningPair(2, 1, 20), pair)
        );
    }

    /// <summary>
    /// Verifies unsupported horizontal pair formats fail explicitly.
    /// </summary>
    [Fact]
    public void KernTable_rejectsUnsupportedHorizontalFormat()
    {
        var table = BuildTable(BuildFormatZeroSubtable(0x0101, []));

        var exception = Assert.Throws<InvalidDataException>(() =>
            KernTable.Parse(new TrueTypeReader(table), glyphCount: 3)
        );

        Assert.Contains("format 1", exception.Message);
    }

    /// <summary>
    /// Creates a classic version-zero kern table containing the supplied subtables.
    /// </summary>
    /// <param name="subtables">The complete subtable byte sequences.</param>
    /// <returns>The encoded kern table.</returns>
    private static byte[] BuildTable(params byte[][] subtables)
    {
        var table = new byte[4 + subtables.Sum(subtable => subtable.Length)];
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(2), checked((ushort)subtables.Length));
        var offset = 4;
        foreach (var subtable in subtables)
        {
            subtable.CopyTo(table, offset);
            offset += subtable.Length;
        }

        return table;
    }

    /// <summary>
    /// Creates a format-zero subtable from ordered glyph-index pairs.
    /// </summary>
    /// <param name="coverage">The subtable format and coverage flags.</param>
    /// <param name="pairs">The left glyph, right glyph, and adjustment tuples.</param>
    /// <returns>The encoded format-zero subtable.</returns>
    private static byte[] BuildFormatZeroSubtable(
        ushort coverage,
        (ushort Left, ushort Right, short Adjustment)[] pairs
    )
    {
        var subtable = new byte[14 + pairs.Length * 6];
        BinaryPrimitives.WriteUInt16BigEndian(subtable.AsSpan(2), checked((ushort)subtable.Length));
        BinaryPrimitives.WriteUInt16BigEndian(subtable.AsSpan(4), coverage);
        BinaryPrimitives.WriteUInt16BigEndian(subtable.AsSpan(6), checked((ushort)pairs.Length));
        for (var index = 0; index < pairs.Length; index++)
        {
            var pair = pairs[index];
            var offset = 14 + index * 6;
            BinaryPrimitives.WriteUInt16BigEndian(subtable.AsSpan(offset), pair.Left);
            BinaryPrimitives.WriteUInt16BigEndian(subtable.AsSpan(offset + 2), pair.Right);
            BinaryPrimitives.WriteInt16BigEndian(subtable.AsSpan(offset + 4), pair.Adjustment);
        }

        return subtable;
    }
}
