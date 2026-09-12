namespace Nexus.Runtime;

/// <summary>
/// Implements the main application entry point for the Nexus Game Engine runtime.
/// </summary>
public sealed class Application : IApplication, IDisposable
{
    public static ServiceProvider _serviceProvider = null!;
    public static IServiceProvider Services => _serviceProvider;
    private bool _disposed;

    public Application(IConfiguration configuration, IServiceCollection? services = null)
    {
        services ??= new ServiceCollection();

        services.AddOptions<WindowSettings>();

        services.TryAddSingleton<IRuntimeWindowService, WindowService>();
        services.TryAddSingleton<INexusRuntime, NexusRuntime>();

        _serviceProvider = services.BuildServiceProvider();
    }

    /// <inheritdoc />
    public void Run()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var runtime = Services.GetRequiredService<INexusRuntime>();
        var windowService = Services.GetRequiredService<IRuntimeWindowService>();

        runtime.Initialize();

        var window = windowService.GetOrCreateWindow();

        window.Run();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _serviceProvider.Dispose();
        _disposed = true;
    }
}
