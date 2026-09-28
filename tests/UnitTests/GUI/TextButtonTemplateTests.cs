using Nexus.Assets.Fonts;
using Nexus.Core;
using Nexus.Graphics;
using Nexus.Graphics.Text;
using Nexus.Graphics.Textures;
using Nexus.GUI;
using Silk.NET.Maths;

namespace Tests;

/// <summary>
/// Tests template-created text button composition, initialization, layout, and attachment.
/// </summary>
public sealed class TextButtonTemplateTests
{
    /// <summary>
    /// Verifies each call creates a detached composition and applies its initializer once.
    /// </summary>
    [Fact]
    public void Create_returnsFreshDetachedElementsAndInitializesAfterComposition()
    {
        var template = CreateTemplate();
        ITemplate<IGameObject> covariantTemplate = template;
        var initializerCalls = 0;
        var first = template.Create(button =>
        {
            initializerCalls++;
            Assert.Null(button.Parent);
            Assert.Null(button.GameModel);
            Assert.False(button.IsActive);
            Assert.Equal(2, button.Components.Count());
            Assert.Single(button.Components.OfType<NinePatchComponent>());
            Assert.Single(button.Components.OfType<TextComponent>());
            button.GetComponent<TextComponent>()!.Text = "B";
        });
        var second = Assert.IsType<Element>(covariantTemplate.Create());

        Assert.Equal(1, initializerCalls);
        Assert.NotSame(first, second);
        Assert.NotSame(
            first.GetComponent<NinePatchComponent>(),
            second.GetComponent<NinePatchComponent>()
        );
        Assert.NotSame(first.GetComponent<TextComponent>(), second.GetComponent<TextComponent>());
        Assert.Equal("B", first.GetComponent<TextComponent>()?.Text);
        Assert.Equal("A", second.GetComponent<TextComponent>()?.Text);
        Assert.Equal("A", template.DefaultLabel);
    }

    /// <summary>
    /// Verifies fitting the displayed label does not replace its complete measured source.
    /// </summary>
    [Fact]
    public void Arrange_preservesInitializerLabelForLaterMeasurement()
    {
        var element = CreateTemplate()
            .Create(button => button.GetComponent<TextComponent>()!.Text = "AB");

        Assert.Equal(new Vector2D<float>(10f, 7f), element.Measure(new(100f, 100f)));
        element.Arrange(new Rectangle<float>(0f, 0f, 9f, 7f));
        Assert.Equal("A", element.GetComponent<TextComponent>()?.Text);

        Assert.Equal(new Vector2D<float>(10f, 7f), element.Measure(new(100f, 100f)));
    }

    /// <summary>
    /// Verifies arranging keeps the complete button bounds independent of glyph bounds.
    /// </summary>
    [Fact]
    public void Arrange_keepsFullBoundsForBackgroundAndHitArea()
    {
        var button = CreateTemplate().Create();
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
    /// Verifies a throwing initializer cannot attach a partially created object.
    /// </summary>
    [Fact]
    public void Create_initializerFailureLeavesParentUnchanged()
    {
        var parent = new GameObject();

        Assert.Throws<InvalidOperationException>(() =>
            parent.AddChild(CreateTemplate().Create(_ => throw new InvalidOperationException()))
        );

        Assert.Empty(parent.Children);
    }

    /// <summary>
    /// Verifies AddChild performs model registration and activation for a created button.
    /// </summary>
    [Fact]
    public void AddChild_registersAndActivatesTemplateCreatedElement()
    {
        var parent = new GameObject();
        var gameModel = new TestGameModel();
        parent.SetGameModel(gameModel);
        parent.Activate();
        var addedComponents = new List<IComponent>();
        parent.ComponentAdded += addedComponents.Add;
        var button = CreateTemplate().Create();

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
    /// Creates a template with small in-memory resources suitable for layout tests.
    /// </summary>
    /// <returns>The configured text button template.</returns>
    private static TextButtonTemplate CreateTemplate() =>
        new(
            new TestTextStyle(),
            new Texture("button", 8, 8, new Color[64]),
            "A",
            horizontalPadding: 4f,
            verticalPadding: 3f,
            sourceBorders: new Vector4D<float>(1f, 1f, 1f, 1f)
        );

    /// <summary>
    /// Provides a minimal game model for verifying template-created object registration.
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
