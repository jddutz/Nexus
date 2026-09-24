using System.Buffers.Binary;
using System.Text;
using Nexus.AssetPipeline.Typography.FontReader.TrueType;

namespace Nexus.AssetPipeline.Tests;

/// <summary>
/// Tests bounded TrueType reads and SFNT table-directory parsing.
/// </summary>
public sealed class TrueTypeFontReaderTests
{
    /// <summary>
    /// Verifies numeric reads use big-endian interpretation and advance the cursor.
    /// </summary>
    [Fact]
    public void TrueTypeReader_readsBigEndianValues()
    {
        var reader = new TrueTypeReader(
            new byte[]
            {
                0xAB,
                0x12,
                0x34,
                0xFF,
                0xFE,
                0x01,
                0x02,
                0x03,
                0x04,
                0xFF,
                0xFF,
                0xFF,
                0xFF,
            }
        );

        Assert.Equal((byte)0xAB, reader.ReadUInt8());
        Assert.Equal((ushort)0x1234, reader.ReadUInt16());
        Assert.Equal((short)-2, reader.ReadInt16());
        Assert.Equal(0x01020304u, reader.ReadUInt32());
        Assert.Equal(-1, reader.ReadInt32());
        Assert.Equal(13, reader.Position);
    }

    /// <summary>
    /// Verifies slices and cursor reads reject ranges beyond the input buffer.
    /// </summary>
    [Fact]
    public void TrueTypeReader_rejectsOutOfBoundsRanges()
    {
        var reader = new TrueTypeReader(new byte[] { 1, 2, 3 });

        Assert.Equal(new byte[] { 2, 3 }, reader.Slice(1, 2).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Slice(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Slice(0, int.MaxValue));
        Assert.Equal((byte)1, reader.ReadUInt8());
        Assert.Equal((ushort)0x0203, reader.ReadUInt16());
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadUInt8());
    }

    /// <summary>
    /// Verifies the SFNT directory preserves each table's tag, checksum, offset, and length.
    /// </summary>
    [Fact]
    public void TrueTypeFontReader_parsesTableDirectory()
    {
        var font = new TrueTypeFontReader(
            CreateFont(
                ("head", 0x11223344, 60u, 1u),
                ("cmap", 0x55667788, 61u, 1u),
                ("glyf", 0x99AABBCC, 62u, 1u)
            )
        );

        Assert.Equal(
            new[] { "head", "cmap", "glyf" },
            font.TableDirectory.Tables.Select(table => table.Tag)
        );
        Assert.True(font.TableDirectory.TryGetTable("head", out var head));
        Assert.Equal(0x11223344u, head.Checksum);
        Assert.Equal(60u, head.Offset);
        Assert.Equal(1u, head.Length);
        Assert.Equal(new byte[] { 60 }, font.GetTable("head").ToArray());
    }

    /// <summary>
    /// Verifies invalid signatures and table ranges are rejected.
    /// </summary>
    [Fact]
    public void TrueTypeFontReader_rejectsInvalidDirectoryData()
    {
        var invalidSignature = CreateFont(("head", 0u, 28u, 1u));
        invalidSignature[0] = 0xFF;

        Assert.Throws<InvalidDataException>(() => new TrueTypeFontReader(invalidSignature));
        Assert.Throws<InvalidDataException>(() =>
            new TrueTypeFontReader(CreateFont(("head", 0u, uint.MaxValue, 1u)))
        );
    }

    /// <summary>
    /// Verifies Unicode format 4 resolves BMP characters and missing characters to glyph zero.
    /// </summary>
    [Fact]
    public void TrueTypeFontReader_resolvesFormat4GlyphIndices()
    {
        var font = CreateFontWithTables(
            ("cmap", CreateCmap((3, 1, CreateFormat4Subtable()))),
            ("maxp", CreateMaxpTable(40))
        );

        var reader = new TrueTypeFontReader(font);

        Assert.Equal((ushort)37, reader.GetGlyphIndex('A'));
        Assert.Equal((ushort)38, reader.GetGlyphIndex('B'));
        Assert.Equal((ushort)4, reader.GetGlyphIndex(' '));
        Assert.Equal((ushort)0, reader.GetGlyphIndex('Z'));
        Assert.Equal((ushort)0, reader.GetGlyphIndex(-1));
        Assert.Equal((ushort)0, reader.GetGlyphIndex(0x110000));
    }

