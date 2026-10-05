namespace Nexus.Samples.SnakeGame;

using Silk.NET.Maths;
using Nexus.Input.Events;

/// <summary>Composes the Snake sample's board, score, prompts, and input mappings.</summary>
[Scene("SnakeGame")]
public sealed class SnakeGameScene : Scene
{
    private const int HeaderHeight = 48;
    private const int FooterHeight = 76;
    private const int ControllerInputBindingLimit = 32;

    private static readonly (string SemanticName, SnakeDirection Direction)[] ControllerDirections =
    [
        (ControllerSemanticNames.DPadUp, SnakeDirection.Up),
        (ControllerSemanticNames.DPadDown, SnakeDirection.Down),
        (ControllerSemanticNames.DPadLeft, SnakeDirection.Left),
        (ControllerSemanticNames.DPadRight, SnakeDirection.Right),
    ];

    private static readonly string[] ControllerButtonSemantics =
    [
        ControllerSemanticNames.FaceBottom,
        ControllerSemanticNames.FaceRight,
        ControllerSemanticNames.FaceLeft,
        ControllerSemanticNames.FaceTop,
        ControllerSemanticNames.LeftBumper,
        ControllerSemanticNames.RightBumper,
        ControllerSemanticNames.LeftStickClick,
        ControllerSemanticNames.RightStickClick,
        ControllerSemanticNames.Back,
        ControllerSemanticNames.Start,
        ControllerSemanticNames.Home,
    ];

    private readonly IWindowService _windowService;
    private readonly StaticCamera _camera = new();
    private readonly SnakeGameState _game = new();
    private readonly GridLayout _board = new()
    {
        Rows = Enumerable.Repeat(GridSize.Relative(), SnakeGameState.BoardSize).ToArray(),
        Columns = Enumerable.Repeat(GridSize.Relative(), SnakeGameState.BoardSize).ToArray(),
        HorizontalAlignment = AlignHorizontal.Center,
        VerticalAlignment = AlignVertical.Center,
    };
    private readonly TextElement _scoreText;
    private readonly TextElement _promptText;
    private readonly HashSet<KeyEnum> _pressedKeys = [];
    private Vector2D<int> _lastWindowSize;
    private SnakeDirection? _lastStickDirection;
    private bool _stickArmed = true;

    /// <summary>Creates the scene and configures its one-time input bindings.</summary>
    /// <param name="textStyles">Provides cached styles for the built-in font.</param>
    /// <param name="eventHub">Dispatches scene input events.</param>
    /// <param name="windowService">Provides the main window size for responsive layout.</param>
    public SnakeGameScene(
        ITextStyleRegistry textStyles,
        IEventHub eventHub,
        IWindowService windowService
    )
    {
        ArgumentNullException.ThrowIfNull(textStyles);
        ArgumentNullException.ThrowIfNull(eventHub);
        ArgumentNullException.ThrowIfNull(windowService);

        _windowService = windowService;
        MainCamera = _camera;
        _scoreText = new TextElement
        {
            Style = textStyles.GetOrCreate(BuiltInFonts.Default, 24),
            Color = Colors.WhiteSmoke,
            HorizontalAlignment = AlignHorizontal.Center,
            VerticalAlignment = AlignVertical.Center,
        };
        _promptText = new TextElement
        {
            Style = textStyles.GetOrCreate(BuiltInFonts.Default, 20),
            Color = Colors.WhiteSmoke,
            MaximumLines = 2,
            HorizontalAlignment = AlignHorizontal.Center,
            VerticalAlignment = AlignVertical.Center,
        };

        InputMap = CreateInputMap(eventHub);
        _game.StateChanged += RenderGameState;
        _lastWindowSize = _windowService.GetMainWindow().Size;
        UpdateBoardSize(_lastWindowSize);
    }

    /// <summary>Initializes the view and responsive GUI hierarchy.</summary>
    public override void Initialize()
    {
        base.Initialize();
        Children.Add(new View { Camera = _camera, PreserveDrawOrder = true });

        var screenLayout = new GridLayout
        {
            Rows =
            [
                GridSize.Absolute(HeaderHeight),
                GridSize.Relative(),
                GridSize.Absolute(FooterHeight),
            ],
            Columns = [GridSize.Relative()],
        };
        screenLayout.SetCell(0, 0, _scoreText);
        screenLayout.SetCell(1, 0, _board);
        screenLayout.SetCell(2, 0, _promptText);
        Children.Add(screenLayout);
        RenderGameState();
    }

