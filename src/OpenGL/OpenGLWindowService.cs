namespace Nexus.Graphics.OpenGL;

using System.Diagnostics;
using Nexus.Core.Events;
using Nexus.Graphics.Events;

/// <summary>
/// Provides the OpenGL-backed application window.
/// </summary>
public sealed class OpenGLWindowService : IWindowService, IDisposable
{
    private const string WindowUnavailable = "Application Window has not been initialized yet.";
    private readonly Dictionary<WindowId, IWindow> _windows = new();
    private readonly Dictionary<WindowId, Vector2D<float>> _dpiScales = new();
    private readonly IEventHub _eventHub;
    private ulong _nextWindowId = 1;
    private WindowId _mainWindowId = WindowId.Invalid;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenGLWindowService"/> class.
    /// </summary>
    /// <param name="eventHub">The event hub used to publish window events.</param>
    public OpenGLWindowService(IEventHub eventHub)
    {
        ArgumentNullException.ThrowIfNull(eventHub);
        _eventHub = eventHub;
    }

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
        _dpiScales.Add(windowId, new Vector2D<float>(1f, 1f));
        window.Resize += size => _eventHub.Publish(new WindowResizedEvent(windowId, size));
        window.FramebufferResize += framebufferSize =>
            PublishDpiChanged(windowId, window, framebufferSize);
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

        _dpiScales.Remove(id);
        window.Close();
        window.Dispose();
        if (id == _mainWindowId)
        {
            _mainWindowId = _windows.Keys.FirstOrDefault();
            Debug.WriteLine($"Main OpenGL window changed to {_mainWindowId.Value}.");
        }
    }

    /// <summary>
    /// Publishes a DPI change when the framebuffer-to-window scale has changed.
    /// </summary>
    /// <param name="windowId">The identifier of the affected window.</param>
    /// <param name="window">The window whose DPI scale changed.</param>
    /// <param name="framebufferSize">The new framebuffer size in pixels.</param>
    private void PublishDpiChanged(WindowId windowId, IWindow window, Vector2D<int> framebufferSize)
    {
        var windowSize = window.Size;
        if (
            windowSize.X <= 0
            || windowSize.Y <= 0
            || framebufferSize.X <= 0
            || framebufferSize.Y <= 0
        )
            return;

        var scale = new Vector2D<float>(
            MathF.Round(framebufferSize.X / (float)windowSize.X, 2),
            MathF.Round(framebufferSize.Y / (float)windowSize.Y, 2)
        );
        if (_dpiScales.TryGetValue(windowId, out var previousScale) && previousScale == scale)
            return;

        _dpiScales[windowId] = scale;
        _eventHub.Publish(new WindowDpiChangedEvent(windowId, scale));
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
        _dpiScales.Clear();
        _mainWindowId = WindowId.Invalid;
        GC.SuppressFinalize(this);
    }
}
