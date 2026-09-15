namespace Nexus.Graphics;

/// <summary>
/// Defines the lifecycle and resource access contract for a graphics system.
/// </summary>
public interface IGraphicsSystem
{
    /// <summary>
    /// Initializes the graphics system before the update loop begins.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Updates the graphics system for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time in seconds since the previous frame.</param>
    //void Update(double deltaTime);

    /// <summary>
    /// Renders the current frame.
    /// </summary>
    void Render();

    /// <summary>
    /// Gets the render layers prepared by the graphics system.
    /// </summary>
    RenderLayers RenderLayers { get; }

    /// <summary>
    /// Activates a component in the graphics system.
    /// </summary>
    /// <typeparam name="TComponent">The type of component to activate.</typeparam>
    /// <param name="component">The component to activate.</param>
    /// <returns><see langword="true"/> when the component was activated successfully; otherwise, <see langword="false"/>.</returns>
    bool Activate<TComponent>(TComponent component)
        where TComponent : class, IGraphicsComponent;

    /// <summary>
    /// Deactivates a component in the graphics system.
    /// </summary>
    /// <typeparam name="TComponent">The type of component to deactivate.</typeparam>
    /// <param name="component">The component to deactivate.</param>
    /// <returns><see langword="true"/> when the component was deactivated successfully; otherwise, <see langword="false"/>.</returns>
    void Deactivate<TComponent>(TComponent component)
        where TComponent : class, IGraphicsComponent;
}
