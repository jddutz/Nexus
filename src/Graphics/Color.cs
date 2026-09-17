namespace Nexus.Graphics;

/// <summary>
/// Represents a color with red, green, blue, and alpha components in the 0.0 to 1.0 range.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Color(float red, float green, float blue, float alpha = 1.0f)
{
    public readonly float R = red;
    public readonly float G = green;
    public readonly float B = blue;
    public readonly float A = alpha;

    public Color WithTransparency(float a) => new(R, G, B, a);

    public static Color RandomGray(Random rng, float min, float range)
    {
        var c = min + (float)rng.NextDouble() * range;
        return new Color(c, c, c, 1.0f);
    }

    public static Color GaussianGray(Random rng, float median, float scale)
    {
        var uniformSum = 0.0;
        for (var index = 0; index < 6; index++)
        {
            uniformSum += rng.NextDouble();
        }

        var standardNormal = (uniformSum - 3.0) * 1.4142135623730951;
        var value = Math.Clamp(median + scale * (float)standardNormal, 0.0f, 1.0f);
        return new Color(value, value, value, 1.0f);
    }

    // Helper method to convert ARGB hex to Color RGBA
    public static Color FromArgb(uint argb)
    {
        var a = ((argb >> 24) & 0xFF) / 255.0f;
        var r = ((argb >> 16) & 0xFF) / 255.0f;
        var g = ((argb >> 8) & 0xFF) / 255.0f;
        var b = (argb & 0xFF) / 255.0f;
        return new Color(r, g, b, a);
    }

    public static Color Lerp(Color a, Color b, float t)
    {
        return new Color(
            a.R + (b.R - a.R) * t,
            a.G + (b.G - a.G) * t,
            a.B + (b.B - a.B) * t,
            a.A + (b.A - a.A) * t
        );
    }

    /// <summary>
    /// Converts this color into the packed bytes required by <paramref name="format"/>.
    /// </summary>
    /// <param name="format">The pixel format to produce.</param>
    /// <returns>The packed bytes for one pixel.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="format"/> is unsupported.</exception>
    public ReadOnlyMemory<byte> ToColorData(ColorFormatEnum format)
    {
        var data = new byte[format.GetBytesPerPixel()];
        WriteColorData(format, data);
        return data;
    }

    /// <summary>
    /// Writes this color into the packed byte format at the start of <paramref name="destination"/>
    /// without allocating.
    /// </summary>
    /// <param name="format">The pixel format to produce.</param>
    /// <param name="destination">The destination span, which must hold one pixel in the selected format.</param>
    /// <returns>The number of bytes written.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="format"/> is unsupported.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="destination"/> is too small.</exception>
    public int WriteColorData(ColorFormatEnum format, Span<byte> destination) =>
        format switch
        {
            ColorFormatEnum.RGB8UNorm => WriteChannels(
                destination,
                ToUNorm8(R),
                ToUNorm8(G),
                ToUNorm8(B),
                default,
                3
            ),
            ColorFormatEnum.RGBA8UNorm => WriteChannels(
                destination,
                ToUNorm8(R),
                ToUNorm8(G),
                ToUNorm8(B),
                ToUNorm8(A),
                4
            ),
            ColorFormatEnum.ARGB8UNorm => WriteChannels(
                destination,
                ToUNorm8(A),
                ToUNorm8(R),
                ToUNorm8(G),
                ToUNorm8(B),
                4
            ),
            ColorFormatEnum.RGB16UNorm => WriteChannels(
                destination,
                ToUNorm16(R),
                ToUNorm16(G),
                ToUNorm16(B),
                default,
                3
            ),
            ColorFormatEnum.RGBA16UNorm => WriteChannels(
                destination,
                ToUNorm16(R),
                ToUNorm16(G),
                ToUNorm16(B),
                ToUNorm16(A),
                4
            ),
            ColorFormatEnum.ARGB16UNorm => WriteChannels(
                destination,
                ToUNorm16(A),
                ToUNorm16(R),
                ToUNorm16(G),
                ToUNorm16(B),
                4
            ),
            ColorFormatEnum.RGB16Float => WriteChannels(
                destination,
                ToHalf(R),
                ToHalf(G),
                ToHalf(B),
                default,
                3
            ),
            ColorFormatEnum.RGBA16Float => WriteChannels(
                destination,
                ToHalf(R),
                ToHalf(G),
                ToHalf(B),
                ToHalf(A),
                4
            ),
            ColorFormatEnum.RGB32UInt => WriteChannels(
                destination,
                ToUInt32(R),
                ToUInt32(G),
                ToUInt32(B),
                default,
                3
            ),
            ColorFormatEnum.RGBA32UInt => WriteChannels(
                destination,
                ToUInt32(R),
                ToUInt32(G),
                ToUInt32(B),
                ToUInt32(A),
                4
            ),
            ColorFormatEnum.RGB32Float => WriteChannels(destination, R, G, B, default, 3),
            ColorFormatEnum.RGBA32Float => WriteChannels(destination, R, G, B, A, 4),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

    /// <summary>Writes up to four unmanaged channel values into the destination span.</summary>
    private static int WriteChannels<T>(
        Span<byte> destination,
        T first,
        T second,
        T third,
        T fourth,
        int channelCount
    )
        where T : unmanaged
    {
        Span<T> components = stackalloc T[4];
        components[0] = first;
        components[1] = second;
        components[2] = third;
        components[3] = fourth;

        var bytes = MemoryMarshal.AsBytes(components[..channelCount]);
        if (destination.Length < bytes.Length)
        {
            throw new ArgumentException("The destination span is too small.", nameof(destination));
        }

        bytes.CopyTo(destination);
        return bytes.Length;
    }

    /// <summary>Clamps a channel and scales it to an 8-bit unsigned normalized value.</summary>
    private static byte ToUNorm8(float value) =>
        (byte)Math.Round(Math.Clamp(value, 0f, 1f) * byte.MaxValue);

    /// <summary>Clamps a channel and scales it to a 16-bit unsigned normalized value.</summary>
    private static ushort ToUNorm16(float value) =>
        (ushort)Math.Round(Math.Clamp(value, 0f, 1f) * ushort.MaxValue);

    /// <summary>Clamps a channel and scales it to the full range of a 32-bit unsigned integer.</summary>
    private static uint ToUInt32(float value) =>
        (uint)Math.Round(Math.Clamp(value, 0f, 1f) * (double)uint.MaxValue);

    /// <summary>Converts a channel to a 16-bit floating point value.</summary>
    private static Half ToHalf(float value) => (Half)value;
}
