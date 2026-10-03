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
