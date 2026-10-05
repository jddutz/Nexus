namespace Nexus.Samples.Breakout;

using Nexus.GUI.Elements;
using Nexus.GUI;
using Nexus.Graphics.Textures;

/// <summary>Renderable scene child representing one live Breakout brick.</summary>
public sealed class BreakoutBrickElement : ImageElement
{
    /// <summary>Gets the logical brick index represented by this element.</summary>
    public int BrickIndex { get; }

    /// <summary>Creates a solid-color brick element.</summary>
    /// <param name="brickIndex">The zero-based brick index.</param>
    /// <param name="color">The brick tint.</param>
    public BreakoutBrickElement(int brickIndex, Nexus.Graphics.Color color)
    {
        BrickIndex = brickIndex;
        Texture = BuiltInTextures.Uniform;
        SizingMode = ImageSizingMode.Stretch;
        Color = color;
        HorizontalAlignment = AlignHorizontal.Left;
        VerticalAlignment = AlignVertical.Top;
    }
}
