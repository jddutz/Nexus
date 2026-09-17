namespace Nexus.Runtime;

/// <summary>
/// Provides a singleton factory for the Silk.NET application window.
/// Handles window creation, access, and disposal for the application lifecycle.
/// </summary>
public class OpenGLWindowService : IWindowService, IDisposable
{
    private const string WINDOW_UNAVAILABLE = "Application Window has not been initialized yet.";
    private readonly WindowSettings _settings;

    public OpenGLWindowService(IOptions<WindowSettings> options)
    {
        _settings = options.Value;
    }

    private IWindow? _window;

    /// <summary>
    /// Gets the singleton application window instance.
    /// Throws <see cref="InvalidOperationException"/> if the window has not been created.
    /// </summary>
    /// <returns>The initialized Silk.NET <see cref="IWindow"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the window has not been created.</exception>
    public IWindow GetWindow() =>
        _window ?? throw new InvalidOperationException(WINDOW_UNAVAILABLE);

    /// <summary>
    /// Gets the singleton application window, creating it if it does not already exist.
    /// </summary>
    /// <param name="options">The Silk.NET window options to use for creation.</param>
    /// <returns>The Silk.NET <see cref="IWindow"/> instance.</returns>
    public IWindow GetOrCreateWindow()
    {
        if (_window is null)
        {
            var windowOptions = WindowOptions.Default;
            windowOptions.Title = _settings.Title;
            windowOptions.Size = new Vector2D<int>(_settings.Width, _settings.Height);
            windowOptions.VSync = _settings.VSync;
            windowOptions.UpdatesPerSecond = 60;

            _window = Window.Create(windowOptions);
        }

        return _window!;
    }

    public void CloseWindow()
    {
        _window?.Close();
    }

    /// <summary>
    /// Disposes the window service and the underlying Silk.NET window instance.
    /// Calls <see cref="GC.SuppressFinalize(object)"/> to prevent finalization if derived types introduce a finalizer.
    /// </summary>
    public void Dispose()
    {
        _window?.Dispose();
        _window = null;
        GC.SuppressFinalize(this);
    }
}
