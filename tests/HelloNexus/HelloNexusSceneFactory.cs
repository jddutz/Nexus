namespace HelloNexus;

using Nexus.Core;
using Nexus.Game;
using Nexus.Graphics;
using Nexus.Graphics.Cameras;
using Nexus.Graphics.Components;
using Nexus.Graphics.Text;
using Nexus.Graphics.Textures;
using Nexus.GUI;
using Nexus.GUI.Elements;
using Nexus.Input;
using Nexus.Input.Devices;
using Silk.NET.Maths;
using GuiView = Nexus.GUI.Elements.View;

/// <summary>
/// Creates the HelloNexus welcome scene and its application-specific content.
/// </summary>
/// <param name="windowService">Provides the main window dimensions.</param>
/// <param name="textStyleRegistry">Builds and caches text styles for the welcome text.</param>
/// <param name="textureRegistry">Loads and tracks textures from the content library.</param>
internal sealed class HelloNexusSceneFactory(
    IWindowService windowService,
    ITextStyleRegistry textStyleRegistry,
    ITextureRegistry textureRegistry
)
{
    /// <summary>
    /// Creates the initial scene and its HelloNexus-specific content.
    /// </summary>
    /// <param name="nodeId">The identifier assigned to the scene node.</param>
    /// <param name="inputMap">The scene's keyboard bindings.</param>
    /// <returns>The configured initial scene.</returns>
    public Scene Create(NodeId nodeId, InputMap inputMap)
    {
        var mainWindow = windowService.GetMainWindow();
        var scene = new Scene(nodeId);
        scene.InputMap = inputMap;

        var sceneView = scene.Children.OfType<IGameObject>().Single();
        var camera = sceneView.Components.OfType<StaticCamera>().Single();
        camera.SetViewportSize(mainWindow.Size.X, mainWindow.Size.Y);

        var guiLayerDefinition =
            scene.RenderLayers[0]
            ?? throw new InvalidOperationException("The default GUI render layer is missing.");
        var guiLayer = 1UL << guiLayerDefinition.Index;
        var backgroundLayer =
            1UL << scene.RenderLayers.Create("Background", RenderPasses.Main).Index;
        var backgroundView = sceneView.Components.OfType<ViewRenderer>().Single();
        backgroundView.Name = "Background";
        backgroundView.LayerMask = backgroundLayer;

        var guiView = scene.CreateChild<GuiView>();
        guiView.ViewComponent.Name = "GUI";
        guiView.Camera = camera;
        guiView.LayerMask = guiLayer;
        guiView.RenderOrder = 1;

        var backgroundTexture = new TextureRenderer
        {
            Texture = textureRegistry.GetOrCreate((ContentId)"hello_nexus_background_image"),
            Destination = new Rectangle<float>(0f, 0f, mainWindow.Size.X, mainWindow.Size.Y),
            RenderLayerMask = backgroundLayer,
        };
        var backgroundElement = new BackgroundElement(backgroundTexture);
        scene.Children.Add(backgroundElement);

        var textStyle = textStyleRegistry.GetOrCreate(
            new TextStyleDescription("Roboto", (ContentId)"ui.default", 16)
        );
        const string pressText = "Press ESC to quit";
        var pressTextElement = new TextElement(pressText, textStyle, 1, guiLayer)
        {
            Color = Colors.WhiteSmoke,
        };
        pressTextElement.HorizontalAlignment = AlignHorizontal.Center;
        pressTextElement.VerticalAlignment = AlignVertical.Center;

        const string welcomeText = "Welcome to the Nexus";
        var welcomeTextElement = new TextElement(welcomeText, textStyle, renderLayerMask: guiLayer)
        {
            Color = Colors.WhiteSmoke,
        };

        const string buttonLabel = "Start Physics Test";
        var buttonElement = new TextButton(
            textStyle,
            textureRegistry.GetOrCreate((ContentId)"button_texture"),
            horizontalPadding: 16f,
            verticalPadding: 10f,
            renderLayerMask: guiLayer,
            sourceBorders: new(64f, 64f, 64f, 64f)
        )
        {
            Label = buttonLabel,
            TextColor = Colors.WhiteSmoke,
        };
        var buttonSize = buttonElement.Measure(
            new Vector2D<float>(mainWindow.Size.X, mainWindow.Size.Y)
        );
        buttonElement.Width = buttonSize.X + 100f;
        buttonElement.Height = buttonSize.Y;
        var buttonFocused = false;
        buttonElement.Action = () => buttonElement.Label = "Physics Test Started";
        buttonElement
            .InputMap.OnAnyControllerButtonPressed(ControllerSemanticNames.FaceBottom)
            .Invoke(() =>
            {
                if (buttonFocused)
                    buttonElement.Action?.Invoke();
            });
        foreach (
            var direction in new[]
            {
                ControllerSemanticNames.DPadUp,
                ControllerSemanticNames.DPadRight,
                ControllerSemanticNames.DPadDown,
                ControllerSemanticNames.DPadLeft,
            }
        )
            inputMap.OnAnyControllerButtonPressed(direction).Invoke(() => buttonFocused = true);
        var audioTexture = textureRegistry.GetOrCreate((ContentId)"icon_audio_on");
        var leftIcon = new ImageElement
        {
            Texture = audioTexture,
            SizingMode = ImageSizingMode.Fit,
            HorizontalAlignment = AlignHorizontal.Left,
            Margins = new Margins(10f, 0f, 0f, 0f),
            RenderLayerMask = guiLayer,
        };
        var rightIcon = new ImageElement
        {
            Texture = audioTexture,
            SizingMode = ImageSizingMode.Fit,
            HorizontalAlignment = AlignHorizontal.Right,
            Margins = new Margins(0f, 10f, 0f, 0f),
            RenderLayerMask = guiLayer,
        };
        buttonElement.HorizontalAlignment = AlignHorizontal.Center;
        buttonElement.VerticalAlignment = AlignVertical.Top;
        buttonElement.Margins = new Margins(10f);
        welcomeTextElement.HorizontalAlignment = AlignHorizontal.Center;
        welcomeTextElement.VerticalAlignment = AlignVertical.Bottom;
        welcomeTextElement.Margins = new Margins(10f);

        var grid = new GridLayout();
        grid.Columns = [GridSize.Relative(1f), GridSize.Relative(1f), GridSize.Relative(1f)];
        grid.Rows = [GridSize.Absolute(40f), GridSize.Relative(1f), GridSize.Relative(1f)];
        grid.SetCell(0, 0, leftIcon);
        grid.SetCell(0, 1, pressTextElement);
        grid.SetCell(0, 2, rightIcon);
        grid.SetCell(1, 1, welcomeTextElement);
        grid.SetCell(2, 1, buttonElement);
        scene.Children.Add(grid);

        return scene;
    }

    /// <summary>
    /// Covers the available bounds with a centered aspect-preserving background texture.
    /// </summary>
    private sealed class BackgroundElement : Element
    {
        private readonly TextureRenderer _texture;

        /// <summary>
        /// Initializes the background element with its texture component.
        /// </summary>
        /// <param name="texture">The background texture component.</param>
        public BackgroundElement(TextureRenderer texture)
            : base(components: [texture])
        {
            _texture = texture;
        }

        /// <inheritdoc />
        public override void Arrange(Rectangle<float> bounds)
        {
            base.Arrange(bounds);
            var texture = _texture.Texture!;
            var boundsAspectRatio = Bounds.Size.X / Bounds.Size.Y;
            var textureAspectRatio = (float)texture.Width / texture.Height;
            var imageSize =
                boundsAspectRatio > textureAspectRatio
                    ? new Vector2D<float>(Bounds.Size.X, Bounds.Size.X / textureAspectRatio)
                    : new Vector2D<float>(Bounds.Size.Y * textureAspectRatio, Bounds.Size.Y);

            _texture.Destination = new Rectangle<float>(
                Bounds.Origin.X + (Bounds.Size.X - imageSize.X) / 2f,
                Bounds.Origin.Y + (Bounds.Size.Y - imageSize.Y) / 2f,
                imageSize.X,
                imageSize.Y
            );
        }
    }
}
