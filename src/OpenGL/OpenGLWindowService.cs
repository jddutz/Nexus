namespace Nexus.Graphics.OpenGL;

using System.Diagnostics;

/// <summary>
/// Provides the OpenGL-backed application window.
/// </summary>
public sealed class OpenGLWindowService : IWindowService, IDisposable
{
    private const string WindowUnavailable = "Application Window has not been initialized yet.";
    private readonly Dictionary<WindowId, IWindow> _windows = new();
    private ulong _nextWindowId = 1;
    private WindowId _mainWindowId = WindowId.Invalid;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenGLWindowService"/> class.
    /// </summary>
    public OpenGLWindowService() { }

    /// <inheritdoc />
    public WindowId MainWindowId => _mainWindowId;

    /// <inheritdoc />
    public IWindow GetWindow(WindowId windowId)
    {
        if (_windows.TryGetValue(windowId, out var window))
            return window;

        Debug.WriteLine($"OpenGL window {windowId.Value} was not found.");
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

        Debug.WriteLine(
            $"Created OpenGL window {windowId.Value} with title {settings.Title} and size {settings.Width}x{settings.Height}. MainWindow={isMainWindow}"
        );
        return windowId;
    }

    /// <inheritdoc />
    public void CloseWindow(WindowId? windowId = null)
    {
        var id = windowId ?? _mainWindowId;
        if (!_windows.Remove(id, out var window))
        {
            Debug.WriteLine($"OpenGL window {id.Value} was not open when close was requested.");
            return;
        }

        window.Close();
        window.Dispose();
        if (id == _mainWindowId)
        {
            _mainWindowId = _windows.Keys.FirstOrDefault();
            Debug.WriteLine($"Main OpenGL window changed to {_mainWindowId.Value}.");
        }
    }

    /// <summary>
    /// Releases the window and its native resources.
    /// </summary>
    public void Dispose()
    {
        Debug.WriteLine($"Disposing OpenGL window service with {_windows.Count} open windows.");
        foreach (var window in _windows.Values)
            window.Dispose();

        _windows.Clear();
        _mainWindowId = WindowId.Invalid;
        GC.SuppressFinalize(this);
    }
}
