namespace HelloNexus;

using Nexus.Assets.Fonts;
using Nexus.Core;
using Nexus.Game;
using Nexus.Graphics;
using Nexus.Graphics.Cameras;
using Nexus.Graphics.Components;
using Nexus.Graphics.Text;
using Nexus.Graphics.Textures;
using Nexus.GUI;
using Nexus.GUI.Elements;
using Nexus.Input;
using Nexus.Input.Devices;
using Silk.NET.Maths;

/// <summary>
/// Creates the HelloNexus welcome scene and its application-specific content.
/// </summary>
/// <param name="windowService">Provides the main window dimensions.</param>
/// <param name="contentManifest">Describes the content available to the application.</param>
/// <param name="fontBuilder">Builds font data for the welcome text.</param>
/// <param name="textureProvider">Loads textures from the content library.</param>
internal sealed class HelloNexusSceneFactory(
    IWindowService windowService,
    IContentManifest contentManifest,
    IFontBuilder fontBuilder,
    IContentProvider<Texture> textureProvider
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
        var mainWindow = windowService.GetMainWindow();
        var scene = new Scene(nodeId);
        scene.InputMap = inputMap;

        var sceneView = scene.Children.OfType<IGameObject>().Single();
        var camera = sceneView.Components.OfType<StaticCamera>().Single();
        camera.SetViewportSize(mainWindow.Size.X, mainWindow.Size.Y);

        const ulong backgroundLayer = 1;
        const ulong foregroundLayer = 2;
        var backgroundView = sceneView.Components.OfType<ViewComponent>().Single();
        backgroundView.Name = "Background";
        backgroundView.LayerMask = backgroundLayer;

        var foregroundView = scene.CreateChild<View>();
        foregroundView.ViewComponent.Name = "Foreground";
        foregroundView.ViewComponent.Camera = camera;
        foregroundView.ViewComponent.LayerMask = foregroundLayer;
        foregroundView.ViewComponent.RenderOrder = 1;

        var backgroundTexture = new TextureComponent
        {
            Texture = textureProvider.Get((ContentId)"hello_nexus_background_image"),
            Destination = new Rectangle<float>(0f, 0f, mainWindow.Size.X, mainWindow.Size.Y),
            RenderLayerMask = backgroundLayer,
        };
        var backgroundElement = new BackgroundElement(backgroundTexture);
        scene.Children.Add(backgroundElement);

        var textStyle = CreateRobotoTextStyle();
        const string pressText = "Press ESC to quit";
        var pressTextElement = new TextElement(pressText, textStyle, 1, foregroundLayer);

        const string welcomeText = "Welcome to the Nexus";
        var welcomeTextElement = new TextElement(
            welcomeText,
            textStyle,
            renderLayerMask: foregroundLayer
        );

        const string buttonLabel = "Start Physics Test";
        var buttonElement = new TextButton(
            textStyle,
            textureProvider.Get((ContentId)"button_texture"),
            horizontalPadding: 16f,
            verticalPadding: 10f,
            backgroundRenderLayerMask: backgroundLayer,
            textRenderLayerMask: foregroundLayer,
            sourceBorders: new(64f, 64f, 64f, 64f)
        ) { Label = buttonLabel };
        var buttonFocused = false;
        buttonElement.Action = () => buttonElement.Label = "Physics Test Started";
        buttonElement
            .InputMap.OnAnyControllerButtonPressed(ControllerSemanticNames.FaceBottom)
            .Invoke(() =>
            {
                if (buttonFocused)
                    buttonElement.Action?.Invoke();
            });
        foreach (
            var direction in new[]
            {
                ControllerSemanticNames.DPadUp,
                ControllerSemanticNames.DPadRight,
                ControllerSemanticNames.DPadDown,
                ControllerSemanticNames.DPadLeft,
            }
        )
            inputMap.OnAnyControllerButtonPressed(direction).Invoke(() => buttonFocused = true);
        const float buttonLabelGap = 10f;

        var audioTexture = textureProvider.Get((ContentId)"icon_audio_on");
        var leftIcon = CreateAudioIconElement(audioTexture, foregroundLayer);
        var rightIcon = CreateAudioIconElement(audioTexture, foregroundLayer);
        var middleHeader = new MiddleHeaderElement(pressTextElement);

        var header = new HeaderElement(leftIcon, middleHeader, rightIcon);

        var main = new MainContentElement(welcomeTextElement, buttonElement, buttonLabelGap);

        var textLayout = new TextLayoutElement(header, main);
        scene.Children.Add(textLayout);

        return scene;
    }

    /// <summary>Creates an audio icon element with a nominal fixed 48-by-48 size.</summary>
    /// <param name="texture">The audio icon texture.</param>
    /// <param name="renderLayerMask">The render layer selected by the header view.</param>
    /// <returns>The arranged icon element.</returns>
    private static Element CreateAudioIconElement(Texture texture, ulong renderLayerMask)
    {
        return new AudioIconElement(texture, renderLayerMask);
    }

    /// <summary>
    /// Covers the available bounds with a centered aspect-preserving background texture.
    /// </summary>
    private sealed class BackgroundElement : Element
    {
        private readonly TextureComponent _texture;

        /// <summary>
        /// Initializes the background element with its texture component.
        /// </summary>
        /// <param name="texture">The background texture component.</param>
        public BackgroundElement(TextureComponent texture)
            : base(components: [texture])
        {
            _texture = texture;
        }

        /// <inheritdoc />
        public override void Arrange(Rectangle<float> bounds)
        {
            base.Arrange(bounds);
            var texture = _texture.Texture!;
            var boundsAspectRatio = bounds.Size.X / bounds.Size.Y;
            var textureAspectRatio = (float)texture.Width / texture.Height;
            var imageSize =
                boundsAspectRatio > textureAspectRatio
                    ? new Vector2D<float>(bounds.Size.X, bounds.Size.X / textureAspectRatio)
                    : new Vector2D<float>(bounds.Size.Y * textureAspectRatio, bounds.Size.Y);

            _texture.Destination = new Rectangle<float>(
                bounds.Origin.X + (bounds.Size.X - imageSize.X) / 2f,
                bounds.Origin.Y + (bounds.Size.Y - imageSize.Y) / 2f,
                imageSize.X,
                imageSize.Y
            );
        }
    }

    /// <summary>Arranges a fitted text element within the middle header.</summary>
    private sealed class MiddleHeaderElement : Element
    {
        private readonly TextElement _textElement;

        /// <summary>Initializes the header around its text element.</summary>
        /// <param name="textElement">The fitted instruction text.</param>
        public MiddleHeaderElement(TextElement textElement)
        {
            _textElement = textElement;
            AddChild(textElement);
        }

        /// <inheritdoc />
        public override void Arrange(Rectangle<float> bounds)
        {
            Bounds = bounds;
            _textElement.Arrange(bounds);
        }
    }

    /// <summary>Arranges the two audio icons around the centered middle header.</summary>
    private sealed class HeaderElement : Element
    {
        private readonly Element _leftIcon;
        private readonly Element _middle;
        private readonly Element _rightIcon;

        /// <summary>Initializes the header with its three arranged children.</summary>
        /// <param name="leftIcon">The leading audio icon.</param>
        /// <param name="middle">The centered instruction element.</param>
        /// <param name="rightIcon">The trailing audio icon.</param>
        public HeaderElement(Element leftIcon, Element middle, Element rightIcon)
        {
            _leftIcon = leftIcon;
            _middle = middle;
            _rightIcon = rightIcon;
            AddChild(leftIcon);
            AddChild(middle);
            AddChild(rightIcon);
        }

        /// <inheritdoc />
        public override Vector2D<float> Measure(Vector2D<float> constraint) =>
            new(constraint.X, MathF.Min(48f, constraint.Y));

        /// <inheritdoc />
        public override void Arrange(Rectangle<float> bounds)
        {
            Bounds = bounds;
            const float sectionPadding = 10f;
            const float iconSize = 48f;
            var gutter = MathF.Min(sectionPadding, bounds.Size.X / 2f);
            var sideWidth = MathF.Min(iconSize, MathF.Max(0f, bounds.Size.X - gutter * 2f) / 2f);
            var middleWidth = MathF.Max(0f, bounds.Size.X - sideWidth * 2f - gutter * 2f);

            _leftIcon.Arrange(
                new Rectangle<float>(bounds.Origin, new Vector2D<float>(sideWidth, bounds.Size.Y))
            );
            _middle.Arrange(
                new Rectangle<float>(
                    new Vector2D<float>(bounds.Origin.X + sideWidth + gutter, bounds.Origin.Y),
                    new Vector2D<float>(middleWidth, bounds.Size.Y)
                )
            );
            _rightIcon.Arrange(
                new Rectangle<float>(
                    new Vector2D<float>(
                        bounds.Origin.X + bounds.Size.X - sideWidth,
                        bounds.Origin.Y
                    ),
                    new Vector2D<float>(sideWidth, bounds.Size.Y)
                )
            );
        }
    }

    /// <summary>Centers the welcome text and Physics button as one content group.</summary>
    private sealed class MainContentElement : Element
    {
        private readonly TextElement _welcomeElement;
        private readonly TextButton _button;
        private readonly float _buttonGap;

        /// <summary>Initializes the content group with its text and button children.</summary>
        /// <param name="welcomeElement">The text element arranged above the button.</param>
        /// <param name="button">The Physics button.</param>
        /// <param name="buttonGap">The vertical gap between text and button.</param>
        public MainContentElement(TextElement welcomeElement, TextButton button, float buttonGap)
        {
            _welcomeElement = welcomeElement;
            _button = button;
            _buttonGap = buttonGap;
            AddChild(welcomeElement);
            AddChild(button);
        }

        /// <inheritdoc />
        public override Vector2D<float> Measure(Vector2D<float> constraint) => constraint;

        /// <inheritdoc />
        public override void Arrange(Rectangle<float> bounds)
        {
            Bounds = bounds;
            var buttonSize = _button.Measure(bounds.Size);
            var welcomeAvailableHeight = MathF.Max(0f, bounds.Size.Y - buttonSize.Y - _buttonGap);
            var welcomeSize = _welcomeElement.Measure(
                new Vector2D<float>(bounds.Size.X, welcomeAvailableHeight)
            );
            var groupHeight = welcomeSize.Y + _buttonGap + buttonSize.Y;
            var groupTop = bounds.Origin.Y + (bounds.Size.Y - groupHeight) / 2f;
            _welcomeElement.Arrange(
                new Rectangle<float>(
                    new Vector2D<float>(bounds.Origin.X, groupTop),
                    new Vector2D<float>(bounds.Size.X, welcomeSize.Y)
                )
            );
            _button.Arrange(
                new Rectangle<float>(
                    new Vector2D<float>(
                        MathF.Round(bounds.Origin.X + (bounds.Size.X - buttonSize.X) / 2f),
                        MathF.Round(groupTop + welcomeSize.Y + _buttonGap)
                    ),
                    buttonSize
                )
            );
        }
    }

    /// <summary>
    /// Places the header and main content within the welcome screen bounds.
    /// </summary>
    private sealed class TextLayoutElement : Element
    {
        private readonly HeaderElement _header;
        private readonly MainContentElement _main;

        /// <summary>
        /// Initializes the screen layout with its header and main content.
        /// </summary>
        /// <param name="header">The top header.</param>
        /// <param name="main">The centered main content.</param>
        public TextLayoutElement(HeaderElement header, MainContentElement main)
        {
            _header = header;
            _main = main;
            AddChild(header);
            AddChild(main);
        }

        /// <inheritdoc />
        public override void Arrange(Rectangle<float> bounds)
        {
            Bounds = bounds;

            const float headerMargin = 10f;
            const float mainMargin = 24f;
            const float sectionPadding = 10f;
            var headerAvailable = new Vector2D<float>(
                MathF.Max(0f, bounds.Size.X - headerMargin * 2f),
                MathF.Max(0f, bounds.Size.Y - headerMargin * 2f)
            );
            var headerSize = _header.Measure(headerAvailable);
            _header.Arrange(
                new Rectangle<float>(
                    new Vector2D<float>(
                        bounds.Origin.X + headerMargin,
                        bounds.Origin.Y + headerMargin
                    ),
                    headerSize
                )
            );

            var mainOriginY = bounds.Origin.Y + headerMargin + headerSize.Y + sectionPadding;
            var mainHeight = MathF.Max(
                0f,
                bounds.Size.Y - (mainOriginY - bounds.Origin.Y) - mainMargin
            );
            _main.Arrange(
                new Rectangle<float>(
                    new Vector2D<float>(bounds.Origin.X + mainMargin, mainOriginY),
                    new Vector2D<float>(MathF.Max(0f, bounds.Size.X - mainMargin * 2f), mainHeight)
                )
            );
        }
    }

    /// <summary>
    /// Measures and crops an audio icon within a square maximum size.
    /// </summary>
    private sealed class AudioIconElement : Element
    {
        private const float IconSize = 48f;
        private readonly TextureComponent _texture;

        /// <summary>
        /// Initializes an audio icon with its texture and render layer.
        /// </summary>
        /// <param name="texture">The icon texture.</param>
        /// <param name="renderLayerMask">The render layer selected by the header view.</param>
        public AudioIconElement(Texture texture, ulong renderLayerMask)
            : this(new TextureComponent { Texture = texture, RenderLayerMask = renderLayerMask })
        { }

        /// <summary>
        /// Initializes the icon from its owned texture component.
        /// </summary>
        /// <param name="texture">The owned icon texture component.</param>
        private AudioIconElement(TextureComponent texture)
            : base(components: [texture]) => _texture = texture;

        /// <inheritdoc />
        public override Vector2D<float> Measure(Vector2D<float> constraint) =>
            new(MathF.Min(IconSize, constraint.X), MathF.Min(IconSize, constraint.Y));

        /// <inheritdoc />
        public override void Arrange(Rectangle<float> bounds)
        {
            var visibleSize = new Vector2D<float>(
                MathF.Min(IconSize, bounds.Size.X),
                MathF.Min(IconSize, bounds.Size.Y)
            );
            var cropRatio = new Vector2D<float>(visibleSize.X / IconSize, visibleSize.Y / IconSize);
            var cropOrigin = new Vector2D<float>((1f - cropRatio.X) / 2f, (1f - cropRatio.Y) / 2f);
            Bounds = new Rectangle<float>(
                new Vector2D<float>(
                    bounds.Origin.X + (bounds.Size.X - visibleSize.X) / 2f,
                    bounds.Origin.Y + (bounds.Size.Y - visibleSize.Y) / 2f
                ),
                visibleSize
            );
            _texture.Destination = new Rectangle<float>(Bounds.Origin, visibleSize);
            _texture.TexCoord = new(cropOrigin.X, cropOrigin.Y, cropRatio.X, cropRatio.Y);
        }
    }

    /// <summary>Builds the Roboto text style from the font registered as <c>ui.default</c>.</summary>
    /// <returns>The generated style at size 16 with an off-white color.</returns>
    private ITextStyle CreateRobotoTextStyle()
    {
        var fontId = (ContentId)"ui.default";
        var fontPath = Path.Combine(
            contentManifest.ContentLibraryPath,
            contentManifest.Fonts.GetContentFilePath(fontId)
        );
        var codepoints = new FontGlyphRepertoire().GetCodepoints();
        var font = fontBuilder.Build(fontPath, codepoints, new FontGenerationSettings());
        var atlas = font.Atlas;
        var colors = new Color[checked(atlas.Width * atlas.Height)];

        for (var index = 0; index < colors.Length; index++)
        {
            var sourceOffset = index * 3;
            colors[index] = new Color(
                atlas.Pixels[sourceOffset],
                atlas.Pixels[sourceOffset + 1],
                atlas.Pixels[sourceOffset + 2]
            );
        }

        var texture = new Texture(
            (ContentId)"Roboto",
            checked((uint)atlas.Width),
            checked((uint)atlas.Height),
            colors
        );

        return new TextStyle(font, texture, 16, Colors.WhiteSmoke);
    }
}