    /// <summary>
    /// Verifies Unicode format 12 resolves supplementary codepoints and takes precedence.
    /// </summary>
    [Fact]
    public void TrueTypeFontReader_resolvesFormat12GlyphIndices()
    {
        var format4 = CreateFormat4Subtable();
        var format12 = CreateFormat12Subtable(
            (0x20u, 0x20u, 4u),
            (0x41u, 0x41u, 37u),
            (0x42u, 0x42u, 38u),
            (0x1F600u, 0x1F600u, 7u)
        );
        var cmap = CreateCmap(
            (0, 4, format12),
            (3, 1, format4)
        );
        var font = new TrueTypeFontReader(
            CreateFontWithTables(("cmap", cmap), ("maxp", CreateMaxpTable(40)))
        );

        Assert.Equal((ushort)37, font.GetGlyphIndex('A'));
        Assert.Equal((ushort)7, font.GetGlyphIndex(0x1F600));
        Assert.Equal((ushort)0, font.GetGlyphIndex(0x1F601));
    }

    /// <summary>
    /// Verifies a locally supplied TTF contains the expected core tables when available.
    /// </summary>
    [Fact]
    public void Open_readsTablesFromLocalTrueTypeFontWhenAvailable()
    {
        var fontPath = FindLocalFont("Roboto-Regular.ttf");
        if (fontPath is null)
            return;

        var font = TrueTypeFontReader.Open(fontPath);
        Assert.Contains(font.TableDirectory.Tables, table => table.Tag == "head");
        Assert.Contains(font.TableDirectory.Tables, table => table.Tag == "cmap");
        Assert.Contains(font.TableDirectory.Tables, table => table.Tag == "glyf");
        Assert.Equal((ushort)2048, font.FontFace.UnitsPerEm);
        Assert.Equal((short)1900, font.FontFace.Ascender);
        Assert.Equal((short)-500, font.FontFace.Descender);
        Assert.Equal((short)0, font.FontFace.LineGap);
        Assert.Equal((ushort)1321, font.FontFace.GlyphCount);
        Assert.Equal((ushort)1321, font.FontFace.NumberOfHorizontalMetrics);
        Assert.Equal((short)0, font.FontFace.IndexToLocFormat);
        Assert.Equal((ushort)37, font.GetGlyphIndex('A'));
        Assert.Equal((ushort)38, font.GetGlyphIndex('B'));
        Assert.Equal((ushort)4, font.GetGlyphIndex(' '));
        Assert.Equal((ushort)0, font.GetGlyphIndex(0x10FFFF));
    }

    /// <summary>
    /// Verifies glyph resolution against a local font with Unicode format 12 subtables when available.
    /// </summary>
    [Fact]
    public void Open_resolvesGlyphsFromLocalFormat12FontWhenAvailable()
    {
        var fontPath = FindLocalFont("HomeVideo-BLG6G.ttf");
        if (fontPath is null)
            return;

        var font = TrueTypeFontReader.Open(fontPath);

        Assert.Equal((ushort)28, font.GetGlyphIndex('A'));
        Assert.Equal((ushort)29, font.GetGlyphIndex('B'));
        Assert.Equal((ushort)1, font.GetGlyphIndex(' '));
        Assert.Equal((ushort)0, font.GetGlyphIndex(0x10FFFF));
    }

    /// <summary>
    /// Verifies locally supplied OTF directories are parseable when available.
    /// </summary>
    [Fact]
    public void Open_readsTablesFromLocalOpenTypeFontsWhenAvailable()
    {
        var fontDirectory = FindLocalFontDirectory();
        if (fontDirectory is null)
            return;

        var fontPaths = Directory.GetFiles(fontDirectory, "*.otf");
        foreach (var fontPath in fontPaths)
        {
            var font = TrueTypeFontReader.Open(fontPath);
            Assert.Contains(font.TableDirectory.Tables, table => table.Tag == "head");
            Assert.Contains(font.TableDirectory.Tables, table => table.Tag == "cmap");
        }
    }

