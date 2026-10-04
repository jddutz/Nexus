using Microsoft.Extensions.Configuration;
using Nexus.Graphics;
using Nexus.Graphics.Textures;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Core;

namespace Nexus.UnitTests.Graphics;

/// <summary>
/// Verifies texture loading, lookup, and cache lifetime behavior.
/// </summary>
public sealed class TextureRegistryTests
{
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
        var manifest = new ContentManifest(
            string.Empty,
            new ConfigurationBuilder().Build()
        );
        return new TextureRegistry(manifest, NullLogger<TextureRegistry>.Instance);
    }
}
