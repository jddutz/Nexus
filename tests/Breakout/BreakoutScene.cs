namespace Nexus.Samples.Breakout;

using Nexus.Core;
using Nexus.Core.Events;
using Nexus.Game;
using Nexus.GUI;
using Nexus.GUI.Elements;
using Nexus.Graphics;
using Nexus.Graphics.Cameras;
using Nexus.Graphics.Text;
using Nexus.Graphics.Textures;
using Nexus.Input;
using Nexus.Input.Events;
using Silk.NET.Maths;

/// <summary>Builds and presents the fixed-ratio Breakout sample.</summary>
[Scene("Breakout")]
public sealed class BreakoutScene : Scene
{
    private readonly IInputSystem _input;
    private readonly IWindowService _windowService;
    private readonly StaticCamera _camera = new();
    private readonly BreakoutGameState _game = new();
    private readonly Element _playfield = new()
    {
        HorizontalAlignment = AlignHorizontal.Center,
        VerticalAlignment = AlignVertical.Center,
    };
    private readonly ImageElement _playfieldBackground = new()
    {
        Texture = BuiltInTextures.Uniform,
        SizingMode = ImageSizingMode.Stretch,
        Color = Colors.Black,
        HorizontalAlignment = AlignHorizontal.Left,
        VerticalAlignment = AlignVertical.Top,
        SortOrder = -100,
    };
    private readonly ImageElement _paddle = CreateSolidImage(Colors.WhiteSmoke);
    private readonly ImageElement _ball = CreateSolidImage(Colors.White);
    private readonly TextElement _scoreText;
    private readonly TextElement _livesText;
    private readonly TextElement _statusText;
    private readonly HashSet<KeyEnum> _pressedKeys = [];
    private Vector2D<int> _lastWindowSize;
    private float _scale = 1f;

    /// <summary>Creates the scene, game state, camera, and one-shot input bindings.</summary>
    /// <param name="textStyles">Provides the built-in font styles.</param>
    /// <param name="eventHub">Dispatches key transitions.</param>
    /// <param name="input">Provides held keyboard state.</param>
    /// <param name="windowService">Provides the current window size.</param>
    public BreakoutScene(
        ITextStyleRegistry textStyles,
        IEventHub eventHub,
        IInputSystem input,
        IWindowService windowService
    )
    {
        ArgumentNullException.ThrowIfNull(textStyles);
        ArgumentNullException.ThrowIfNull(eventHub);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(windowService);
        _input = input;
        _windowService = windowService;
        MainCamera = _camera;
        _scoreText = CreateText(textStyles, 24);
        _livesText = CreateText(textStyles, 24);
        _statusText = CreateText(textStyles, 24);
        _statusText.MaximumLines = 2;
        InputMap = CreateInputMap(eventHub);
        _lastWindowSize = windowService.GetMainWindow().Size;
        UpdateLayout(_lastWindowSize);
    }

    /// <summary>Initializes the background, HUD, playfield, paddle, ball, and bricks.</summary>
    public override void Initialize()
    {
        base.Initialize();
        Children.Add(new View { Camera = _camera, PreserveDrawOrder = true });
        Children.Add(_playfield);
        _playfield.Children.Add(_playfieldBackground);
        _playfield.Children.Add(_paddle);
        _playfield.Children.Add(_ball);
        _playfield.Children.Add(_scoreText);
        _playfield.Children.Add(_livesText);
        _playfield.Children.Add(_statusText);
        RebuildBricks();
        RenderState();
    }

    /// <summary>Advances gameplay and reapplies the fixed-ratio layout after resizing.</summary>
    /// <param name="deltaTime">Elapsed rendered-frame time in seconds.</param>
    public override void Update(double deltaTime)
    {
        base.Update(deltaTime);
        _game.Advance(
            deltaTime,
            _input.Keyboard.IsKeyDown(KeyEnum.Left) || _input.Keyboard.IsKeyDown(KeyEnum.A),
            _input.Keyboard.IsKeyDown(KeyEnum.Right) || _input.Keyboard.IsKeyDown(KeyEnum.D)
        );
        var windowSize = _windowService.GetMainWindow().Size;
        if (windowSize != _lastWindowSize)
        {
            _lastWindowSize = windowSize;
            UpdateLayout(windowSize);
        }
        RenderState();
    }

    /// <summary>Creates one-shot bindings for launch, pause, and restart.</summary>
    /// <param name="eventHub">The event hub used by the input map.</param>
    /// <returns>The configured input map.</returns>
    private InputMap CreateInputMap(IEventHub eventHub)
    {
        var map = new InputMap(eventHub);
        map.OnKeyPressed(KeyEnum.Space).Invoke(() => HandlePressed(KeyEnum.Space));
        map.OnKeyPressed(KeyEnum.Escape).Invoke(() => HandlePressed(KeyEnum.Escape));
        map.OnKeyPressed(KeyEnum.R).Invoke(() => HandlePressed(KeyEnum.R));
        map.OnKeyReleased(KeyEnum.Space).Invoke(() => _pressedKeys.Remove(KeyEnum.Space));
        map.OnKeyReleased(KeyEnum.Escape).Invoke(() => _pressedKeys.Remove(KeyEnum.Escape));
        map.OnKeyReleased(KeyEnum.R).Invoke(() => _pressedKeys.Remove(KeyEnum.R));
        return map;
    }

