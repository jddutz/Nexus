using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Nexus.Testing;

/// <summary>
/// Builds a minimal Nexus runtime for testing without registering
/// the standard engine subsystems.
/// </summary>
public class TestRuntimeBuilder : IRuntimeBuilder
{
    protected IServiceCollection Services { get; }
    protected IConfiguration? Configuration { get; private set; }

    public TestRuntimeBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Services = services;
    }

    /// <summary>
    /// Builds a minimal runtime using only the services explicitly
    /// registered for the test environment.
    /// </summary>
    public virtual INexusRuntime Build()
    {
        Services.TryAddSingleton(Configuration ?? new ConfigurationBuilder().Build());
        Services.TryAddSingleton<IEventHub, EventHub>();
        Services.TryAddSingleton<INexusRuntime, NexusRuntime>();

        var serviceProvider = Services.BuildServiceProvider();

        return serviceProvider.GetRequiredService<INexusRuntime>();
    }

    public IRuntimeBuilder AddServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (var service in services)
        {
            Services.Add(service);
        }

        return this;
    }

    public IRuntimeBuilder AddConfiguration(IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Configuration = config;
        return this;
    }

    public IRuntimeBuilder UseVulkan() => this;

    public IRuntimeBuilder UseOpenGL() => throw new NotImplementedException();
}
