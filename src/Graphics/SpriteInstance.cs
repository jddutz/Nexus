namespace Nexus.Graphics;

/// <summary>An editable quad; anchor (0, 0) is the local minimum corner.</summary>
public partial class SpriteInstance : IObservable
{
    /// <inheritdoc />
    public event Action<string>? PropertyChanged;
    [Observable(PublicSetter = true)]
    private Matrix4X4<float> _transform = Matrix4X4<float>.Identity;
    [Observable(PublicSetter = true)]
    private Vector2D<float> _size = new(1f, 1f);
    [Observable(PublicSetter = true)]
    private Vector2D<float> _anchor;
    [Observable(PublicSetter = true)]
    private Vector4D<float> _texCoord = new(0f, 0f, 1f, 1f);
    [Observable(PublicSetter = true)]
    private Color _color = Colors.White;
    [Observable(PublicSetter = true)]
    private bool _isVisible = true;
}
