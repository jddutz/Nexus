namespace HelloNexus;

using System.Text;
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
    /// <param name="sceneId">The identifier assigned to the scene.</param>
    /// <param name="gameModel">Associates game objects with their owner before components are attached.</param>
    /// <param name="inputMap">The scene's keyboard bindings.</param>
    /// <returns>The configured initial scene.</returns>
    public Scene Create(SceneId sceneId, IGameModel gameModel, InputMap inputMap)
    {
        var mainWindow = windowService.GetMainWindow();
        var scene = new Scene(sceneId) { InputMap = inputMap };
        scene.SetGameModel(gameModel);

        var sceneView = scene.Children.Single();
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
            Size = new(mainWindow.Size.X, mainWindow.Size.Y),
            RenderLayerMask = backgroundLayer,
        };
        var backgroundElement = new BackgroundElement(backgroundTexture);
        scene.AddChild(backgroundElement);

        var textStyle = CreateRobotoTextStyle();
        const string pressText = "Press ESC to quit";
        var pressTextComponent = new TextComponent(textStyle)
        {
            RenderLayerMask = foregroundLayer,
            Text = pressText,
        };
        var pressTextElement = new TextContentElement(pressTextComponent, textStyle, 1);

        const string welcomeText = "Welcome to the Nexus";
        var welcomeTextComponent = new TextComponent(textStyle)
        {
            RenderLayerMask = foregroundLayer,
            Text = welcomeText,
        };
        var welcomeTextElement = new TextContentElement(welcomeTextComponent, textStyle);

        const string buttonLabel = "Start Physics Test";
        var buttonElement = new TextButton(
            buttonLabel,
            textStyle,
            textureProvider.Get((ContentId)"button_texture"),
            horizontalPadding: 16f,
            verticalPadding: 10f,
            backgroundRenderLayerMask: backgroundLayer,
            textRenderLayerMask: foregroundLayer
        );
        const float buttonLabelGap = 10f;

        var audioTexture = textureProvider.Get((ContentId)"icon_audio_on");
        var leftIcon = CreateAudioIconElement(audioTexture, foregroundLayer);
        var rightIcon = CreateAudioIconElement(audioTexture, foregroundLayer);
        var middleHeader = new MiddleHeaderElement(
            textStyle,
            pressText,
            pressTextComponent,
            pressTextElement
        );
        middleHeader.AddChild(pressTextElement);

        var header = new HeaderElement(leftIcon, middleHeader, rightIcon);
        header.AddChild(leftIcon);
        header.AddChild(middleHeader);
        header.AddChild(rightIcon);

        var main = new MainContentElement(
            textStyle,
            welcomeText,
            welcomeTextElement,
            buttonElement,
            buttonLabelGap
        );
        main.AddChild(welcomeTextElement);
        main.AddChild(buttonElement);

        var textLayout = new TextLayoutElement(header, main);
        textLayout.AddChild(header);
        textLayout.AddChild(main);
        scene.AddChild(textLayout);

        return scene;
    }

    /// <summary>Measures wrapped text without changing the text component or element.</summary>
    /// <param name="text">The complete source text.</param>
    /// <param name="style">The font metrics used for wrapping.</param>
    /// <param name="availableSize">The maximum available size.</param>
    /// <param name="maximumLines">The maximum allowed line count.</param>
    /// <returns>The visible glyph bounds size after wrapping.</returns>
    private static Vector2D<float> MeasureText(
        string text,
        ITextStyle style,
        Vector2D<float> availableSize,
        int maximumLines
    )
    {
        var scale = style.FontMetrics.EmSize == 0 ? 1.0 : style.Size / style.FontMetrics.EmSize;
        var lineHeight = (float)(style.FontMetrics.LineHeight * scale);
        if (!float.IsFinite(lineHeight) || lineHeight <= 0f)
            lineHeight = (float)style.Size;

        var lineCount =
            lineHeight > 0f
                ? Math.Min(maximumLines, (int)MathF.Floor(availableSize.Y / lineHeight))
                : 0;
        var wrappedText = WrapText(text, style, availableSize.X, lineCount);
        return MeasureWrappedText(style, wrappedText);
    }

    /// <summary>Measures the combined glyph bounds of newline-separated text spans.</summary>
    /// <param name="style">The font metrics used for wrapping.</param>
    /// <param name="text">The wrapped text.</param>
    /// <returns>The combined glyph bounds size.</returns>
    private static Vector2D<float> MeasureWrappedText(ITextStyle style, string text)
    {
        if (string.IsNullOrEmpty(text))
            return Vector2D<float>.Zero;

        var scale = style.FontMetrics.EmSize == 0 ? 1.0 : style.Size / style.FontMetrics.EmSize;
        var lineHeight = (float)(style.FontMetrics.LineHeight * scale);
        var lines = text.Split('\n');
        var top = float.PositiveInfinity;
        var bottom = float.NegativeInfinity;
        var width = 0f;

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var bounds = new TextSpan(style, lines[lineIndex]).LayoutBounds;
            width = MathF.Max(width, bounds.Size.X);
            top = MathF.Min(top, bounds.Origin.Y + lineIndex * lineHeight);
            bottom = MathF.Max(bottom, bounds.Max.Y + lineIndex * lineHeight);
        }

        return float.IsFinite(top) && float.IsFinite(bottom)
            ? new Vector2D<float>(width, bottom - top)
            : Vector2D<float>.Zero;
    }

    /// <summary>Gets the maximum number of text lines that fit vertically.</summary>
    /// <param name="style">The font metrics used to determine line height.</param>
    /// <param name="availableHeight">The available vertical space.</param>
    /// <returns>The maximum number of lines.</returns>
    private static int GetMaximumLineCount(ITextStyle style, float availableHeight)
    {
        var scale = style.FontMetrics.EmSize == 0 ? 1.0 : style.Size / style.FontMetrics.EmSize;
        var lineHeight = (float)(style.FontMetrics.LineHeight * scale);
        if (!float.IsFinite(lineHeight) || lineHeight <= 0f)
            lineHeight = (float)style.Size;

        return lineHeight > 0f ? Math.Max(0, (int)MathF.Floor(availableHeight / lineHeight)) : 0;
    }

    /// <summary>Wraps words to the available width, cropping the last line at glyph boundaries.</summary>
    /// <param name="text">The complete source text.</param>
    /// <param name="style">The font metrics used for measuring and wrapping.</param>
    /// <param name="availableWidth">The maximum line width.</param>
    /// <param name="maximumLines">The maximum number of lines.</param>
    /// <returns>The visible lines joined by newline characters.</returns>
    private static string WrapText(
        string text,
        ITextStyle style,
        float availableWidth,
        int maximumLines
    )
    {
        if (availableWidth <= 0f || maximumLines <= 0)
            return string.Empty;

        var lines = new List<string>();
        var currentLine = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = currentLine.Length == 0 ? word : $"{currentLine} {word}";
            if (MeasureTextWidth(style, candidate) <= availableWidth)
            {
                currentLine = candidate;
                continue;
            }

            if (currentLine.Length > 0)
            {
                if (lines.Count + 1 >= maximumLines)
                {
                    lines.Add(FitTextToWidth(style, candidate, availableWidth));
                    return string.Join('\n', lines);
                }

                lines.Add(currentLine);
                currentLine = string.Empty;
            }

            var remainder = word;
            while (MeasureTextWidth(style, remainder) > availableWidth)
            {
                var fittingPrefix = FitTextToWidth(style, remainder, availableWidth);
                if (fittingPrefix.Length == 0)
                    return string.Join('\n', lines);

                lines.Add(fittingPrefix);
                remainder = remainder[fittingPrefix.Length..];
                if (lines.Count >= maximumLines)
                    return string.Join('\n', lines);
            }

            currentLine = remainder;
        }

        if (currentLine.Length > 0 && lines.Count < maximumLines)
            lines.Add(currentLine);

        return string.Join('\n', lines);
    }

    /// <summary>Measures the visible glyph width of a candidate line.</summary>
    /// <param name="style">The font metrics used to measure glyphs.</param>
    /// <param name="text">The candidate line.</param>
    /// <returns>The candidate's visible glyph width.</returns>
    private static float MeasureTextWidth(ITextStyle style, string text) =>
        new TextSpan(style, text).LayoutBounds.Size.X;

    /// <summary>Returns the longest leading rune sequence that fits in a line.</summary>
    /// <param name="style">The font metrics used to measure glyphs.</param>
    /// <param name="text">The text to crop.</param>
    /// <param name="availableWidth">The maximum line width.</param>
    /// <returns>The fitting text prefix.</returns>
    private static string FitTextToWidth(ITextStyle style, string text, float availableWidth)
    {
        var prefix = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            var candidate = prefix.ToString() + rune;
            if (MeasureTextWidth(style, candidate) > availableWidth)
                break;

            prefix.Append(rune);
        }

        return prefix.ToString();
    }

    /// <summary>Creates an audio icon element with a nominal fixed 48-by-48 size.</summary>
    /// <param name="texture">The audio icon texture.</param>
    /// <param name="renderLayerMask">The render layer selected by the header view.</param>
    /// <returns>The arranged icon element.</returns>
    private static Element CreateAudioIconElement(Texture texture, ulong renderLayerMask)
    {
        return new AudioIconElement(texture, renderLayerMask);
    }

    /// <summary>Centers visible glyphs and assigns their actual bounds to a text element.</summary>
    /// <param name="element">The text element being arranged.</param>
    /// <param name="bounds">The rectangle assigned by the parent.</param>
    /// <param name="textComponent">The component containing the visible spans.</param>
    private static void ArrangeText(
        Element element,
        Rectangle<float> bounds,
        TextComponent textComponent
    )
    {
        element.Bounds = bounds;
        var textBounds = textComponent.LayoutBounds;
        var textOrigin = new Vector2D<float>(
            bounds.Origin.X + (bounds.Size.X - textBounds.Size.X) / 2f,
            bounds.Origin.Y + (bounds.Size.Y - textBounds.Size.Y) / 2f
        );
        element.Bounds = new Rectangle<float>(textOrigin, textBounds.Size);
        element.Position = new(
            textOrigin.X - textBounds.Origin.X,
            textOrigin.Y - textBounds.Origin.Y
        );
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

            Position = bounds.Origin;
            _texture.Size = imageSize;
            _texture.TransformationMatrix = Matrix4X4.CreateTranslation(
                (bounds.Size.X - imageSize.X) / 2f,
                (bounds.Size.Y - imageSize.Y) / 2f,
                0f
            );
        }
    }

    /// <summary>
    /// Measures and arranges a text component as a centered text element.
    /// </summary>
    private sealed class TextContentElement : Element
    {
        private readonly ITextStyle _style;
        private readonly int? _maximumLines;

        /// <summary>
        /// Gets the text component owned by this element.
        /// </summary>
        public TextComponent TextComponent { get; }

        /// <summary>
        /// Initializes the text element with its style and optional line limit.
        /// </summary>
        /// <param name="textComponent">The text component owned by the element.</param>
        /// <param name="style">The style used for text measurement.</param>
        /// <param name="maximumLines">The fixed maximum line count, or <see langword="null"/> to fit the height.</param>
        public TextContentElement(
            TextComponent textComponent,
            ITextStyle style,
            int? maximumLines = null
        )
            : base(components: [textComponent])
        {
            TextComponent = textComponent;
            _style = style;
            _maximumLines = maximumLines;
        }

        /// <inheritdoc />
        public override Vector2D<float> Measure(Vector2D<float> constraint) =>
            MeasureText(
                TextComponent.Text,
                _style,
                constraint,
                _maximumLines ?? GetMaximumLineCount(_style, constraint.Y)
            );

        /// <inheritdoc />
        public override void Arrange(Rectangle<float> bounds)
        {
            base.Arrange(bounds);
            ArrangeText(this, bounds, TextComponent);
        }
    }

    /// <summary>
    /// Fits the header's press instruction and arranges its text element.
    /// </summary>
    private sealed class MiddleHeaderElement : Element
    {
        private readonly ITextStyle _style;
        private readonly string _label;
        private readonly TextComponent _text;
        private readonly TextContentElement _textElement;

        /// <summary>
        /// Initializes the middle header around its text element.
        /// </summary>
        /// <param name="style">The style used to fit the label.</param>
        /// <param name="label">The complete press instruction.</param>
        /// <param name="text">The text component to update.</param>
        /// <param name="textElement">The element that arranges the text.</param>
        public MiddleHeaderElement(
            ITextStyle style,
            string label,
            TextComponent text,
            TextContentElement textElement
        )
        {
            _style = style;
            _label = label;
            _text = text;
            _textElement = textElement;
            AddChild(textElement);
        }

        /// <inheritdoc />
        public override void Arrange(Rectangle<float> bounds)
        {
            Bounds = bounds;
            var visibleText = FitTextToWidth(_style, _label, bounds.Size.X);
            if (_text.Text != visibleText)
                _text.Text = visibleText;

            _textElement.Arrange(bounds);
        }
    }

    /// <summary>
    /// Arranges the two audio icons around the centered middle header.
    /// </summary>
    private sealed class HeaderElement : Element
    {
        private readonly Element _leftIcon;
        private readonly Element _middle;
        private readonly Element _rightIcon;

        /// <summary>
        /// Initializes the header with its three arranged children.
        /// </summary>
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

    /// <summary>
    /// Centers the welcome text and Physics button as one content group.
    /// </summary>
    private sealed class MainContentElement : Element
    {
        private readonly ITextStyle _style;
        private readonly string _welcomeText;
        private readonly TextContentElement _welcomeElement;
        private readonly TextButton _button;
        private readonly float _buttonGap;

        /// <summary>
        /// Initializes the content group with its text and button children.
        /// </summary>
        /// <param name="style">The style used to wrap the welcome text.</param>
        /// <param name="welcomeText">The complete welcome message.</param>
        /// <param name="welcomeElement">The text element arranged above the button.</param>
        /// <param name="button">The Physics button.</param>
        /// <param name="buttonGap">The vertical gap between text and button.</param>
        public MainContentElement(
            ITextStyle style,
            string welcomeText,
            TextContentElement welcomeElement,
            TextButton button,
            float buttonGap
        )
        {
            _style = style;
            _welcomeText = welcomeText;
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
            var wrappedText = WrapText(
                _welcomeText,
                _style,
                bounds.Size.X,
                GetMaximumLineCount(_style, welcomeAvailableHeight)
            );
            if (_welcomeElement.TextComponent.Text != wrappedText)
                _welcomeElement.TextComponent.Text = wrappedText;

            var welcomeSize = MeasureWrappedText(_style, wrappedText);
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
            : this(
                new TextureComponent
                {
                    Texture = texture,
                    Size = new Vector2D<float>(IconSize, IconSize),
                    RenderLayerMask = renderLayerMask,
                }
            ) { }

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
            Position = Bounds.Origin;
            _texture.Size = visibleSize;
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
