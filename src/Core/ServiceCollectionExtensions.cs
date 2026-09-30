namespace Nexus.Core;

using Microsoft.Extensions.Logging;
using Nexus.Core.Events;

/// <summary>
/// Registers services owned by Nexus.Core.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers core services and binds core settings from configuration.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="contentManifestLocation">The configured content manifest location.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddCoreServices(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentManifestLocation
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentManifestLocation);

        services.AddOptions<DiagnosticsSettings>().Bind(configuration.GetSection("Diagnostics"));
        services.AddOptions<ContentSettings>().Bind(configuration.GetSection("Content"));
        services.AddEventHub(configuration);
        services.TryAddSingleton<IContentManifest>(_ =>
        {
            var manifestPath = Path.IsPathRooted(contentManifestLocation)
                ? contentManifestLocation
                : Path.Combine(AppContext.BaseDirectory, contentManifestLocation);
            var contentLibraryPath = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;

            return new ContentManifest(contentLibraryPath, configuration);
        });

        return services;
    }

    /// <summary>
    /// Registers an event hub configured from the application diagnostics settings.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddEventHub(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton<IEventHub>(serviceProvider =>
        {
            var applicationSettings = configuration.GetSection("Application");
            return new EventHub(
                serviceProvider.GetService<ILogger<EventHub>>(),
                applicationSettings.GetValue<bool>("DiagnosticsEnabled"),
                applicationSettings.GetValue<bool>("LogHighFrequencyEvents"),
                applicationSettings.GetValue("EventLogLimit", 20)
            );
        });

        return services;
    }

    public static IServiceCollection RegisterEventHandlersFromAssemblyContaining<T>(
        this IServiceCollection services
    )
    {
        return services;
    }
}
