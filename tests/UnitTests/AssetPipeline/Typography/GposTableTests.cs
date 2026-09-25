using System.Buffers.Binary;
using System.Text;
using Nexus.Assets.Fonts;
using Nexus.Assets.Typography.FontReader;
using Nexus.Assets.Typography.FontReader.TrueType;
using Nexus.Assets.Typography.FontReader.TrueType.Tables.Gpos;

namespace Nexus.AssetPipeline.Tests;

/// <summary>
/// Verifies GPOS PairPos parsing and kerning-source precedence.
/// </summary>
public sealed class GposTableTests
{
    /// <summary>
    /// Verifies PairPos format 1 reads explicit glyph pairs and first-glyph xAdvance values.
    /// </summary>
    [Fact]
    public void GposTable_parsesExplicitPairPositioning()
    {
        var gpos = GposTable.Parse(
            new TrueTypeReader(BuildGpos(BuildPairPositioningFormatOne(-120))),
            glyphCount: 3,
            glyphIndices: [1, 2]
        );

        Assert.True(gpos.HasSupportedKerning);
        Assert.Equal(new GlyphKerningPair(1, 2, -120), Assert.Single(gpos.Pairs));
    }

    /// <summary>
    /// Verifies flagged PairPos lookups are skipped and legacy kern data is used as fallback.
    /// </summary>
    [Fact]
    public void TrueTypeFontReader_fallsBackToLegacyKernWhenGposLookupHasFlags()
    {
        var font = new TrueTypeFontReader(
            BuildFont(
                ("cmap", BuildCmap()),
                ("head", BuildHead()),
                ("hhea", BuildHhea()),
                ("maxp", BuildMaxp(3)),
                ("kern", BuildKern(-80)),
                ("GPOS", BuildGpos(BuildPairPositioningFormatOne(-120), lookupFlags: 0x0008))
            )
        );

        Assert.Equal(
            new TextKerningPair(0x41, 0x56, -0.08),
            Assert.Single(font.GetKerningPairs([0x41, 0x56]))
        );
    }

    /// <summary>
    /// Verifies PairPos format 1 uses the first glyph's xAdvance and ignores other fields.
    /// </summary>
    [Fact]
    public void GposTable_usesFirstGlyphAdvanceForExplicitPairPositioning()
    {
        var gpos = GposTable.Parse(
            new TrueTypeReader(
                BuildGpos(
                    BuildPairPositioningFormatOne(
                        -120,
                        secondAdjustment: 45,
                        firstValueFormat: 0x0005
                    )
                )
            ),
            glyphCount: 3,
            glyphIndices: [1, 2]
        );

        Assert.True(gpos.HasSupportedKerning);
        Assert.Equal(new GlyphKerningPair(1, 2, -120), Assert.Single(gpos.Pairs));
    }

    /// <summary>
    /// Verifies PairPos format 2 expands class pairs only across requested glyphs.
    /// </summary>
    [Fact]
    public void GposTable_expandsClassPairsForRequestedGlyphs()
    {
        var gpos = GposTable.Parse(
            new TrueTypeReader(BuildGpos(BuildPairPositioningFormatTwo(-60, 35))),
            glyphCount: 4,
            glyphIndices: [1, 2]
        );

        Assert.True(gpos.HasSupportedKerning);
        Assert.Equal(new GlyphKerningPair(1, 2, -60), Assert.Single(gpos.Pairs));
    }

    /// <summary>
    /// Verifies a second-glyph advance without first-glyph xAdvance is not scalar kerning.
    /// </summary>
    [Fact]
    public void GposTable_doesNotSupportSecondGlyphAdvanceAsScalarKerning()
    {
        var gpos = GposTable.Parse(
            new TrueTypeReader(
                BuildGpos(
                    BuildPairPositioningFormatOne(
                        0,
                        secondAdjustment: -120,
                        firstValueFormat: 0x0001
                    )
                )
            ),
            glyphCount: 3,
            glyphIndices: [1, 2]
        );

        Assert.False(gpos.HasSupportedKerning);
        Assert.Empty(gpos.Pairs);
    }

