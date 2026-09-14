namespace Nexus.Graphics;

/// <summary>
/// Defines the lifecycle and resource access contract for a graphics system.
/// </summary>
public interface IGraphicsSystem
{
    /// <summary>
    /// Gets the manager used to access graphics resources.
    /// </summary>
    IGraphicsResourceManager Resources { get; }

    /// <summary>
    /// Initializes the graphics system before the update loop begins.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Updates the graphics system for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time in seconds since the previous frame.</param>
    void Update(double deltaTime);

    /// <summary>
    /// Renders the current frame.
    /// </summary>
    void Render();
}
