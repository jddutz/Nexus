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
    /// Resets the current state of the GraphicsSystem and loads the specified resources.
    /// </summary>
    ResourceId Load(IResourceDescription resource);

    /// <summary>
    /// Updates the graphics system for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time in seconds since the previous frame.</param>
    void Update(double deltaTime);

    /// <summary>
    /// Renders the current frame.
    /// </summary>
    void Render();

    RenderLayers RenderLayers { get; }

    bool CanActivate<TComponent>(TComponent component)
        where TComponent : class, IComponent;

    bool Activate<TComponent>(TComponent component)
        where TComponent : class, IComponent;

    bool Deactivate<TComponent>(TComponent component)
        where TComponent : class, IComponent;
}
