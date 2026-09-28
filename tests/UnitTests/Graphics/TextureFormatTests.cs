using Nexus.Graphics;
using Nexus.Graphics.Textures;
using Nexus.Graphics.Vulkan;
using Silk.NET.Vulkan;

namespace Nexus.UnitTests.Graphics;

/// <summary>Verifies texture formats preserve pixel bytes and select the intended Vulkan format.</summary>
public sealed class TextureFormatTests
{
    /// <summary>Verifies RGBA sRGB texture data retains the source channel bytes.</summary>
    [Fact]
    public void Rgba8_srgb_texture_write_preserves_source_bytes()
    {
        var texture = new Texture(
            "source",
            1,
            1,
            [new Color(6, 14, 29)],
            ColorFormatEnum.RGBA8Srgb
        );
        Span<byte> bytes = stackalloc byte[4];

        texture.WriteTo(0, 1, texture.TextureFormat, bytes);

        Assert.Equal(new byte[] { 6, 14, 29, 255 }, bytes.ToArray());
    }

    /// <summary>Verifies RGBA sRGB texture storage maps to Vulkan's sRGB format.</summary>
    [Fact]
    public void Rgba8_srgb_maps_to_vulkan_srgb_format()
    {
        Assert.Equal(Format.R8G8B8A8Srgb, ColorFormatEnum.RGBA8Srgb.ToVulkanFormat());
    }

    /// <summary>Verifies generated textures default to linear UNorm sampling.</summary>
    [Fact]
    public void Texture_format_defaults_to_unorm()
    {
        var texture = new Texture("generated", 1, 1, [Colors.White]);

        Assert.Equal(ColorFormatEnum.RGBA8UNorm, texture.TextureFormat);
    }
}
