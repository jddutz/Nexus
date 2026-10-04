namespace Nexus.Runtime;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Registers and composes services for the Nexus runtime.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the runtime service and its application settings.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddRuntimeServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ApplicationSettings>().Bind(configuration.GetSection("Application"));
        services.TryAddSingleton<INexusRuntime, NexusRuntime>();

        return services;
    }

    /// <summary>
    /// Registers the default services for a complete Nexus runtime.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddNexusServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var applicationSettings =
            configuration.GetSection("Application").Get<ApplicationSettings>()
            ?? new ApplicationSettings();

        services.AddCoreServices(configuration, applicationSettings.ContentManifestLocation);
        services.AddGraphicsServices(configuration);
        services.AddInputServices();
        services.AddPhysicsServices();
        services.AddAudioServices();
        services.AddNexusGui();
        services.AddGameServices(configuration);
        services.AddRuntimeServices(configuration);

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IGraphicsSystem)))
        {
            services.AddVkGraphicsServices(configuration);
        }

        return services;
    }
}
