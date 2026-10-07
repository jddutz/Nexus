namespace Nexus.Graphics.Textures;

/// <summary>
/// Source for loading texture data.
/// Implementations handle different texture formats and loading mechanisms.
/// </summary>
public interface ITexture
{
    /// <summary>
    /// Gets the unique identifier of this texture.
    /// </summary>
    TextureId Id { get; }

    /// <summary>
    /// Gets the content identifier associated with this texture.
    /// </summary>
    public ContentId ContentId { get; }

    /// <summary>
    /// Gets the width of the texture in pixels.
    /// </summary>
    public uint Width { get; }

    /// <summary>
    /// Gets the height of the texture in pixels.
    /// </summary>
    public uint Height { get; }

    /// <summary>
    /// Gets the number of pixels in the texture.
    /// </summary>
    ulong Count { get; }

    /// <summary>Gets the pixel format used to store and sample this texture.</summary>
    ColorFormatEnum TextureFormat { get; }

    /// <summary>Gets the atlas regions, or an empty list for a standalone texture.</summary>
    IReadOnlyList<TextureRegion> Regions { get; }

    /// <summary>Gets an atlas region by its case-sensitive name.</summary>
    /// <exception cref="KeyNotFoundException">The region does not exist.</exception>
    TextureRegion GetRegion(string name);

    /// <summary>
    /// Writes a range of pixels to a byte buffer using the specified format.
    /// </summary>
    /// <param name="start">The zero-based index of the first pixel to write.</param>
    /// <param name="count">The number of pixels to write.</param>
    /// <param name="format">The format used to serialize each pixel.</param>
    /// <param name="target">The destination buffer for the serialized pixels. It must have space for at least <paramref name="count"/> pixels in <paramref name="format"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the requested pixel range exceeds the texture bounds or <paramref name="format"/> is unsupported.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="target"/> does not have enough space for the serialized pixels.</exception>
    void WriteTo(ulong start, ulong count, ColorFormatEnum format, Span<byte> target);
}
