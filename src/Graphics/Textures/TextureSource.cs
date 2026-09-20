namespace Nexus.Graphics.Textures;

/// <summary>
/// Creates a texture source from an array of <see cref="Color"/> values, converting them on demand
/// into packed pixel data for any format defined by <see cref="ColorFormatEnum"/>.
/// </summary>
public sealed class TextureSource : ITextureDataSource
{
    private readonly Color[] _colorData;

    public ResourceId Id { get; }
    public ulong Count { get; }

    public TextureSource(Color[] colorData)
    {
        _colorData = colorData;

        Count = (ulong)_colorData.Length;

        var hash = new IdentityHashBuilder(nameof(TextureSource));

        foreach (var color in _colorData)
        {
            hash.Add(color.R).Add(color.G).Add(color.B).Add(color.A);
        }

        Id = hash.Compute();
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="format"/> is not a supported <see cref="ColorFormatEnum"/> value.</exception>
    public ReadOnlyMemory<byte> GetPixelData(ColorFormatEnum format)
    {
        var bytesPerPixel = format.GetBytesPerPixel();
        var pixelData = new byte[_colorData.Length * bytesPerPixel];

        for (var pixelIndex = 0; pixelIndex < _colorData.Length; pixelIndex++)
        {
            _colorData[pixelIndex]
                .ToColorData(format)
                .Span.CopyTo(pixelData.AsSpan(pixelIndex * bytesPerPixel, bytesPerPixel));
        }

        return pixelData;
    }

    public static readonly ITextureDataSource Invalid = new TextureSource([Colors.Magenta]);
    public static readonly ITextureDataSource Uniform = new TextureSource([Colors.White]);
}
