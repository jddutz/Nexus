using Microsoft.Extensions.Configuration;

namespace Nexus.Graphics.Textures;

/// <summary>A named atlas region with top-left pixel bounds and normalized XYWH coordinates.</summary>
public sealed record TextureRegion(string Name, Rectangle<int> Bounds, Rectangle<float> TexCoords);

public static class TextureRegionManifestExtensions
{
    /// <summary>Loads all regions from a texture entry, or an empty list when none are configured.</summary>
    public static IReadOnlyList<TextureRegion> GetTextureRegions(this IContentManifest manifest, ContentId textureId) =>
        manifest.Textures.GetRequiredSection("Content").GetRequiredSection(textureId.Value)
            .GetSection("Regions").GetChildren()
            .Select(region => manifest.GetTextureRegion(textureId, region.Key)).ToArray();

    /// <summary>Loads a named region from a texture's manifest entry.</summary>
    public static TextureRegion GetTextureRegion(this IContentManifest manifest, ContentId textureId, string name)
    {
        var region = manifest.Textures.GetRequiredSection("Content").GetRequiredSection(textureId.Value)
            .GetRequiredSection("Regions").GetRequiredSection(name);
        int Pixel(string key) => int.Parse(region.GetRequiredSection("Bounds").GetRequiredValue(key), System.Globalization.CultureInfo.InvariantCulture);
        float Uv(string key) => float.Parse(region.GetRequiredSection("TexCoords").GetRequiredValue(key), System.Globalization.CultureInfo.InvariantCulture);
        return new(name, new(Pixel("X"), Pixel("Y"), Pixel("Width"), Pixel("Height")),
            new(Uv("X"), Uv("Y"), Uv("Width"), Uv("Height")));
    }
}

