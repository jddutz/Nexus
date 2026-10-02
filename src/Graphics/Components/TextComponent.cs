namespace Nexus.Graphics.Components;

/// <summary>
/// Groups text spans that share the component's lifetime.
/// </summary>
public partial class TextComponent : Component, IGraphicsComponent
{
    /// <summary>Gets or sets normalized horizontal and vertical alignment within the bounds.</summary>
    [Observable(PublicSetter = true)]
    private Vector2D<float> _alignment = Vector2D<float>.Zero;

    /// <summary>Validates normalized horizontal and vertical alignment values.</summary>
    /// <param name="value">The proposed normalized alignment.</param>
    private void BeforeAlignmentChanges(Vector2D<float> value)
    {
        if (
            !float.IsFinite(value.X)
            || !float.IsFinite(value.Y)
            || value.X is < 0f or > 1f
            || value.Y is < 0f or > 1f
        )
            throw new ArgumentOutOfRangeException(nameof(value));
    }

    public IReadOnlyList<IDrawable> Drawables => throw new NotImplementedException();

    // Required by IGraphicsComponent; drawable collection synchronization is not implemented yet.
#pragma warning disable CS0067
    public event EventHandler<DrawableEventArgs>? DrawableAdded;
    public event EventHandler<DrawableEventArgs>? DrawableRemoved;
#pragma warning restore CS0067
}
