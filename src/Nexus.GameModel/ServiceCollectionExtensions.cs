namespace Nexus.GameModel;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGameSystemServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IGameSystem, GameSystem>();
        services.TryAddSingleton<IGameModel>(serviceProvider =>
            (GameSystem)serviceProvider.GetRequiredService<IGameSystem>()
        );
        services.TryAddSingleton<ISceneRegistry, SceneRegistry>();

        return services;
    }
}
