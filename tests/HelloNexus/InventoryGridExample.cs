namespace HelloNexus;

internal sealed class InventoryGridExample(ITextStyleRegistry textStyles, ITextureRegistry textures)
{
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
                new GridLayout
                {
                    HorizontalAlignment = AlignHorizontal.Center,
                    VerticalAlignment = AlignVertical.Center,
                    Columns =
                    [
                        GridSize.Absolute(80f),
                        GridSize.Absolute(80f),
                        GridSize.Absolute(80f),
                        GridSize.Absolute(80f),
                    ],
                    Rows =
                    [
                        GridSize.Absolute(80f),
                        GridSize.Absolute(80f),
                        GridSize.Absolute(80f),
                        GridSize.Absolute(80f),
                        GridSize.Absolute(80f),
                    ],
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
                    // ...
                },
            },
        };

        return scene;
    }
}
