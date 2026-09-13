namespace Nexus.Runtime;

/// <summary>
/// Defines a builder capable of constructing a configured Nexus runtime.
/// </summary>
public class RuntimeBuilder : IRuntimeBuilder
{
    private readonly IServiceCollection _services;
    private IConfiguration? _configuration;

    public RuntimeBuilder()
    {
        _services = new ServiceCollection();
    }

    public IRuntimeBuilder AddServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (var service in services)
        {
            _services.Add(service);
        }

        return this;
    }

    public IRuntimeBuilder AddConfiguration(IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _configuration = config;
        return this;
    }

    public IRuntimeBuilder UseVulkan() => this;

    public IRuntimeBuilder UseOpenGL() => throw new NotImplementedException();

    /// <inheritdoc/>
    public INexusRuntime Build()
    {
        _services.TryAddSingleton(_configuration ?? new ConfigurationBuilder().Build());
        _services.TryAddSingleton<IInputSystem, InputSystem>();
        _services.TryAddSingleton<IGameSystem, GameSystem>();
        _services.TryAddSingleton<IPhysicsSystem, PhysicsSystem>();
        _services.TryAddSingleton<IGraphicsSystem, VulkanGraphicsSystem>();
        _services.TryAddSingleton<IAudioSystem, AudioSystem>();
        _services.TryAddSingleton<INexusRuntime, NexusRuntime>();

        var serviceProvider = _services.BuildServiceProvider();

        return serviceProvider.GetRequiredService<INexusRuntime>();
    }
}
