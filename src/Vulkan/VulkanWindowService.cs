namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Provides the Vulkan-backed application window.
/// </summary>
public sealed class VulkanWindowService : IWindowService, IDisposable
{
    private const string WindowUnavailable = "Application Window has not been initialized yet.";
    private readonly Dictionary<WindowId, IWindow> _windows = new();
    private ulong _nextWindowId = 1;
    private WindowId _mainWindowId = WindowId.Invalid;

    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanWindowService"/> class.
    /// </summary>
    public VulkanWindowService() { }

    /// <inheritdoc />
    public WindowId MainWindowId => _mainWindowId;

    /// <inheritdoc />
    public IWindow GetWindow(WindowId windowId)
    {
        return _windows.TryGetValue(windowId, out var window)
            ? window
            : throw new InvalidOperationException(WindowUnavailable);
    }

    /// <inheritdoc />
    public IWindow GetMainWindow() => GetWindow(_mainWindowId);

    /// <inheritdoc />
    public WindowId CreateWindow(WindowSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var windowOptions = WindowOptions.DefaultVulkan;
        windowOptions.Title = settings.Title;
        windowOptions.Size = new Vector2D<int>(settings.Width, settings.Height);
        windowOptions.VSync = settings.VSync;
        windowOptions.UpdatesPerSecond = 60;
        var window = Window.Create(windowOptions);
        var windowId = new WindowId(_nextWindowId++);
        _windows.Add(windowId, window);
        if (_mainWindowId == WindowId.Invalid)
            _mainWindowId = windowId;

        return windowId;
    }

    /// <inheritdoc />
    public void CloseWindow(WindowId? windowId = null)
    {
        var id = windowId ?? _mainWindowId;
        if (!_windows.Remove(id, out var window))
            return;

        window.Close();
        window.Dispose();
        if (id == _mainWindowId)
            _mainWindowId = _windows.Keys.FirstOrDefault();
    }

    /// <summary>
    /// Releases the window and its native resources.
    /// </summary>
    public void Dispose()
    {
        foreach (var window in _windows.Values)
            window.Dispose();

        _windows.Clear();
        _mainWindowId = WindowId.Invalid;
        GC.SuppressFinalize(this);
    }
}
