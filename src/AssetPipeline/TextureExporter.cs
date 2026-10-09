using Nexus.Graphics.Textures;
using StbImageWriteSharp;

namespace Nexus.AssetPipeline;

internal static class TextureExporter
{
    public static TextureMipLevel Decode(string path)
    {
        using var stream = File.OpenRead(path);
        if (Path.GetExtension(path).Equals(".ktx2", StringComparison.OrdinalIgnoreCase))
            return Ktx2Texture.Read(stream)[0];
        var image = StbImageSharp.ImageResult.FromStream(stream, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
        return new((uint)image.Width, (uint)image.Height, image.Data);
    }

    public static void Export(string path, TextureMipLevel source, string format, bool mipmaps, int quality)
    {
        if (format == "ktx2")
        {
            using var stream = File.Create(path);
            Ktx2Texture.Write(stream, TextureMipmaps.Generate(source, mipmaps));
            return;
        }
        if (format == "jpg")
        {
            // JPEG cannot represent alpha. Reject rather than silently destroy it.
            for (var i = 3; i < source.Pixels.Length; i += 4)
                if (source.Pixels[i] != 255) throw new InvalidOperationException("JPEG output requires an opaque texture. Use png or ktx2 for transparency.");
        }
        using (var output = File.Create(path))
        {
            var writer = new ImageWriter();
            if (format == "png") writer.WritePng(source.Pixels, (int)source.Width, (int)source.Height, ColorComponents.RedGreenBlueAlpha, output);
            else writer.WriteJpg(source.Pixels, (int)source.Width, (int)source.Height, ColorComponents.RedGreenBlueAlpha, output, quality);
        }
        if (mipmaps && (source.Width > 1 || source.Height > 1))
        {
            // Generate from the encoded base so the chain agrees with JPEG's lossy pixels.
            using var stream = File.Create(path + ".mips.ktx2");
            Ktx2Texture.Write(stream, TextureMipmaps.Generate(Decode(path)).Skip(1).ToArray());
        }
    }
}
