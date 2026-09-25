namespace Nexus.Graphics;

/// <summary>
/// Creates and manages application windows, and provides access to each window by identifier.
/// </summary>
public interface IWindowService
{
    /// <summary>
    /// Gets the identifier of the current main window, or <see cref="WindowId.Invalid"/> if no windows are open.
    /// </summary>
    WindowId MainWindowId { get; }

    /// <summary>
    /// Gets an existing application window.
    /// </summary>
    /// <param name="windowId">The identifier of the window to get.</param>
    /// <returns>The requested Silk.NET <see cref="IWindow"/> instance.</returns>
    /// <exception cref="InvalidOperationException">The specified window does not exist.</exception>
    IWindow GetWindow(WindowId windowId);

    /// <summary>
    /// Gets the current main application window.
    /// </summary>
    /// <returns>The main Silk.NET <see cref="IWindow"/> instance.</returns>
    /// <exception cref="InvalidOperationException">No main window exists.</exception>
    IWindow GetMainWindow();

    /// <summary>
    /// Creates an application window and returns its identifier.
    /// </summary>
    /// <param name="settings">The settings to apply to the new window.</param>
    /// <returns>The identifier of the created window.</returns>
    /// <remarks>The first created window becomes the main window.</remarks>
    WindowId CreateWindow(WindowSettings settings);

    /// <summary>
    /// Closes the specified window, or the main window if no identifier is supplied.
    /// </summary>
    /// <param name="windowId">The identifier of the window to close, or <see langword="null"/> to close the main window.</param>
    void CloseWindow(WindowId? windowId = null);
}
