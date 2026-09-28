namespace HelloNexus;

using Nexus.Assets.Fonts;
using Nexus.Core;
using Nexus.Core.Events;
using Nexus.Game;
using Nexus.Graphics;
using Nexus.Graphics.Cameras;
using Nexus.Graphics.Components;
using Nexus.Graphics.Text;
using Nexus.Graphics.Textures;
using Nexus.GUI;
using Nexus.Input;
using Nexus.Input.Events;

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
                var inputMap = new SceneInputMap(eventHub);
                var window = windowService.GetMainWindow();
                eventHub.Register(new ControllerDiagnostics());
                inputMap.OnKeyPressed(KeyEnum.Escape).Invoke(() => window.Close());
                inputMap.OnAnyControllerButtonPressed(0).Invoke(() => window.Close());

                var gameSystem = ActivatorUtilities.CreateInstance<GameSystem>(serviceProvider);
                gameSystem.InitialScene = CreateHelloNexusScene(
                    gameSystem,
                    windowService,
                    serviceProvider.GetRequiredService<IContentManifest>(),
                    serviceProvider.GetRequiredService<IFontBuilder>(),
                    serviceProvider.GetRequiredService<IContentProvider<Texture>>(),
                    inputMap
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

    /// <summary>
    /// Creates the initial scene and its HelloNexus-specific content.
    /// </summary>
    /// <param name="windowService">Provides the main window dimensions.</param>
    /// <param name="gameModel">Associates game objects with their owner before components are attached.</param>
    /// <param name="contentManifest">Describes the content available to the application.</param>
    /// <param name="fontBuilder">Builds font data for the welcome text.</param>
    /// <param name="textureProvider">Loads textures from the content library.</param>
    /// <param name="inputMap">The scene's keyboard bindings.</param>
    /// <returns>The configured initial scene.</returns>
    private static Scene CreateHelloNexusScene(
        IGameModel gameModel,
        IWindowService windowService,
        IContentManifest contentManifest,
        IFontBuilder fontBuilder,
        IContentProvider<Texture> textureProvider,
        SceneInputMap inputMap
    )
    {
        var mainWindow = windowService.GetMainWindow();
        var scene = new Scene { InputMap = inputMap };
        scene.SetGameModel(gameModel);

        var camera = new StaticCamera();
        camera.SetViewportSize(mainWindow.Size.X, mainWindow.Size.Y);
        scene.CreateChild<GameObject>().AddComponent(camera);

        var backgroundTexture = new TextureComponent
        {
            Texture = textureProvider.Get((ContentId)"hello_nexus_background_image"),
            Size = new(mainWindow.Size.X, mainWindow.Size.Y),
        };
        var backgroundElement = new Element(
            arrange: (element, bounds) =>
            {
                ArrangementRules.Default(element, bounds);
                backgroundTexture.Size = bounds.Size;
            }
        );
        backgroundElement.AddComponent(backgroundTexture);
        scene.AddChild(backgroundElement);

        var textComponent = new TextComponent(CreateRobotoTextStyle(contentManifest, fontBuilder))
        {
            Text = "Welcome to the Nexus",
        };
        var textGameObject = scene.CreateChild<GameObject2D>();
        textGameObject.AddComponent(textComponent);
        textGameObject.Position = new(mainWindow.Size.X / 2f - 48f, mainWindow.Size.Y / 2f - 8f);

        return scene;
    }

    /// <summary>
    /// Builds the Roboto text style from the font registered as <c>ui.default</c>.
    /// </summary>
    /// <param name="contentManifest">Describes the font's content path.</param>
    /// <param name="fontBuilder">Builds the font data and atlas.</param>
    /// <returns>The generated style at size 16 with an off-white color.</returns>
    private static ITextStyle CreateRobotoTextStyle(
        IContentManifest contentManifest,
        IFontBuilder fontBuilder
    )
    {
        var fontId = (ContentId)"ui.default";
        var fontPath = Path.Combine(
            contentManifest.ContentLibraryPath,
            contentManifest.Fonts.GetContentFilePath(fontId)
        );
        var codepoints = new FontGlyphRepertoire().GetCodepoints();
        var font = fontBuilder.Build(fontPath, codepoints, new FontGenerationSettings());
        var atlas = font.Atlas;
        var colors = new Color[checked(atlas.Width * atlas.Height)];

        for (var index = 0; index < colors.Length; index++)
        {
            var sourceOffset = index * 3;
            colors[index] = new Color(
                atlas.Pixels[sourceOffset],
                atlas.Pixels[sourceOffset + 1],
                atlas.Pixels[sourceOffset + 2]
            );
        }

        var texture = new Texture(
            (ContentId)"Roboto",
            checked((uint)atlas.Width),
            checked((uint)atlas.Height),
            colors
        );

        return new TextStyle(font, texture, 16, Colors.WhiteSmoke);
    }

    /// <summary>Writes controller discovery and sampled control changes to the demo console.</summary>
    private sealed class ControllerDiagnostics
    {
        /// <summary>Logs the controller's controls when it connects.</summary>
        /// <param name="message">The controller connection event.</param>
        public void Handle(ControllerConnectedEvent message)
        {
            Console.WriteLine(
                $"Controller connected: {message.Controller.Name} (ID {message.Controller.Id})"
            );
            foreach (var button in message.Controller.Buttons)
                Console.WriteLine($"  {GetButtonLabel(button.Index, button.SemanticName)}");
            foreach (var analogInput in message.Controller.AnalogInputs)
                Console.WriteLine(
                    $"  {GetAnalogLabel(analogInput.Index, analogInput.SemanticName)}"
                );
        }

        /// <summary>Logs a controller button press.</summary>
        /// <param name="message">The captured button-press event.</param>
        public void Handle(ControllerButtonPressedEvent message) =>
            Console.WriteLine(
                $"Controller {message.Controller.Name}: {GetButtonLabel(message.ButtonIndex, message.Button.SemanticName)} pressed"
            );

        /// <summary>Logs a controller button release.</summary>
        /// <param name="message">The captured button-release event.</param>
        public void Handle(ControllerButtonReleasedEvent message) =>
            Console.WriteLine(
                $"Controller {message.Controller.Name}: {GetButtonLabel(message.ButtonIndex, message.Button.SemanticName)} released"
            );

        /// <summary>Logs an analog position captured when it changed.</summary>
        /// <param name="message">The captured analog-change event.</param>
        public void Handle(ControllerAnalogChangedEvent message) =>
            Console.WriteLine(
                $"Controller {message.Controller.Name}: {GetAnalogLabel(message.AnalogInputIndex, message.AnalogInput.SemanticName)} = {message.Position}"
            );

        /// <summary>Logs a controller disconnection.</summary>
        /// <param name="message">The controller disconnection event.</param>
        public void Handle(ControllerDisconnectedEvent message) =>
            Console.WriteLine(
                $"Controller disconnected: {message.Controller.Name} (ID {message.Controller.Id})"
            );

        /// <summary>Creates a semantic or one-based display label for a controller button.</summary>
        /// <param name="index">The controller-local logical index.</param>
        /// <param name="semanticName">The optional normalized role.</param>
        /// <returns>The display label.</returns>
        private static string GetButtonLabel(int index, string? semanticName) =>
            semanticName ?? $"Button {index + 1}";

        /// <summary>Creates a semantic or one-based display label for an analog input.</summary>
        /// <param name="index">The controller-local logical index.</param>
        /// <param name="semanticName">The optional normalized role.</param>
        /// <returns>The display label.</returns>
        private static string GetAnalogLabel(int index, string? semanticName) =>
            semanticName ?? $"Analog {index + 1}";
    }
}
