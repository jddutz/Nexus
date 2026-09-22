namespace Nexus.Game;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGameSystemServices(this IServiceCollection services)
    {
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
