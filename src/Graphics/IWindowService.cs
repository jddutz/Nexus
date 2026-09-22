namespace Nexus.Graphics;

/// <summary>
/// Provides access to the primary application window and manages its lifecycle.
/// </summary>
public interface IWindowService
{
    /// <summary>
    /// Gets the application window, if it exists, throwing if it has not been created yet.
    /// </summary>
    /// <returns>The Silk.NET <see cref="IWindow"/> instance.</returns>
    IWindow GetWindow();

    /// <summary>
    /// Gets the singleton application window, creating it if it does not already exist.
    /// </summary>
    /// <returns>The Silk.NET <see cref="IWindow"/> instance.</returns>
    IWindow GetOrCreateWindow();

    /// <summary>
    /// Closes the main application window, closing all child windows as well.
    /// </summary>
    void CloseWindow();
}