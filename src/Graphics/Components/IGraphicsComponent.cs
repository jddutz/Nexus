namespace Nexus.Graphics.Components;

/// <summary>
/// Marks a component that the graphics system can activate and deactivate. Carries no members
/// of its own; <see cref="IDrawable"/> and camera components are the concrete
/// specializations the graphics system knows how to handle.
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
