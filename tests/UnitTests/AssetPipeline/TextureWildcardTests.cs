using System.Text.Json;

namespace Nexus.AssetPipeline.Tests;

public sealed class TextureWildcardTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"nap-wildcards-{Guid.NewGuid():N}");

    [Fact]
    public void WildcardsCopyMatchingFilesDeduplicateAndPreserveRelativePaths()
    {
        var source = Path.Combine(_folder, "Portraits");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        File.WriteAllBytes(Path.Combine(source, "hero_a.png"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(source, "hero_b.png"), [4, 5]);
        File.WriteAllBytes(Path.Combine(source, "notes.txt"), [6]);
        File.WriteAllBytes(Path.Combine(source, "nested", "ignored.png"), [7]);
        var input = Path.Combine(_folder, "assets.yaml");
        File.WriteAllText(input, """
            assets:
              - assetType: texture
                files: ["Portraits/hero_?.png", "Portraits/*.png", "Portraits/hero_a.png"]
            """);
        var output = Path.Combine(_folder, "output");
        Assert.Equal(0, new Pipeline([input], output).Execute());
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "content-manifest.json")));
        var content = json.RootElement.GetProperty("Textures").GetProperty("Content");
        Assert.Equal(new[] { "hero_a", "hero_b" }, content.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Portraits/hero_a.png", content.GetProperty("hero_a").GetProperty("FilePath").GetString());
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(output, "Portraits", "hero_a.png")));
        Assert.Equal(new byte[] { 4, 5 }, File.ReadAllBytes(Path.Combine(output, "Portraits", "hero_b.png")));
        Assert.False(Directory.Exists(Path.Combine(output, "Portraits", "nested")));
    }

    [Theory]
    [InlineData("portraits", "characters", 0)]
    [InlineData("portraits", "portraits", 1)]
    public void GroupsNamespaceFullFilenamesAndRejectDuplicateIds(string firstGroup, string secondGroup, int expectedExitCode)
    {
        Directory.CreateDirectory(Path.Combine(_folder, "first"));
        Directory.CreateDirectory(Path.Combine(_folder, "second"));
        File.WriteAllBytes(Path.Combine(_folder, "first", "hero.png"), [1]);
        File.WriteAllBytes(Path.Combine(_folder, "second", "hero.png"), [2]);
        var input = Path.Combine(_folder, "assets.yaml");
        File.WriteAllText(input, $"""
            assets:
              - assetType: texture
                path: first
                groupName: {firstGroup}
                files: ["*.png"]
              - assetType: texture
                path: second
                groupName: {secondGroup}
                files: ["*.png"]
            """);
        var output = Path.Combine(_folder, "output");
        Assert.Equal(expectedExitCode, new Pipeline([input], output).Execute());
        if (expectedExitCode != 0) return;
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "content-manifest.json")));
        var content = json.RootElement.GetProperty("Textures").GetProperty("Content");
        Assert.Equal("first/hero.png", content.GetProperty("portraits.hero.png").GetProperty("FilePath").GetString());
        Assert.Equal("second/hero.png", content.GetProperty("characters.hero.png").GetProperty("FilePath").GetString());
    }

    [Theory]
    [InlineData("missing*.png")]
    [InlineData("**/*.png")]
    [InlineData("../outside.png")]
    public void InvalidOrUnmatchedPatternsFailBuild(string pattern)
    {
        Directory.CreateDirectory(_folder);
        var input = Path.Combine(_folder, "assets.yaml");
        File.WriteAllText(input, $"assets:\n  - assetType: texture\n    files: [\"{pattern}\"]\n");
        Assert.Equal(1, new Pipeline([input], Path.Combine(_folder, "output")).Execute());
        Assert.False(File.Exists(Path.Combine(_folder, "output", "content-manifest.json")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }
}