    /// <summary>
    /// Verifies the reader prefers applicable GPOS values and converts glyph pairs to codepoints.
    /// </summary>
    [Fact]
    public void TrueTypeFontReader_prefersGposAndConvertsPairsToCodepoints()
    {
        var font = new TrueTypeFontReader(
            BuildFont(
                ("cmap", BuildCmap()),
                ("head", BuildHead()),
                ("hhea", BuildHhea()),
                ("maxp", BuildMaxp(3)),
                ("kern", BuildKern(-80)),
                ("GPOS", BuildGpos(BuildPairPositioningFormatOne(-120)))
            )
        );

        var pairs = font.GetKerningPairs([0x41, 0x56]);

        Assert.Equal(new TextKerningPair(0x41, 0x56, -0.12), Assert.Single(pairs));
        Assert.Empty(font.GetKerningPairs([0x41]));
    }

    /// <summary>
    /// Verifies the reader falls back to legacy kern when no supported GPOS kern lookup is present.
    /// </summary>
    [Fact]
    public void TrueTypeFontReader_fallsBackToLegacyKern()
    {
        var font = new TrueTypeFontReader(
            BuildFont(
                ("cmap", BuildCmap()),
                ("head", BuildHead()),
                ("hhea", BuildHhea()),
                ("maxp", BuildMaxp(3)),
                ("kern", BuildKern(-80))
            )
        );

        Assert.Equal(
            new TextKerningPair(0x41, 0x56, -0.08),
            Assert.Single(font.GetKerningPairs([0x41, 0x56]))
        );
    }

    /// <summary>
    /// Verifies selected GPOS data is not supplemented by legacy kern for unmatched pairs.
    /// </summary>
    [Fact]
    public void TrueTypeFontReader_doesNotSupplementSelectedGposWithLegacyKern()
    {
        var font = new TrueTypeFontReader(
            BuildFont(
                ("cmap", BuildCmap()),
                ("head", BuildHead()),
                ("hhea", BuildHhea()),
                ("maxp", BuildMaxp(3)),
                ("kern", BuildKern(-80)),
                ("GPOS", BuildGpos(BuildPairPositioningFormatOne(-120, rightGlyphIndex: 1)))
            )
        );

        Assert.Equal(
            new TextKerningPair(0x41, 0x41, -0.12),
            Assert.Single(font.GetKerningPairs([0x41, 0x56]))
        );
    }

    /// <summary>
    /// Creates a minimal GPOS 1.0 table with one Latin default-language kern feature and lookup.
    /// </summary>
    /// <param name="pairPositioning">The PairPos lookup subtable.</param>
    /// <param name="lookupFlags">The lookup flags.</param>
    /// <returns>The encoded GPOS table.</returns>
    private static byte[] BuildGpos(byte[] pairPositioning, ushort lookupFlags = 0)
    {
        var scriptList = new byte[20];
        WriteUInt16(scriptList, 0, 1);
        Encoding.ASCII.GetBytes("latn", scriptList.AsSpan(2, 4));
        WriteUInt16(scriptList, 6, 8);
        WriteUInt16(scriptList, 8, 4);
        WriteUInt16(scriptList, 12, 0);
        WriteUInt16(scriptList, 14, ushort.MaxValue);
        WriteUInt16(scriptList, 16, 1);
        WriteUInt16(scriptList, 18, 0);

        var featureList = new byte[14];
        WriteUInt16(featureList, 0, 1);
        Encoding.ASCII.GetBytes("kern", featureList.AsSpan(2, 4));
        WriteUInt16(featureList, 6, 8);
        WriteUInt16(featureList, 10, 1);
        WriteUInt16(featureList, 12, 0);

        var lookupList = new byte[12 + pairPositioning.Length];
        WriteUInt16(lookupList, 0, 1);
        WriteUInt16(lookupList, 2, 4);
        WriteUInt16(lookupList, 4, 2);
        WriteUInt16(lookupList, 6, lookupFlags);
        WriteUInt16(lookupList, 8, 1);
        WriteUInt16(lookupList, 10, 8);
        pairPositioning.CopyTo(lookupList, 12);

        var scriptOffset = 10;
        var featureOffset = scriptOffset + scriptList.Length;
        var lookupOffset = featureOffset + featureList.Length;
        var gpos = new byte[lookupOffset + lookupList.Length];
        WriteUInt16(gpos, 0, 1);
        WriteUInt16(gpos, 2, 0);
        WriteUInt16(gpos, 4, checked((ushort)scriptOffset));
        WriteUInt16(gpos, 6, checked((ushort)featureOffset));
        WriteUInt16(gpos, 8, checked((ushort)lookupOffset));
        scriptList.CopyTo(gpos, scriptOffset);
        featureList.CopyTo(gpos, featureOffset);
        lookupList.CopyTo(gpos, lookupOffset);
        return gpos;
    }

