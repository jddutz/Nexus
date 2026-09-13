namespace Nexus.Graphics;

public interface IGraphicsSystem
{
    /// <summary>
    /// Initializes the graphics system before the update loop begins.
    /// </summary>
    void Initialize();
    void Configure();
    void Render();
}
