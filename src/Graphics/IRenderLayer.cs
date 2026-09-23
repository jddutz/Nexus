namespace Nexus.Graphics;

/// <summary>
/// Defines the data contract for a render layer.
/// </summary>
public interface IRenderLayer
{
    /// <summary>
    /// Gets the zero-based index of the render layer.
    /// </summary>
    int Index { get; }

    /// <summary>
    /// Gets the name of the render layer.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets or sets the render-pass mask for the render layer.
    /// </summary>
    uint RenderPassMask { get; set; }
}
