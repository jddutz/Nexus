namespace Nexus.Graphics.OpenGL;

using Microsoft.Extensions.Logging;

/// <summary>
/// Provides the OpenGL-backed application window.
/// </summary>
public sealed class OpenGLWindowService : IWindowService, IDisposable
{
    private const string WindowUnavailable = "Application Window has not been initialized yet.";
    private readonly ILogger<OpenGLWindowService> _logger;
    private readonly Dictionary<WindowId, IWindow> _windows = new();
    private ulong _nextWindowId = 1;
    private WindowId _mainWindowId = WindowId.Invalid;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenGLWindowService"/> class.
    /// </summary>
    /// <param name="logger">The logger used to report window lifecycle events.</param>
    public OpenGLWindowService(ILogger<OpenGLWindowService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public WindowId MainWindowId => _mainWindowId;

    /// <inheritdoc />
    public IWindow GetWindow(WindowId windowId)
    {
        if (_windows.TryGetValue(windowId, out var window))
            return window;

        _logger.LogDebug("OpenGL window {WindowId} was not found.", windowId.Value);
        throw new InvalidOperationException(WindowUnavailable);
    }

    /// <inheritdoc />
    public IWindow GetMainWindow() => GetWindow(_mainWindowId);

    /// <inheritdoc />
    public WindowId CreateWindow(WindowSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var windowOptions = WindowOptions.Default;
        windowOptions.Title = settings.Title;
        windowOptions.Size = new Vector2D<int>(settings.Width, settings.Height);
        windowOptions.VSync = settings.VSync;
        windowOptions.UpdatesPerSecond = 60;
        var window = Window.Create(windowOptions);
        var windowId = new WindowId(_nextWindowId++);
        _windows.Add(windowId, window);
        var isMainWindow = _mainWindowId == WindowId.Invalid;
        if (isMainWindow)
            _mainWindowId = windowId;

        _logger.LogDebug(
            "Created OpenGL window {WindowId} with title {Title} and size {Width}x{Height}. MainWindow={IsMainWindow}",
            windowId.Value,
            settings.Title,
            settings.Width,
            settings.Height,
            isMainWindow
        );
        return windowId;
    }

    /// <inheritdoc />
    public void CloseWindow(WindowId? windowId = null)
    {
        var id = windowId ?? _mainWindowId;
        if (!_windows.Remove(id, out var window))
        {
            _logger.LogDebug("OpenGL window {WindowId} was not open when close was requested.", id.Value);
            return;
        }

        window.Close();
        window.Dispose();
        if (id == _mainWindowId)
        {
            _mainWindowId = _windows.Keys.FirstOrDefault();
            _logger.LogDebug("Main OpenGL window changed to {WindowId}.", _mainWindowId.Value);
        }
    }

    /// <summary>
    /// Releases the window and its native resources.
    /// </summary>
    public void Dispose()
    {
        _logger.LogDebug("Disposing OpenGL window service with {WindowCount} open windows.", _windows.Count);
        foreach (var window in _windows.Values)
            window.Dispose();

        _windows.Clear();
        _mainWindowId = WindowId.Invalid;
        GC.SuppressFinalize(this);
    }
}
