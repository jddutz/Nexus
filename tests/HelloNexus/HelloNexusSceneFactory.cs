namespace HelloNexus;

using Nexus.Assets.Fonts;
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

/// <summary>
/// Creates the HelloNexus welcome scene and its application-specific content.
/// </summary>
/// <param name="windowService">Provides the main window dimensions.</param>
/// <param name="contentManifest">Describes the content available to the application.</param>
/// <param name="fontBuilder">Builds font data for the welcome text.</param>
/// <param name="textureProvider">Loads textures from the content library.</param>
internal sealed class HelloNexusSceneFactory(
    IWindowService windowService,
    IContentManifest contentManifest,
    IFontBuilder fontBuilder,
    IContentProvider<Texture> textureProvider
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

        var guiLayer = 1UL << scene.RenderLayers[0]!.Index;
        var backgroundLayer =
            1UL << scene.RenderLayers.Create("Background", RenderPasses.Main).Index;
        var backgroundView = sceneView.Components.OfType<ViewComponent>().Single();
        backgroundView.Name = "Background";
        backgroundView.LayerMask = backgroundLayer;

        var guiView = scene.CreateChild<View>();
        guiView.ViewComponent.Name = "GUI";
        guiView.ViewComponent.Camera = camera;
        guiView.ViewComponent.LayerMask = guiLayer;
        guiView.ViewComponent.RenderOrder = 1;

        var backgroundTexture = new TextureComponent
        {
            Texture = textureProvider.Get((ContentId)"hello_nexus_background_image"),
            Destination = new Rectangle<float>(0f, 0f, mainWindow.Size.X, mainWindow.Size.Y),
            RenderLayerMask = backgroundLayer,
        };
        var backgroundElement = new BackgroundElement(backgroundTexture);
        scene.Children.Add(backgroundElement);

        var textStyle = CreateRobotoTextStyle();
        const string pressText = "Press ESC to quit";
        var pressTextElement = new TextElement(pressText, textStyle, 1, guiLayer);
        pressTextElement.HorizontalAlignment = AlignHorizontal.Center;
        pressTextElement.VerticalAlignment = AlignVertical.Center;

        const string welcomeText = "Welcome to the Nexus";
        var welcomeTextElement = new TextElement(welcomeText, textStyle, renderLayerMask: guiLayer);

        const string buttonLabel = "Start Physics Test";
        var buttonElement = new TextButton(
            textStyle,
            textureProvider.Get((ContentId)"button_texture"),
            horizontalPadding: 16f,
            verticalPadding: 10f,
            renderLayerMask: guiLayer,
            sourceBorders: new(64f, 64f, 64f, 64f)
        )
        {
            Label = buttonLabel,
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
        var audioTexture = textureProvider.Get((ContentId)"icon_audio_on");
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
        grid.Columns =
        [
            GridSize.Relative(1f),
            GridSize.Relative(1f),
            GridSize.Relative(1f),
        ];
        grid.Rows =
        [
            GridSize.Absolute(40f),
            GridSize.Relative(1f),
            GridSize.Relative(1f),
        ];
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
        private readonly TextureComponent _texture;

        /// <summary>
        /// Initializes the background element with its texture component.
        /// </summary>
        /// <param name="texture">The background texture component.</param>
        public BackgroundElement(TextureComponent texture)
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

    /// <summary>Builds the Roboto text style from the font registered as <c>ui.default</c>.</summary>
    /// <returns>The generated style at size 16 with an off-white color.</returns>
    private ITextStyle CreateRobotoTextStyle()
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
}
