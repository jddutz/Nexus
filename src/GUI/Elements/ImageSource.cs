namespace Nexus.GUI.Elements;

/// <summary>Identifies a texture and the pixel rectangle containing an image within it.</summary>
public sealed class ImageSource
{
    /// <summary>Gets the texture containing the image.</summary>
    public Texture Texture { get; }

    /// <summary>Gets the image rectangle in top-left-origin texture pixels.</summary>
    public Rectangle<int> SourceRegion { get; }

    /// <summary>Initializes an image source from a texture and an optional pixel rectangle.</summary>
    /// <param name="texture">The texture containing the image.</param>
    /// <param name="sourceRegion">The image rectangle, or the full texture when omitted.</param>
    public ImageSource(Texture texture, Rectangle<int>? sourceRegion = null)
    {
        ArgumentNullException.ThrowIfNull(texture);
        if (
            texture.Width == 0
            || texture.Height == 0
            || texture.Width > int.MaxValue
            || texture.Height > int.MaxValue
        )
            throw new ArgumentOutOfRangeException(
                nameof(texture),
                "Texture dimensions must be positive and representable as pixel coordinates."
            );

        Texture = texture;
        SourceRegion =
            sourceRegion ?? new Rectangle<int>(0, 0, (int)texture.Width, (int)texture.Height);
        ValidateSourceRegion(SourceRegion, texture);
    }

    /// <summary>Validates that a pixel rectangle is positive and contained by its texture.</summary>
    /// <param name="sourceRegion">The proposed image rectangle.</param>
    /// <param name="texture">The texture containing the rectangle.</param>
    private static void ValidateSourceRegion(Rectangle<int> sourceRegion, Texture texture)
    {
        var origin = sourceRegion.Origin;
        var size = sourceRegion.Size;
        if (
            origin.X < 0
            || origin.Y < 0
            || size.X <= 0
            || size.Y <= 0
            || (long)origin.X + size.X > texture.Width
            || (long)origin.Y + size.Y > texture.Height
        )
            throw new ArgumentOutOfRangeException(
                nameof(sourceRegion),
                "The source rectangle must have positive dimensions and fit inside the texture."
            );
    }
}
