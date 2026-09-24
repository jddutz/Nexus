using Nexus.AssetPipeline.Fonts;
using Nexus.Graphics.Text;

namespace Nexus.AssetPipeline.Tests;

public sealed class FontProcessorTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        $"nap-tests-{Guid.NewGuid():N}"
    );

    [Theory]
    [InlineData("font.woff")]
    [InlineData("font.png")]
    public void Process_rejectsUnsupportedExtensions(string fileName)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, fileName), "not a font");
        var processor = new FontProcessor(new FakeRasterizer());
        var definition = CreateDefinition(fileName);

        var exception = Assert.Throws<FontBuildException>(() =>
            processor.Process(definition, _folder)
        );

        Assert.Contains("ui.default", exception.Message);
        Assert.Contains(fileName, exception.Message);
        Assert.Contains(".ttf or .otf", exception.Message);
    }

    [Fact]
    public void Process_rejectsMissingSourceBeforeRasterization()
    {
        var rasterizer = new FakeRasterizer();
        var processor = new FontProcessor(rasterizer);

        var exception = Assert.Throws<FontBuildException>(() =>
            processor.Process(CreateDefinition("missing.ttf"), _folder)
        );

        Assert.Contains("ui.default", exception.Message);
        Assert.Contains("missing.ttf", exception.Message);
        Assert.False(rasterizer.WasCalled);
    }

    [Fact]
    public void Process_convertsRasterizerResultAndWrapsErrors()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(Path.Combine(_folder, "font.ttf"), [1]);
        var expected = CreateResult();
        var processor = new FontProcessor(new FakeRasterizer(expected));

        var actual = processor.Process(CreateDefinition("font.ttf"), _folder);

        Assert.Same(expected, actual);
        Assert.Equal(95, actual.Glyphs.Count);
    }

    [Fact]
    public void Process_addsAssetAndSourceContextToRasterizerErrors()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(Path.Combine(_folder, "broken.otf"), [1]);
        var processor = new FontProcessor(new ThrowingRasterizer());

        var exception = Assert.Throws<FontBuildException>(() =>
            processor.Process(CreateDefinition("broken.otf"), _folder)
        );

        Assert.Contains("ui.default", exception.Message);
        Assert.Contains("broken.otf", exception.Message);
        Assert.Contains("invalid font", exception.Message);
    }

    [Fact]
    public void OutputPath_usesOneLogicalFontPackage()
    {
        Assert.Equal(
            Path.Combine("Content", "fonts", "ui.default"),
            FontProcessor.GetOutputPath("Content", "ui.default")
        );
    }

    [Fact]
    public void AtlasWriter_writesOnlyAtlasArtifact()
    {
        var package = Path.Combine(_folder, "Content", "fonts", "ui.default");
        var atlasPath = Path.Combine(package, "atlas.rgb8");

        FontAtlasWriter.Write(atlasPath, CreateResult(1));

        Assert.Equal(
            new[] { "atlas.rgb8" },
            Directory.GetFiles(package).Select(Path.GetFileName).Order()
        );
        Assert.Equal(12, File.ReadAllBytes(atlasPath).Length);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, true);
    }

    private static FontDefinition CreateDefinition(string source) =>
        new("ui.default", source, new FontGlyphRepertoire(), new FontGenerationSettings());

    private static FontBuildResult CreateResult(int glyphCount = 95) =>
        new(
            new FontAtlas(2, 2, new byte[12]),
            new FontMetrics(48, .8, -.2, 1.2),
            Enumerable
                .Range(32, glyphCount)
                .Select(codepoint => new FontGlyph(
                    codepoint,
                    .5,
                    new(0, 0, .5, 1),
                    new(0, 0, 1, 2)
                ))
                .ToArray(),
            [],
            new MsdfMetadata(4, 48)
        );

    private sealed class FakeRasterizer(FontBuildResult? result = null) : IFontRasterizer
    {
        public bool WasCalled { get; private set; }

        public FontBuildResult Rasterize(
            string sourcePath,
            IReadOnlyList<int> codepoints,
            FontGenerationSettings settings
        )
        {
            WasCalled = true;
            return result ?? CreateResult(codepoints.Count);
        }
    }

    private sealed class ThrowingRasterizer : IFontRasterizer
    {
        public FontBuildResult Rasterize(
            string sourcePath,
            IReadOnlyList<int> codepoints,
            FontGenerationSettings settings
        ) => throw new FontBuildException("invalid font");
    }
}
