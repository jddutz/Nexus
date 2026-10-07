namespace Nexus.Samples.Menus;

/// <summary>
/// Presents the main menu and its available actions.
/// </summary>
[Scene("MainMenu")]
public sealed class MainMenu : Scene
{
    private readonly StaticCamera _camera = new();
    private readonly ITextStyleRegistry _textStyles;
    private readonly ITextureRegistry _textures;
    private readonly ISceneManager _sceneManager;

    /// <summary>
    /// Creates the main menu with the text-style and texture resources used by its buttons.
    /// </summary>
    /// <param name="textStyles">Provides generated styles for built-in fonts.</param>
    /// <param name="textures">Loads textures from the shared content library.</param>
    /// <param name="sceneManager">Requests transitions to registered scenes.</param>
    public MainMenu(
        ITextStyleRegistry textStyles,
        ITextureRegistry textures,
        ISceneManager sceneManager
    )
    {
        ArgumentNullException.ThrowIfNull(textStyles);
        ArgumentNullException.ThrowIfNull(textures);
        ArgumentNullException.ThrowIfNull(sceneManager);

        _textStyles = textStyles;
        _textures = textures;
        _sceneManager = sceneManager;
        MainCamera = _camera;
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();

        Children.Add(new View { Camera = _camera, PreserveDrawOrder = true });
        Children.Add(
            new TextButton
            {
                Label = "Settings",
                Width = 280f,
                Height = 64f,
                Style = _textStyles.GetOrCreate(BuiltInFonts.Default, 24f),
                Texture = _textures.GetOrCreate(new ContentId("button_line_corners_square")),
                Action = _ => _sceneManager.LoadScene<SettingsScene>(),
            }
        );
    }
}
