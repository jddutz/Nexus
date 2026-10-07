namespace Nexus.Samples.Menus;

/// <summary>
/// Displays the settings-page visit count and provides navigation back to the main menu.
/// </summary>
[Scene("Settings")]
public sealed class SettingsScene : Scene
{
    private readonly StaticCamera _camera = new();
    private readonly MenuSettings _menuSettings;
    private readonly ITextStyleRegistry _textStyles;
    private readonly ITextureRegistry _textures;
    private readonly ISceneManager _sceneManager;

    /// <summary>
    /// Creates the settings page with its shared state and GUI resources.
    /// </summary>
    /// <param name="menuSettings">Stores the number of visits to this page.</param>
    /// <param name="textStyles">Provides generated styles for built-in fonts.</param>
    /// <param name="textures">Loads textures from the shared content library.</param>
    /// <param name="sceneManager">Requests transitions to registered scenes.</param>
    public SettingsScene(
        MenuSettings menuSettings,
        ITextStyleRegistry textStyles,
        ITextureRegistry textures,
        ISceneManager sceneManager
    )
    {
        ArgumentNullException.ThrowIfNull(menuSettings);
        ArgumentNullException.ThrowIfNull(textStyles);
        ArgumentNullException.ThrowIfNull(textures);
        ArgumentNullException.ThrowIfNull(sceneManager);

        _menuSettings = menuSettings;
        _textStyles = textStyles;
        _textures = textures;
        _sceneManager = sceneManager;
        MainCamera = _camera;
    }

    /// <summary>Initializes the settings page and increments its visit count.</summary>
    public override void Initialize()
    {
        base.Initialize();
        _menuSettings.SettingsCount++;

        var grid = new GridLayout
        {
            Rows = [GridSize.Relative(), GridSize.Relative(), GridSize.Relative()],
            Columns = [GridSize.Relative(), GridSize.Relative(), GridSize.Relative()],
        };
        grid[1, 1] = new TextElement(
            $"Settings Count: {_menuSettings.SettingsCount}",
            _textStyles.GetOrCreate(BuiltInFonts.Default, 24f)
        )
        {
            HorizontalAlignment = AlignHorizontal.Center,
            VerticalAlignment = AlignVertical.Center,
        };
        grid[2, 1] = new TextButton
        {
            Label = "Back to Main Menu",
            Width = 280f,
            Height = 64f,
            Style = _textStyles.GetOrCreate(BuiltInFonts.Default, 24f),
            Texture = _textures.GetOrCreate(new ContentId("button_line_corners_square")),
            HorizontalAlignment = AlignHorizontal.Center,
            VerticalAlignment = AlignVertical.Top,
            Action = _ => _sceneManager.LoadScene<MainMenu>(),
        };

        Children.Add(new View { Camera = _camera, PreserveDrawOrder = true });
        Children.Add(grid);
    }
}
