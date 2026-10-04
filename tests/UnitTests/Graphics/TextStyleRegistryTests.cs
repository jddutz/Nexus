using Microsoft.Extensions.Configuration;
using Nexus.Assets.Fonts;
using Nexus.Core;
using Nexus.Graphics;
using Nexus.Graphics.Text;

namespace Nexus.UnitTests.Graphics;

/// <summary>Verifies generated font atlases are converted to supported runtime texture formats.</summary>
public sealed class TextStyleRegistryTests
{
    /// <summary>Verifies RGB atlas channels are preserved and opaque alpha is added to RGBA8 data.</summary>
    [Fact]
    public void GetOrCreate_convertsRgbAtlasToRgbaTexture()
    {
        var font = new FontBuildResult(
            "test-font",
            new FontAtlas(2, 1, [17, 29, 43, 51, 61, 71]),
            new FontMetrics(16, 12, -4, 16),
            [],
            [],
            new MsdfMetadata(4, 16)
        );
        var manifest = new ContentManifest(
            string.Empty,
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Fonts:Content:test-font:FilePath"] = "test-font.ttf",
                    }
                )
                .Build()
        );
        using var registry = new TextStyleRegistry(manifest, new StubFontBuilder(font));

        var style = registry.GetOrCreate("test-font", 16);
        Span<byte> pixels = stackalloc byte[8];

        Assert.Equal(ColorFormatEnum.RGBA8UNorm, style.Texture.TextureFormat);
        style.Texture.WriteTo(0, 2, style.Texture.TextureFormat, pixels);
        Assert.Equal(new byte[] { 17, 29, 43, 255, 51, 61, 71, 255 }, pixels.ToArray());
    }

    /// <summary>Verifies exact text styles share a compatible generated raster.</summary>
    [Fact]
    public void GetOrCreate_caches_requested_styles_and_reuses_a_compatible_raster()
    {
        var font = new FontBuildResult(
            "test-font",
            new FontAtlas(1, 1, [17, 29, 43]),
            new FontMetrics(16, 12, -4, 16),
            [],
            [],
            new MsdfMetadata(4, 16)
        );
        var manifest = new ContentManifest(
            string.Empty,
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Fonts:Content:test-font:FilePath"] = "test-font.ttf",
                    }
                )
                .Build()
        );
        var builder = new StubFontBuilder(font);
        using var registry = new TextStyleRegistry(manifest, builder);

        var style16 = registry.GetOrCreate("test-font", 16);
        var sameStyle16 = registry.GetOrCreate("test-font", 16);
        var style18 = registry.GetOrCreate("test-font", 18);

        Assert.Same(style16, sameStyle16);
        Assert.NotSame(style16, style18);
        Assert.NotEqual(style16.Id, style18.Id);
        Assert.Equal(16, style16.Size);
        Assert.Equal(18, style18.Size);
        Assert.Same(style16.Texture, style18.Texture);
        Assert.Equal(1, builder.BuildCount);
        Assert.Same(style16, registry.Get(style16.Id));
        Assert.Same(style18, registry.Get(style18.Id));
    }

    /// <summary>Provides one fixed font result to the text-style registry test.</summary>
    private sealed class StubFontBuilder(FontBuildResult result) : IFontBuilder
    {
        public int BuildCount { get; private set; }

        /// <inheritdoc />
        public FontBuildResult Build(
            ContentId fontId,
            string sourcePath,
            IReadOnlyList<int> codepoints,
            FontGenerationSettings settings
        )
        {
            BuildCount++;
            return result with { FontId = fontId };
        }
    }
}
