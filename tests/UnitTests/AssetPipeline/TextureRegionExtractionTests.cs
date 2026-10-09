using Microsoft.Extensions.Configuration;
using Nexus.Core;
using Nexus.Graphics.Textures;

namespace Nexus.AssetPipeline.Tests;

public sealed class TextureRegionExtractionTests
{
    private static byte[] Pixels(int width, int height, params (int X, int Y, byte A)[] pixels)
    {
        var data = new byte[width * height * 4];
        foreach (var p in pixels) data[(p.Y * width + p.X) * 4 + 3] = p.A;
        return data;
    }

    [Fact]
    public void DefaultsSuppressSpecklesAndFaintBridgesWhilePaddingAssets()
    {
        var pixels = new List<(int X, int Y, byte A)>();
        for (var y = 3; y < 11; y++)
        for (var x = 3; x < 11; x++)
        {
            pixels.Add((x, y, 255));
            pixels.Add((x + 14, y, 255));
        }
        for (var x = 11; x < 17; x++) pixels.Add((x, 6, 16));
        pixels.Add((29, 14, 255));
        var regions = TextureRegionExtractor.Extract(Pixels(32, 16, pixels.ToArray()), 32, 16, new());
        Assert.Equal(2, regions.Count);
        Assert.Equal(1, regions[0].Bounds.Origin.X);
        Assert.Equal(15, regions[1].Bounds.Origin.X);
        Assert.All(regions, region =>
        {
            Assert.Equal(1, region.Bounds.Origin.Y);
            Assert.Equal(12, region.Bounds.Size.X);
            Assert.Equal(12, region.Bounds.Size.Y);
        });
    }

    [Fact]
    public void ThresholdIsStrictAndAreaCountsOccupiedPixels()
    {
        var regions = TextureRegionExtractor.Extract(Pixels(8, 4,
            (0, 0, 100), (1, 1, 101), (2, 2, 255), (7, 0, 255)), 8, 4,
            new() { AlphaThreshold = 100, MinimumIslandArea = 2, Padding = 0 });
        var region = Assert.Single(regions);
        Assert.Equal("region-0000", region.Name);
        Assert.Equal(1, region.Bounds.Origin.X);
        Assert.Equal(1, region.Bounds.Origin.Y);
        Assert.Equal(2, region.Bounds.Size.X);
        Assert.Equal(2, region.Bounds.Size.Y);
        Assert.Equal(0.125f, region.TexCoords.Origin.X);
        Assert.Equal(0.5f, region.TexCoords.Size.Y);
    }

    [Fact]
    public void MergeIsTransitiveAndPaddingClampsToImage()
    {
        var regions = TextureRegionExtractor.Extract(Pixels(8, 2,
            (0, 0, 255), (2, 0, 255), (4, 0, 255)), 8, 2,
            new() { MinimumIslandArea = 1, MergeDistance = 1, Padding = 2 });
        var region = Assert.Single(regions);
        Assert.Equal(0, region.Bounds.Origin.X);
        Assert.Equal(7, region.Bounds.Size.X);
        Assert.Equal(2, region.Bounds.Size.Y);
    }

    [Fact]
    public void GroupsConsumeCandidatesAndNamedBoundsReplaceCandidate()
    {
        var regions = TextureRegionExtractor.Extract(Pixels(10, 2,
            (0, 0, 255), (3, 0, 255), (9, 0, 255)), 10, 2,
            new() { MinimumIslandArea = 1, Padding = 0, Groups = new() { ["panel"] = [0, 1] },
                NamedBounds = new() { ["region-0002"] = new() { X = 8, Y = 0, Width = 2, Height = 2 } } });
        Assert.Equal(2, regions.Count);
        Assert.Equal(4, regions.Single(r => r.Name == "panel").Bounds.Size.X);
        Assert.Equal(2, regions.Single(r => r.Name == "region-0002").Bounds.Size.Y);
    }

    [Fact]
    public void EmptyMaskAndInvalidSettingsAreHandled()
    {
        Assert.Empty(TextureRegionExtractor.Extract(new byte[16], 2, 2, new()));
        Assert.Throws<ArgumentOutOfRangeException>(() => TextureRegionExtractor.Extract(new byte[16], 2, 2, new() { AlphaThreshold = 256 }));
        Assert.Throws<ArgumentException>(() => TextureRegionExtractor.Extract(new byte[16], 2, 2, new() { Groups = new() { ["missing"] = [0] } }));
    }

    [Fact]
    public void PipelineExportsRegionsThatRuntimeCanRead()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"nap-regions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            // Uncompressed 32-bit TGA, top-left origin, two isolated pixels.
            byte[] header = [0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 0, 1, 0, 32, 40];
            File.WriteAllBytes(Path.Combine(folder, "atlas.tga"), [.. header, .. Pixels(4, 1, (0, 0, 255), (3, 0, 255))]);
            var input = Path.Combine(folder, "assets.yaml");
            File.WriteAllText(input, """
                assets:
                  - assetType: texture
                    files: [atlas.tga]
                    regions:
                      alphaThreshold: 0
                      minimumIslandArea: 1
                      groups:
                        panel: [0, 1]
                """);
            var output = Path.Combine(folder, "output");
            Assert.Equal(0, new Pipeline([input], output).Execute());
            var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(output, "content-manifest.json")).Build();
            var manifest = new ContentManifest(output, configuration);
            var region = manifest.GetTextureRegion((ContentId)"atlas", "panel");
            Assert.Equal(4, region.Bounds.Size.X);
            Assert.Equal(1f, region.TexCoords.Size.X);
            Assert.Equal("atlas.ktx2", manifest.Textures.GetContentFilePath((ContentId)"atlas"));
            File.WriteAllText(input, "assets:\n  - assetType: texture\n    files: [atlas.tga]\n");
            Assert.Equal(0, new Pipeline([input], output).Execute());
            using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "content-manifest.json")));
            Assert.False(json.RootElement.GetProperty("Textures").GetProperty("Content").GetProperty("atlas").TryGetProperty("Regions", out _));
        }
        finally { Directory.Delete(folder, recursive: true); }
    }
}
