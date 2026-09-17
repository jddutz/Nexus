namespace Nexus.Graphics.Components;

/// <summary>
/// Marks a component that the graphics system can activate and deactivate. Carries no members
/// of its own; <see cref="IRenderableComponent"/> and camera components are the concrete
/// specializations the graphics system knows how to handle.
/// </summary>
public interface IGraphicsComponent : IComponent { }
