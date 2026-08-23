using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Nexus.Runtime;

/// <summary>
/// Builds a configured Nexus runtime from application services and configuration.
/// </summary>
public class RuntimeBuilder : IRuntimeBuilder
{
    protected IServiceCollection Services { get; }

    protected IConfiguration Configuration { get; }

    public RuntimeBuilder(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        Services = services;
        Configuration = configuration;
    }

    /// <summary>
    /// Builds the runtime using the configured application services
    /// and the default Nexus platform services.
    /// </summary>
    public virtual IRuntime Build()
    {
        ConfigureServices();

        var serviceProvider = Services.BuildServiceProvider();

        return new NexusRuntime(serviceProvider);
    }

    /// <summary>
    /// Configures the services required by the standard Nexus runtime.
    /// </summary>
    protected virtual void ConfigureServices()
    {
        AddConfiguration();
        AddCoreServices();
    }

    /// <summary>
    /// Makes application configuration available through dependency injection.
    /// </summary>
    protected virtual void AddConfiguration()
    {
        Services.AddSingleton(Configuration);
    }

    /// <summary>
    /// Adds the services required by the standard Nexus runtime.
    /// </summary>
    protected virtual void AddCoreServices()
    {
        Services.AddSingleton<IWindowService, WindowService>();

        // Timing
        // Content
        // Scene graph
        // Graphics
        // Windowing
        // Performance
        // Runtime lifecycle
        // etc.
    }
}
