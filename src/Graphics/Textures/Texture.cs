namespace Nexus.Graphics.Textures;

public sealed class Texture : ITexture
{
    private readonly Color[] _colorData;

    public ContentId Id { get; }
    public uint Width { get; }
    public uint Height { get; }

    public RenderableId GraphicsId { get; }
    public ulong Count { get; }

    public Texture(ContentId contentId, uint width, uint height, Color[] colorData)
    {
        Id = contentId;
        Width = width;
        Height = height;

        _colorData = colorData;
        Count = (ulong)_colorData.Length;

        var hash = new IdentityHashBuilder(nameof(Texture));

        foreach (var color in _colorData)
        {
            hash.Add(color.R).Add(color.G).Add(color.B).Add(color.A);
        }

        GraphicsId = hash.Compute();
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

    public static readonly ITexture Invalid = new Texture(string.Empty, 1, 1, [Colors.Magenta]);
    public static readonly ITexture Uniform = new Texture("uniform", 1, 1, [Colors.White]);
}
