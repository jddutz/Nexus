namespace Nexus.Samples.Breakout;

using Nexus.Core;
using Nexus.Core.Events;
using Nexus.Game;
using Nexus.GUI;
using Nexus.GUI.Elements;
using Nexus.Graphics;
using Nexus.Graphics.Cameras;
using Nexus.Graphics.Components;
using Nexus.Graphics.Geometry;
using Nexus.Graphics.Text;
using Nexus.Input;
using Silk.NET.Maths;

/// <summary>Builds and presents the transform-driven Breakout sample.</summary>
[Scene("Breakout")]
public sealed class BreakoutScene : Scene
{
    private const double FixedStep = 1d / 120d;
    private const int MaximumCatchUpSteps = 8;
    private const float PaddleWidth = 120f;
    private const float PaddleHeight = 20f;
    private const float BallSize = 12f;
    private const float PaddleSpeed = 600f;
    private const float BallSpeed = 400f;
    private const float PaddleY = 670f;

    private static readonly Mesh RectangleMesh = new(
        "BreakoutRectangle",
        PrimitiveTopologyEnum.TriangleList,
        [
            new(new(0f, 0f, 0f)),
            new(new(1f, 0f, 0f)),
            new(new(1f, 1f, 0f)),
            new(new(0f, 1f, 0f)),
        ],
        [0, 2, 1, 0, 3, 2]
    );

    private readonly IWindowService _windowService;
    private readonly StaticCamera _camera = new();
    private readonly Paddle _paddle;
    private readonly Ball _ball;
    private readonly List<Brick> _bricks = [];
    private readonly TextElement _scoreText;
    private readonly TextElement _livesText;
    private readonly TextElement _statusText;
    private readonly HashSet<KeyEnum> _heldKeys = [];
    private readonly HashSet<KeyEnum> _pressedKeys = [];
    private Vector2D<int> _windowSize;
    private Vector2D<float> _bounds;
    private Vector2D<float> _ballVelocity;
    private double _accumulator;
    private BreakoutRoundState _state;
    private int _score;
    private int _lives;

    /// <summary>Creates the scene, camera, world objects, HUD, and input map.</summary>
    /// <param name="textStyles">Provides built-in font styles.</param>
    /// <param name="eventHub">Dispatches key transitions.</param>
    /// <param name="windowService">Provides the current window size.</param>
    public BreakoutScene(
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
        _windowSize = windowService.GetMainWindow().Size;
        _bounds = new(MathF.Max(1f, _windowSize.X), MathF.Max(1f, _windowSize.Y));
        _paddle = new Paddle(RectangleMesh);
        _ball = new Ball(RectangleMesh);
        _scoreText = CreateText(textStyles, 24);
        _livesText = CreateText(textStyles, 24);
        _statusText = CreateText(textStyles, 24);
        _statusText.MaximumLines = 2;
        InputMap = CreateInputMap(eventHub);
        UpdateLayout(_windowSize);
        Reset();
    }

    /// <summary>Initializes the view, world objects, and HUD.</summary>
    public override void Initialize()
    {
        base.Initialize();
        Children.Add(new View { Camera = _camera, PreserveDrawOrder = true });
        Children.Add(_paddle);
        Children.Add(_ball);
        Children.Add(_scoreText);
        Children.Add(_livesText);
        Children.Add(_statusText);
        foreach (var brick in _bricks)
            Children.Add(brick);
        UpdateHud();
    }

    /// <summary>Advances the fixed-step simulation and updates the HUD.</summary>
    /// <param name="deltaTime">Elapsed rendered-frame time in seconds.</param>
    public override void Update(double deltaTime)
    {
        var size = _windowService.GetMainWindow().Size;
        if (size != _windowSize)
        {
            _windowSize = size;
            _bounds = new(MathF.Max(1f, size.X), MathF.Max(1f, size.Y));
            UpdateLayout(size);
        }

        base.Update(deltaTime);

        if (_state is not (BreakoutRoundState.Paused or BreakoutRoundState.Won or BreakoutRoundState.Lost))
        {
            _accumulator += Math.Min(Math.Max(deltaTime, 0d), 0.25d);
            var steps = 0;
            while (_accumulator >= FixedStep && steps++ < MaximumCatchUpSteps)
            {
                _accumulator -= FixedStep;
                Step(
                    (IsHeld(KeyEnum.Right) || IsHeld(KeyEnum.D) ? 1f : 0f)
                        - (IsHeld(KeyEnum.Left) || IsHeld(KeyEnum.A) ? 1f : 0f)
                );
                if (_state is BreakoutRoundState.Won or BreakoutRoundState.Lost)
                    break;
            }
            if (steps > MaximumCatchUpSteps)
                _accumulator = 0d;
        }
        UpdateHud();
    }

