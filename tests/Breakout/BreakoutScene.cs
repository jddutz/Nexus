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
using Nexus.Physics;
using Nexus.Physics.Components;
using Silk.NET.Maths;

/// <summary>Builds and presents the physics-driven Breakout sample.</summary>
[Scene("Breakout")]
public sealed class BreakoutScene : Scene
{
    private const float PaddleWidth = 120f;
    private const float PaddleHeight = 20f;
    private const float BallSize = 12f;
    private const float PaddleSpeed = 600f;
    private const float BallSpeed = 400f;
    private const float PaddleY = 670f;

    private static readonly Mesh RectangleMesh = new(
        "BreakoutRectangle",
        PrimitiveTopologyEnum.TriangleList,
        [new(new(0f, 0f, 0f)), new(new(1f, 0f, 0f)), new(new(1f, 1f, 0f)), new(new(0f, 1f, 0f))],
        [0, 2, 1, 0, 3, 2]
    );

    private readonly IWindowService _windowService;
    private readonly IEventHub _eventHub;
    private readonly PhysicsWorld2D _world;
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
    private Vector2D<float> _savedBallVelocity;
    private BreakoutRoundState _state;
    private int _score;
    private int _lives;

    /// <summary>Creates the scene, camera, world objects, HUD, and input map.</summary>
    /// <param name="textStyles">Provides built-in font styles.</param>
    /// <param name="eventHub">Dispatches key transitions.</param>
    /// <param name="windowService">Provides the current window size.</param>
    /// <param name="physics">Owns the scene's simulation world.</param>
    public BreakoutScene(
        ITextStyleRegistry textStyles,
        IEventHub eventHub,
        IWindowService windowService,
        IPhysicsSystem physics
    )
    {
        ArgumentNullException.ThrowIfNull(textStyles);
        ArgumentNullException.ThrowIfNull(eventHub);
        ArgumentNullException.ThrowIfNull(windowService);
        ArgumentNullException.ThrowIfNull(physics);
        _windowService = windowService;
        _eventHub = eventHub;
        _world = physics.CreateWorld2D();
        MainCamera = _camera;
        _windowSize = windowService.GetMainWindow().Size;
        _bounds = new(MathF.Max(1f, _windowSize.X), MathF.Max(1f, _windowSize.Y));
        _paddle = new Paddle(RectangleMesh, _world.Id);
        _ball = new Ball(RectangleMesh, _world.Id);
        _scoreText = CreateText(textStyles, 24);
        _livesText = CreateText(textStyles, 24);
        _statusText = CreateText(textStyles, 24);
        _statusText.MaximumLines = 2;
        InputMap = CreateInputMap(eventHub);
        UpdateLayout(_windowSize);
        Reset();
    }

    /// <inheritdoc />
    public override void Activate()
    {
        if (IsActive)
            return;

        base.Activate();
        if (IsActive)
            _eventHub.Register(this);
    }

    /// <inheritdoc />
    public override void Deactivate()
    {
        if (!IsActive)
            return;

        _eventHub.Unregister(this);
        base.Deactivate();
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

    /// <summary>Updates paddle movement, game rules, and the HUD around the physics simulation.</summary>
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

        if (
            _state
            is not (BreakoutRoundState.Paused or BreakoutRoundState.Won or BreakoutRoundState.Lost)
        )
        {
            var direction = (IsHeld(KeyEnum.Right) || IsHeld(KeyEnum.D) ? 1f : 0f)
                - (IsHeld(KeyEnum.Left) || IsHeld(KeyEnum.A) ? 1f : 0f);
            var paddleX = Math.Clamp(
                _paddle.Position.X + direction * PaddleSpeed * (float)Math.Max(0d, deltaTime),
                0f,
                _bounds.X - _paddle.Width
            );
            _paddle.Position = new(paddleX, _paddle.Position.Y);
            if (_state == BreakoutRoundState.Ready)
                PositionBallAbovePaddle();
        }

        base.Update(deltaTime);
        if (_state == BreakoutRoundState.Playing)
            ResolvePlayfieldBounds();
        UpdateHud();
    }

    /// <summary>Applies gameplay reactions to a deferred overlap or swept-contact notification.</summary>
    /// <param name="message">The reported overlap pair.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public void Handle(PhysicsCollisionEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (_state != BreakoutRoundState.Playing)
            return;

