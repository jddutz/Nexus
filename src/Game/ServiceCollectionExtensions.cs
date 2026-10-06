namespace Nexus.Game;

using Microsoft.Extensions.Configuration;
using Nexus.Assets.Fonts;

/// <summary>
/// Registers services required by the game system.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the game system and its default dependencies.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddGameServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<GameSettings>().Bind(configuration.GetSection("Game"));
        services.AddOptions<SceneRegistrySettings>();
        services.TryAddSingleton<IFontBuilder, FontBuilder>();
        services.TryAddSingleton<IGameSystem, GameSystem>();
        services.TryAddSingleton<ISceneManager, SceneManager>();
        services.TryAddSingleton<ISceneRegistry, SceneRegistry>();

        return services;
    }
}
