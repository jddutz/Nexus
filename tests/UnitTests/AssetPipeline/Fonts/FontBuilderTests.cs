using System.Buffers.Binary;
using System.Text;
using Nexus.Assets.Fonts;

namespace Nexus.AssetPipeline.Tests;

/// <summary>
/// Verifies that the managed typography stages produce a complete runtime font result.
/// </summary>
public sealed class FontBuilderTests
{
    /// <summary>
    /// Builds selected glyphs and checks metrics, geometry, atlas bounds, and RGB8 data.
    /// </summary>
    [Fact]
    public void Build_assemblesManagedFontBuildResult()
    {
        var fontPath = FindTestFont();
        if (fontPath is null)
            return;

        var result = new FontBuilder().Build(
            fontPath,
            ['A', 'V', ' '],
            new FontGenerationSettings
            {
                EmSize = 32,
                DistanceRange = 4,
                Padding = 2,
            }
        );

        Assert.Equal(32, result.Metrics.EmSize);
        Assert.True(result.Metrics.Ascender > 0);
        Assert.True(result.Metrics.Descender < 0);
        Assert.True(result.Metrics.LineHeight > 0);
        Assert.Equal(new MsdfMetadata(4, 32), result.Msdf);
        Assert.Equal(['A', 'V', ' '], result.Glyphs.Select(glyph => glyph.Codepoint));
        Assert.All(result.Glyphs, glyph => Assert.True(glyph.Advance > 0));

        var glyphA = Assert.Single(result.Glyphs, glyph => glyph.Codepoint == 'A');
        Assert.True(glyphA.PlaneBounds.Right > glyphA.PlaneBounds.Left);
        Assert.True(glyphA.PlaneBounds.Top > glyphA.PlaneBounds.Bottom);
        Assert.True(glyphA.AtlasBounds.Right > glyphA.AtlasBounds.Left);
        Assert.True(glyphA.AtlasBounds.Top > glyphA.AtlasBounds.Bottom);
        Assert.All(
            result.Glyphs.Where(glyph => glyph.Codepoint != ' '),
            glyph =>
            {
                Assert.InRange(glyph.AtlasBounds.Left, 0, result.Atlas.Width);
                Assert.InRange(glyph.AtlasBounds.Right, 0, result.Atlas.Width);
                Assert.InRange(glyph.AtlasBounds.Bottom, 0, result.Atlas.Height);
                Assert.InRange(glyph.AtlasBounds.Top, 0, result.Atlas.Height);
            }
        );
        Assert.Equal(result.Atlas.Width * result.Atlas.Height * 3, result.Atlas.Pixels.Length);
        Assert.Contains(result.Atlas.Pixels, value => value != 0);
    }

    /// <summary>
    /// Verifies disposable bitmap padding is removed so plane and atlas bounds have equal extents.
    /// </summary>
    [Fact]
    public void Build_preservesPlaneBoundsExtentAfterRemovingBitmapPadding()
    {
        var fontPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ttf");
        File.WriteAllBytes(fontPath, CreateSyntheticRectangularFont());

        try
        {
            var result = new FontBuilder().Build(
                fontPath,
                ['A'],
                new FontGenerationSettings
                {
                    EmSize = 100,
                    DistanceRange = 3.5,
                    Padding = 2,
                }
            );

            var glyph = Assert.Single(result.Glyphs);
            Assert.Equal(6.5, glyph.PlaneBounds.Left);
            Assert.Equal(16.5, glyph.PlaneBounds.Bottom);
            Assert.Equal(53.5, glyph.PlaneBounds.Right);
            Assert.Equal(73.5, glyph.PlaneBounds.Top);
            Assert.Equal(
                glyph.PlaneBounds.Right - glyph.PlaneBounds.Left,
                glyph.AtlasBounds.Right - glyph.AtlasBounds.Left
            );
            Assert.Equal(
                glyph.PlaneBounds.Top - glyph.PlaneBounds.Bottom,
                glyph.AtlasBounds.Top - glyph.AtlasBounds.Bottom
            );
        }
        finally
        {
            File.Delete(fontPath);
        }
    }

