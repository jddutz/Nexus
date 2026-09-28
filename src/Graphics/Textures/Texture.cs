namespace Nexus.Graphics.Textures;

/// <summary>Stores pixel data and its intended GPU sampling format.</summary>
public sealed class Texture : ITexture
{
    private readonly Color[] _colorData;

    public TextureId Id { get; }
    public ContentId ContentId { get; }
    public uint Width { get; }
    public uint Height { get; }

    /// <inheritdoc />
    public ColorFormatEnum TextureFormat { get; }

    public ulong Count { get; }

    /// <summary>Creates a texture with the specified pixels and sampling format.</summary>
    /// <param name="contentId">The content identifier assigned to the texture.</param>
    /// <param name="width">The texture width in pixels.</param>
    /// <param name="height">The texture height in pixels.</param>
    /// <param name="colorData">The texture's pixel colors.</param>
    /// <param name="textureFormat">The GPU storage and sampling format.</param>
    public Texture(
        ContentId contentId,
        uint width,
        uint height,
        Color[] colorData,
        ColorFormatEnum textureFormat = ColorFormatEnum.RGBA8UNorm
    )
    {
        ContentId = contentId;
        Width = width;
        Height = height;
        TextureFormat = textureFormat;

        _colorData = colorData;
        Count = (ulong)_colorData.Length;

        var hash = new IdentityHashBuilder(nameof(Texture));

        foreach (var color in _colorData)
        {
            hash.Add(color.R).Add(color.G).Add(color.B).Add(color.A);
        }

        Id = hash.Compute();
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="format"/> is not a supported <see cref="ColorFormatEnum"/> value.</exception>
    public void WriteTo(ulong start, ulong count, ColorFormatEnum format, Span<byte> target)
    {
        if (start > Count || count > Count - start)
            throw new ArgumentOutOfRangeException(
                nameof(count),
                "The requested pixel range exceeds the texture bounds."
            );

        var bytesPerPixel = format.GetBytesPerPixel();
        var requiredBytes = checked(count * (ulong)bytesPerPixel);

        if ((ulong)target.Length < requiredBytes)
            throw new ArgumentException(
                $"Target must contain at least {requiredBytes} bytes.",
                nameof(target)
            );

        for (ulong index = 0; index < count; index++)
        {
            _colorData[checked((int)(start + index))]
                .ToColorData(format)
                .Span.CopyTo(
                    target.Slice(checked((int)(index * (ulong)bytesPerPixel)), bytesPerPixel)
                );
        }
    }

    public static readonly ITexture Invalid = new Texture(string.Empty, 1, 1, [Colors.Magenta]);
    public static readonly ITexture Uniform = new Texture("uniform", 1, 1, [Colors.White]);
}
