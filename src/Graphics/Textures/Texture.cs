namespace Nexus.Graphics.Textures;

public sealed class Texture : ITexture
{
    private readonly Color[] _colorData;

    public TextureId Id { get; }
    public ContentId ContentId { get; }
    public uint Width { get; }
    public uint Height { get; }

    public ulong Count { get; }

    public Texture(ContentId contentId, uint width, uint height, Color[] colorData)
    {
        ContentId = contentId;
        Width = width;
        Height = height;

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
