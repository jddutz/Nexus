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
        var configuration = _configuration ?? new ConfigurationBuilder().Build();
        _services.TryAddSingleton(configuration);
        _services.AddNexusServices(configuration);

        var serviceProvider = _services.BuildServiceProvider();

        return serviceProvider.GetRequiredService<INexusRuntime>();
    }
}
