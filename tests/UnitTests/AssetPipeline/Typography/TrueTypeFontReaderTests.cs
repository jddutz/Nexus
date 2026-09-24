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
            new byte[] { 0xAB, 0x12, 0x34, 0xFF, 0xFE, 0x01, 0x02, 0x03, 0x04, 0xFF, 0xFF, 0xFF, 0xFF }
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
        var font = new TrueTypeFontReader(CreateFont(
            ("head", 0x11223344, 60u, 1u),
            ("cmap", 0x55667788, 61u, 1u),
            ("glyf", 0x99AABBCC, 62u, 1u)
        ));

        Assert.Equal(new[] { "head", "cmap", "glyf" }, font.TableDirectory.Tables.Select(table => table.Tag));
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
        Assert.Throws<InvalidDataException>(() => new TrueTypeFontReader(
            CreateFont(("head", 0u, uint.MaxValue, 1u))
        ));
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
    private static byte[] CreateFont(params (string Tag, uint Checksum, uint Offset, uint Length)[] records)
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
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, ".assets", "Fonts");
            if (Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }
}