    /// <summary>Creates mapped one-shot and held controls.</summary>
    /// <param name="eventHub">The event hub used by the input map.</param>
    /// <returns>The configured input map.</returns>
    private InputMap CreateInputMap(IEventHub eventHub)
    {
        var map = new InputMap(eventHub);
        BindHeld(map, KeyEnum.Left);
        BindHeld(map, KeyEnum.Right);
        BindHeld(map, KeyEnum.A);
        BindHeld(map, KeyEnum.D);
        map.OnKeyPressed(KeyEnum.Space).Invoke(() => HandlePressed(KeyEnum.Space));
        map.OnKeyPressed(KeyEnum.Escape).Invoke(() => HandlePressed(KeyEnum.Escape));
        map.OnKeyPressed(KeyEnum.R).Invoke(() => HandlePressed(KeyEnum.R));
        map.OnKeyReleased(KeyEnum.Space).Invoke(() => _pressedKeys.Remove(KeyEnum.Space));
        map.OnKeyReleased(KeyEnum.Escape).Invoke(() => _pressedKeys.Remove(KeyEnum.Escape));
        map.OnKeyReleased(KeyEnum.R).Invoke(() => _pressedKeys.Remove(KeyEnum.R));
        return map;
    }

    /// <summary>Registers pressed and released actions for a held key.</summary>
    /// <param name="map">The input map receiving the bindings.</param>
    /// <param name="key">The physical key to track independently.</param>
    private void BindHeld(InputMap map, KeyEnum key)
    {
        map.OnKeyPressed(key).Invoke(() => _heldKeys.Add(key));
        map.OnKeyReleased(key).Invoke(() => _heldKeys.Remove(key));
    }

    /// <summary>Determines whether a physical movement key is currently held.</summary>
    /// <param name="key">The physical key to check.</param>
    /// <returns><see langword="true"/> when the key is held; otherwise, <see langword="false"/>.</returns>
    private bool IsHeld(KeyEnum key) => _heldKeys.Contains(key);

    /// <summary>Handles launch, pause, restart, and one-shot key repeat filtering.</summary>
    private void HandlePressed(KeyEnum key)
    {
        if (!_pressedKeys.Add(key))
            return;
        if (key == KeyEnum.R)
            Reset();
        else if (key == KeyEnum.Space && _state == BreakoutRoundState.Ready)
        {
            _ballVelocity = new(120f, -MathF.Sqrt(BallSpeed * BallSpeed - 120f * 120f));
            _state = BreakoutRoundState.Playing;
        }
        else if (key == KeyEnum.Escape)
            _state = _state == BreakoutRoundState.Playing
                ? BreakoutRoundState.Paused
                : _state == BreakoutRoundState.Paused ? BreakoutRoundState.Playing : _state;
    }

    /// <summary>Resets score, lives, transforms, velocity, and brick ownership.</summary>
    private void Reset()
    {
        foreach (var brick in _bricks)
            Children.Remove(brick);
        _bricks.Clear();
        _score = 0;
        _lives = 3;
        _accumulator = 0d;
        _state = BreakoutRoundState.Ready;
        _ballVelocity = default;
        _paddle.Position = new((_bounds.X - PaddleWidth) / 2f, MathF.Min(PaddleY, _bounds.Y - PaddleHeight));
        _ball.Position = new(_paddle.Position.X + (PaddleWidth - BallSize) / 2f, _paddle.Position.Y - BallSize - 2f);

        const int columns = 10;
        const int rows = 5;
        const float gap = 8f;
        var brickWidth = (_bounds.X - (columns + 1) * gap) / columns;
        for (var row = 0; row < rows; row++)
        for (var column = 0; column < columns; column++)
        {
            var brick = new Brick(
                RectangleMesh,
                new(
                    gap + column * (brickWidth + gap),
                    70f + row * 32f,
                    brickWidth,
                    24f
                ),
                row % 2 == 0 ? new Color(0.9f, 0.25f, 0.25f) : new Color(0.95f, 0.65f, 0.15f)
            );
            _bricks.Add(brick);
            if (IsInitialized)
                Children.Add(brick);
        }
    }