    /// <summary>Consumes a fresh action press without responding to key repeat.</summary>
    /// <param name="key">The action key that was pressed.</param>
    private void HandlePressed(KeyEnum key)
    {
        if (!_pressedKeys.Add(key))
            return;
        if (key == KeyEnum.R)
        {
            _game.Reset();
            RebuildBricks();
        }
        else if (key == KeyEnum.Space)
            _game.Launch();
        else
            _game.TogglePause();
    }

    /// <summary>Replaces all live brick scene children from the current game state.</summary>
    private void RebuildBricks()
    {
        foreach (var child in _playfield.Children.OfType<BreakoutBrickElement>().ToArray())
            _playfield.Children.Remove(child);
        for (var index = 0; index < _game.Bricks.Count; index++)
            _playfield.Children.Add(new BreakoutBrickElement(_game.Bricks[index].Id, _game.Bricks[index].Color));
    }

    /// <summary>Synchronizes renderable bounds, HUD text, and brick ownership with gameplay.</summary>
    private void RenderState()
    {
        if (!IsInitialized)
            return;
        for (var index = 0; index < _game.Bricks.Count; index++)
        {
            var brick = _game.Bricks[index];
            var element = _playfield.Children.OfType<BreakoutBrickElement>().FirstOrDefault(x => x.BrickIndex == brick.Id);
            if (element is null)
                continue;
            SetRect(element, brick.Bounds);
        }
        var visible = _playfield.Children.OfType<BreakoutBrickElement>().ToArray();
        var activeIds = _game.Bricks.Select(brick => brick.Id).ToHashSet();
        foreach (var element in visible)
            if (!activeIds.Contains(element.BrickIndex))
                _playfield.Children.Remove(element);
        SetRect(_paddle, _game.Paddle);
        SetRect(_ball, _game.Ball);
        _scoreText.Text = $"Score: {_game.Score}";
        _livesText.Text = $"Lives: {_game.Lives}";
        _statusText.Text = _game.State switch
        {
            BreakoutRoundState.Ready => "Press Space to launch.",
            BreakoutRoundState.Paused => "Paused",
            BreakoutRoundState.Won => "You win! Press R to restart",
            BreakoutRoundState.Lost => "Game over. Press R to restart",
            _ => string.Empty,
        };
    }

    /// <summary>Fits the logical playfield uniformly inside the current window.</summary>
    /// <param name="windowSize">The current window size in pixels.</param>
    private void UpdateLayout(Vector2D<int> windowSize)
    {
        _scale = MathF.Min(windowSize.X / BreakoutGameState.FieldWidth, windowSize.Y / BreakoutGameState.FieldHeight);
        _playfield.Width = BreakoutGameState.FieldWidth * _scale;
        _playfield.Height = BreakoutGameState.FieldHeight * _scale;
        _playfieldBackground.Width = _playfield.Width;
        _playfieldBackground.Height = _playfield.Height;
        _scoreText.Width = 300f * _scale;
        _scoreText.Height = 42f * _scale;
        _scoreText.HorizontalAlignment = AlignHorizontal.Left;
        _scoreText.VerticalAlignment = AlignVertical.Top;
        _scoreText.Margins = new Margins(20f * _scale, 0f, 8f * _scale, 0f);
        _livesText.Width = 300f * _scale;
        _livesText.Height = 42f * _scale;
        _livesText.HorizontalAlignment = AlignHorizontal.Right;
        _livesText.VerticalAlignment = AlignVertical.Top;
        _livesText.Margins = new Margins(0f, 20f * _scale, 8f * _scale, 0f);
        _statusText.Width = _playfield.Width;
        _statusText.Height = 80f * _scale;
        _statusText.HorizontalAlignment = AlignHorizontal.Center;
        _statusText.VerticalAlignment = AlignVertical.Top;
        _statusText.Margins = new Margins(0f, 0f, 8f * _scale, 0f);
        RenderState();
    }

    /// <summary>Maps a logical rectangle to the current uniformly scaled GUI field.</summary>
    /// <param name="element">The image element to position.</param>
    /// <param name="rectangle">The logical rectangle.</param>
    private void SetRect(ImageElement element, Rectangle<float> rectangle)
    {
        element.Width = rectangle.Size.X * _scale;
        element.Height = rectangle.Size.Y * _scale;
        element.Margins = new Margins(rectangle.Origin.X * _scale, 0f, rectangle.Origin.Y * _scale, 0f);
    }

    /// <summary>Creates a uniformly colored rectangle renderer.</summary>
    /// <param name="color">The rectangle tint.</param>
    /// <returns>The configured image element.</returns>
    private static ImageElement CreateSolidImage(Color color) =>
        new()
        {
            Texture = BuiltInTextures.Uniform,
            SizingMode = ImageSizingMode.Stretch,
            Color = color,
            HorizontalAlignment = AlignHorizontal.Left,
            VerticalAlignment = AlignVertical.Top,
        };

    /// <summary>Creates a HUD text element using the built-in font.</summary>
    /// <param name="styles">The text style registry.</param>
    /// <param name="size">The logical font size.</param>
    /// <returns>The configured text element.</returns>
    private static TextElement CreateText(ITextStyleRegistry styles, float size) =>
        new()
        {
            Style = styles.GetOrCreate(BuiltInFonts.Default, size),
            Color = Colors.WhiteSmoke,
            HorizontalAlignment = AlignHorizontal.Left,
            VerticalAlignment = AlignVertical.Top,
        };
}