    /// <summary>Advances the game and adapts the board when the window is resized.</summary>
    /// <param name="deltaTime">Elapsed frame time in seconds.</param>
    public override void Update(double deltaTime)
    {
        base.Update(deltaTime);
        _game.Advance(deltaTime);

        var windowSize = _windowService.GetMainWindow().Size;
        if (windowSize != _lastWindowSize)
        {
            _lastWindowSize = windowSize;
            UpdateBoardSize(windowSize);
        }
    }

    /// <summary>Creates key, controller-button, and left-stick mappings.</summary>
    /// <param name="eventHub">The event hub used by the scene lifecycle.</param>
    /// <returns>The completed input map.</returns>
    private InputMap CreateInputMap(IEventHub eventHub)
    {
        var inputMap = new InputMap(eventHub);
        foreach (var key in Enum.GetValues<KeyEnum>())
        {
            if (key != KeyEnum.Unknown)
            {
                inputMap.OnKeyPressed(key).Invoke(() => HandleKeyPressed(key));
                inputMap.OnKeyReleased(key).Invoke(() => _pressedKeys.Remove(key));
            }
        }

        foreach (var (semanticName, direction) in ControllerDirections)
            inputMap
                .OnAnyControllerButtonPressed(semanticName)
                .Invoke(() => HandleControllerDirection(direction));

        foreach (var semanticName in ControllerButtonSemantics)
            inputMap.OnAnyControllerButtonPressed(semanticName).Invoke(StartFromPrompt);

        for (var index = 0; index < ControllerInputBindingLimit; index++)
            inputMap.OnAnyControllerAnalogChanged(index).Invoke(HandleControllerAnalogChanged);

        return inputMap;
    }

    /// <summary>Starts or restarts on a fresh keyboard press, or queues its direction in play.</summary>
    /// <param name="key">The pressed key.</param>
    private void HandleKeyPressed(KeyEnum key)
    {
        if (!_pressedKeys.Add(key))
            return;
        if (_game.Status != SnakeGameStatus.Playing)
        {
            _game.Start();
            return;
        }

        if (TryGetKeyDirection(key, out var direction))
            _game.RequestDirection(direction);
    }

    /// <summary>Starts from a prompt or queues a D-pad turn while playing.</summary>
    /// <param name="direction">The D-pad direction.</param>
    private void HandleControllerDirection(SnakeDirection direction)
    {
        if (_game.Status != SnakeGameStatus.Playing)
        {
            _game.Start();
            return;
        }

        _game.RequestDirection(direction);
    }

    /// <summary>Starts the game when a non-directional controller button is pressed at a prompt.</summary>
    private void StartFromPrompt()
    {
        if (_game.Status != SnakeGameStatus.Playing)
            _game.Start();
    }

    /// <summary>Handles the normalized left-stick position when its analog input changes.</summary>
    /// <param name="analogEvent">The changed controller analog input.</param>
    private void HandleControllerAnalogChanged(ControllerAnalogChangedEvent analogEvent)
    {
        if (analogEvent.AnalogInput.SemanticName != ControllerSemanticNames.LeftStick)
            return;

        var position = analogEvent.Position;
        var absoluteX = MathF.Abs(position.X);
        var absoluteY = MathF.Abs(position.Y);
        if (_game.Status != SnakeGameStatus.Playing)
        {
            if (absoluteX < 0.3f && absoluteY < 0.3f)
            {
                _stickArmed = true;
                _lastStickDirection = null;
            }
            else
            {
                _stickArmed = false;
                _lastStickDirection = GetDominantDirection(position, absoluteX, absoluteY);
            }
            return;
        }

        if (absoluteX < 0.3f && absoluteY < 0.3f)
        {
            _stickArmed = true;
            _lastStickDirection = null;
            return;
        }

        if (absoluteX == absoluteY)
            return;

        var magnitude = MathF.Max(absoluteX, absoluteY);
        if (magnitude < 0.5f)
            return;

        var direction = GetDominantDirection(position, absoluteX, absoluteY);
        if (direction is null)
            return;
        if (!_stickArmed && _lastStickDirection == direction)
            return;

        _stickArmed = false;
        _lastStickDirection = direction;
        _game.RequestDirection(direction.Value);
    }

