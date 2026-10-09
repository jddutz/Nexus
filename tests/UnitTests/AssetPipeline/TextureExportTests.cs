using System.Buffers.Binary;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Core;
using Nexus.Graphics.Textures;

namespace Nexus.AssetPipeline.Tests;

public sealed class TextureExportTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"nap-textures-{Guid.NewGuid():N}");

    [Theory]
    [InlineData("ktx2", true, 3)]
    [InlineData("ktx2", false, 1)]
    [InlineData("png", true, 3)]
    [InlineData("png", false, 1)]
    [InlineData("jpg", true, 3)]
    [InlineData("jpeg", false, 1)]
    public void ExportLoadsAllLevelsAtRuntime(string format, bool mipmaps, uint count)
    {
        Directory.CreateDirectory(_folder);
        WriteImage("image.png", 5, 3);
        var input = Path.Combine(_folder, "assets.yaml");
        File.WriteAllText(input, $"textureFormat: {format}\nmipmaps: {mipmaps.ToString().ToLowerInvariant()}\nassets:\n  - assetType: texture\n    groupName: UI\n    files: [image.png]\n");
        var output = Path.Combine(_folder, "out");
        Assert.Equal(0, new Pipeline([input], output).Execute());
        var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(output, "content-manifest.json")).Build();
        var manifest = new ContentManifest(output, configuration);
        using var registry = new TextureRegistry(manifest, NullLogger<TextureRegistry>.Instance);
        var texture = registry.GetOrCreate((ContentId)"UI.image.png");
        Assert.Equal(5u, texture.Width); Assert.Equal(3u, texture.Height);
        Assert.Equal(count, texture.MipLevelCount);
        Assert.Equal($"image.{(format == "jpeg" ? "jpg" : format)}", manifest.Textures.GetContentFilePath((ContentId)"UI.image.png"));
        var last = new byte[count == 1 ? 60 : 4];
        texture.WriteMipLevel(count - 1, texture.TextureFormat, last);
        Assert.Equal(255, last[3]);
    }

    [Fact]
    public void DefaultAndPerAssetOverridesAreIndependent()
    {
        Directory.CreateDirectory(_folder);
        WriteImage("first.png", 4, 4); WriteImage("second.png", 4, 4);
        var input = Path.Combine(_folder, "assets.yaml");
        File.WriteAllText(input, "assets:\n  - assetType: texture\n    files: [first.png]\n  - assetType: texture\n    files: [second.png]\n    textureFormat: png\n    mipmaps: false\n");
        var output = Path.Combine(_folder, "out");
        Assert.Equal(0, new Pipeline([input], output).Execute());
        var bytes = File.ReadAllBytes(Path.Combine(output, "first.ktx2"));
        Assert.Equal(43u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12)));
        Assert.Equal(3u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(40)));
        Assert.True(File.Exists(Path.Combine(output, "second.png")));
        Assert.False(File.Exists(Path.Combine(output, "second.png.mips.ktx2")));
    }

    [Theory]
    [InlineData("textureFormat: jp2")]
    [InlineData("jpegQuality: 0")]
    [InlineData("textureFormat: jpg", true)]
    public void InvalidSettingsAndTransparentJpegFail(string settings, bool transparent = false)
    {
        Directory.CreateDirectory(_folder); WriteImage("image.png", 2, 2, transparent);
        var input = Path.Combine(_folder, "assets.yaml");
        File.WriteAllText(input, $"{settings}\nassets:\n  - assetType: texture\n    files: [image.png]\n");
        Assert.Equal(1, new Pipeline([input], Path.Combine(_folder, "out")).Execute());
    }

    [Fact]
    public void ConvertedFilenameCollisionsFail()
    {
        Directory.CreateDirectory(_folder); WriteImage("image.png", 2, 2);
        File.Copy(Path.Combine(_folder, "image.png"), Path.Combine(_folder, "image.jpg"));
        var input = Path.Combine(_folder, "assets.yaml");
        File.WriteAllText(input, "assets:\n  - assetType: texture\n    groupName: UI\n    files: [image.png, image.jpg]\n");
        Assert.Equal(1, new Pipeline([input], Path.Combine(_folder, "out")).Execute());
    }

    [Fact]
    public void MipsFilterInLinearLightAndPreserveAlphaWithoutDarkFringes()
    {
        var opaque = TextureMipmaps.Generate(new(2, 1, [0, 0, 0, 255, 255, 255, 255, 255]));
        Assert.InRange(opaque[1].Pixels[0], 187, 189);
        var alpha = TextureMipmaps.Generate(new(3, 1, [255, 0, 0, 255, 0, 0, 0, 0, 0, 0, 0, 0]));
        Assert.Equal(new byte[] { 255, 0, 0, 85 }, alpha[1].Pixels);
    }

    [Fact]
    public void KtxRoundTripAndCorruptIndexValidation()
    {
        var levels = TextureMipmaps.Generate(new(2, 2, Enumerable.Repeat((byte)255, 16).ToArray()));
        using var stream = new MemoryStream(); Ktx2Texture.Write(stream, levels);
        stream.Position = 0; var decoded = Ktx2Texture.Read(stream);
        Assert.Equal(2, decoded.Length); Assert.Equal(levels[0].Pixels, decoded[0].Pixels);
        var bytes = stream.ToArray(); BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(80), ulong.MaxValue);
        Assert.Throws<InvalidDataException>(() => Ktx2Texture.Read(new MemoryStream(bytes)));
    }

    [Fact]
    public void IdentityIncludesMipChainAndShape()
    {
        var pixels = new Nexus.Graphics.Color[] { Nexus.Graphics.Colors.White, Nexus.Graphics.Colors.White };
        var baseOnly = new Texture((ContentId)"a", 2, 1, pixels);
        var mipped = new Texture((ContentId)"a", 2, 1, pixels, mipmaps: [new(1, 1, [255, 255, 255, 255])]);
        Assert.NotEqual(baseOnly.Id, mipped.Id);
        Assert.NotEqual(baseOnly.Id, new Texture((ContentId)"a", 1, 2, pixels).Id);
    }

    [Fact]
    public void VulkanUploadIncludesEveryLevelWithCorrectExtentsAndOffsets()
    {
        var texture = new Texture((ContentId)"upload", 3, 1,
            [Nexus.Graphics.Colors.White, Nexus.Graphics.Colors.White, Nexus.Graphics.Colors.White],
            mipmaps: [new(1, 1, [255, 0, 0, 255])]);
        var (data, regions) = Nexus.Graphics.Vulkan.Textures.ImageRegistry.SerializeMipLevels(texture);
        Assert.Equal(2, regions.Length);
        Assert.Equal(3u, regions[0].ImageExtent.Width);
        Assert.Equal(1u, regions[1].ImageExtent.Width);
        Assert.Equal(1u, regions[1].ImageExtent.Height);
        Assert.Equal(1u, regions[1].ImageSubresource.MipLevel);
        Assert.Equal(12ul, regions[1].BufferOffset);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, data[12..]);
    }

    private void WriteImage(string name, int width, int height, bool transparent = false)
    {
        var pixels = Enumerable.Repeat((byte)255, width * height * 4).ToArray();
        if (transparent) pixels[3] = 0;
        using var stream = File.Create(Path.Combine(_folder, name));
        new StbImageWriteSharp.ImageWriter().WritePng(pixels, width, height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream);
    }

    public void Dispose() { if (Directory.Exists(_folder)) Directory.Delete(_folder, true); }
}

