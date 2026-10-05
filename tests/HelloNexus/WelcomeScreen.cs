namespace HelloNexus;

/// <summary>
/// Builds the HelloNexus welcome screen and its application-specific content.
/// </summary>
public class WelcomeScreen : Scene
{
    private readonly ITextStyleRegistry _textStyles;
    private readonly StaticCamera _camera;

    /// <summary>Creates the welcome screen with its camera, input bindings, and content services.</summary>
    /// <param name="textStyles">Builds and caches text styles for the welcome text.</param>
    /// <param name="eventHub">Dispatches the welcome screen's input bindings.</param>
    /// <param name="windowService">Provides the main window closed by the exit bindings.</param>
    public WelcomeScreen(
        ITextStyleRegistry textStyles,
        IEventHub eventHub,
        IWindowService windowService
    )
    {
        _textStyles = textStyles;
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

        var textStyleSmall = _textStyles.GetOrCreate(BuiltInFonts.Default, 10);
        var textStyleLarge = _textStyles.GetOrCreate(BuiltInFonts.Default, 32);

        Children.Add(new View { Camera = _camera, PreserveDrawOrder = true });
        Children.Add(
            new ImageElement
            {
                Texture = BuiltInTextures.Uniform,
                SizingMode = ImageSizingMode.Stretch,
                Color = Colors.MidnightBlue,
                Margins = default,
                SortOrder = -32768,
            }
        );
        Children.Add(
            new TextElement
            {
                Text = "Welcome to the Nexus",
                Style = textStyleLarge,
                Color = Colors.WhiteSmoke,
                HorizontalAlignment = AlignHorizontal.Center,
                VerticalAlignment = AlignVertical.Center,
            }
        );
        Children.Add(
            new TextElement
            {
                Text = "Press ESC to quit",
                Style = textStyleSmall,
                Color = Colors.DarkGray,
                Margins = new Margins(0f, 0f, 0f, 20f),
                MaximumLines = 1,
                HorizontalAlignment = AlignHorizontal.Center,
                VerticalAlignment = AlignVertical.Bottom,
            }
        );
    }
}