        var firstOwner = message.First.Owner;
        var secondOwner = message.Second.Owner;
        if (firstOwner is Ball ball && secondOwner is Brick brick)
            HandleBrickCollision(ball, brick, message.Contact);
        else if (secondOwner is Ball otherBall && firstOwner is Brick otherBrick)
            HandleBrickCollision(otherBall, otherBrick, message.Contact);
        else if (firstOwner is Ball paddleBall && secondOwner is Paddle paddle)
        {
            if (ReferenceEquals(paddle, _paddle))
                HandlePaddleCollision(paddleBall, message.Contact);
        }
        else if (secondOwner is Ball otherPaddleBall && firstOwner is Paddle otherPaddle)
        {
            if (ReferenceEquals(otherPaddle, _paddle))
                HandlePaddleCollision(otherPaddleBall, message.Contact);
        }
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
            _ball.Body.Velocity = new(120f, -MathF.Sqrt(BallSpeed * BallSpeed - 120f * 120f));
            _state = BreakoutRoundState.Playing;
        }
        else if (key == KeyEnum.Escape)
        {
            if (_state == BreakoutRoundState.Playing)
            {
                _savedBallVelocity = _ball.Body.Velocity;
                _ball.Body.Velocity = default;
                _state = BreakoutRoundState.Paused;
            }
            else if (_state == BreakoutRoundState.Paused)
            {
                _ball.Body.Velocity = _savedBallVelocity;
                _state = BreakoutRoundState.Playing;
            }
        }
    }

    /// <summary>Resets score, lives, transforms, velocity, and brick ownership.</summary>
    private void Reset()
    {
        foreach (var brick in _bricks)
            Children.Remove(brick);
        _bricks.Clear();
        _score = 0;
        _lives = 3;
        _savedBallVelocity = default;
        _state = BreakoutRoundState.Ready;
        _ball.Body.Velocity = default;
        _paddle.Position = new(
            Math.Clamp((_bounds.X - _paddle.Width) / 2f, 0f, _bounds.X - _paddle.Width),
            Math.Clamp(PaddleY, 0f, MathF.Max(0f, _bounds.Y - _paddle.Height))
        );
        PositionBallAbovePaddle();

        const int columns = 10;
        const int rows = 5;
        var gap = MathF.Min(8f, _bounds.X / (columns + 1) * 0.5f);
        var brickWidth = (_bounds.X - (columns + 1) * gap) / columns;
        Color[] rowColors =
        [
            new(0.95f, 0.1f, 0.1f),
            new(1f, 0.45f, 0.05f),
            new(1f, 0.9f, 0.05f),
            new(0.1f, 0.85f, 0.1f),
            new(0.1f, 0.4f, 1f),
        ];
        for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var brick = new Brick(
                    RectangleMesh,
                    new(gap + column * (brickWidth + gap), 70f + row * 32f, brickWidth, 24f),
                    _world.Id,
                    row,
                    column,
                    rowColors[row]
                );
                _bricks.Add(brick);
                if (IsInitialized)
                    Children.Add(brick);
            }
    }

    /// <summary>Positions the stationary ball above the paddle using the authoritative physics pose.</summary>
    private void PositionBallAbovePaddle()
    {
        _ball.Body.Velocity = default;
        _ball.Body.Teleport(
            new(
                _paddle.Position.X + (_paddle.Width - _ball.Width) / 2f,
                _paddle.Position.Y - _ball.Height - 2f
            ),
            0f
        );
    }

    /// <summary>Reflects the ball from the playfield edges and applies the bottom loss rule.</summary>
    private void ResolvePlayfieldBounds()
    {
        var position = _ball.Position;
        var velocity = _ball.Body.Velocity;
        var corrected = false;
        if (position.X < 0f)
        {
            position.X = 0f;
            if (velocity.X < 0f)
                velocity.X = -velocity.X;
            corrected = true;
        }
        else if (_ball.Bounds.Max.X > _bounds.X)
        {
            position.X = _bounds.X - _ball.Width;
            if (velocity.X > 0f)
                velocity.X = -velocity.X;
            corrected = true;
        }
        if (position.Y < 0f)
        {
            position.Y = 0f;
            if (velocity.Y < 0f)
                velocity.Y = -velocity.Y;
            corrected = true;
        }
        if (corrected)
            _ball.Body.Teleport(position, 0f);
        _ball.Body.Velocity = velocity;

        if (position.Y <= _bounds.Y)
            return;

        _lives--;
        _ball.Body.Velocity = default;
        if (_lives == 0)
        {
            _state = BreakoutRoundState.Lost;
            return;
        }
        _state = BreakoutRoundState.Ready;
        PositionBallAbovePaddle();
    }

    /// <summary>Removes one current-round brick and responds using overlap or swept-contact data.</summary>
    /// <param name="ball">The ball from the overlap pair.</param>
    /// <param name="brick">The contacted brick.</param>
    /// <param name="contact">The optional swept-contact data.</param>
    private void HandleBrickCollision(
        Ball ball,
        Brick brick,
        PhysicsCollisionContact? contact
    )
    {
        if (!ReferenceEquals(ball, _ball) || !_bricks.Contains(brick))
            return;

        if (IsBallContact(ball, contact))
            ApplyContactReflection(ball, contact);
        else
            SeparateAndReflect(ball, brick.Bounds);
        _score += 10;
        _bricks.Remove(brick);
        Children.Remove(brick);
        if (_bricks.Count == 0)
        {
            _state = BreakoutRoundState.Won;
            _ball.Body.Velocity = default;
        }
    }

    /// <summary>Responds to paddle overlap or contact and selects the gameplay rebound direction.</summary>
    /// <param name="ball">The ball from the overlap pair.</param>
    /// <param name="contact">The optional swept-contact data.</param>
    private void HandlePaddleCollision(Ball ball, PhysicsCollisionContact? contact)
    {
        if (!ReferenceEquals(ball, _ball))
            return;

        var hasBallContact = IsBallContact(ball, contact);
        var velocity = hasBallContact
            ? contact.Value.IncomingVelocity
            : ball.Body.Velocity;
        var movingIntoPaddleTop = hasBallContact
            ? contact!.Value.Normal.Y < 0f
            : HasPositiveOverlap(ball.Bounds, _paddle.Bounds)
                && ball.Bounds.Origin.Y < _paddle.Bounds.Origin.Y;
        if (velocity.Y > 0f && movingIntoPaddleTop)
        {
            var position = ball.Position;
            position.Y = _paddle.Position.Y - ball.Height;
            ball.Body.Teleport(position, 0f);
            var offset = Math.Clamp(
                (position.X + ball.Width / 2f - (_paddle.Position.X + _paddle.Width / 2f))
                    / (_paddle.Width / 2f),
                -1f,
                1f
            );
            var angle = offset * MathF.PI / 3f;
            ball.Body.Velocity = new(MathF.Sin(angle) * BallSpeed, -MathF.Cos(angle) * BallSpeed);
            return;
        }

        if (hasBallContact)
            ApplyContactReflection(ball, contact);
        else
            SeparateAndReflect(ball, _paddle.Bounds);
    }

    /// <summary>Determines whether a contact snapshot belongs to the Breakout ball.</summary>
    /// <param name="ball">The ball in the collision pair.</param>
    /// <param name="contact">The optional swept-contact data.</param>
    /// <returns><see langword="true"/> when the snapshot describes this ball's motion.</returns>
    private static bool IsBallContact(Ball ball, PhysicsCollisionContact? contact) =>
        contact is { Body: var body } && ReferenceEquals(body, ball.Body);

    /// <summary>Restores a swept ball's incoming velocity and reflects it from the contact normal.</summary>
    /// <param name="ball">The ball whose motion was stopped at contact.</param>
    /// <param name="contact">The optional swept contact data.</param>
    private static void ApplyContactReflection(Ball ball, PhysicsCollisionContact? contact)
    {
        if (contact is not { Body: var body } || !ReferenceEquals(body, ball.Body))
            return;

        var velocity = contact.Value.IncomingVelocity;
        var normal = contact.Value.Normal;
        var speedAlongNormal = velocity.X * normal.X + velocity.Y * normal.Y;
        if (speedAlongNormal < 0f)
            ball.Body.Velocity = velocity - 2f * speedAlongNormal * normal;
    }

    /// <summary>Determines whether two rectangles overlap with positive area.</summary>
    /// <param name="first">The first rectangle.</param>
    /// <param name="second">The second rectangle.</param>
    /// <returns><see langword="true"/> when both overlap axes have positive depth.</returns>
    private static bool HasPositiveOverlap(Rectangle<float> first, Rectangle<float> second) =>
        MathF.Min(first.Max.X, second.Max.X) > MathF.Max(first.Origin.X, second.Origin.X)
        && MathF.Min(first.Max.Y, second.Max.Y) > MathF.Max(first.Origin.Y, second.Origin.Y);

    /// <summary>Moves the ball out of an overlapping rectangle and reflects only an inward velocity.</summary>
    /// <param name="ball">The ball to separate.</param>
    /// <param name="surface">The contacted rectangle.</param>
    /// <returns><see langword="true"/> when the ball still overlaps the surface.</returns>
    private static bool SeparateAndReflect(Ball ball, Rectangle<float> surface)
    {
        var bounds = ball.Bounds;
        if (!HasPositiveOverlap(bounds, surface))
            return false;

        var overlapX = MathF.Min(bounds.Max.X, surface.Max.X) - MathF.Max(bounds.Origin.X, surface.Origin.X);
        var overlapY = MathF.Min(bounds.Max.Y, surface.Max.Y) - MathF.Max(bounds.Origin.Y, surface.Origin.Y);
        var position = ball.Position;
        var velocity = ball.Body.Velocity;
        if (overlapX < overlapY)
        {
            var ballCenterX = bounds.Origin.X + bounds.Size.X / 2f;
            var surfaceCenterX = surface.Origin.X + surface.Size.X / 2f;
            var isLeft = ballCenterX < surfaceCenterX
                || (ballCenterX == surfaceCenterX && velocity.X >= 0f);
            position.X = isLeft ? surface.Origin.X - bounds.Size.X : surface.Max.X;
            if ((isLeft && velocity.X > 0f) || (!isLeft && velocity.X < 0f))
                velocity.X = -velocity.X;
        }
        else
        {
            var ballCenterY = bounds.Origin.Y + bounds.Size.Y / 2f;
            var surfaceCenterY = surface.Origin.Y + surface.Size.Y / 2f;
            var isAbove = ballCenterY < surfaceCenterY
                || (ballCenterY == surfaceCenterY && velocity.Y >= 0f);
            position.Y = isAbove ? surface.Origin.Y - bounds.Size.Y : surface.Max.Y;
            if ((isAbove && velocity.Y > 0f) || (!isAbove && velocity.Y < 0f))
                velocity.Y = -velocity.Y;
        }
        ball.Body.Teleport(position, 0f);
        ball.Body.Velocity = velocity;
        return true;
    }

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
        _paddle.SetSize(MathF.Min(PaddleWidth, _bounds.X), MathF.Min(PaddleHeight, _bounds.Y));
        _paddle.Position = new(
            Math.Clamp(_paddle.Position.X, 0f, _bounds.X - _paddle.Width),
            Math.Clamp(_paddle.Position.Y, 0f, _bounds.Y - _paddle.Height)
        );
        _ball.SetSize(MathF.Min(BallSize, _bounds.X), MathF.Min(BallSize, _bounds.Y));
        _scoreText.Width = MathF.Max(1f, size.X);
        _scoreText.Height = 48f;
        _scoreText.Margins = new Margins(20f, 0f, 14f, 0f);
        _livesText.Width = MathF.Max(1f, size.X);
        _livesText.Height = 48f;
        _livesText.HorizontalAlignment = AlignHorizontal.Right;
        _livesText.Margins = new Margins(0f, 20f, 14f, 0f);
        _statusText.Width = MathF.Max(1f, size.X);
        _statusText.Height = 80f;
        _statusText.HorizontalAlignment = AlignHorizontal.Center;
        _statusText.VerticalAlignment = AlignVertical.Top;
        _statusText.Margins = new Margins(0f, 0f, 8f, 0f);

        const int columns = 10;
        var gap = MathF.Min(8f, _bounds.X / (columns + 1) * 0.5f);
        var brickWidth = (_bounds.X - (columns + 1) * gap) / columns;
        foreach (var brick in _bricks)
        {
            brick.SetBounds(
                new(
                    gap + brick.Column * (brickWidth + gap),
                    70f + brick.Row * 32f,
                    brickWidth,
                    MathF.Min(24f, _bounds.Y)
                )
            );
        }
        if (_state == BreakoutRoundState.Ready)
            PositionBallAbovePaddle();
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

    /// <summary>Owns the paddle transform, drawable, and static physics collider.</summary>
    private sealed class Paddle : GameObject2D
    {
        /// <summary>Creates a paddle from shared rectangle geometry.</summary>
        /// <param name="mesh">The shared unit rectangle mesh.</param>
        /// <param name="worldId">The simulation world identifier.</param>
        public Paddle(Mesh mesh, PhysicsWorldId worldId)
        {
            Scale = new(PaddleWidth, PaddleHeight);
            AddComponent(
                new UniformColorMeshRenderer
                {
                    Mesh = mesh,
                    Color = Colors.WhiteSmoke,
                    DrawOrder = 10,
                }
            );
            AddComponent(new PhysicsCollider2D
            {
                WorldId = worldId,
                Shape = new RectangleShape2D(new(1f, 1f), new(0.5f, 0.5f)),
            });
        }

        /// <summary>Gets the current collision rectangle.</summary>
        public Rectangle<float> Bounds => new(Position.X, Position.Y, Width, Height);

        /// <summary>Gets the current paddle width.</summary>
        public float Width => Scale.X;

        /// <summary>Gets the current paddle height.</summary>
        public float Height => Scale.Y;

        /// <summary>Sets the paddle dimensions represented by its transform scale.</summary>
        /// <param name="width">The positive paddle width.</param>
        /// <param name="height">The positive paddle height.</param>
        public void SetSize(float width, float height) => Scale = new(width, height);
    }

    /// <summary>Owns the ball transform, drawable, and integrated physics state.</summary>
    private sealed class Ball : GameObject2D
    {
        /// <summary>Creates a ball from shared rectangle geometry.</summary>
        /// <param name="mesh">The shared unit rectangle mesh.</param>
        /// <param name="worldId">The simulation world identifier.</param>
        public Ball(Mesh mesh, PhysicsWorldId worldId)
        {
            Scale = new(BallSize, BallSize);
            AddComponent(
                new UniformColorMeshRenderer
                {
                    Mesh = mesh,
                    Color = Colors.White,
                    DrawOrder = 20,
                }
            );
            Body = new PhysicsBody2D { WorldId = worldId };
            AddComponent(new PhysicsCollider2D
            {
                WorldId = worldId,
                Shape = new RectangleShape2D(new(1f, 1f), new(0.5f, 0.5f)),
            });
            AddComponent(Body);
        }

        /// <summary>Gets the authoritative physics body controlling ball motion.</summary>
        public PhysicsBody2D Body { get; }

        /// <summary>Gets the current collision rectangle.</summary>
        public Rectangle<float> Bounds => new(Position.X, Position.Y, Width, Height);

        /// <summary>Gets the current ball width.</summary>
        public float Width => Scale.X;

        /// <summary>Gets the current ball height.</summary>
        public float Height => Scale.Y;

        /// <summary>Sets the ball dimensions represented by its transform scale.</summary>
        /// <param name="width">The positive ball width.</param>
        /// <param name="height">The positive ball height.</param>
        public void SetSize(float width, float height) => Scale = new(width, height);
    }

    /// <summary>Owns one brick transform, bounds, color, and lifetime.</summary>
    private sealed class Brick : GameObject2D
    {
        /// <summary>Creates a brick with a shared mesh and logical rectangle.</summary>
        /// <param name="mesh">The shared unit rectangle mesh.</param>
        /// <param name="bounds">The brick's initial logical rectangle.</param>
        /// <param name="worldId">The simulation world identifier.</param>
        /// <param name="row">The brick's stable row index.</param>
        /// <param name="column">The brick's stable column index.</param>
        /// <param name="color">The brick color.</param>
        public Brick(
            Mesh mesh,
            Rectangle<float> bounds,
            PhysicsWorldId worldId,
            int row,
            int column,
            Color color
        )
        {
            Position = bounds.Origin;
            Scale = bounds.Size;
            Row = row;
            Column = column;
            AddComponent(
                new UniformColorMeshRenderer
                {
                    Mesh = mesh,
                    Color = color,
                    DrawOrder = 5,
                }
            );
            AddComponent(new PhysicsCollider2D
            {
                WorldId = worldId,
                Shape = new RectangleShape2D(new(1f, 1f), new(0.5f, 0.5f)),
            });
        }

        /// <summary>Gets the current collision rectangle.</summary>
        public Rectangle<float> Bounds => new(Position.X, Position.Y, Scale.X, Scale.Y);

        /// <summary>Gets the stable brick row index used for responsive layout.</summary>
        public int Row { get; }

        /// <summary>Gets the stable brick column index used for responsive layout.</summary>
        public int Column { get; }

        /// <summary>Moves and scales the brick to match the responsive layout.</summary>
        /// <param name="bounds">The new logical rectangle.</param>
        public void SetBounds(Rectangle<float> bounds)
        {
            Position = bounds.Origin;
            Scale = bounds.Size;
        }
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
