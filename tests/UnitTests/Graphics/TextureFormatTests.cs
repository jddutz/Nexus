using Nexus.Graphics;
using Nexus.Graphics.Components;
using Nexus.Graphics.Vulkan;
using Silk.NET.Vulkan;

namespace Nexus.UnitTests.Graphics;

/// <summary>Verifies texture formats preserve pixel bytes and select the intended Vulkan format.</summary>
public sealed class TextureFormatTests
{
    /// <summary>Verifies RGBA sRGB texture data retains the source channel bytes.</summary>
    [Fact]
    public void Rgba8_srgb_preserves_source_bytes()
    {
        var color = new Color(6, 14, 29);
        Span<byte> bytes = stackalloc byte[4];

        color.WriteColorData(ColorFormatEnum.RGBA8Srgb, bytes);

        Assert.Equal(new byte[] { 6, 14, 29, 255 }, bytes.ToArray());
    }

    /// <summary>Verifies RGBA sRGB texture storage maps to Vulkan's sRGB format.</summary>
    [Fact]
    public void Rgba8_srgb_maps_to_vulkan_srgb_format()
    {
        Assert.Equal(Format.R8G8B8A8Srgb, ColorFormatEnum.RGBA8Srgb.ToVulkanFormat());
    }

    /// <summary>Verifies texture components remain UNorm unless explicitly configured otherwise.</summary>
    [Fact]
    public void Texture_component_texture_format_defaults_to_unorm_and_is_changeable()
    {
        var component = new TextureComponent();
        var textureChanged = 0;
        component.TextureChanged += (_, _) => textureChanged++;

        Assert.Equal(ColorFormatEnum.RGBA8UNorm, ((IDrawable)component).TextureFormat);

        component.TextureFormat = ColorFormatEnum.RGBA8Srgb;

        Assert.Equal(ColorFormatEnum.RGBA8Srgb, ((IDrawable)component).TextureFormat);
        Assert.Equal(1, textureChanged);
    }
}