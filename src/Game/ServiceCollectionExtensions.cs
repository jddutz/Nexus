namespace Nexus.Game;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGameSystemServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IGameModel, GameSystem>();
        services.TryAddSingleton(serviceProvider =>
            (Core.IGameModel)(GameSystem)serviceProvider.GetRequiredService<IGameModel>()
        );
        services.TryAddSingleton<ISceneRegistry, SceneRegistry>();

        return services;
    }
}
