using Nexus.AssetPipeline.Typography.Atlas;
using Nexus.AssetPipeline.Typography.DistanceFields;
using Nexus.Graphics.Text;

namespace Nexus.AssetPipeline.Tests;

/// <summary>
/// Verifies deterministic glyph packing and RGB8 pixel placement.
/// </summary>
public sealed class FontAtlasBuilderTests
{
    /// <summary>
    /// Packs a handful of rectangles and checks their positions and copied pixels.
    /// </summary>
    [Fact]
    public void Build_packsSyntheticRectanglesInInputOrder()
    {
        GlyphBitmap[] glyphs =
        [
            CreateBitmap(2, 2, 10),
            CreateBitmap(3, 1, 20),
            CreateBitmap(1, 2, 30),
        ];

        var result = new FontAtlasBuilder().Build(glyphs);

        Assert.Equal((3, 5), (result.Atlas.Width, result.Atlas.Height));
        Assert.Equal(
            new[]
            {
                new FontBounds(0, 3, 2, 5),
                new FontBounds(0, 2, 3, 3),
                new FontBounds(0, 0, 1, 2),
            },
            result.AtlasBounds
        );
        Assert.Equal(45, result.Atlas.Pixels.Length);
        Assert.Equal(new byte[] { 10, 10, 10 }, GetPixel(result.Atlas, 0, 0));
        Assert.Equal(new byte[] { 20, 20, 20 }, GetPixel(result.Atlas, 0, 2));
        Assert.Equal(new byte[] { 30, 30, 30 }, GetPixel(result.Atlas, 0, 3));
    }

    /// <summary>
    /// Packs the 95 printable ASCII glyph slots deterministically and keeps every bound in range.
    /// </summary>
    [Fact]
    public void Build_packsPrintableAsciiGlyphSetDeterministically()
    {
        var glyphs = Enumerable
            .Range(0, 95)
            .Select(index => CreateBitmap(1 + index % 7, 2 + index % 5, (byte)(index + 1)))
            .ToArray();
        var builder = new FontAtlasBuilder();

        var result = builder.Build(glyphs);
        var repeated = builder.Build(glyphs);

        Assert.Equal(95, result.AtlasBounds.Count);
        Assert.Equal(result.Atlas.Width, repeated.Atlas.Width);
        Assert.Equal(result.Atlas.Height, repeated.Atlas.Height);
        Assert.Equal(result.AtlasBounds, repeated.AtlasBounds);
        Assert.Equal(result.Atlas.Pixels, repeated.Atlas.Pixels);
        Assert.All(
            result.AtlasBounds,
            bounds =>
            {
                Assert.InRange(bounds.Left, 0, result.Atlas.Width);
                Assert.InRange(bounds.Right, 0, result.Atlas.Width);
                Assert.InRange(bounds.Bottom, 0, result.Atlas.Height);
                Assert.InRange(bounds.Top, 0, result.Atlas.Height);
            }
        );
    }

    /// <summary>Creates an RGB8 bitmap filled with one channel value.</summary>
    /// <param name="width">The bitmap width.</param>
    /// <param name="height">The bitmap height.</param>
    /// <param name="value">The value used for every channel.</param>
    /// <returns>The filled bitmap.</returns>
    private static GlyphBitmap CreateBitmap(int width, int height, byte value)
    {
        var pixels = new byte[width * height * 3];
        Array.Fill(pixels, value);
        return new GlyphBitmap(width, height, pixels);
    }

    /// <summary>Gets the RGB value of one atlas pixel.</summary>
    /// <param name="atlas">The packed atlas.</param>
    /// <param name="x">The pixel's horizontal coordinate.</param>
    /// <param name="y">The pixel's row-major vertical coordinate.</param>
    /// <returns>The pixel's RGB channels.</returns>
    private static byte[] GetPixel(Fonts.FontAtlas atlas, int x, int y)
    {
        var offset = (y * atlas.Width + x) * 3;
        return atlas.Pixels[offset..(offset + 3)];
    }
}
