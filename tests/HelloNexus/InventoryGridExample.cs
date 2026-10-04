namespace HelloNexus;

/// <summary>Describes the independent status effects that can apply to an inventory item.</summary>
[Flags]
public enum ItemStatusFlags
{
    /// <summary>Indicates that the item has no status flags.</summary>
    None = 0,

    /// <summary>Indicates that the item is damaged.</summary>
    Damaged = 1 << 0,

    /// <summary>Indicates that the item is dull.</summary>
    Dull = 1 << 1,

    /// <summary>Indicates that the item is sharp.</summary>
    Sharp = 1 << 2,

    /// <summary>Indicates that the item is heavy.</summary>
    Heavy = 1 << 3,

    /// <summary>Indicates that the item is poisonous.</summary>
    Poison = 1 << 4,
}

/// <summary>Represents an item displayed in an inventory grid.</summary>
public class InventoryItem : Element
{
    [Observable(Public = true)]
    private ContentId? _imageId = null;

    [Observable(Public = true)]
    private string _itemName = string.Empty;

    [Observable(Public = true)]
    private ItemStatusFlags _statusFlags;

    /// <summary>Initializes the inventory item.</summary>
    public override void Initialize() { }
}

/// <summary>Contains reusable inventory item templates.</summary>
public static partial class Templates
{
    /// <summary>Gets the example zweihander inventory item template.</summary>
    public static readonly ITemplate<InventoryItem> MyTemplate = new Template<InventoryItem>
    {
        Children =
        [
            new Template<ImageElement>(),
            new Template<TextElement>(),
            new Template<ImageElement>(),
        ],
    };
}

/// <summary>Creates the inventory grid example scene.</summary>
internal sealed class InventoryGridExample(ITextStyleRegistry textStyles, ITextureRegistry textures)
{
    /// <summary>Creates the inventory grid scene.</summary>
    /// <param name="nodeId">The identifier assigned to the scene root.</param>
    /// <param name="inputMap">The input map used by the scene.</param>
    /// <returns>The configured inventory grid scene.</returns>
    public Scene Create(NodeId nodeId, InputMap inputMap)
    {
        var background = textures.GetOrCreate((ContentId)"hello_nexus_background_image");
        var textStyleNormal = textStyles.GetOrCreate((ContentId)"ui.default", 16);
        var textStyleLarge = textStyles.GetOrCreate((ContentId)"ui.default", 48);
        var audioTexture = textures.GetOrCreate((ContentId)"icon_audio_on");
        var buttonTexture = textures.GetOrCreate((ContentId)"button_texture");

        var camera = new StaticCamera();

        var inventoryItem = new Element
        {
            Children =
            {
                new ImageElement
                {
                    Texture = textures.GetOrCreate(ImageId),
                    SizingMode = ImageSizingMode.Stretch,
                },
                new TextElement
                {
                    Text = "Press ESC to quit",
                    Style = textStyles.GetOrCreate((ContentId)"ui.default", 16),
                    Binding = new Binding<string>(nameof(ItemName)),
                },
                new ImageElement
                {
                    Texture = textures.GetOrCreate(ImageId),
                    SizingMode = ImageSizingMode.Stretch,
                    // Bind status flags
                },
            },
        };

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
                    [0, 0] = inventoryItem.Clone(inventorySlot[2]),
                    [0, 1] = inventoryItem.Clone(inventorySlot[1]),
                    // ...
                },
            },
        };

        return scene;
    }
}
