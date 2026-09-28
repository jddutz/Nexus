using Nexus.Assets.Fonts;
using Nexus.Core;
using Nexus.Graphics;
using Nexus.Graphics.Text;
using Nexus.Graphics.Textures;
using Nexus.GUI;
using Nexus.GUI.Elements;
using Silk.NET.Maths;

namespace Tests;

/// <summary>
/// Tests the text button's owned composition, layout, and hierarchy lifecycle.
/// </summary>
public sealed class TextButtonTests
{
    /// <summary>
    /// Verifies separate buttons own distinct components and independent labels.
    /// </summary>
    [Fact]
    public void Constructor_createsDetachedButtonsWithFreshComponents()
    {
        var first = CreateButton("A");
        var second = CreateButton("A");

        first.Label = "B";

        Assert.Null(first.Parent);
        Assert.Null(first.GameModel);
        Assert.False(first.IsActive);
        Assert.Equal(2, first.Components.Count());
        Assert.NotSame(first, second);
        Assert.NotSame(
            first.GetComponent<NinePatchComponent>(),
            second.GetComponent<NinePatchComponent>()
        );
        Assert.NotSame(first.GetComponent<TextComponent>(), second.GetComponent<TextComponent>());
        Assert.Equal("B", first.Label);
        Assert.Equal("B", first.GetComponent<TextComponent>()?.Text);
        Assert.Equal("A", second.Label);
        Assert.Equal("A", second.GetComponent<TextComponent>()?.Text);
    }

    /// <summary>
    /// Verifies fitting the displayed label does not replace its complete measured source.
    /// </summary>
    [Fact]
    public void Arrange_preservesLabelForLaterMeasurement()
    {
        var button = CreateButton("AB");

        Assert.Equal(new Vector2D<float>(10f, 7f), button.Measure(new(100f, 100f)));
        button.Arrange(new Rectangle<float>(0f, 0f, 9f, 7f));
        Assert.Equal("AB", button.Label);
        Assert.Equal("A", button.GetComponent<TextComponent>()?.Text);

        Assert.Equal(new Vector2D<float>(10f, 7f), button.Measure(new(100f, 100f)));
    }

    /// <summary>
    /// Verifies the background and hit area use the complete arranged bounds.
    /// </summary>
    [Fact]
    public void Arrange_keepsFullBoundsForBackgroundAndHitArea()
    {
        var button = CreateButton("A");
        var bounds = new Rectangle<float>(4f, 5f, 60f, 24f);

        button.Arrange(bounds);

        Assert.Equal(bounds, button.Bounds);
        Assert.Equal(
            new Vector2D<float>(60f, 24f),
            button.GetComponent<NinePatchComponent>()!.Size
        );
        Assert.NotEqual(bounds.Size, button.GetComponent<TextComponent>()!.LayoutBounds.Size);
    }

    /// <summary>
    /// Verifies padding affects measured size and alignment changes label placement.
    /// </summary>
    [Fact]
    public void PaddingAndAlignment_controlMeasurementAndLabelPosition()
    {
        var button = CreateButton("A");
        var measuredSize = button.Measure(new(100f, 100f));
        var bounds = new Rectangle<float>(0f, 0f, 60f, 24f);

        button.Arrange(bounds);
        var centeredPosition = button.Position.X;
        button.LabelAlignment = TextButtonLabelAlignment.Start;
        button.Arrange(bounds);
        var startPosition = button.Position.X;
        button.LabelAlignment = TextButtonLabelAlignment.End;
        button.Arrange(bounds);
        var endPosition = button.Position.X;
        button.Padding = new(8f, 6f);

        Assert.NotEqual(centeredPosition, startPosition);
        Assert.True(startPosition < endPosition);
        Assert.Equal(
            new Vector2D<float>(measuredSize.X + 8f, measuredSize.Y + 6f),
            button.Measure(new(100f, 100f))
        );
    }

    /// <summary>
    /// Verifies AddChild performs model registration and activation for a new button.
    /// </summary>
    [Fact]
    public void AddChild_registersAndActivatesButton()
    {
        var parent = new GameObject();
        var gameModel = new TestGameModel();
        parent.SetGameModel(gameModel);
        parent.Activate();
        var addedComponents = new List<IComponent>();
        parent.ComponentAdded += addedComponents.Add;
        var button = CreateButton("A");

        parent.AddChild(button);

        Assert.Same(parent, button.Parent);
        Assert.Same(gameModel, button.GameModel);
        Assert.Same(button, gameModel.GetGameObject(button.Id));
        Assert.True(button.IsActive);
        Assert.Equal(2, addedComponents.Count);
        Assert.Contains(button.GetComponent<NinePatchComponent>(), addedComponents);
        Assert.Contains(button.GetComponent<TextComponent>(), addedComponents);
    }

    /// <summary>
    /// Creates a button with small in-memory resources suitable for layout tests.
    /// </summary>
    /// <param name="label">The initial label for the button.</param>
    /// <returns>The configured button.</returns>
    private static TextButton CreateButton(string label) =>
        new(
            label,
            new TestTextStyle(),
            new Texture("button", 8, 8, new Color[64]),
            horizontalPadding: 4f,
            verticalPadding: 3f,
            sourceBorders: new Vector4D<float>(1f, 1f, 1f, 1f)
        );

    /// <summary>
    /// Provides a minimal game model for verifying button registration.
    /// </summary>
    private sealed class TestGameModel : IGameModel
    {
        private readonly Dictionary<GameObjectId, IGameObject> _gameObjects = [];

        /// <inheritdoc/>
        public IGameObject? GetGameObject(GameObjectId gameObjectId) =>
            _gameObjects.GetValueOrDefault(gameObjectId);

        /// <inheritdoc/>
        public void RegisterGameObject(IGameObject gameObject) =>
            _gameObjects[gameObject.Id] = gameObject;

        /// <inheritdoc/>
        public void UnregisterGameObject(IGameObject gameObject) =>
            _gameObjects.Remove(gameObject.Id);
    }

    /// <summary>
    /// Provides deterministic glyphs for button label measurement and rendering.
    /// </summary>
    private sealed class TestTextStyle : ITextStyle
    {
        /// <inheritdoc/>
        public ITexture Texture { get; } = new Texture("font", 2, 1, [Colors.White, Colors.White]);

        /// <inheritdoc/>
        public IReadOnlyDictionary<int, FontGlyph> Glyphs { get; } =
            new Dictionary<int, FontGlyph>
            {
                ['A'] = new('A', 1, new(0, 0, 1, 1), new(0, 0, 1, 1)),
                ['B'] = new('B', 1, new(0, 0, 1, 1), new(1, 0, 1, 1)),
            };

        /// <inheritdoc/>
        public FontMetrics FontMetrics { get; } = new(1, 1, 0, 1);

        /// <inheritdoc/>
        public MsdfMetadata Msdf { get; } = new(4, 1);

        /// <inheritdoc/>
        public IReadOnlyDictionary<
            (int LeftCodepoint, int RightCodepoint),
            double
        > Kerning { get; } = new Dictionary<(int, int), double>();

        /// <inheritdoc/>
        public Color Color { get; } = Colors.White;

        /// <inheritdoc/>
        public double Size { get; } = 1;
    }
}
