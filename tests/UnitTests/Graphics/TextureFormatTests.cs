using Nexus.Graphics;
using Nexus.Graphics.Textures;
using Nexus.Graphics.Vulkan;
using Silk.NET.Vulkan;

namespace Nexus.UnitTests.Graphics;

/// <summary>Verifies texture formats preserve pixel bytes and select the intended Vulkan format.</summary>
public sealed class TextureFormatTests
{
    [Fact]
    public void LargeTextureHashAndUploadAvoidPerPixelAllocations()
    {
        var colors = Enumerable.Range(0, 4096)
            .Select(index => new Color((byte)index, (byte)(index >> 4), (byte)(index >> 8))).ToArray();
        var originalHash = new Nexus.Core.IdentityHashBuilder(nameof(Texture));
        originalHash.Add(64u).Add(64u).Add((uint)ColorFormatEnum.RGBA8UNorm).Add(1u);
        foreach (var color in colors)
            originalHash.Add(BitConverter.GetBytes(color.R)).Add(BitConverter.GetBytes(color.G))
                .Add(BitConverter.GetBytes(color.B)).Add(BitConverter.GetBytes(color.A));
        _ = new Texture("warmup", 64, 64, colors);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var texture = new Texture("test", 64, 64, colors);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal((TextureId)originalHash.Compute(), texture.Id);
        Assert.InRange(allocated, 0, 1024);

        foreach (var format in Enum.GetValues<ColorFormatEnum>())
        {
            var expected = colors.Skip(1).Take(4094).SelectMany(color => color.ToColorData(format).ToArray()).ToArray();
            var destination = new byte[expected.Length];
            texture.WriteTo(1, 4094, format, destination);
            before = GC.GetAllocatedBytesForCurrentThread();
            texture.WriteTo(1, 4094, format, destination);
            allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(expected, destination);
            Assert.Equal(0, allocated);
        }
    }

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
