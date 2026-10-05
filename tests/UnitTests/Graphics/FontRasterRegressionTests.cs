using System.Security.Cryptography;
using Nexus.Assets.Fonts;
using Nexus.Graphics.Text;
using Nexus.Graphics;
using Nexus.Core;
using Microsoft.Extensions.Options;

namespace Nexus.UnitTests.Graphics;

public sealed class FontRasterRegressionTests
{
    // Generated with the unpruned solver: protects every RGB byte of all 95 glyphs.
    [Theory]
    [InlineData(32, 285, 349, "9FA041E9EBC5C3D9DABE8CAEA9E08CDC9B105D09CE72EE3016B13ADD6884D620")]
    [InlineData(48, 367, 465, "B2E07D2B0018F97818ACE9A8D52318B4EEF84EF4B814D7E4BE7F8C4F40B6C3A1")]
    public void PrunedRasterizationPreservesReferenceAtlas(int em, int width, int height, string hash)
    {
        Assert.True(BuiltInFonts.TryGetRasterizerInput(BuiltInFonts.Regular, out var input));
        var result = new FontBuilder().Build(BuiltInFonts.Regular, input!, Enumerable.Range(32, 95).ToArray(),
            new FontGenerationSettings { EmSize = em, DistanceRange = 4, Padding = 2 });
        Assert.Equal(width, result.Atlas.Width);
        Assert.Equal(height, result.Atlas.Height);
        Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(result.Atlas.Pixels)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void WorkerCountPreservesAtlasMetadataAndRequestedOrder(int workers)
    {
        Assert.True(BuiltInFonts.TryGetRasterizerInput(BuiltInFonts.Regular, out var input));
        var codes = Enumerable.Range(32, 95).Reverse().ToArray();
        var builder = new FontBuilder();
        var reference = builder.Build(BuiltInFonts.Regular, input!, codes,
            new FontGenerationSettings { EmSize = 32, MaxDegreeOfParallelism = 1 });
        var actual = builder.Build(BuiltInFonts.Regular, input!, codes,
            new FontGenerationSettings { EmSize = 32, MaxDegreeOfParallelism = workers });
        Assert.Equal(reference.Atlas.Width, actual.Atlas.Width);
        Assert.Equal(reference.Atlas.Height, actual.Atlas.Height);
        Assert.Equal(reference.Atlas.Pixels, actual.Atlas.Pixels);
        Assert.Equal(reference.Glyphs, actual.Glyphs);
        Assert.Equal(codes, actual.Glyphs.Select(glyph => glyph.Codepoint));
        Assert.Equal(reference.Metrics, actual.Metrics);
        Assert.Equal(reference.Kerning, actual.Kerning);
        Assert.Equal(reference.Msdf, actual.Msdf);
    }

    [Fact]
    public void ParallelBatchTelemetryIncludesWorkerAllocations()
    {
        Assert.True(BuiltInFonts.TryGetRasterizerInput(BuiltInFonts.Regular, out var input));
        using var profiler = new GraphicsProfiler(Options.Create(new DiagnosticsSettings { EnablePerformanceMetrics = true }));
        new FontBuilder(profiler).Build(BuiltInFonts.Regular, input!, Enumerable.Range(32, 95).ToArray(),
            new FontGenerationSettings { EmSize = 32, MaxDegreeOfParallelism = 4 });
        var samples = profiler.GetSnapshot();
        var batch = Assert.Single(samples, sample => sample.Operation == "font.glyphs.build");
        var glyphs = Assert.Single(samples, sample => sample.Operation == "font.msdf.glyph");
        Assert.Equal(95, glyphs.Count);
        Assert.Equal(95, batch.Units);
        Assert.True(glyphs.AllocatedBytes > 0);
        Assert.True(batch.AllocatedBytes >= glyphs.AllocatedBytes);
        var outlines = Assert.Single(samples, sample => sample.Operation == "font.outline.glyph");
        var bounds = Assert.Single(samples, sample => sample.Operation == "font.bounds.glyph");
        Assert.Equal(95, outlines.Count);
        Assert.Equal(95, bounds.Count);
        Assert.True(batch.AllocatedBytes >= glyphs.AllocatedBytes + outlines.AllocatedBytes + bounds.AllocatedBytes);
    }

    [Fact]
    public void InvalidWorkerCountIsRejectedBeforeGeneration()
    {
        Assert.True(BuiltInFonts.TryGetRasterizerInput(BuiltInFonts.Regular, out var input));
        Assert.Throws<FontBuildException>(() => new FontBuilder().Build(BuiltInFonts.Regular, input!, [65],
            new FontGenerationSettings { MaxDegreeOfParallelism = -1 }));
    }
}
