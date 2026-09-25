using System.Numerics;
using Nexus.Assets.Typography.DistanceFields;
using Nexus.Assets.Typography.Geometry;

namespace Nexus.AssetPipeline.Tests;

/// <summary>
/// Verifies deterministic single-glyph multi-channel distance-field generation.
/// </summary>
public sealed class MsdfGeneratorTests
{
    /// <summary>
    /// Generates a synthetic capital A, checks its RGB channels, and writes a PPM proof image.
    /// </summary>
    [Fact]
    public void Generate_producesDeterministicRgbBitmapForCapitalA()
    {
        var contours = CreateCapitalA();
        var settings = new MsdfGenerationSettings
        {
            PixelsPerUnit = 12f,
            DistanceRange = 3f,
            Padding = 3,
        };
        var generator = new MsdfGenerator();
        var bitmap = generator.Generate(contours, settings);
        var repeated = generator.Generate(contours, settings);

        Assert.Equal(30, bitmap.Width);
        Assert.Equal(42, bitmap.Height);
        Assert.Equal(bitmap.Width * bitmap.Height * 3, bitmap.Pixels.Length);
        Assert.Equal(bitmap.Pixels, repeated.Pixels);
        Assert.Contains(bitmap.Pixels, channel => channel < 128);
        Assert.Contains(bitmap.Pixels, channel => channel > 128);
        Assert.Contains(
            Enumerable.Range(0, bitmap.Pixels.Length / 3),
            pixel =>
            {
                var offset = pixel * 3;
                return bitmap.Pixels[offset] != bitmap.Pixels[offset + 1]
                    || bitmap.Pixels[offset + 1] != bitmap.Pixels[offset + 2];
            }
        );
        Assert.All(GetPixelChannels(bitmap, 6, 33), channel => Assert.True(channel > 128));
        Assert.All(GetPixelChannels(bitmap, 15, 15), channel => Assert.True(channel < 128));

        var imagePath = Path.Combine(AppContext.BaseDirectory, "msdf-A.ppm");
        using var image = File.Create(imagePath);
        var header = System.Text.Encoding.ASCII.GetBytes(
            $"P6\n{bitmap.Width} {bitmap.Height}\n255\n"
        );
        image.Write(header);
        image.Write(bitmap.Pixels);
    }

    /// <summary>
    /// Uses true quadratic extrema for bitmap dimensions and pixel-center sampling coordinates.
    /// </summary>
    [Fact]
    public void Generate_usesTrueQuadraticBoundsForDimensionsAndPixelCoordinates()
    {
        var contour = new Contour([
            new LineSegment(Vector2.Zero, new Vector2(4f, 0f)),
            new QuadraticSegment(new Vector2(4f, 0f), new Vector2(2f, 2f), Vector2.Zero),
        ]);

        var bitmap = new MsdfGenerator().Generate(
            [contour],
            new MsdfGenerationSettings
            {
                PixelsPerUnit = 2f,
                DistanceRange = 2f,
                Padding = 1,
            }
        );

        Assert.Equal(10, bitmap.Width);
        Assert.Equal(4, bitmap.Height);
        Assert.Equal([96, 96, 96], GetPixelChannels(bitmap, 5, 3));
    }

    /// <summary>Gets the RGB channel values for one bitmap pixel.</summary>
    /// <param name="bitmap">The bitmap containing the pixel.</param>
    /// <param name="x">The pixel's horizontal coordinate.</param>
    /// <param name="y">The pixel's vertical coordinate.</param>
    /// <returns>The pixel's red, green, and blue values.</returns>
    private static byte[] GetPixelChannels(GlyphBitmap bitmap, int x, int y)
    {
        var offset = (y * bitmap.Width + x) * 3;
        return bitmap.Pixels[offset..(offset + 3)];
    }

    /// <summary>Creates polygonal outer and counter contours for a capital A.</summary>
    /// <returns>The capital A's outer contour and oppositely oriented counter.</returns>
    private static Contour[] CreateCapitalA()
    {
        Vector2[] outerPoints = [new(0f, 0f), new(0.7f, 3f), new(1.3f, 3f), new(2f, 0f)];
        Vector2[] counterPoints = [new(1f, 2.5f), new(0.75f, 1f), new(1.25f, 1f)];
        return [CreatePolygon(outerPoints), CreatePolygon(counterPoints)];
    }

    /// <summary>Creates a closed polygon contour in the supplied point order.</summary>
    /// <param name="points">The ordered contour vertices.</param>
    /// <returns>The polygon contour.</returns>
    private static Contour CreatePolygon(Vector2[] points) =>
        new(
            points.Select(
                (point, index) => new LineSegment(point, points[(index + 1) % points.Length])
            )
        );
}
