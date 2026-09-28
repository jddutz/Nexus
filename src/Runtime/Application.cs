namespace Nexus.Runtime;

/// <summary>
/// Implements the main application entry point for the Nexus Game Engine runtime.
/// </summary>
public sealed class Application : IApplication, IDisposable
{
    public static ServiceProvider _serviceProvider = null!;
    public static IServiceProvider Services => _serviceProvider;
    private readonly ILogger<Application> _logger;
    private bool _disposed;

    public Application(IConfiguration configuration, IServiceCollection? services = null)
    {
        services ??= new ServiceCollection();

        services.AddNexusServices(configuration);

        services.AddLogging(builder =>
        {
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.AddConsole();
            builder.AddDebug();

            var vulkanSettings =
                configuration.GetSection("Vulkan").Get<VulkanSettings>() ?? new VulkanSettings();
            if (vulkanSettings.EnableValidationLayers)
            {
                builder.AddFilter(typeof(Validation).FullName, LogLevel.Debug);
            }
        });
        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<Application>>();
    }

    /// <inheritdoc />
    public void Run()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            var windowService = Services.GetRequiredService<IWindowService>();

            var windowSettings = Services.GetRequiredService<IOptions<WindowSettings>>().Value;
            windowService.CreateWindow(windowSettings);

            var window = windowService.GetMainWindow();
            window.Initialize();

            var runtime = Services.GetRequiredService<INexusRuntime>();
            runtime.Initialize();

            window.Run();
        }
        catch (Exception exception)
        {
            _logger.LogCritical(exception, "Application execution failed.");
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _serviceProvider.Dispose();
        _disposed = true;
    }
}
