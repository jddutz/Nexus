using Nexus.Assets.Fonts;
using Nexus.Core;
using Nexus.Core.Events;
using Nexus.Game;
using Nexus.Graphics;
using Nexus.Graphics.Text;
using Nexus.Graphics.Textures;
using Nexus.GUI;
using Nexus.GUI.Elements;
using Nexus.Input;
using Nexus.Input.Devices;
using Nexus.Input.Events;
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
        first.Arrange(new Rectangle<float>(0f, 0f, 20f, 20f));
        second.Arrange(new Rectangle<float>(0f, 0f, 20f, 20f));

        Assert.Null(first.Parent);
        Assert.Null(first.Root);
        Assert.False(first.IsActivated);
        Assert.True(first.CanFocus);
        Assert.False(first.IsFocused);
        Assert.Equal(2, first.Components.Count());
        Assert.NotSame(first, second);
        Assert.NotSame(
            first.GetComponent<NinePatchComponent>(),
            second.GetComponent<NinePatchComponent>()
        );
        Assert.NotSame(DrawableTestData.TextGraphics(first), DrawableTestData.TextGraphics(second));
        Assert.Equal("B", first.Label);
        Assert.Equal(1UL, DrawableTestData.TextInstanceCount(first));
        Assert.Equal("A", second.Label);
        Assert.Equal(1UL, DrawableTestData.TextInstanceCount(second));
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
        Assert.Equal(1UL, DrawableTestData.TextInstanceCount(button));

        Assert.Equal(new Vector2D<float>(10f, 7f), button.Measure(new(100f, 100f)));
    }

    /// <summary>Verifies hiding removes visuals while retaining layout state for fresh components.</summary>
    [Fact]
    public void Visibility_removesAndRecreatesVisualComponents()
    {
        var scene = new Scene();
        var parent = new Element();
        var button = CreateButton("AB");
        var bounds = new Rectangle<float>(4f, 5f, 60f, 24f);
        button.Arrange(bounds);
        var originalBackground = button.GetComponent<NinePatchComponent>();
        var originalText = DrawableTestData.TextGraphics(button);
        var addedComponents = new List<IComponent>();
        var removedComponents = new List<IComponent>();
        scene.Children.Add(parent);
        scene.Activate();
        button.Components.ItemAdded += addedComponents.Add;
        button.Components.ItemRemoved += removedComponents.Add;
        parent.AddChild(button);

        button.IsVisible = false;

        Assert.Empty(button.Components);
        Assert.Equal(2, removedComponents.Count);
        Assert.Equal(Vector2D<float>.Zero, button.Measure(new(100f, 100f)));
        button.Arrange(new Rectangle<float>(0f, 0f, 10f, 10f));
        Assert.Equal(bounds, button.Bounds);
        button.Label = "BA";
        button.Padding = new(6f, 5f);

        button.IsVisible = true;

        var recreatedBackground = button.GetComponent<NinePatchComponent>();
        var recreatedText = DrawableTestData.TextGraphics(button);
        Assert.NotNull(recreatedBackground);
        Assert.NotNull(recreatedText);
        Assert.NotSame(originalBackground, recreatedBackground);
        Assert.NotSame(originalText, recreatedText);
        Assert.Equal(2UL, DrawableTestData.TextInstanceCount(button));
        Assert.Equal(2, addedComponents.Count);
        Assert.Equal(2, removedComponents.Count);
        Assert.Equal(bounds, button.Bounds);
        Assert.Equal(new Vector2D<float>(14f, 11f), button.Measure(new(100f, 100f)));
    }

    /// <summary>Verifies ancestor visibility removes and restores descendant button visuals.</summary>
    [Fact]
    public void AncestorVisibility_updatesDescendantVisualComponents()
    {
        var parent = new Element { IsVisible = false };
        var button = CreateButton("A");

        parent.AddChild(button);

        Assert.Empty(button.Components);
        parent.IsVisible = true;
        Assert.Equal(2, button.Components.Count());
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
        Assert.Equal(bounds, button.GetComponent<NinePatchComponent>()!.Destination);
        Assert.Equal(1UL, DrawableTestData.TextInstanceCount(button));
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
    /// Verifies a button added under an active scene is indexed in the scene hierarchy.
    /// </summary>
    [Fact]
    public void AddChild_registersButtonInScene()
    {
        var scene = new Scene();
        var parent = new GameObject();
        scene.Children.Add(parent);
        scene.Activate();
        var button = CreateButton("A");

        parent.AddChild(button);

        Assert.Same(parent, button.Parent);
        Assert.Same(scene, button.Root);
        Assert.Same(button, scene.GetSceneNode(button.Id));
        Assert.Contains(button, parent.Children);
        Assert.Equal(2, button.Components.Count);
    }

    /// <summary>Verifies the assigned action runs for a click anywhere within the full bounds.</summary>
    [Fact]
    public void ClickInsideFullBounds_invokesAssignedAction()
    {
        var (eventHub, button) = CreateAttachedButton();
        var mouse = new TestMouse(1);
        var actionCount = 0;
        button.Action = () => actionCount++;

        Publish(eventHub, new MouseButtonPressedEvent(mouse, MouseButtonEnum.Left, new(63f, 28f)));
        Publish(eventHub, new MouseButtonReleasedEvent(mouse, MouseButtonEnum.Left, new(63f, 28f)));

        Assert.Equal(1, actionCount);
    }

    /// <summary>Verifies an outside release or outside-origin press does not invoke the action.</summary>
    [Fact]
    public void ClickOutsideButton_doesNotInvokeAssignedAction()
    {
        var (eventHub, button) = CreateAttachedButton();
        var mouse = new TestMouse(2);
        var actionCount = 0;
        button.Action = () => actionCount++;

        Publish(eventHub, new MouseButtonPressedEvent(mouse, MouseButtonEnum.Left, new(10f, 10f)));
        Publish(
            eventHub,
            new MouseButtonReleasedEvent(mouse, MouseButtonEnum.Left, new(100f, 100f))
        );
        Publish(
            eventHub,
            new MouseButtonPressedEvent(mouse, MouseButtonEnum.Left, new(100f, 100f))
        );
        Publish(eventHub, new MouseButtonReleasedEvent(mouse, MouseButtonEnum.Left, new(10f, 10f)));

        Assert.Equal(0, actionCount);
    }

    /// <summary>Verifies a captured click uses the current bounds when it is released.</summary>
    [Fact]
    public void ClickAfterBoundsChange_usesCurrentBounds()
    {
        var (eventHub, button) = CreateAttachedButton();
        var mouse = new TestMouse(5);
        var actionCount = 0;
        button.Action = () => actionCount++;

        Publish(eventHub, new MouseButtonPressedEvent(mouse, MouseButtonEnum.Left, new(10f, 10f)));
        button.Arrange(new Rectangle<float>(8f, 8f, 4f, 4f));
        Publish(eventHub, new MouseButtonReleasedEvent(mouse, MouseButtonEnum.Left, new(10f, 10f)));

        Assert.Equal(1, actionCount);
    }

    /// <summary>Verifies another pointer cannot release or steal capture and detach clears it.</summary>
    [Fact]
    public void CapturedClick_ignoresOtherPointerAndResetsOnDetach()
    {
        var (eventHub, button, scene) = CreateAttachedButtonWithScene();
        var firstMouse = new TestMouse(3);
        var secondMouse = new TestMouse(4);
        var actionCount = 0;
        button.Action = () => actionCount++;

        Publish(
            eventHub,
            new MouseButtonPressedEvent(firstMouse, MouseButtonEnum.Left, new(10f, 10f))
        );
        Publish(
            eventHub,
            new MouseButtonPressedEvent(secondMouse, MouseButtonEnum.Left, new(10f, 10f))
        );
        Publish(
            eventHub,
            new MouseButtonReleasedEvent(secondMouse, MouseButtonEnum.Left, new(10f, 10f))
        );
        Assert.Equal(0, actionCount);
        Assert.True(scene.Children.Remove(button));
        button.Deactivate();
        eventHub.Publish(new GameObjectDeactivatedEvent(button));
        eventHub.Drain();

        scene.Children.Add(button);
        button.Activate();
        eventHub.Publish(new GameObjectActivatedEvent(button));
        eventHub.Drain();
        Publish(
            eventHub,
            new MouseButtonPressedEvent(firstMouse, MouseButtonEnum.Left, new(10f, 10f))
        );
        Publish(
            eventHub,
            new MouseButtonReleasedEvent(firstMouse, MouseButtonEnum.Left, new(10f, 10f))
        );

        Assert.Equal(1, actionCount);
    }

    /// <summary>Verifies input cancellation clears capture and permits a later click.</summary>
    [Fact]
    public void MouseCancellation_clearsCaptureAndAllowsFreshClick()
    {
        var (eventHub, button) = CreateAttachedButton();
        var mouse = new TestMouse(6);
        var actionCount = 0;
        button.Action = () => actionCount++;

        Publish(eventHub, new MouseButtonPressedEvent(mouse, MouseButtonEnum.Left, new(10f, 10f)));
        Publish(eventHub, new MouseCanceledEvent(mouse, new(10f, 10f)));
        Publish(eventHub, new MouseButtonReleasedEvent(mouse, MouseButtonEnum.Left, new(10f, 10f)));
        Publish(eventHub, new MouseButtonPressedEvent(mouse, MouseButtonEnum.Left, new(10f, 10f)));
        Publish(eventHub, new MouseButtonReleasedEvent(mouse, MouseButtonEnum.Left, new(10f, 10f)));

        Assert.Equal(1, actionCount);
    }

    /// <summary>Verifies the scene map owns focus and the button map owns controller selection.</summary>
    [Fact]
    public void ControllerSelection_usesSceneFocusAndButtonInputMaps()
    {
        var (eventHub, button) = CreateAttachedButton();
        var sceneInputMap = new InputMap(eventHub);
        var actionCount = 0;
        var buttonFocused = false;
        button.Action = () => actionCount++;
        button
            .InputMap.OnAnyControllerButtonPressed(ControllerSemanticNames.FaceBottom)
            .Invoke(() =>
            {
                if (buttonFocused)
                    button.Action?.Invoke();
            });
        sceneInputMap
            .OnAnyControllerButtonPressed(ControllerSemanticNames.DPadDown)
            .Invoke(() => buttonFocused = true);
        eventHub.Register(sceneInputMap);

        using var controller = new Controller(
            "Test controller",
            [
                new ButtonMapping(0, 0, ControllerSemanticNames.FaceBottom),
                new ButtonMapping(1, 1, ControllerSemanticNames.DPadDown),
            ],
            [],
            _ => false,
            _ => 0f,
            () => true
        );

        Publish(eventHub, new ControllerButtonPressedEvent(controller, controller.Button(0)));
        Assert.Equal(0, actionCount);

        Publish(eventHub, new ControllerButtonPressedEvent(controller, controller.Button(1)));
        Publish(eventHub, new ControllerButtonPressedEvent(controller, controller.Button(0)));

        Assert.Equal(1, actionCount);
    }

    /// <summary>Creates and activates a button with its element input map registered on an event hub.</summary>
    /// <returns>The event hub and button.</returns>
    private static (EventHub EventHub, TextButton Button) CreateAttachedButton()
    {
        var (eventHub, button, _) = CreateAttachedButtonWithScene();
        return (eventHub, button);
    }

    /// <summary>Creates an event hub, scene, and registered button for pointer tests.</summary>
    /// <returns>The event hub, button, and containing scene.</returns>
    private static (
        EventHub EventHub,
        TextButton Button,
        Scene Scene
    ) CreateAttachedButtonWithScene()
    {
        var eventHub = new EventHub();
        var gui = new GraphicalUserInterface(eventHub);
        var button = CreateButton("A");
        button.Arrange(new Rectangle<float>(4f, 5f, 60f, 24f));
        var scene = new Scene();
        scene.Children.Add(button);
        scene.Activate();
        button.Activate();
        gui.Initialize();
        eventHub.Publish(new SceneLoadedEvent(scene));
        eventHub.Drain();
        return (eventHub, button, scene);
    }

    /// <summary>Publishes one raw input event and drains the event hub once.</summary>
    /// <param name="eventHub">The event hub receiving the event.</param>
    /// <param name="inputEvent">The raw input event.</param>
    private static void Publish(EventHub eventHub, IEvent inputEvent)
    {
        eventHub.Publish(inputEvent);
        eventHub.Drain();
    }

    /// <summary>
    /// Creates a button with small in-memory resources suitable for layout tests.
    /// </summary>
    /// <param name="label">The initial label for the button.</param>
    /// <returns>The configured button.</returns>
    private static TextButton CreateButton(string label) =>
        new(
            new TestTextStyle(),
            new Texture("button", 8, 8, new Color[64]),
            horizontalPadding: 4f,
            verticalPadding: 3f,
            sourceBorders: new Vector4D<float>(1f, 1f, 1f, 1f)
        )
        {
            Label = label,
        };

    /// <summary>Provides a distinct mouse pointer identity for input-routing tests.</summary>
    private sealed class TestMouse(ulong id) : IMouseInputDevice
    {
        /// <inheritdoc />
        public InputDeviceId Id { get; } = id;

        /// <inheritdoc />
        public string Name => "Test mouse";

        /// <inheritdoc />
        public bool IsConnected => true;

        /// <inheritdoc />
        public Vector2D<float> Position => Vector2D<float>.Zero;

        /// <inheritdoc />
        public event Action<IMouseInputDevice, Vector2D<float>>? Moved
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        public event Action<IMouseInputDevice, MouseButtonEnum>? ButtonPressed
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        public event Action<IMouseInputDevice, MouseButtonEnum>? ButtonReleased
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        public event Action<IMouseInputDevice, Vector2D<float>>? WheelMoved
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        public bool IsButtonDown(MouseButtonEnum button) => false;
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
        > Kerning
        { get; } = new Dictionary<(int, int), double>();

        /// <inheritdoc/>
        public Color Color { get; } = Colors.White;

        /// <inheritdoc/>
        public double Size { get; } = 1;
    }
}
