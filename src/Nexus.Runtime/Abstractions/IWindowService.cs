namespace Nexus.Runtime.Abstractions;

/// <summary>
/// Provides access to the primary application window and manages its lifecycle.
/// </summary>
public interface IWindowService
{
    /// <summary>
    /// Gets the primary application window.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the window has not been created.
    /// </exception>
    IWindow GetWindow();

    /// <summary>
    /// Gets the primary application window, creating it from the configured
    /// window settings if necessary.
    /// </summary>
    IWindow GetOrCreateWindow();
}
