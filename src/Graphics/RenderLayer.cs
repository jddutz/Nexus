namespace Nexus.Graphics;

/// <summary>
/// Identifies a category of drawable contents and the render passes it participates in.
/// </summary>
/// <param name="index">The zero-based index of the render layer.</param>
/// <param name="name">The name of the render layer.</param>
/// <param name="renderPassMask">The render-pass mask for the render layer.</param>
public readonly record struct RenderLayer(int index, string name, uint renderPassMask)
{
    /// <summary>
    /// Gets the default GUI render layer.
    /// </summary>
    public static RenderLayer DefaultGui { get; } = new(0, "GUI", RenderPasses.Main);

    /// <summary>
    /// Gets the zero-based index of the render layer.
    /// </summary>
    public int Index { get; } = index;

    /// <summary>
    /// Gets the name of the render layer.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the render-pass mask for the render layer.
    /// </summary>
    public uint RenderPassMask { get; } = renderPassMask;
}