    /// <summary>Advances one fixed simulation step and resolves terminal state immediately.</summary>
    private void Step(float direction)
    {
        var paddleX = Math.Clamp(_paddle.Position.X + direction * PaddleSpeed * (float)FixedStep, 0f, _bounds.X - PaddleWidth);
        _paddle.Position = new(paddleX, _paddle.Position.Y);
        if (_state == BreakoutRoundState.Ready)
        {
            _ball.Position = new(_paddle.Position.X + (PaddleWidth - BallSize) / 2f, _paddle.Position.Y - BallSize - 2f);
            return;
        }

        var previous = _ball.Bounds;
        _ball.Position += _ballVelocity * (float)FixedStep;
        ResolveWalls();
        if (_ballVelocity.Y > 0f && Intersects(_ball.Bounds, _paddle.Bounds))
        {
            _ball.Position = new(_ball.Position.X, _paddle.Position.Y - BallSize);
            var offset = Math.Clamp(
                (_ball.Position.X + BallSize / 2f - (_paddle.Position.X + PaddleWidth / 2f))
                    / (PaddleWidth / 2f),
                -1f,
                1f
            );
            var angle = offset * MathF.PI / 3f;
            _ballVelocity = new(MathF.Sin(angle) * BallSpeed, -MathF.Cos(angle) * BallSpeed);
        }
        else
        {
            ResolveBrick(previous);
        }

        if (_ball.Position.Y > _bounds.Y)
        {
            _lives--;
            _accumulator = 0d;
            if (_lives == 0)
            {
                _state = BreakoutRoundState.Lost;
                return;
            }
            _state = BreakoutRoundState.Ready;
            _ballVelocity = default;
        }
    }

    /// <summary>Reflects the ball from the current camera bounds.</summary>
    private void ResolveWalls()
    {
        if (_ball.Position.X < 0f)
        {
            _ball.Position = new(0f, _ball.Position.Y);
            _ballVelocity = new(MathF.Abs(_ballVelocity.X), _ballVelocity.Y);
        }
        else if (_ball.Bounds.Max.X > _bounds.X)
        {
            _ball.Position = new(_bounds.X - BallSize, _ball.Position.Y);
            _ballVelocity = new(-MathF.Abs(_ballVelocity.X), _ballVelocity.Y);
        }
        if (_ball.Position.Y < 0f)
        {
            _ball.Position = new(_ball.Position.X, 0f);
            _ballVelocity = new(_ballVelocity.X, MathF.Abs(_ballVelocity.Y));
        }
    }

    /// <summary>Removes the first brick hit by the ball and reflects its velocity.</summary>
    private void ResolveBrick(Rectangle<float> previous)
    {
        var brick = _bricks.FirstOrDefault(candidate => Intersects(_ball.Bounds, candidate.Bounds));
        if (brick is null)
            return;
        var hitFromVerticalFace = previous.Max.X <= brick.Bounds.Origin.X
            || previous.Origin.X >= brick.Bounds.Max.X;
        _ballVelocity = hitFromVerticalFace
            ? new(-_ballVelocity.X, _ballVelocity.Y)
            : new(_ballVelocity.X, -_ballVelocity.Y);
        _score += 10;
        _bricks.Remove(brick);
        Children.Remove(brick);
        if (_bricks.Count == 0)
            _state = BreakoutRoundState.Won;
    }

    /// <summary>Determines whether two axis-aligned rectangles overlap.</summary>
    private static bool Intersects(Rectangle<float> left, Rectangle<float> right) =>
        left.Origin.X < right.Max.X
        && left.Max.X > right.Origin.X
        && left.Origin.Y < right.Max.Y
        && left.Max.Y > right.Origin.Y;

