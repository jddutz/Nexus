namespace Nexus.Graphics;

/// <summary>
/// Describes a render layer and the rendering state used for its contents.
/// </summary>
/// <param name="index">The zero-based index of the render layer.</param>
/// <param name="name">The name of the render layer.</param>
/// <param name="renderPassMask">The render-pass mask for the render layer.</param>
/// <param name="preserveDrawOrder">Whether draw order is preserved.</param>
/// <param name="blendMode">The blending mode used by the layer.</param>
/// <param name="enableDepthTest">Whether depth testing is enabled.</param>
/// <param name="enableDepthWrite">Whether depth writes are enabled.</param>
/// <param name="depthComparison">The depth comparison operation.</param>
public readonly record struct RenderLayer(
    int index,
    string name,
    uint renderPassMask,
    bool preserveDrawOrder = false,
    BlendMode blendMode = BlendMode.Opaque,
    bool enableDepthTest = true,
    bool enableDepthWrite = true,
    DepthComparison depthComparison = DepthComparison.Less
)
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

    /// <summary>
    /// Gets a value indicating whether draw order is preserved.
    /// </summary>
    public bool PreserveDrawOrder { get; } = preserveDrawOrder;

    /// <summary>
    /// Gets the blending mode used by the layer.
    /// </summary>
    public BlendMode BlendMode { get; } = blendMode;

    /// <summary>
    /// Gets a value indicating whether depth testing is enabled.
    /// </summary>
    public bool EnableDepthTest { get; } = enableDepthTest;

    /// <summary>
    /// Gets a value indicating whether depth writes are enabled.
    /// </summary>
    public bool EnableDepthWrite { get; } = enableDepthWrite;

    /// <summary>
    /// Gets the depth comparison operation.
    /// </summary>
    public DepthComparison DepthComparison { get; } = depthComparison;
}