    /// <summary>
    /// Finds an explicitly supplied or repository-local font for integration testing.
    /// </summary>
    /// <returns>The font path, or null when local font assets are unavailable.</returns>
    private static string? FindTestFont()
    {
        var configuredPath = Environment.GetEnvironmentVariable("NAP_TEST_FONT_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return configuredPath;

        for (
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent
        )
        {
            var fontDirectory = Path.Combine(directory.FullName, ".assets", "Fonts");
            if (Directory.Exists(fontDirectory))
                return Directory
                    .GetFiles(fontDirectory, "*.ttf")
                    .Order(StringComparer.Ordinal)
                    .FirstOrDefault();
        }

        return null;
    }

    /// <summary>
    /// Creates a minimal TrueType font whose A glyph is a known rectangular outline.
    /// </summary>
    /// <returns>The complete SFNT font bytes.</returns>
    private static byte[] CreateSyntheticRectangularFont()
    {
        var head = new byte[54];
        BinaryPrimitives.WriteUInt32BigEndian(head, 0x00010000);
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(12), 0x5F0F3CF5);
        BinaryPrimitives.WriteUInt16BigEndian(head.AsSpan(18), 1000);
        BinaryPrimitives.WriteInt16BigEndian(head.AsSpan(50), 1);

        var maxp = new byte[6];
        BinaryPrimitives.WriteUInt32BigEndian(maxp, 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(maxp.AsSpan(4), 2);

        var hhea = new byte[36];
        BinaryPrimitives.WriteUInt32BigEndian(hhea, 0x00010000);
        BinaryPrimitives.WriteInt16BigEndian(hhea.AsSpan(4), 800);
        BinaryPrimitives.WriteInt16BigEndian(hhea.AsSpan(6), -200);
        BinaryPrimitives.WriteUInt16BigEndian(hhea.AsSpan(34), 2);

        var hmtx = new byte[8];
        BinaryPrimitives.WriteUInt16BigEndian(hmtx, 600);
        BinaryPrimitives.WriteUInt16BigEndian(hmtx.AsSpan(4), 600);

        var glyph = new byte[34];
        BinaryPrimitives.WriteInt16BigEndian(glyph, 1);
        BinaryPrimitives.WriteInt16BigEndian(glyph.AsSpan(2), 100);
        BinaryPrimitives.WriteInt16BigEndian(glyph.AsSpan(4), 200);
        BinaryPrimitives.WriteInt16BigEndian(glyph.AsSpan(6), 500);
        BinaryPrimitives.WriteInt16BigEndian(glyph.AsSpan(8), 700);
        BinaryPrimitives.WriteUInt16BigEndian(glyph.AsSpan(10), 3);
        for (var index = 0; index < 4; index++)
            glyph[14 + index] = 1;
        short[] xDeltas = [100, 400, 0, -400];
        short[] yDeltas = [200, 0, 500, 0];
        for (var index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt16BigEndian(glyph.AsSpan(18 + index * 2), xDeltas[index]);
            BinaryPrimitives.WriteInt16BigEndian(glyph.AsSpan(26 + index * 2), yDeltas[index]);
        }

        var glyf = new byte[glyph.Length];
        glyph.CopyTo(glyf, 0);
        var loca = new byte[12];
        BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(8), checked((uint)glyf.Length));

        var format4 = new byte[32];
        BinaryPrimitives.WriteUInt16BigEndian(format4, 4);
        BinaryPrimitives.WriteUInt16BigEndian(format4.AsSpan(2), checked((ushort)format4.Length));
        BinaryPrimitives.WriteUInt16BigEndian(format4.AsSpan(6), 4);
        BinaryPrimitives.WriteUInt16BigEndian(format4.AsSpan(8), 4);
        BinaryPrimitives.WriteUInt16BigEndian(format4.AsSpan(10), 1);
        BinaryPrimitives.WriteUInt16BigEndian(format4.AsSpan(14), 'A');
        BinaryPrimitives.WriteUInt16BigEndian(format4.AsSpan(16), ushort.MaxValue);
        BinaryPrimitives.WriteUInt16BigEndian(format4.AsSpan(20), 'A');
        BinaryPrimitives.WriteUInt16BigEndian(format4.AsSpan(22), ushort.MaxValue);
        BinaryPrimitives.WriteInt16BigEndian(format4.AsSpan(24), unchecked((short)(1 - 'A')));
        BinaryPrimitives.WriteInt16BigEndian(format4.AsSpan(26), 1);
        var cmap = new byte[44];
        BinaryPrimitives.WriteUInt16BigEndian(cmap.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16BigEndian(cmap.AsSpan(4), 3);
        BinaryPrimitives.WriteUInt16BigEndian(cmap.AsSpan(6), 1);
        BinaryPrimitives.WriteUInt32BigEndian(cmap.AsSpan(8), 12);
        format4.CopyTo(cmap, 12);

        return CreateSfnt(
            ("cmap", cmap),
            ("glyf", glyf),
            ("head", head),
            ("hhea", hhea),
            ("hmtx", hmtx),
            ("loca", loca),
            ("maxp", maxp)
        );
    }

    /// <summary>
    /// Creates an SFNT directory and appends the supplied table data.
    /// </summary>
    /// <param name="tables">The table tags and contents in directory order.</param>
    /// <returns>The complete SFNT font bytes.</returns>
    private static byte[] CreateSfnt(params (string Tag, byte[] Data)[] tables)
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
            BinaryPrimitives.WriteUInt32BigEndian(
                font.AsSpan(recordOffset + 8),
                checked((uint)tablePosition)
            );
            BinaryPrimitives.WriteUInt32BigEndian(
                font.AsSpan(recordOffset + 12),
                checked((uint)table.Data.Length)
            );
            table.Data.CopyTo(font, tablePosition);
            tablePosition += table.Data.Length;
        }

        return font;
    }
}
