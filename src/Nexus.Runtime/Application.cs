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

        services.AddOptions<ApplicationSettings>().Bind(configuration.GetSection("Application"));
        services.AddOptions<DiagnosticsSettings>().Bind(configuration.GetSection("Diagnostics"));
        services.AddOptions<OpenGLSettings>().Bind(configuration.GetSection("OpenGL"));
        services.AddOptions<VulkanSettings>().Bind(configuration.GetSection("Vulkan"));
        services.AddOptions<WindowSettings>().Bind(configuration.GetSection("Window"));

        services.AddGameSystemServices();

        services.TryAddSingleton<IInputSystem, InputSystem>();
        services.TryAddSingleton<IPhysicsSystem, PhysicsSystem>();
        services.TryAddSingleton<IAudioSystem, AudioSystem>();
        services.TryAddSingleton<IEventHub, EventHub>();
        services.TryAddSingleton<INexusRuntime, NexusRuntime>();

        if (!services.Any(x => x.ServiceType == typeof(IGraphicsSystem)))
        {
            services.AddVkGraphicsServices();
        }

        services.AddLogging(builder =>
        {
            builder.AddConsole();

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

            var window = windowService.GetOrCreateWindow();
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