    /// <summary>
    /// Creates one explicit PairPos format 1 pair with an xAdvance adjustment.
    /// </summary>
    /// <param name="adjustment">The signed xAdvance adjustment.</param>
    /// <returns>The encoded PairPos format 1 subtable.</returns>
    private static byte[] BuildPairPositioningFormatOne(
        short adjustment,
        ushort rightGlyphIndex = 2,
        short secondAdjustment = 0,
        ushort firstValueFormat = 0x0004
    )
    {
        var firstRecordLength =
            2 * ((firstValueFormat & 0x0001) != 0 ? 1 : 0)
            + 2 * ((firstValueFormat & 0x0004) != 0 ? 1 : 0);
        var pairPositioning = new byte[24 + firstRecordLength];
        WriteUInt16(pairPositioning, 0, 1);
        WriteUInt16(pairPositioning, 2, 12);
        WriteUInt16(pairPositioning, 4, firstValueFormat);
        WriteUInt16(pairPositioning, 6, 4);
        WriteUInt16(pairPositioning, 8, 1);
        WriteUInt16(pairPositioning, 10, 18);
        WriteUInt16(pairPositioning, 12, 1);
        WriteUInt16(pairPositioning, 14, 1);
        WriteUInt16(pairPositioning, 16, 1);
        WriteUInt16(pairPositioning, 18, 1);
        WriteUInt16(pairPositioning, 20, rightGlyphIndex);
        var valueOffset = 22;
        if ((firstValueFormat & 0x0001) != 0)
        {
            BinaryPrimitives.WriteInt16BigEndian(pairPositioning.AsSpan(valueOffset), 30);
            valueOffset += 2;
        }

        if ((firstValueFormat & 0x0004) != 0)
        {
            BinaryPrimitives.WriteInt16BigEndian(pairPositioning.AsSpan(valueOffset), adjustment);
            valueOffset += 2;
        }

        BinaryPrimitives.WriteInt16BigEndian(pairPositioning.AsSpan(valueOffset), secondAdjustment);
        return pairPositioning;
    }

    /// <summary>
    /// Creates a PairPos format 2 matrix with a nonzero adjustment for classes one and one.
    /// </summary>
    /// <param name="adjustment">The signed xAdvance adjustment.</param>
    /// <returns>The encoded PairPos format 2 subtable.</returns>
    private static byte[] BuildPairPositioningFormatTwo(short adjustment, short secondAdjustment)
    {
        var pairPositioning = new byte[54];
        WriteUInt16(pairPositioning, 0, 2);
        WriteUInt16(pairPositioning, 2, 32);
        WriteUInt16(pairPositioning, 4, 4);
        WriteUInt16(pairPositioning, 6, 4);
        WriteUInt16(pairPositioning, 8, 38);
        WriteUInt16(pairPositioning, 10, 46);
        WriteUInt16(pairPositioning, 12, 2);
        WriteUInt16(pairPositioning, 14, 2);

        BinaryPrimitives.WriteInt16BigEndian(pairPositioning.AsSpan(28), adjustment);
        BinaryPrimitives.WriteInt16BigEndian(pairPositioning.AsSpan(30), secondAdjustment);

        WriteUInt16(pairPositioning, 32, 1);
        WriteUInt16(pairPositioning, 34, 1);
        WriteUInt16(pairPositioning, 36, 1);

        WriteUInt16(pairPositioning, 38, 1);
        WriteUInt16(pairPositioning, 40, 1);
        WriteUInt16(pairPositioning, 42, 1);
        WriteUInt16(pairPositioning, 44, 1);

        WriteUInt16(pairPositioning, 46, 1);
        WriteUInt16(pairPositioning, 48, 2);
        WriteUInt16(pairPositioning, 50, 1);
        WriteUInt16(pairPositioning, 52, 1);
        return pairPositioning;
    }

    /// <summary>
    /// Creates a classic format-zero kern table with one adjustment.
    /// </summary>
    /// <param name="adjustment">The signed adjustment.</param>
    /// <returns>The encoded kern table.</returns>
    private static byte[] BuildKern(short adjustment)
    {
        var table = new byte[24];
        WriteUInt16(table, 2, 1);
        WriteUInt16(table, 6, 20);
        WriteUInt16(table, 8, 1);
        WriteUInt16(table, 10, 1);
        WriteUInt16(table, 12, 0);
        WriteUInt16(table, 14, 0);
        WriteUInt16(table, 16, 0);
        WriteUInt16(table, 18, 1);
        WriteUInt16(table, 20, 2);
        BinaryPrimitives.WriteInt16BigEndian(table.AsSpan(22), adjustment);
        return table;
    }

