using Nexus.AssetPipeline;
using Nexus.AssetPipeline.Fonts;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Nexus.AssetPipeline.Tests;

public sealed class FontDefinitionTests
{
    [Fact]
    public void YamlDefinition_parsesFontSettings()
    {
        const string yaml = """
            root: Assets
            assets:
              - assetType: font
                contentId: ui.default
                source: Fonts/Regular.ttf
                glyphs:
                  repertoire: ascii
                  characters: "©"
                generation:
                  emSize: 64
                  distanceRange: 6
                  padding: 3
            """;
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var pipeline = deserializer.Deserialize<PipelineDefinition>(yaml);
        var font = FontDefinition.FromAsset(Assert.Single(pipeline.Assets));

        Assert.Equal("ui.default", font.ContentId);
        Assert.Equal("Fonts/Regular.ttf", font.Source);
        Assert.Equal(64, font.Generation.EmSize);
        Assert.Equal(6, font.Generation.DistanceRange);
        Assert.Contains(0xA9, font.Glyphs.GetCodepoints());
    }

    [Fact]
    public void Defaults_arePrintableAsciiAndConservativeMsdfSettings()
    {
        var definition = FontDefinition.FromAsset(new AssetDefinition());

        Assert.Equal(95, definition.Glyphs.GetCodepoints().Length);
        Assert.Equal(0x20, definition.Glyphs.GetCodepoints().First());
        Assert.Equal(0x7e, definition.Glyphs.GetCodepoints().Last());
        Assert.Equal(48, definition.Generation.EmSize);
        Assert.Equal(4, definition.Generation.DistanceRange);
        Assert.Equal(2, definition.Generation.Padding);
    }

    [Fact]
    public void GlyphSelection_isSortedAndDeduplicated()
    {
        var repertoire = new FontGlyphRepertoire { Characters = "éAé" };

        var codepoints = repertoire.GetCodepoints();

        Assert.Equal(codepoints.Order().ToArray(), codepoints);
        Assert.Equal(codepoints.Distinct().Count(), codepoints.Length);
        Assert.Equal(1, codepoints.Count(value => value == 'A'));
        Assert.Equal(1, codepoints.Count(value => value == 'é'));
    }
}
