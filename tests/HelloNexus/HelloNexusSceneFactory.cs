namespace HelloNexus;

/// <summary>
/// Creates the HelloNexus welcome scene and its application-specific content.
/// </summary>
/// <param name="textStyles">Builds and caches text styles for the welcome text.</param>
/// <param name="textures">Loads and tracks textures from the content library.</param>
internal sealed class HelloNexusSceneFactory(
    ITextStyleRegistry textStyles,
    ITextureRegistry textures
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
        var background = textures.GetOrCreate((ContentId)"hello_nexus_background_image");
        var textStyleNormal = textStyles.GetOrCreate((ContentId)"ui.default", 16);
        var textStyleLarge = textStyles.GetOrCreate((ContentId)"ui.default", 48);
        var audioTexture = textures.GetOrCreate((ContentId)"icon_audio_on");
        var buttonTexture = textures.GetOrCreate((ContentId)"button_texture");

        var camera = new StaticCamera();

        var scene = new Scene(nodeId)
        {
            MainCamera = camera,
            InputMap = inputMap,
            Children =
            {
                new View { Camera = camera, PreserveDrawOrder = true },
                new ImageElement
                {
                    Texture = background,
                    SizingMode = ImageSizingMode.Fill,
                    Margins = default,
                    SortOrder = -32768,
                },
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
                        Color = Colors.WhiteSmoke,
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
                },
            },
        };

        return scene;
    }
}