    /// <summary>
    /// Creates a cmap format 12 mapping A and V to glyphs one and two.
    /// </summary>
    /// <returns>The encoded cmap table.</returns>
    private static byte[] BuildCmap()
    {
        var cmap = new byte[52];
        WriteUInt16(cmap, 2, 1);
        WriteUInt16(cmap, 4, 0);
        WriteUInt16(cmap, 6, 4);
        WriteUInt32(cmap, 8, 12);
        WriteUInt16(cmap, 12, 12);
        WriteUInt32(cmap, 16, 40);
        WriteUInt32(cmap, 24, 2);
        WriteUInt32(cmap, 28, 0x41);
        WriteUInt32(cmap, 32, 0x41);
        WriteUInt32(cmap, 36, 1);
        WriteUInt32(cmap, 40, 0x56);
        WriteUInt32(cmap, 44, 0x56);
        WriteUInt32(cmap, 48, 2);
        return cmap;
    }

    /// <summary>
    /// Creates a minimal head table with one thousand units per em.
    /// </summary>
    /// <returns>The encoded head table.</returns>
    private static byte[] BuildHead()
    {
        var table = new byte[54];
        WriteUInt32(table, 0, 0x00010000);
        WriteUInt32(table, 12, 0x5F0F3CF5);
        WriteUInt16(table, 18, 1000);
        return table;
    }

    /// <summary>
    /// Creates the minimal horizontal header table needed by the reader.
    /// </summary>
    /// <returns>The encoded hhea table.</returns>
    private static byte[] BuildHhea()
    {
        var table = new byte[36];
        WriteUInt32(table, 0, 0x00010000);
        WriteUInt16(table, 34, 3);
        return table;
    }

    /// <summary>
    /// Creates a minimal maximum-profile table for the requested glyph count.
    /// </summary>
    /// <param name="glyphCount">The number of glyphs in the font.</param>
    /// <returns>The encoded maxp table.</returns>
    private static byte[] BuildMaxp(ushort glyphCount)
    {
        var table = new byte[6];
        WriteUInt32(table, 0, 0x00010000);
        WriteUInt16(table, 4, glyphCount);
        return table;
    }

    /// <summary>
    /// Creates an SFNT directory and appends the supplied table data.
    /// </summary>
    /// <param name="tables">The four-character table tags and data.</param>
    /// <returns>The complete SFNT font data.</returns>
    private static byte[] BuildFont(params (string Tag, byte[] Data)[] tables)
    {
        const int directoryStart = 12;
        const int recordSize = 16;
        var dataStart = directoryStart + tables.Length * recordSize;
        var font = new byte[dataStart + tables.Sum(table => table.Data.Length)];
        WriteUInt32(font, 0, 0x00010000);
        WriteUInt16(font, 4, checked((ushort)tables.Length));
        var tableDataOffset = dataStart;
        for (var index = 0; index < tables.Length; index++)
        {
            var table = tables[index];
            var recordOffset = directoryStart + index * recordSize;
            Encoding.ASCII.GetBytes(table.Tag, font.AsSpan(recordOffset, 4));
            WriteUInt32(font, recordOffset + 8, checked((uint)tableDataOffset));
            WriteUInt32(font, recordOffset + 12, checked((uint)table.Data.Length));
            table.Data.CopyTo(font, tableDataOffset);
            tableDataOffset += table.Data.Length;
        }

        return font;
    }

    /// <summary>
    /// Writes an unsigned 16-bit big-endian value into a buffer.
    /// </summary>
    /// <param name="buffer">The destination byte buffer.</param>
    /// <param name="offset">The destination offset.</param>
    /// <param name="value">The value to encode.</param>
    private static void WriteUInt16(byte[] buffer, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset), value);

    /// <summary>
    /// Writes an unsigned 32-bit big-endian value into a buffer.
    /// </summary>
    /// <param name="buffer">The destination byte buffer.</param>
    /// <param name="offset">The destination offset.</param>
    /// <param name="value">The value to encode.</param>
    private static void WriteUInt32(byte[] buffer, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset), value);
}
