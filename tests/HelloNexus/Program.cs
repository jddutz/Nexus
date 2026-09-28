namespace HelloNexus;

using System.Text;
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
using Silk.NET.Maths;

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

        var sceneView = scene.Children.Single();
        var camera = sceneView.Components.OfType<StaticCamera>().Single();
        camera.SetViewportSize(mainWindow.Size.X, mainWindow.Size.Y);

        const ulong backgroundLayer = 1;
        const ulong foregroundLayer = 2;
        var backgroundView = sceneView.Components.OfType<ViewComponent>().Single();
        backgroundView.Name = "Background";
        backgroundView.LayerMask = backgroundLayer;

        var foregroundView = scene.CreateChild<View>();
        foregroundView.ViewComponent.Name = "Foreground";
        foregroundView.ViewComponent.Camera = camera;
        foregroundView.ViewComponent.LayerMask = foregroundLayer;
        foregroundView.ViewComponent.RenderOrder = 1;

        var backgroundTexture = new TextureComponent
        {
            Texture = textureProvider.Get((ContentId)"hello_nexus_background_image"),
            Size = new(mainWindow.Size.X, mainWindow.Size.Y),
            RenderLayerMask = backgroundLayer,
        };
        var backgroundElement = new Element(
            arrange: (element, bounds) =>
            {
                ArrangementRules.Default(element, bounds);
                var texture = backgroundTexture.Texture!;
                var boundsAspectRatio = bounds.Size.X / bounds.Size.Y;
                var textureAspectRatio = (float)texture.Width / texture.Height;
                var imageSize =
                    boundsAspectRatio > textureAspectRatio
                        ? new Vector2D<float>(bounds.Size.X, bounds.Size.X / textureAspectRatio)
                        : new Vector2D<float>(bounds.Size.Y * textureAspectRatio, bounds.Size.Y);

                element.Position = bounds.Origin;
                backgroundTexture.Size = imageSize;
                backgroundTexture.TransformationMatrix = Matrix4X4.CreateTranslation(
                    (bounds.Size.X - imageSize.X) / 2f,
                    (bounds.Size.Y - imageSize.Y) / 2f,
                    0f
                );
            }
        );
        backgroundElement.AddComponent(backgroundTexture);
        scene.AddChild(backgroundElement);

        var textStyle = CreateRobotoTextStyle(contentManifest, fontBuilder);
        const string pressText = "Press ESC to quit";
        var pressTextComponent = new TextComponent(textStyle)
        {
            RenderLayerMask = foregroundLayer,
            Text = pressText,
        };
        var pressTextElement = new Element(
            measure: (_, availableSize) =>
                MeasureText(pressTextComponent, pressText, textStyle, availableSize, 2),
            arrange: (element, bounds) => ArrangeText(element, bounds, pressTextComponent)
        );
        pressTextElement.AddComponent(pressTextComponent);

        const string welcomeText = "Welcome to the Nexus";
        var welcomeTextComponent = new TextComponent(textStyle)
        {
            RenderLayerMask = foregroundLayer,
            Text = welcomeText,
        };
        var welcomeTextElement = new Element(
            measure: (_, availableSize) =>
                MeasureText(
                    welcomeTextComponent,
                    welcomeText,
                    textStyle,
                    availableSize,
                    int.MaxValue
                ),
            arrange: (element, bounds) => ArrangeText(element, bounds, welcomeTextComponent)
        );
        welcomeTextElement.AddComponent(welcomeTextComponent);

        var textLayout = new Element(
            arrange: (element, bounds) =>
            {
                element.Bounds = bounds;

                const float margin = 24f;
                const float spacing = 16f;
                const float bottomPadding = 24f;
                var contentWidth = MathF.Max(0f, bounds.Size.X - margin * 2f);
                var contentHeight = MathF.Max(0f, bounds.Size.Y - margin - bottomPadding);
                var pressSize = pressTextElement.Measure(
                    new Vector2D<float>(contentWidth, contentHeight)
                );
                var welcomeHeight = MathF.Max(0f, contentHeight - pressSize.Y - spacing);
                welcomeTextElement.Measure(new Vector2D<float>(contentWidth, welcomeHeight));

                pressTextElement.Arrange(
                    new Rectangle<float>(
                        new Vector2D<float>(bounds.Origin.X + margin, bounds.Origin.Y + margin),
                        new Vector2D<float>(contentWidth, pressSize.Y)
                    )
                );
                welcomeTextElement.Arrange(
                    new Rectangle<float>(
                        new Vector2D<float>(
                            bounds.Origin.X + margin,
                            bounds.Origin.Y + margin + pressSize.Y + spacing
                        ),
                        new Vector2D<float>(contentWidth, welcomeHeight)
                    )
                );
            }
        );
        textLayout.AddChild(pressTextElement);
        textLayout.AddChild(welcomeTextElement);
        scene.AddChild(textLayout);

        return scene;
    }

    /// <summary>Wraps and measures text within the available layout size.</summary>
    /// <param name="textComponent">The component receiving one span per wrapped line.</param>
    /// <param name="text">The complete source text.</param>
    /// <param name="style">The font metrics used for wrapping.</param>
    /// <param name="availableSize">The maximum available size.</param>
    /// <param name="maximumLines">The maximum allowed line count.</param>
    /// <returns>The visible glyph bounds size after wrapping.</returns>
    private static Vector2D<float> MeasureText(
        TextComponent textComponent,
        string text,
        ITextStyle style,
        Vector2D<float> availableSize,
        int maximumLines
    )
    {
        var scale = style.FontMetrics.EmSize == 0 ? 1.0 : style.Size / style.FontMetrics.EmSize;
        var lineHeight = (float)(style.FontMetrics.LineHeight * scale);
        if (!float.IsFinite(lineHeight) || lineHeight <= 0f)
            lineHeight = (float)style.Size;

        var lineCount =
            lineHeight > 0f
                ? Math.Min(maximumLines, (int)MathF.Floor(availableSize.Y / lineHeight))
                : 0;
        var wrappedText = WrapText(text, style, availableSize.X, lineCount);
        if (textComponent.Text != wrappedText)
            textComponent.Text = wrappedText;

        return textComponent.LayoutBounds.Size;
    }

    /// <summary>Wraps words to the available width and crops the last line at glyph boundaries.</summary>
    /// <param name="text">The complete source text.</param>
    /// <param name="style">The font metrics used to measure glyphs.</param>
    /// <param name="availableWidth">The maximum line width.</param>
    /// <param name="maximumLines">The maximum number of lines.</param>
    /// <returns>The visible lines joined by newline characters.</returns>
    private static string WrapText(
        string text,
        ITextStyle style,
        float availableWidth,
        int maximumLines
    )
    {
        if (availableWidth <= 0f || maximumLines <= 0)
            return string.Empty;

        var lines = new List<string>();
        var currentLine = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = currentLine.Length == 0 ? word : $"{currentLine} {word}";
            if (MeasureTextWidth(style, candidate) <= availableWidth)
            {
                currentLine = candidate;
                continue;
            }

            if (currentLine.Length > 0)
            {
                if (lines.Count + 1 >= maximumLines)
                {
                    lines.Add(FitTextToWidth(style, candidate, availableWidth));
                    return string.Join('\n', lines);
                }

                lines.Add(currentLine);
                currentLine = word;
            }

            if (MeasureTextWidth(style, currentLine) > availableWidth)
            {
                lines.Add(FitTextToWidth(style, currentLine, availableWidth));
                currentLine = string.Empty;
                if (lines.Count >= maximumLines)
                    return string.Join('\n', lines);
            }
        }

        if (currentLine.Length > 0 && lines.Count < maximumLines)
            lines.Add(currentLine);

        return string.Join('\n', lines);
    }

    /// <summary>Measures the visible glyph width of a candidate line.</summary>
    /// <param name="style">The font metrics used to measure glyphs.</param>
    /// <param name="text">The candidate line.</param>
    /// <returns>The candidate's visible glyph width.</returns>
    private static float MeasureTextWidth(ITextStyle style, string text) =>
        new TextSpan(style, text).LayoutBounds.Size.X;

    /// <summary>Returns the longest leading rune sequence that fits in a line.</summary>
    /// <param name="style">The font metrics used to measure glyphs.</param>
    /// <param name="text">The text to crop.</param>
    /// <param name="availableWidth">The maximum line width.</param>
    /// <returns>The fitting text prefix.</returns>
    private static string FitTextToWidth(ITextStyle style, string text, float availableWidth)
    {
        var prefix = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            var candidate = prefix.ToString() + rune;
            if (MeasureTextWidth(style, candidate) > availableWidth)
                break;

            prefix.Append(rune);
        }

        return prefix.ToString();
    }

    /// <summary>Centers visible glyphs and assigns their actual bounds to a text element.</summary>
    /// <param name="element">The text element being arranged.</param>
    /// <param name="bounds">The rectangle assigned by the parent.</param>
    /// <param name="textComponent">The component containing the visible spans.</param>
    private static void ArrangeText(
        Element element,
        Rectangle<float> bounds,
        TextComponent textComponent
    )
    {
        ArrangementRules.Default(element, bounds);
        var textBounds = textComponent.LayoutBounds;
        var textOrigin = new Vector2D<float>(
            bounds.Origin.X + (bounds.Size.X - textBounds.Size.X) / 2f,
            bounds.Origin.Y + (bounds.Size.Y - textBounds.Size.Y) / 2f
        );
        element.Bounds = new Rectangle<float>(textOrigin, textBounds.Size);
        element.Position = new(
            textOrigin.X - textBounds.Origin.X,
            textOrigin.Y - textBounds.Origin.Y
        );
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
