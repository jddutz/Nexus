namespace Nexus.Game;

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
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddGameSystemServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IFontBuilder, FontBuilder>();
        services.TryAddSingleton<IGameSystem, GameSystem>();
        services.TryAddSingleton<IGameModel>(serviceProvider =>
            (GameSystem)serviceProvider.GetRequiredService<IGameSystem>()
        );
        services.TryAddSingleton(serviceProvider =>
            (Core.IGameModel)(GameSystem)serviceProvider.GetRequiredService<IGameSystem>()
        );
        services.TryAddSingleton<ISceneRegistry, SceneRegistry>();

        return services;
    }
}
