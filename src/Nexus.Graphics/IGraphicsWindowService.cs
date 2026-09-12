namespace Nexus.Graphics;

public interface IGraphicsWindowService
{
    /// <summary>
    /// Gets the singleton application window instance.
    /// Throws <see cref="InvalidOperationException"/> if the window has not been created.
    /// </summary>
    /// <returns>The initialized Silk.NET <see cref="IWindow"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the window has not been created.</exception>
    IWindow GetWindow();
}
