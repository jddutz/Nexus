namespace Nexus.Graphics;

/// <summary>
/// Defines the lifecycle and resource access contract for a graphics system.
/// </summary>
public interface IGraphicsSystem
{
    /// <summary>
    /// Gets the manager used to access graphics resources.
    /// </summary>
    IGraphicsResourceManager ResourceManager { get; }

    /// <summary>
    /// Initializes the graphics system before the update loop begins.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Renders the current frame.
    /// </summary>
    void Render();
}