    /// <summary>Converts an unequal-axis stick position to a cardinal direction.</summary>
    /// <param name="position">The normalized stick position.</param>
    /// <param name="absoluteX">The absolute horizontal value.</param>
    /// <param name="absoluteY">The absolute vertical value.</param>
    /// <returns>The dominant cardinal direction, or null for an exact axis tie.</returns>
    private static SnakeDirection? GetDominantDirection(
        Vector2D<float> position,
        float absoluteX,
        float absoluteY
    ) =>
        absoluteX == absoluteY
            ? null
            : absoluteX > absoluteY
                ? position.X < 0f ? SnakeDirection.Left : SnakeDirection.Right
                : position.Y > 0f ? SnakeDirection.Up : SnakeDirection.Down;

    /// <summary>Maps supported keyboard keys to their cardinal direction.</summary>
    /// <param name="key">The pressed key.</param>
    /// <param name="direction">The mapped direction, when the key is directional.</param>
    /// <returns>True when the key maps to a direction.</returns>
    private static bool TryGetKeyDirection(KeyEnum key, out SnakeDirection direction)
    {
        direction = key switch
        {
            KeyEnum.W or KeyEnum.Up => SnakeDirection.Up,
            KeyEnum.S or KeyEnum.Down => SnakeDirection.Down,
            KeyEnum.A or KeyEnum.Left => SnakeDirection.Left,
            KeyEnum.D or KeyEnum.Right => SnakeDirection.Right,
            _ => default,
        };
        return key is
            KeyEnum.W
            or KeyEnum.Up
            or KeyEnum.S
            or KeyEnum.Down
            or KeyEnum.A
            or KeyEnum.Left
            or KeyEnum.D
            or KeyEnum.Right;
    }

    /// <summary>Refreshes the board tiles, score, and prompt from the game state.</summary>
    private void RenderGameState()
    {
        if (!IsInitialized)
            return;

        for (var row = 0; row < SnakeGameState.BoardSize; row++)
        for (var column = 0; column < SnakeGameState.BoardSize; column++)
        {
            var tile = new SnakeTile(column, row);
            var color = GetTileColor(tile);
            var image = color is null
                ? null
                : new ImageElement
                {
                    Texture = BuiltInTextures.Uniform,
                    SizingMode = ImageSizingMode.Stretch,
                    Color = color.Value,
                };
            _board.SetCell(row, column, image);
        }

        _scoreText.Text = _game.Status == SnakeGameStatus.Playing
            ? $"Score: {_game.Score}"
            : string.Empty;
        _promptText.Text = _game.Status switch
        {
            SnakeGameStatus.Waiting => "Press any key or button to begin",
            SnakeGameStatus.GameOver => "Game over\nPress any key or button to restart",
            SnakeGameStatus.Won => "You win!\nPress any key or button to restart",
            _ => string.Empty,
        };
    }

    /// <summary>Gets the color for the head, body, target, or empty board tile.</summary>
    /// <param name="tile">The board tile to inspect.</param>
    /// <returns>The display color, or null when the tile is empty.</returns>
    private Color? GetTileColor(SnakeTile tile)
    {
        if (_game.Snake.Count > 0 && _game.Snake[0] == tile)
            return Colors.BrightGreen;
        if (_game.Snake.Skip(1).Contains(tile))
            return Colors.DarkGreen;
        if (_game.Target == tile)
            return Colors.Red;
        return null;
    }

    /// <summary>Updates the board size to the largest square that leaves room for text.</summary>
    /// <param name="windowSize">The current logical window size.</param>
    private void UpdateBoardSize(Vector2D<int> windowSize)
    {
        var side = Math.Max(0, Math.Min(windowSize.X, windowSize.Y - HeaderHeight - FooterHeight));
        _board.Width = side;
        _board.Height = side;
    }
}