    /// <summary>
    /// Creates a minimal SFNT byte buffer containing the requested table records.
    /// </summary>
    /// <param name="records">The records to include in the table directory.</param>
    /// <returns>A font buffer with one data byte for each table.</returns>
    private static byte[] CreateFont(
        params (string Tag, uint Checksum, uint Offset, uint Length)[] records
    )
    {
        const int headerLength = 12;
        const int recordLength = 16;
        var dataOffset = headerLength + records.Length * recordLength;
        var font = new byte[dataOffset + records.Length];
        BinaryPrimitives.WriteUInt32BigEndian(font, 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(4), checked((ushort)records.Length));

        for (var index = 0; index < records.Length; index++)
        {
            var record = records[index];
            var recordOffset = headerLength + index * recordLength;
            Encoding.ASCII.GetBytes(record.Tag, font.AsSpan(recordOffset, 4));
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(recordOffset + 4), record.Checksum);
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(recordOffset + 8), record.Offset);
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(recordOffset + 12), record.Length);

            var tableDataIndex = dataOffset + index;
            if (record.Offset == tableDataIndex && record.Length > 0)
                font[tableDataIndex] = (byte)tableDataIndex;
        }

        return font;
    }

    /// <summary>
    /// Creates an SFNT containing the supplied table bytes.
    /// </summary>
    /// <param name="tables">The table tags and contents to include.</param>
    /// <returns>A complete SFNT byte buffer.</returns>
    private static byte[] CreateFontWithTables(params (string Tag, byte[] Data)[] tables)
    {
        const int headerLength = 12;
        const int recordLength = 16;
        var dataOffset = headerLength + tables.Length * recordLength;
        var font = new byte[dataOffset + tables.Sum(table => table.Data.Length)];
        BinaryPrimitives.WriteUInt32BigEndian(font, 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(4), checked((ushort)tables.Length));

        var tablePosition = dataOffset;
        for (var index = 0; index < tables.Length; index++)
        {
            var table = tables[index];
            var recordOffset = headerLength + index * recordLength;
            Encoding.ASCII.GetBytes(table.Tag, font.AsSpan(recordOffset, 4));
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(recordOffset + 8), checked((uint)tablePosition));
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(recordOffset + 12), checked((uint)table.Data.Length));
            table.Data.CopyTo(font, tablePosition);
            tablePosition += table.Data.Length;
        }

        return font;
    }

    /// <summary>
    /// Creates a cmap header and its encoding records.
    /// </summary>
    /// <param name="subtables">The platform, encoding, and subtable tuples.</param>
    /// <returns>The cmap table bytes.</returns>
    private static byte[] CreateCmap(params (ushort Platform, ushort Encoding, byte[] Data)[] subtables)
    {
        var headerLength = 4 + subtables.Length * 8;
        var cmap = new byte[headerLength + subtables.Sum(subtable => subtable.Data.Length)];
        BinaryPrimitives.WriteUInt16BigEndian(cmap.AsSpan(2), checked((ushort)subtables.Length));
        var subtablePosition = headerLength;
        for (var index = 0; index < subtables.Length; index++)
        {
            var subtable = subtables[index];
            var recordOffset = 4 + index * 8;
            BinaryPrimitives.WriteUInt16BigEndian(cmap.AsSpan(recordOffset), subtable.Platform);
            BinaryPrimitives.WriteUInt16BigEndian(cmap.AsSpan(recordOffset + 2), subtable.Encoding);
            BinaryPrimitives.WriteUInt32BigEndian(cmap.AsSpan(recordOffset + 4), checked((uint)subtablePosition));
            subtable.Data.CopyTo(cmap, subtablePosition);
            subtablePosition += subtable.Data.Length;
        }

        return cmap;
    }

    /// <summary>
    /// Creates a format 4 map for space, A, B, and the required terminal segment.
    /// </summary>
    /// <returns>The format 4 subtable bytes.</returns>
    private static byte[] CreateFormat4Subtable()
    {
        ushort[] codepoints = [0x20, 0x41, 0x42, 0xFFFF];
        ushort[] glyphs = [4, 37, 38, 0];
        var subtable = new byte[16 + codepoints.Length * 8];
        BinaryPrimitives.WriteUInt16BigEndian(subtable, 4);
        BinaryPrimitives.WriteUInt16BigEndian(subtable.AsSpan(2), checked((ushort)subtable.Length));
        BinaryPrimitives.WriteUInt16BigEndian(subtable.AsSpan(6), checked((ushort)(codepoints.Length * 2)));
        var position = 14;
        foreach (var codepoint in codepoints)
        {
            BinaryPrimitives.WriteUInt16BigEndian(subtable.AsSpan(position), codepoint);
            position += 2;
        }

        position += 2;
        foreach (var codepoint in codepoints)
        {
            BinaryPrimitives.WriteUInt16BigEndian(subtable.AsSpan(position), codepoint);
            position += 2;
        }

        for (var index = 0; index < codepoints.Length; index++)
        {
            var delta = unchecked((short)(glyphs[index] - codepoints[index]));
            BinaryPrimitives.WriteInt16BigEndian(subtable.AsSpan(position), delta);
            position += 2;
        }

        return subtable;
    }

    /// <summary>
    /// Creates a format 12 map from sequential codepoint/glyph groups.
    /// </summary>
    /// <param name="groups">The start codepoint, end codepoint, and start glyph tuples.</param>
    /// <returns>The format 12 subtable bytes.</returns>
    private static byte[] CreateFormat12Subtable(params (uint Start, uint End, uint Glyph)[] groups)
    {
        var subtable = new byte[16 + groups.Length * 12];
        BinaryPrimitives.WriteUInt16BigEndian(subtable, 12);
        BinaryPrimitives.WriteUInt32BigEndian(subtable.AsSpan(4), checked((uint)subtable.Length));
        BinaryPrimitives.WriteUInt32BigEndian(subtable.AsSpan(12), checked((uint)groups.Length));
        for (var index = 0; index < groups.Length; index++)
        {
            var group = groups[index];
            var position = 16 + index * 12;
            BinaryPrimitives.WriteUInt32BigEndian(subtable.AsSpan(position), group.Start);
            BinaryPrimitives.WriteUInt32BigEndian(subtable.AsSpan(position + 4), group.End);
            BinaryPrimitives.WriteUInt32BigEndian(subtable.AsSpan(position + 8), group.Glyph);
        }

        return subtable;
    }

    /// <summary>
    /// Creates a minimal maximum-profile table with the requested glyph count.
    /// </summary>
    /// <param name="glyphCount">The number of glyphs in the test font.</param>
    /// <returns>The maxp table bytes.</returns>
    private static byte[] CreateMaxpTable(ushort glyphCount)
    {
        var table = new byte[6];
        BinaryPrimitives.WriteUInt32BigEndian(table, 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(4), glyphCount);
        return table;
    }

    /// <summary>
    /// Finds a local font asset by walking upward from the test output directory.
    /// </summary>
    /// <param name="fileName">The font file to find in the local asset folder.</param>
    /// <returns>The full font path, or null when local font assets are unavailable.</returns>
    private static string? FindLocalFont(string fileName)
    {
        var fontDirectory = FindLocalFontDirectory();
        var path = fontDirectory is null ? null : Path.Combine(fontDirectory, fileName);
        return path is not null && File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Finds the repository's ignored local font-assets folder, if present.
    /// </summary>
    /// <returns>The font-assets directory, or null when it is unavailable.</returns>
    private static string? FindLocalFontDirectory()
    {
        for (
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent
        )
        {
            var candidate = Path.Combine(directory.FullName, ".assets", "Fonts");
            if (Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