    /// <summary>Updates HUD content without using it as a gameplay model.</summary>
    private void UpdateHud()
    {
        _scoreText.Text = $"Score: {_score}";
        _livesText.Text = $"Lives: {_lives}";
        _statusText.Text = _state switch
        {
            BreakoutRoundState.Ready => "Press Space to launch.",
            BreakoutRoundState.Paused => "Paused",
            BreakoutRoundState.Won => "You win! Press R to restart",
            BreakoutRoundState.Lost => "Game over. Press R to restart",
            _ => string.Empty,
        };
    }

    /// <summary>Updates camera viewport and HUD bounds after a window resize.</summary>
    private void UpdateLayout(Vector2D<int> size)
    {
        _camera.SetViewportSize(MathF.Max(1f, size.X), MathF.Max(1f, size.Y));
        _scoreText.Width = size.X;
        _scoreText.Height = 48f;
        _scoreText.Margins = new Margins(20f, 0f, 14f, 0f);
        _livesText.Width = size.X;
        _livesText.Height = 48f;
        _livesText.HorizontalAlignment = AlignHorizontal.Right;
        _livesText.Margins = new Margins(0f, 20f, 14f, 0f);
        _statusText.Width = size.X;
        _statusText.Height = 80f;
        _statusText.HorizontalAlignment = AlignHorizontal.Center;
        _statusText.VerticalAlignment = AlignVertical.Top;
        _statusText.Margins = new Margins(0f, 0f, 8f, 0f);
    }

    /// <summary>Creates a built-in-font HUD element.</summary>
    private static TextElement CreateText(ITextStyleRegistry styles, float size) =>
        new()
        {
            Style = styles.GetOrCreate(BuiltInFonts.Default, size),
            Color = Colors.WhiteSmoke,
            HorizontalAlignment = AlignHorizontal.Left,
            VerticalAlignment = AlignVertical.Top,
        };

    /// <summary>Owns the paddle transform and solid-color drawable.</summary>
    private sealed class Paddle : GameObject2D
    {
        /// <summary>Creates a paddle from shared rectangle geometry.</summary>
        public Paddle(Mesh mesh)
        {
            Scale = new(PaddleWidth, PaddleHeight);
            AddComponent(new UniformColorMeshRenderer { Mesh = mesh, Color = Colors.WhiteSmoke, DrawOrder = 10 });
        }

        /// <summary>Gets the current collision rectangle.</summary>
        public Rectangle<float> Bounds => new(Position.X, Position.Y, PaddleWidth, PaddleHeight);
    }

    /// <summary>Owns the ball transform and solid-color drawable.</summary>
    private sealed class Ball : GameObject2D
    {
        /// <summary>Creates a ball from shared rectangle geometry.</summary>
        public Ball(Mesh mesh)
        {
            Scale = new(BallSize, BallSize);
            AddComponent(new UniformColorMeshRenderer { Mesh = mesh, Color = Colors.White, DrawOrder = 20 });
        }

        /// <summary>Gets the current collision rectangle.</summary>
        public Rectangle<float> Bounds => new(Position.X, Position.Y, BallSize, BallSize);
    }

    /// <summary>Owns one brick transform, bounds, color, and lifetime.</summary>
    private sealed class Brick : GameObject2D
    {
        /// <summary>Creates a brick with a shared mesh and logical rectangle.</summary>
        public Brick(Mesh mesh, Rectangle<float> bounds, Color color)
        {
            Position = bounds.Origin;
            Scale = bounds.Size;
            Bounds = bounds;
            AddComponent(new UniformColorMeshRenderer { Mesh = mesh, Color = color, DrawOrder = 5 });
        }

        /// <summary>Gets the immutable collision rectangle.</summary>
        public Rectangle<float> Bounds { get; }
    }

    /// <summary>Represents the phase of a Breakout round.</summary>
    private enum BreakoutRoundState
    {
        /// <summary>The ball follows the paddle until launch.</summary>
        Ready,
        /// <summary>The ball is moving and collisions are active.</summary>
        Playing,
        /// <summary>Gameplay is temporarily stopped.</summary>
        Paused,
        /// <summary>All bricks have been removed.</summary>
        Won,
        /// <summary>The player has no lives remaining.</summary>
        Lost,
    }
}
