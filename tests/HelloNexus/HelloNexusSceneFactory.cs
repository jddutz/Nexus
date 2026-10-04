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

/// <summary>
/// Creates the HelloNexus welcome scene and its application-specific content.
/// </summary>
/// <param name="textStyleRegistry">Builds and caches text styles for the welcome text.</param>
/// <param name="textureRegistry">Loads and tracks textures from the content library.</param>
internal sealed class HelloNexusSceneFactory(
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
        var scene = new Scene(nodeId) { InputMap = inputMap };

        scene.Children.Add(new View() { Camera = scene.StaticCamera, PreserveDrawOrder = true });

        scene.Children.Add(
            new ImageElement
            {
                Texture = textureRegistry.GetOrCreate((ContentId)"hello_nexus_background_image"),
                SizingMode = ImageSizingMode.Fill,
                Margins = default,
                SortOrder = -32768,
            }
        );

        var textStyle = textStyleRegistry.GetOrCreate(
            new TextStyleDescription("Roboto", (ContentId)"ui.default", 16)
        );
        const string pressText = "Press ESC to quit";
        var pressTextElement = new TextElement(pressText, textStyle, 1)
        {
            Color = Colors.WhiteSmoke,
        };
        pressTextElement.HorizontalAlignment = AlignHorizontal.Center;
        pressTextElement.VerticalAlignment = AlignVertical.Center;

        const string welcomeText = "Welcome to the Nexus";
        var welcomeTextElement = new TextElement(welcomeText, textStyle)
        {
            Color = Colors.WhiteSmoke,
        };

        const string buttonLabel = "Start Physics Test";
        var buttonElement = new TextButton(
            textStyle,
            textureRegistry.GetOrCreate((ContentId)"button_texture"),
            horizontalPadding: 66f,
            verticalPadding: 10f,
            sourceBorders: new(64f, 64f, 64f, 64f)
        )
        {
            Label = buttonLabel,
            TextColor = Colors.WhiteSmoke,
        };
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
        };
        var rightIcon = new ImageElement
        {
            Texture = audioTexture,
            SizingMode = ImageSizingMode.Fit,
            HorizontalAlignment = AlignHorizontal.Right,
            Margins = new Margins(0f, 10f, 0f, 0f),
        };
        buttonElement.HorizontalAlignment = AlignHorizontal.Center;
        buttonElement.VerticalAlignment = AlignVertical.Top;
        buttonElement.Margins = new Margins(10f);
        welcomeTextElement.HorizontalAlignment = AlignHorizontal.Center;
        welcomeTextElement.VerticalAlignment = AlignVertical.Bottom;
        welcomeTextElement.Margins = new Margins(10f);

        var grid = new GridLayout();
        grid.Columns = [GridSize.Relative(1f), GridSize.Auto, GridSize.Relative(1f)];
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
