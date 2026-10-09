namespace Nexus.Graphics.Textures;

/// <summary>A tightly packed RGBA8 image level, in top-left row order.</summary>
public sealed record TextureMipLevel(uint Width, uint Height, byte[] Pixels);

/// <summary>Generates an alpha-aware mip chain in linear light from sRGB pixels.</summary>
public static class TextureMipmaps
{
    public static TextureMipLevel[] Generate(TextureMipLevel source, bool enabled = true)
    {
        Validate(source);
        var levels = new List<TextureMipLevel> { source };
        while (enabled && (source.Width > 1 || source.Height > 1))
        {
            var width = Math.Max(1, source.Width / 2);
            var height = Math.Max(1, source.Height / 2);
            var pixels = new byte[checked((int)(width * height * 4))];
            for (uint y = 0; y < height; y++)
            for (uint x = 0; x < width; x++)
            {
                double alpha = 0, red = 0, green = 0, blue = 0, weight = 0;
                // Area filtering includes the final row/column of odd-sized images.
                double left = (double)x * source.Width / width, right = (double)(x + 1) * source.Width / width;
                double top = (double)y * source.Height / height, bottom = (double)(y + 1) * source.Height / height;
                for (var sy = (int)top; sy < Math.Ceiling(bottom); sy++)
                for (var sx = (int)left; sx < Math.Ceiling(right); sx++)
                {
                    var w = (Math.Min(right, sx + 1) - Math.Max(left, sx)) * (Math.Min(bottom, sy + 1) - Math.Max(top, sy));
                    var offset = checked((sy * (int)source.Width + sx) * 4);
                    var a = source.Pixels[offset + 3] / 255.0 * w;
                    weight += w; alpha += a;
                    red += Linear(source.Pixels[offset]) * a;
                    green += Linear(source.Pixels[offset + 1]) * a;
                    blue += Linear(source.Pixels[offset + 2]) * a;
                }
                var target = checked((int)((y * width + x) * 4));
                pixels[target] = alpha == 0 ? (byte)0 : Srgb(red / alpha);
                pixels[target + 1] = alpha == 0 ? (byte)0 : Srgb(green / alpha);
                pixels[target + 2] = alpha == 0 ? (byte)0 : Srgb(blue / alpha);
                pixels[target + 3] = (byte)Math.Round(alpha / weight * 255);
            }
            source = new(width, height, pixels);
            levels.Add(source);
        }
        return levels.ToArray();
    }

    public static void Validate(TextureMipLevel level)
    {
        if (level.Width == 0 || level.Height == 0 || level.Pixels.LongLength != checked((long)level.Width * level.Height * 4))
            throw new ArgumentException("A texture level must contain Width * Height RGBA8 pixels.");
    }

    private static double Linear(byte value)
    {
        var v = value / 255.0;
        return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }
    private static byte Srgb(double v) => (byte)Math.Round(Math.Clamp(v <= 0.0031308 ? 12.92 * v : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055, 0, 1) * 255);
}
