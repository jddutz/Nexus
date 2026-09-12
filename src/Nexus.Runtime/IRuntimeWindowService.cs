namespace Nexus.Runtime;

/// <summary>
/// Provides access to the primary application window and manages its lifecycle.
/// </summary>
public interface IRuntimeWindowService
{
    /// <summary>
    /// Gets the singleton application window, creating it if it does not already exist.
    /// </summary>
    /// <param name="options">The Silk.NET window options to use for creation.</param>
    /// <returns>The Silk.NET <see cref="IWindow"/> instance.</returns>
    IWindow GetOrCreateWindow();

    /// <summary>
    /// Closes the main application window, closing all child windows as well.
    /// </summary>
    void CloseWindow();
}
