namespace HelloNexus;

/// <summary>
/// Builds the HelloNexus welcome screen and its application-specific content.
/// </summary>
public class WelcomeScreen : Scene
{
    private readonly ITextStyleRegistry _textStyles;
    private readonly ITextureRegistry _textures;
    private readonly StaticCamera _camera;

    /// <summary>Creates the welcome screen with its camera, input bindings, and content services.</summary>
    /// <param name="textStyles">Builds and caches text styles for the welcome text.</param>
    /// <param name="textures">Loads and tracks textures from the content library.</param>
    /// <param name="eventHub">Dispatches the welcome screen's input bindings.</param>
    /// <param name="windowService">Provides the main window closed by the exit bindings.</param>
    public WelcomeScreen(
        ITextStyleRegistry textStyles,
        ITextureRegistry textures,
        IEventHub eventHub,
        IWindowService windowService
    )
    {
        _textStyles = textStyles;
        _textures = textures;
        _camera = new StaticCamera();
        MainCamera = _camera;

        var inputMap = new InputMap(eventHub);
        var window = windowService.GetMainWindow();
        inputMap.OnKeyPressed(KeyEnum.Escape).Invoke(() => window.Close());
        inputMap
            .OnAnyControllerButtonPressed(ControllerSemanticNames.Back)
            .Invoke(() => window.Close());
        InputMap = inputMap;
    }

    /// <summary>Initializes the welcome screen's visual hierarchy.</summary>
    public override void Initialize()
    {
        base.Initialize();

        var background = _textures.GetOrCreate((ContentId)"hello_nexus_background_image");
        var textStyleNormal = _textStyles.GetOrCreate((ContentId)"ui.default", 16);
        var textStyleLarge = _textStyles.GetOrCreate((ContentId)"ui.default", 48);
        var audioTexture = _textures.GetOrCreate((ContentId)"icon_audio_on");
        var buttonTexture = _textures.GetOrCreate((ContentId)"button_texture");

        Children.Add(new View { Camera = _camera, PreserveDrawOrder = true });
        Children.Add(
            new ImageElement
            {
                Texture = background,
                SizingMode = ImageSizingMode.Fill,
                Margins = default,
                SortOrder = -32768,
            }
        );
        Children.Add(
            new GridLayout
            {
                Columns = [GridSize.Relative(1f), GridSize.Auto, GridSize.Relative(1f)],
                Rows = [GridSize.Absolute(40f), GridSize.Relative(1f), GridSize.Relative(1f)],
                [0, 0] = new ImageElement
                {
                    Texture = audioTexture,
                    SizingMode = ImageSizingMode.Fit,
                    HorizontalAlignment = AlignHorizontal.Left,
                    VerticalAlignment = AlignVertical.Top,
                    Margins = new Margins(10f, 0f, 10f, 0f),
                },
                [0, 1] = new TextElement
                {
                    Text = "Press ESC to quit",
                    Style = textStyleNormal,
                    Color = Colors.DarkGray,
                    Margins = new Margins(0f, 0f, 18f, 0f),
                    MaximumLines = 1,
                    HorizontalAlignment = AlignHorizontal.Center,
                    VerticalAlignment = AlignVertical.Top,
                },
                [0, 2] = new ImageElement
                {
                    Texture = audioTexture,
                    SizingMode = ImageSizingMode.Fit,
                    HorizontalAlignment = AlignHorizontal.Right,
                    Margins = new Margins(0f, 10f, 10f, 0f),
                },
                [1, 1] = new TextElement
                {
                    Text = "Welcome to the Nexus",
                    Style = textStyleLarge,
                    Color = Colors.WhiteSmoke,
                    Margins = new Margins(0f, 0f, 0f, 10f),
                    HorizontalAlignment = AlignHorizontal.Center,
                    VerticalAlignment = AlignVertical.Bottom,
                },
                [2, 1] = new TextButton
                {
                    Label = "Start Physics Test",
                    Style = textStyleNormal,
                    TextColor = Colors.WhiteSmoke,
                    Texture = buttonTexture,
                    SourceBorders = new(64f, 64f, 64f, 64f),
                    Width = 280f,
                    Height = 48f,
                    Margins = new Margins(0f, 0f, 10f, 0f),
                    HorizontalAlignment = AlignHorizontal.Center,
                    VerticalAlignment = AlignVertical.Top,
                    Action = button => button.Label = "Physics Test Started",
                },
            }
        );
    }
}
