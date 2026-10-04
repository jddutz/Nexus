namespace Nexus.GUI;

/// <summary>
/// Specifies the non-rendered regions along the boundaries of a GUI element.
/// </summary>
/// <remarks>
/// Derived element types use these regions to exclude boundary areas from rendering and from the element's computed bounds.
/// The default value has all four margins set to zero.
/// </remarks>
/// <param name="Left">The width of the region along the left boundary.</param>
/// <param name="Right">The width of the region along the right boundary.</param>
/// <param name="Top">The height of the region along the top boundary.</param>
/// <param name="Bottom">The height of the region along the bottom boundary.</param>
public readonly record struct Margins(float Left, float Right, float Top, float Bottom)
{
    /// <summary>Adds the total horizontal and vertical margins to a size.</summary>
    /// <param name="size">The content size.</param>
    /// <param name="margins">The margins to add.</param>
    /// <returns>The size including the margins.</returns>
    public static Vector2D<float> operator +(Vector2D<float> size, Margins margins) =>
        new(
            size.X + margins.Left + margins.Right,
            size.Y + margins.Top + margins.Bottom
        );

    /// <summary>Adds the total horizontal and vertical margins to a size.</summary>
    /// <param name="margins">The margins to add.</param>
    /// <param name="size">The content size.</param>
    /// <returns>The size including the margins.</returns>
    public static Vector2D<float> operator +(Margins margins, Vector2D<float> size) =>
        size + margins;

    /// <summary>Subtracts the total horizontal and vertical margins from a size.</summary>
    /// <param name="size">The outer size.</param>
    /// <param name="margins">The margins to subtract.</param>
    /// <returns>The size inside the margins.</returns>
    public static Vector2D<float> operator -(Vector2D<float> size, Margins margins) =>
        new(
            size.X - margins.Left - margins.Right,
            size.Y - margins.Top - margins.Bottom
        );

    /// <summary>Expands bounds outward by the margins.</summary>
    /// <param name="bounds">The content bounds.</param>
    /// <param name="margins">The margins to add around the bounds.</param>
    /// <returns>The outer bounds including the margins.</returns>
    public static Rectangle<float> operator +(Rectangle<float> bounds, Margins margins) =>
        new(
            bounds.Origin.X - margins.Left,
            bounds.Origin.Y - margins.Top,
            bounds.Size.X + margins.Left + margins.Right,
            bounds.Size.Y + margins.Top + margins.Bottom
        );

    /// <summary>Insets bounds by the margins.</summary>
    /// <param name="bounds">The outer bounds.</param>
    /// <param name="margins">The margins to remove from the bounds.</param>
    /// <returns>The content bounds inside the margins.</returns>
    public static Rectangle<float> operator -(Rectangle<float> bounds, Margins margins)
    {
        var width = MathF.Max(0f, bounds.Size.X);
        var height = MathF.Max(0f, bounds.Size.Y);
        return new Rectangle<float>(
            bounds.Origin.X + MathF.Min(margins.Left, width),
            bounds.Origin.Y + MathF.Min(margins.Top, height),
            MathF.Max(0f, width - margins.Left - margins.Right),
            MathF.Max(0f, height - margins.Top - margins.Bottom)
        );
    }

    /// <summary>Initializes all four boundary regions to the same size.</summary>
    /// <param name="all">The size applied to every boundary.</param>
    public Margins(float all)
        : this(all, all, all, all) { }

    /// <summary>Initializes horizontal and vertical boundary regions independently.</summary>
    /// <param name="horizontal">The size applied to the left and right boundaries.</param>
    /// <param name="vertical">The size applied to the top and bottom boundaries.</param>
    public Margins(float horizontal, float vertical)
        : this(horizontal, horizontal, vertical, vertical) { }
}
