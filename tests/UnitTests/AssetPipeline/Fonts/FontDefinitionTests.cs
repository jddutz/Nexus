using Nexus.AssetPipeline;
using Nexus.Assets.Fonts;
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
            """;
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var pipeline = deserializer.Deserialize<PipelineDefinition>(yaml);
        var asset = Assert.Single(pipeline.Assets);

        Assert.Equal("ui.default", asset.ContentId);
        Assert.Equal("Fonts/Regular.ttf", asset.Source);
    }

    [Fact]
    public void Defaults_arePrintableAsciiAndConservativeMsdfSettings()
    {
        var repertoire = new FontGlyphRepertoire();

        Assert.Equal(95, repertoire.GetCodepoints().Length);
        Assert.Equal(0x20, repertoire.GetCodepoints().First());
        Assert.Equal(0x7e, repertoire.GetCodepoints().Last());
        Assert.Equal(48, new FontGenerationSettings().EmSize);
        Assert.Equal(4, new FontGenerationSettings().DistanceRange);
        Assert.Equal(2, new FontGenerationSettings().Padding);
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
