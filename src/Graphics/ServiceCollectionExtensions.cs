namespace Nexus.Graphics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexus.Assets.Fonts;

/// <summary>
/// Registers services owned by Nexus.Graphics.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers graphics settings and the default content providers.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddGraphicsServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<WindowSettings>().Bind(configuration.GetSection("Window"));
        services.AddOptions<DiagnosticsSettings>().Bind(configuration.GetSection("Diagnostics"));
        services.TryAddSingleton<IGraphicsProfiler, GraphicsProfiler>();
        services.TryAddSingleton<Nexus.Core.Performance.IPerformanceTelemetry>(provider =>
            provider.GetRequiredService<IGraphicsProfiler>());
        services.TryAddSingleton<ITextureRegistry, TextureRegistry>();
        services.TryAddSingleton<IFontBuilder, FontBuilder>();
        services.TryAddSingleton<ITextStyleRegistry, TextStyleRegistry>();
        return services;
    }
}
