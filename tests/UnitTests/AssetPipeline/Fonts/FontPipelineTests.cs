using System.Text.Json;

namespace Nexus.AssetPipeline.Tests;

/// <summary>
/// Verifies that font assets are imported as source content.
/// </summary>
public sealed class FontPipelineTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        $"nap-pipeline-tests-{Guid.NewGuid():N}"
    );

    /// <summary>
    /// Copies an OpenType source file and writes only its content path to the manifest.
    /// </summary>
    [Fact]
    public void Execute_copiesFontContentWithoutRasterizationMetadata()
    {
        var sourceRoot = Path.Combine(_folder, "Assets");
        var sourceFolder = Path.Combine(sourceRoot, "Fonts");
        var outputFolder = Path.Combine(_folder, "Content");
        Directory.CreateDirectory(sourceFolder);
        var sourceBytes = new byte[] { 0, 1, 2, 3, 254, 255 };
        File.WriteAllBytes(Path.Combine(sourceFolder, "Regular.OTF"), sourceBytes);
        File.WriteAllText(
            Path.Combine(_folder, "pipeline.yaml"),
            """
            root: Assets
            assets:
              - assetType: font
                contentId: ui.default
                source: Fonts/Regular.OTF
            """
        );

        var pipeline = new Pipeline([Path.Combine(_folder, "pipeline.yaml")], outputFolder);

        Assert.Equal(0, pipeline.Execute());
        Assert.Equal(
            sourceBytes,
            File.ReadAllBytes(Path.Combine(outputFolder, "fonts", "ui.default.OTF"))
        );
        using var manifest = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(outputFolder, "content-manifest.json"))
        );
        var font = manifest
            .RootElement.GetProperty("Fonts")
            .GetProperty("Content")
            .GetProperty("ui.default");

        Assert.Equal("fonts/ui.default.OTF", font.GetProperty("FilePath").GetString());
        Assert.Single(font.EnumerateObject());
        Assert.False(Directory.Exists(Path.Combine(outputFolder, "fonts", "ui.default")));
    }

    /// <summary>
    /// Exports the MSDF atlas beside the copied font when requested.
    /// </summary>
    [Fact]
    public void Execute_exportsMsdfPngBesideFontWhenRequested()
    {
        var sourceRoot = Path.Combine(_folder, "Assets");
        var sourceFolder = Path.Combine(sourceRoot, "Fonts");
        var outputFolder = Path.Combine(_folder, "Content");
        Directory.CreateDirectory(sourceFolder);
        var sourcePath = Path.Combine(sourceFolder, "Regular.ttf");
        var fixturePath = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                ".assets",
                "Fonts",
                "Roboto-Regular.ttf"
            )
        );
        File.Copy(fixturePath, sourcePath);
        File.WriteAllText(
            Path.Combine(_folder, "pipeline.yaml"),
            """
            root: Assets
            assets:
              - assetType: font
                contentId: ui.default
                source: Fonts/Regular.ttf
                includeMsdf: true
            """
        );

        var pipeline = new Pipeline([Path.Combine(_folder, "pipeline.yaml")], outputFolder);

        Assert.Equal(0, pipeline.Execute());
        var fontPath = Path.Combine(outputFolder, "fonts", "ui.default.ttf");
        var atlasPath = Path.Combine(outputFolder, "fonts", "ui.default.png");
        Assert.True(File.Exists(fontPath));
        Assert.Equal(
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
            File.ReadAllBytes(atlasPath)[..8]
        );
    }

    /// <summary>
    /// Removes the temporary pipeline inputs and outputs.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, true);
    }
}
