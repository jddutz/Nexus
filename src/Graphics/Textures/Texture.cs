namespace Nexus.Graphics.Textures;

/// <summary>Stores pixel data and its intended GPU sampling format.</summary>
public sealed class Texture : ITexture
{
    private readonly Color[] _colorData;
    private readonly Dictionary<string, TextureRegion> _regionsByName;

    /// <inheritdoc />
    public IReadOnlyList<TextureRegion> Regions { get; }

    /// <inheritdoc />
    public TextureRegion GetRegion(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _regionsByName.TryGetValue(name, out var region)
            ? region
            : throw new KeyNotFoundException($"Region '{name}' was not found in texture '{ContentId}'.");
    }

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
    /// <param name="regions">Optional named atlas regions; copied into a read-only collection.</param>
    public Texture(
        ContentId contentId,
        uint width,
        uint height,
        Color[] colorData,
        ColorFormatEnum textureFormat = ColorFormatEnum.RGBA8UNorm,
        IEnumerable<TextureRegion>? regions = null
    )
    {
        ContentId = contentId;
        Width = width;
        Height = height;
        TextureFormat = textureFormat;
        var regionArray = regions?.ToArray() ?? [];
        _regionsByName = regionArray.ToDictionary(region => region.Name, StringComparer.Ordinal);
        Regions = Array.AsReadOnly(regionArray);

        _colorData = colorData;
        Count = (ulong)_colorData.Length;

        var hash = new IdentityHashBuilder(nameof(Texture));

        foreach (var color in _colorData)
        {
            hash.Add(color.R).Add(color.G).Add(color.B).Add(color.A);
        }

        // Atlas metadata participates in identity so equal pixels with different regions
        // do not alias each other in the registry.
        foreach (var region in regionArray.OrderBy(region => region.Name, StringComparer.Ordinal))
        {
            hash.Add(region.Name)
                .Add(region.Bounds.Origin.X).Add(region.Bounds.Origin.Y)
                .Add(region.Bounds.Size.X).Add(region.Bounds.Size.Y)
                .Add(region.TexCoords.Origin.X).Add(region.TexCoords.Origin.Y)
                .Add(region.TexCoords.Size.X).Add(region.TexCoords.Size.Y);
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

        if (format is ColorFormatEnum.RGBA8UNorm or ColorFormatEnum.RGBA8Srgb)
        {
            var source = _colorData.AsSpan(checked((int)start), checked((int)count));
            var output = target[..checked((int)requiredBytes)];
            for (var index = 0; index < source.Length; index++)
            {
                var color = source[index];
                var offset = index * 4;
                output[offset] = ToUNorm8(color.R);
                output[offset + 1] = ToUNorm8(color.G);
                output[offset + 2] = ToUNorm8(color.B);
                output[offset + 3] = ToUNorm8(color.A);
            }
            return;
        }

        for (ulong index = 0; index < count; index++)
        {
            _colorData[checked((int)(start + index))]
                .WriteColorData(format,
                    target.Slice(checked((int)(index * (ulong)bytesPerPixel)), bytesPerPixel)
                );
        }
    }

    private static byte ToUNorm8(float value) =>
        (byte)Math.Round(Math.Clamp(value, 0f, 1f) * byte.MaxValue);
}
