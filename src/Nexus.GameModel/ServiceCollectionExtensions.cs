namespace Nexus.GameModel;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGameSystemServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IGameSystem, GameSystem>();
        services.TryAddSingleton<ISceneRegistry, SceneRegistry>();

        return services;
    }
}
