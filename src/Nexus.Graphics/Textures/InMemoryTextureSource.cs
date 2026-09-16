namespace Nexus.Graphics.Textures;

/// <summary>
/// Creates a texture source from an array of <see cref="Color"/> values, converting them on demand
/// into packed pixel data for any format defined by <see cref="PixelFormatEnum"/>.
/// </summary>
public sealed class InMemoryTextureSource(Color[] colorData) : ITextureSource
{
    private readonly Color[] _colorData = [.. colorData];

    // Channel orderings shared by every "RGB"/"RGBA"/"ARGB" format below.
    private static readonly Func<Color, float>[] RgbChannels = [c => c.R, c => c.G, c => c.B];
    private static readonly Func<Color, float>[] RgbaChannels =
    [
        c => c.R,
        c => c.G,
        c => c.B,
        c => c.A,
    ];
    private static readonly Func<Color, float>[] ArgbChannels =
    [
        c => c.A,
        c => c.R,
        c => c.G,
        c => c.B,
    ];

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="format"/> is not a supported <see cref="PixelFormatEnum"/> value.</exception>
    public ReadOnlyMemory<byte> GetPixelData(PixelFormatEnum format) =>
        format switch
        {
            PixelFormatEnum.RGB8UNorm => GetRgb8UNorm(),
            PixelFormatEnum.RGBA8UNorm => GetRgba8UNorm(),
            PixelFormatEnum.ARGB8UNorm => GetArgb8UNorm(),
            PixelFormatEnum.RGB16UNorm => GetRgb16UNorm(),
            PixelFormatEnum.RGBA16UNorm => GetRgba16UNorm(),
            PixelFormatEnum.ARGB16UNorm => GetArgb16UNorm(),
            PixelFormatEnum.RGB16Float => GetRgb16Float(),
            PixelFormatEnum.RGBA16Float => GetRgba16Float(),
            PixelFormatEnum.RGB32UInt => GetRgb32UInt(),
            PixelFormatEnum.RGBA32UInt => GetRgba32UInt(),
            PixelFormatEnum.RGB32Float => GetRgb32Float(),
            PixelFormatEnum.RGBA32Float => GetRgba32Float(),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

    /// <summary>Packs RGB channels as 8-bit unsigned normalized values (3 bytes/pixel).</summary>
    private ReadOnlyMemory<byte> GetRgb8UNorm() => PackChannels(RgbChannels, ToUNorm8);

    /// <summary>Packs RGBA channels as 8-bit unsigned normalized values (4 bytes/pixel).</summary>
    private ReadOnlyMemory<byte> GetRgba8UNorm() => PackChannels(RgbaChannels, ToUNorm8);

    /// <summary>Packs channels as 8-bit unsigned normalized values in alpha-first order (4 bytes/pixel).</summary>
    private ReadOnlyMemory<byte> GetArgb8UNorm() => PackChannels(ArgbChannels, ToUNorm8);

    /// <summary>Packs RGB channels as 16-bit unsigned normalized values (6 bytes/pixel).</summary>
    private ReadOnlyMemory<byte> GetRgb16UNorm() => PackChannels(RgbChannels, ToUNorm16);

    /// <summary>Packs RGBA channels as 16-bit unsigned normalized values (8 bytes/pixel).</summary>
    private ReadOnlyMemory<byte> GetRgba16UNorm() => PackChannels(RgbaChannels, ToUNorm16);

    /// <summary>Packs channels as 16-bit unsigned normalized values in alpha-first order (8 bytes/pixel).</summary>
    private ReadOnlyMemory<byte> GetArgb16UNorm() => PackChannels(ArgbChannels, ToUNorm16);

    /// <summary>Packs RGB channels as 16-bit floating point values (6 bytes/pixel).</summary>
    private ReadOnlyMemory<byte> GetRgb16Float() => PackChannels(RgbChannels, ToHalf);

    /// <summary>Packs RGBA channels as 16-bit floating point values (8 bytes/pixel).</summary>
    private ReadOnlyMemory<byte> GetRgba16Float() => PackChannels(RgbaChannels, ToHalf);

    /// <summary>Packs RGB channels as 32-bit unsigned integers, scaling the 0-1 range to the full uint range (12 bytes/pixel).</summary>
    private ReadOnlyMemory<byte> GetRgb32UInt() => PackChannels(RgbChannels, ToUInt32);

    /// <summary>Packs RGBA channels as 32-bit unsigned integers, scaling the 0-1 range to the full uint range (16 bytes/pixel).</summary>
    private ReadOnlyMemory<byte> GetRgba32UInt() => PackChannels(RgbaChannels, ToUInt32);

    /// <summary>Packs RGB channels as 32-bit floating point values, unchanged from the source range (12 bytes/pixel).</summary>
    private ReadOnlyMemory<byte> GetRgb32Float() =>
        PackChannels(RgbChannels, static value => value);

    /// <summary>Packs RGBA channels as 32-bit floating point values, unchanged from the source range (16 bytes/pixel).</summary>
    private ReadOnlyMemory<byte> GetRgba32Float() =>
        PackChannels(RgbaChannels, static value => value);

    /// <summary>
    /// Converts every color into the channels described by <paramref name="channelSelectors"/> using
    /// <paramref name="convert"/>, and packs the results into a single contiguous byte buffer in pixel order.
    /// </summary>
    /// <typeparam name="T">Unmanaged component type each converted channel value is stored as.</typeparam>
    /// <param name="channelSelectors">Ordered channel accessors defining the per-pixel component layout.</param>
    /// <param name="convert">Converts a single 0-1 range channel value into the destination component type.</param>
    private ReadOnlyMemory<byte> PackChannels<T>(
        Func<Color, float>[] channelSelectors,
        Func<float, T> convert
    )
        where T : unmanaged
    {
        var componentsPerPixel = channelSelectors.Length;
        var components = new T[_colorData.Length * componentsPerPixel];

        for (var pixelIndex = 0; pixelIndex < _colorData.Length; pixelIndex++)
        {
            var color = _colorData[pixelIndex];
            var baseIndex = pixelIndex * componentsPerPixel;

            for (var channelIndex = 0; channelIndex < componentsPerPixel; channelIndex++)
            {
                components[baseIndex + channelIndex] = convert(
                    channelSelectors[channelIndex](color)
                );
            }
        }

        return MemoryMarshal.AsBytes<T>(components).ToArray();
    }

    /// <summary>Clamps a 0-1 channel value and scales it to an 8-bit unsigned normalized byte (0-255).</summary>
    private static byte ToUNorm8(float value) =>
        (byte)Math.Round(Math.Clamp(value, 0f, 1f) * byte.MaxValue);

    /// <summary>Clamps a 0-1 channel value and scales it to a 16-bit unsigned normalized ushort (0-65535).</summary>
    private static ushort ToUNorm16(float value) =>
        (ushort)Math.Round(Math.Clamp(value, 0f, 1f) * ushort.MaxValue);

    /// <summary>Clamps a 0-1 channel value and scales it to the full range of a 32-bit unsigned integer.</summary>
    private static uint ToUInt32(float value) =>
        (uint)Math.Round(Math.Clamp(value, 0f, 1f) * (double)uint.MaxValue);

    /// <summary>Converts a 0-1 channel value to a 16-bit floating point value.</summary>
    private static Half ToHalf(float value) => (Half)value;
}
