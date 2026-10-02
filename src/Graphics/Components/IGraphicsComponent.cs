namespace Nexus.Graphics.Components;

/// <summary>
/// A component that exposes drawables to the graphics system and reports
/// when drawables are added or removed.
/// </summary>
public interface IGraphicsComponent : IComponent
{
    /// <summary>Occurs when a drawable is added to this component.</summary>
    event EventHandler<DrawableEventArgs>? DrawableAdded;

    /// <summary>Occurs when a drawable is removed from this component.</summary>
    event EventHandler<DrawableEventArgs>? DrawableRemoved;

    /// <summary>Gets the drawables currently exposed by this component.</summary>
    IReadOnlyList<IDrawable> Drawables { get; }
}
