namespace HelloNexus;

using Nexus.Core.Events;
using Nexus.Game;
using Nexus.Graphics;
using Nexus.Input;

/// <summary>
/// Entry point for the Hello Nexus application.
/// </summary>
internal static class Program
{
    /// <summary>Creates the application configuration and runs Hello Nexus.</summary>
    /// <param name="args">Command-line configuration overrides.</param>
    private static void Main(string[] args)
    {
        Environment.ExitCode = -1;

        try
        {
            Console.WriteLine("Starting Hello Nexus...");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
                .AddJsonFile(
                    Path.Combine(AppContext.BaseDirectory, ".content", "content-manifest.json")
                )
                .AddCommandLine(args)
                .Build();

            var services = new ServiceCollection();

            services.AddSingleton<IGameSystem>(serviceProvider =>
            {
                var eventHub = serviceProvider.GetRequiredService<IEventHub>();
                var windowService = serviceProvider.GetRequiredService<IWindowService>();
                var inputMap = new InputMap(eventHub);
                var window = windowService.GetMainWindow();
                inputMap.OnKeyPressed(KeyEnum.Escape).Invoke(() => window.Close());
                inputMap.OnAnyControllerButtonPressed(0).Invoke(() => window.Close());

                var gameSystem = ActivatorUtilities.CreateInstance<GameSystem>(serviceProvider);

                var sceneRegistry = serviceProvider.GetRequiredService<ISceneRegistry>();
                var initialSceneId = (SceneId)gameSystem.Settings.InitialScene;
                var sceneFactory = ActivatorUtilities.CreateInstance<HelloNexusSceneFactory>(
                    serviceProvider
                );
                sceneRegistry.Register(
                    initialSceneId,
                    () => sceneFactory.Create(initialSceneId, gameSystem, inputMap)
                );

                return gameSystem;
            });

            using var application = new Application(configuration, services);

            application.Run();

            Environment.ExitCode = 0;
            Console.WriteLine("Hello Nexus exited normally.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Hello Nexus encountered an unhandled error:");
            Console.Error.WriteLine(ex);
        }
    }
}
