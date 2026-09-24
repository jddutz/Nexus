namespace Nexus.AssetPipeline.Typography.DistanceFields;

/// <summary>
/// Contains one glyph's row-major RGB8 distance-field pixels.
/// </summary>
public sealed class GlyphBitmap
{
    /// <summary>
    /// Initializes a glyph bitmap from row-major RGB8 pixel data.
    /// </summary>
    /// <param name="width">The bitmap width in pixels.</param>
    /// <param name="height">The bitmap height in pixels.</param>
    /// <param name="pixels">The RGB8 pixel data, with three bytes per pixel.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is negative.</exception>
    /// <exception cref="ArgumentException">The pixel data does not match the dimensions.</exception>
    public GlyphBitmap(int width, int height, byte[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length != checked(width * height * 3))
            throw new ArgumentException(
                "RGB8 data must contain three bytes per pixel.",
                nameof(pixels)
            );

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    /// <summary>Gets the bitmap width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the bitmap height in pixels.</summary>
    public int Height { get; }

    /// <summary>Gets row-major RGB8 pixel data, with three bytes per pixel.</summary>
    public byte[] Pixels { get; }
}
