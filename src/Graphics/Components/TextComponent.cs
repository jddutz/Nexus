namespace Nexus.Graphics.Components;

/// <summary>
/// Renders a texture-mapped quad with a per-instance transformation matrix, source rectangle, and tint color.
/// </summary>
public class TextComponent : Component, IGraphicsComponent
{
    public IReadOnlyList<IDrawable> Drawables => throw new NotImplementedException();
}
