using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Core;
using Nexus.Graphics.Textures;

namespace Nexus.UnitTests.Graphics;

/// <summary>
/// Verifies texture loading, lookup, and cache lifetime behavior.
/// </summary>
public sealed class TextureRegistryTests
{
    [Fact]
    public void ContentLoadingPreservesAtlasMetadataAndSeparatesEqualPixels()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"nexus-texture-regions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "atlas.tga"),
                [0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 1, 0, 32, 40,
                 0, 0, 255, 255, 0, 255, 0, 255]);
            var values = new Dictionary<string, string?>();
            foreach (var id in new[] { "first", "second", "plain" })
            {
                var prefix = $"Textures:Content:{id}";
                values[$"{prefix}:FilePath"] = "atlas.tga";
                if (id == "plain") continue;
                var region = $"{prefix}:Regions:{id}-panel";
                values[$"{region}:Bounds:X"] = "1";
                values[$"{region}:Bounds:Y"] = "0";
                values[$"{region}:Bounds:Width"] = "1";
                values[$"{region}:Bounds:Height"] = "1";
                values[$"{region}:TexCoords:X"] = "0.5";
                values[$"{region}:TexCoords:Y"] = "0";
                values[$"{region}:TexCoords:Width"] = "0.5";
                values[$"{region}:TexCoords:Height"] = "1";
            }
            var manifest = new ContentManifest(folder, new ConfigurationBuilder().AddInMemoryCollection(values).Build());
            using var registry = new TextureRegistry(manifest, NullLogger<TextureRegistry>.Instance);
            var first = registry.GetOrCreate((ContentId)"first");
            var second = registry.GetOrCreate((ContentId)"second");
            var regionValue = Assert.Single(first.Regions);
            Assert.Same(regionValue, first.GetRegion("first-panel"));
            Assert.Equal(0.5f, regionValue.TexCoords.Origin.X);
            Assert.Equal(1, regionValue.Bounds.Origin.X);
            Assert.Throws<KeyNotFoundException>(() => first.GetRegion("FIRST-PANEL"));
            Assert.Throws<NotSupportedException>(() => ((IList<TextureRegion>)first.Regions).Clear());
            Assert.NotEqual(first.Id, second.Id);
            Assert.Equal("second-panel", second.GetRegion("second-panel").Name);
            Assert.Same(first, registry.GetOrCreate((ContentId)"first"));
            Assert.Same(second, registry.Get(second.Id));
            Assert.Empty(registry.GetOrCreate((ContentId)"plain").Regions);
            Assert.Empty(registry.Load(Path.Combine(folder, "atlas.tga")).Regions);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    /// <summary>
    /// Verifies that content loading registers a texture and subsequent lookups reuse it.
    /// </summary>
    [Fact]
    public void GetOrCreateRegistersAndReusesTexture()
    {
        using var registry = CreateRegistry();

        var first = registry.GetOrCreate(ContentId.Invalid);
        var second = registry.GetOrCreate(ContentId.Invalid);

        Assert.Same(first, second);
        Assert.Same(first, registry.Get(first.Id));
    }

    /// <summary>
    /// Verifies that reset removes all registered textures.
    /// </summary>
    [Fact]
    public void ResetRemovesRegisteredTextures()
    {
        using var registry = CreateRegistry();
        var texture = registry.GetOrCreate(ContentId.Invalid);

        registry.Reset();

        Assert.Throws<KeyNotFoundException>(() => registry.Get(texture.Id));
    }

    private static TextureRegistry CreateRegistry()
    {
        var manifest = new ContentManifest(string.Empty, new ConfigurationBuilder().Build());
        return new TextureRegistry(manifest, NullLogger<TextureRegistry>.Instance);
    }
}
