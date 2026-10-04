namespace HelloNexus;

using Nexus.Core;
using Nexus.Game;
using Nexus.Graphics;
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

        var camera = scene.StaticCamera;
        camera.SetViewportSize(mainWindow.Size.X, mainWindow.Size.Y);

        var guiLayerDefinition =
            scene.RenderLayers[0]
            ?? throw new InvalidOperationException("The default GUI render layer is missing.");
        var guiLayer = 1UL << guiLayerDefinition.Index;
        var backgroundLayer =
            1UL << scene.RenderLayers.Create("Background", RenderPasses.Main).Index;

        scene.Children.Add(
            new View()
            {
                Camera = scene.StaticCamera,
                LayerMask = guiLayer | backgroundLayer,
                PreserveDrawOrder = true,
            }
        );

        scene.Children.Add(
            new ImageElement
            {
                Texture = textureRegistry.GetOrCreate((ContentId)"hello_nexus_background_image"),
                SizingMode = ImageSizingMode.Fill,
                Margins = default,
                SortOrder = -32768,
                RenderLayerMask = backgroundLayer,
            }
        );

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
}
