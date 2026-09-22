namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Provides the Vulkan-backed application window.
/// </summary>
public sealed class VulkanWindowService : IWindowService, IDisposable
{
    private const string WindowUnavailable = "Application Window has not been initialized yet.";
    private readonly IOptions<WindowSettings> _options;
    private IWindow? _window;

    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanWindowService"/> class.
    /// </summary>
    /// <param name="options">The application window settings.</param>
    public VulkanWindowService(IOptions<WindowSettings> options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public IWindow GetWindow() =>
        _window ?? throw new InvalidOperationException(WindowUnavailable);

    /// <inheritdoc />
    public IWindow GetOrCreateWindow()
    {
        if (_window is null)
        {
            var windowOptions = WindowOptions.DefaultVulkan;
            windowOptions.Title = _options.Value.Title;
            windowOptions.Size = new Vector2D<int>(_options.Value.Width, _options.Value.Height);
            windowOptions.VSync = _options.Value.VSync;
            windowOptions.UpdatesPerSecond = 60;
            _window = Window.Create(windowOptions);
        }

        return _window;
    }

    /// <inheritdoc />
    public void CloseWindow() => _window?.Close();

    /// <summary>
    /// Releases the window and its native resources.
    /// </summary>
    public void Dispose()
    {
        _window?.Dispose();
        _window = null;
        GC.SuppressFinalize(this);
    }
}