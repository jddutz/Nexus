namespace Nexus.GameModel;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGameSystemServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IGameSystem, GameModelSystem>();
        services.TryAddSingleton<IGameModel>(serviceProvider =>
            (GameModelSystem)serviceProvider.GetRequiredService<IGameSystem>()
        );
        services.TryAddSingleton<ISceneRegistry, SceneRegistry>();

        return services;
    }
}
