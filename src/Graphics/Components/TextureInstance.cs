namespace Nexus.Graphics.Components;

/// <summary>Describes one textured-quad instance and its normalized source rectangle.</summary>
public readonly record struct TextureInstance
{
    /// <summary>Gets the destination quad's left edge in component-local coordinates.</summary>
    public float X { get; }

    /// <summary>Gets the destination quad's top edge in component-local coordinates.</summary>
    public float Y { get; }

    /// <summary>Gets the positive destination quad width.</summary>
    public float Width { get; }

    /// <summary>Gets the positive destination quad height.</summary>
    public float Height { get; }

    /// <summary>Gets the normalized source rectangle.</summary>
    public Vector4D<float> TexCoord { get; }

    /// <summary>Gets whether to inset the source rectangle's left edge to a texel center.</summary>
    public bool InsetLeft { get; }

    /// <summary>Gets whether to inset the source rectangle's top edge to a texel center.</summary>
    public bool InsetTop { get; }

    /// <summary>Gets whether to inset the source rectangle's right edge to a texel center.</summary>
    public bool InsetRight { get; }

    /// <summary>Gets whether to inset the source rectangle's bottom edge to a texel center.</summary>
    public bool InsetBottom { get; }

    /// <summary>Initializes one instance and its source-edge inset policy.</summary>
    /// <param name="x">The destination quad's left edge.</param>
    /// <param name="y">The destination quad's top edge.</param>
    /// <param name="width">The destination quad width.</param>
    /// <param name="height">The destination quad height.</param>
    /// <param name="texCoord">The normalized source rectangle.</param>
    /// <param name="insetLeft">Whether to inset the selected source's left edge.</param>
    /// <param name="insetTop">Whether to inset the selected source's top edge.</param>
    /// <param name="insetRight">Whether to inset the selected source's right edge.</param>
    /// <param name="insetBottom">Whether to inset the selected source's bottom edge.</param>
    public TextureInstance(
        float x,
        float y,
        float width,
        float height,
        Vector4D<float> texCoord,
        bool insetLeft,
        bool insetTop,
        bool insetRight,
        bool insetBottom
    )
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        TexCoord = texCoord;
        InsetLeft = insetLeft;
        InsetTop = insetTop;
        InsetRight = insetRight;
        InsetBottom = insetBottom;
    }
}