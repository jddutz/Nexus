namespace Nexus.Samples.Asteroids;

/// <summary>Runs a single-wave Asteroids round using indexed uniform-color meshes.</summary>
[Scene("Asteroids")]
public sealed class AsteroidsScene : Scene
{
    private const float DesignWidth = 1280f;
    private const float DesignHeight = 720f;
    private const float ShipRadius = 18f;
    private const float BulletLifetime = 1.2f;
    private const float FireCooldown = 0.18f;
    private const int StartingLives = 3;

    private static readonly Color ShipColor = Colors.White;
    private static readonly Color AsteroidColor = new(0.28f, 0.28f, 0.28f);
    private static readonly Color BulletColor = new(0.15f, 0.45f, 1f);
    private static readonly Mesh ShipMesh = new(
        "AsteroidsShip",
        PrimitiveTopologyEnum.TriangleList,
        [
            new(new(1f, 0f, 0f)),
            new(new(-0.7f, -0.65f, 0f)),
            new(new(-0.3f, 0f, 0f)),
            new(new(-0.7f, 0.65f, 0f)),
        ],
        [0, 2, 1, 0, 3, 2]
    );
    private static readonly Mesh BulletMesh = new(
        "AsteroidsBullet",
        PrimitiveTopologyEnum.TriangleList,
        [
            new(new(8f, 0f, 0f)),
            new(new(3f, -3f, 0f)),
            new(new(-4f, -3f, 0f)),
            new(new(-6f, 0f, 0f)),
            new(new(-4f, 3f, 0f)),
            new(new(3f, 3f, 0f)),
        ],
        [0, 2, 1, 0, 3, 2, 0, 4, 3, 0, 5, 4]
    );
    private static readonly Mesh[] AsteroidMeshes = CreateAsteroidMeshes();

    private readonly IInputSystem _input;
    private readonly IWindowService _windowService;
    private readonly StaticCamera _camera = new();
    private readonly List<Body> _asteroids = [];
    private readonly List<Body> _bullets = [];
    private readonly HashSet<KeyEnum> _pressedKeys = [];
    private readonly TextElement _scoreText;
    private readonly TextElement _livesText;
    private readonly TextElement _statusText;
    private readonly GameObject2D _shipNode = new();
    private readonly GameObject2D _muzzleNode = new() { Position = new(24f, 0f) };
    private readonly UniformColorMeshRenderer _shipRenderer = new()
    {
        Mesh = ShipMesh,
        Color = ShipColor,
        DrawOrder = 100,
    };
    private Vector2D<int> _lastWindowSize;
    private float _fieldScaleX = 1f;
    private float _fieldScaleY = 1f;
    private Vector2D<float> _shipPosition;
    private Vector2D<float> _shipVelocity;
    private float _shipRotation;
    private float _fireTimer;
    private float _invulnerability;
    private int _lives;
    private int _score;
    private bool _finished;

    /// <summary>Creates the scene and its input, camera, and HUD services.</summary>
    /// <param name="textStyles">Provides built-in font styles.</param>
    /// <param name="eventHub">Dispatches key transitions.</param>
    /// <param name="input">Provides held keyboard state.</param>
    /// <param name="windowService">Provides the current window size.</param>
    public AsteroidsScene(
        ITextStyleRegistry textStyles,
        IEventHub eventHub,
        IInputSystem input,
        IWindowService windowService
    )
    {
        _input = input;
        _windowService = windowService;
        MainCamera = _camera;
        _scoreText = CreateText(textStyles, 24);
        _livesText = CreateText(textStyles, 24);
        _statusText = CreateText(textStyles, 28);
        _statusText.MaximumLines = 2;
        _lastWindowSize = windowService.GetMainWindow().Size;
        InputMap = CreateInputMap(eventHub);
        UpdateLayout(_lastWindowSize);
    }

    /// <summary>Initializes the full-window view, HUD, ship, and initial asteroid family.</summary>
    public override void Initialize()
    {
        base.Initialize();
        Children.Add(new View { Camera = _camera, PreserveDrawOrder = true });
        _shipNode.Children.Add(_muzzleNode);
        _shipNode.AddComponent(_shipRenderer);
        Children.Add(_shipNode);
        Children.Add(_scoreText);
        Children.Add(_livesText);
        Children.Add(_statusText);
        ResetRound();
    }

    /// <summary>Advances movement, firing, collisions, wrapping, and responsive layout.</summary>
    /// <param name="deltaTime">Elapsed frame time in seconds.</param>
    public override void Update(double deltaTime)
    {
        base.Update(deltaTime);
        var elapsed = Math.Clamp((float)deltaTime, 0f, 0.1f);
        var size = _windowService.GetMainWindow().Size;
        if (size != _lastWindowSize)
        {
            _lastWindowSize = size;
            UpdateLayout(size);
        }

        if (_finished)
        {
            RenderState();
            return;
        }

        UpdateShip(elapsed);
        UpdateBullets(elapsed);
        UpdateAsteroids(elapsed);
        CheckCollisions();
        _fireTimer = MathF.Max(0f, _fireTimer - elapsed);
        _invulnerability = MathF.Max(0f, _invulnerability - elapsed);
        RenderState();
    }

    /// <summary>Creates restart and quit bindings.</summary>
    /// <param name="eventHub">The event hub used by the input map.</param>
    /// <returns>The configured input map.</returns>
    private InputMap CreateInputMap(IEventHub eventHub)
    {
        var map = new InputMap(eventHub);
        map.OnKeyPressed(KeyEnum.R).Invoke(() => HandlePressed(KeyEnum.R));
        map.OnKeyPressed(KeyEnum.Escape).Invoke(() => HandlePressed(KeyEnum.Escape));
        map.OnKeyReleased(KeyEnum.R).Invoke(() => _pressedKeys.Remove(KeyEnum.R));
        map.OnKeyReleased(KeyEnum.Escape).Invoke(() => _pressedKeys.Remove(KeyEnum.Escape));
        return map;
    }

    /// <summary>Handles one-shot restart and quit keys.</summary>
    /// <param name="key">The newly pressed key.</param>
    private void HandlePressed(KeyEnum key)
    {
        if (!_pressedKeys.Add(key))
            return;
        if (key == KeyEnum.R)
            ResetRound();
        else
            _windowService.GetMainWindow().Close();
    }

    /// <summary>Resets every gameplay object and counter without retaining old drawables.</summary>
    private void ResetRound()
    {
        foreach (var body in _asteroids.Concat(_bullets).ToArray())
            Children.Remove(body.Node);
        _asteroids.Clear();
        _bullets.Clear();
        _lives = StartingLives;
        _score = 0;
        _finished = false;
        RespawnShip(invulnerable: false);

        var positions = new[]
        {
            new Vector2D<float>(150f, 130f),
            new Vector2D<float>(DesignWidth - 150f, 160f),
            new Vector2D<float>(DesignWidth / 2f, DesignHeight - 130f),
        };
        for (var index = 0; index < positions.Length; index++)
            AddAsteroid(positions[index], 0, 68f, 55f + index * 17f, index);
    }

    /// <summary>Respawns the ship at the center, optionally granting crash protection.</summary>
    /// <param name="invulnerable">Whether to grant the post-crash invulnerability window.</param>
    private void RespawnShip(bool invulnerable = true)
    {
        _shipPosition = new(DesignWidth / 2f, DesignHeight / 2f);
        _shipVelocity = default;
        _shipRotation = -MathF.PI / 2f;
        _invulnerability = invulnerable ? 2f : 0f;
        _shipNode.Position = _shipPosition;
        _shipNode.Rotation = _shipRotation;
    }

    /// <summary>Applies rotation, thrust, coasting, and held-fire input.</summary>
    /// <param name="elapsed">Elapsed seconds.</param>
    private void UpdateShip(float elapsed)
    {
        if (_input.Keyboard.IsKeyDown(KeyEnum.Left) || _input.Keyboard.IsKeyDown(KeyEnum.A))
            _shipRotation -= 3.8f * elapsed;
        if (_input.Keyboard.IsKeyDown(KeyEnum.Right) || _input.Keyboard.IsKeyDown(KeyEnum.D))
            _shipRotation += 3.8f * elapsed;
        if (_input.Keyboard.IsKeyDown(KeyEnum.Up) || _input.Keyboard.IsKeyDown(KeyEnum.W))
        {
            var forward = Forward();
            _shipVelocity += forward * (260f * elapsed);
            var speed = Length(_shipVelocity);
            if (speed > 300f)
                _shipVelocity *= 300f / speed;
        }

        _shipPosition += _shipVelocity * elapsed;
        Wrap(ref _shipPosition);
        if ((_input.Keyboard.IsKeyDown(KeyEnum.Space)) && _fireTimer <= 0f)
        {
            AddBullet();
            _fireTimer = FireCooldown;
        }
    }

    /// <summary>Moves and expires bullets.</summary>
    /// <param name="elapsed">Elapsed seconds.</param>
    private void UpdateBullets(float elapsed)
    {
        foreach (var bullet in _bullets.ToArray())
        {
            bullet.Position += bullet.Velocity * elapsed;
            bullet.Lifetime -= elapsed;
            var position = bullet.Position;
            Wrap(ref position);
            bullet.Position = position;
            if (bullet.Lifetime <= 0f)
                RemoveBody(_bullets, bullet);
        }
    }

    /// <summary>Moves and rotates every asteroid.</summary>
    /// <param name="elapsed">Elapsed seconds.</param>
    private void UpdateAsteroids(float elapsed)
    {
        foreach (var asteroid in _asteroids)
        {
            asteroid.Position += asteroid.Velocity * elapsed;
            asteroid.Rotation += asteroid.Spin * elapsed;
            var position = asteroid.Position;
            Wrap(ref position);
            asteroid.Position = position;
        }
    }

    /// <summary>Resolves bullet impacts, recursive splitting, and ship contact.</summary>
    private void CheckCollisions()
    {
        foreach (var bullet in _bullets.ToArray())
        {
            var target = _asteroids.FirstOrDefault(asteroid =>
                DistanceSquared(bullet.Position, asteroid.Position)
                <= MathF.Pow(bullet.Radius + asteroid.Radius, 2f)
            );
            if (target is null)
                continue;

            RemoveBody(_bullets, bullet);
            RemoveBody(_asteroids, target);
            _score += 100;
            if (target.Generation < 4)
            {
                var direction = Normalize(target.Velocity);
                var perpendicular = new Vector2D<float>(-direction.Y, direction.X);
                AddAsteroid(
                    target.Position,
                    target.Generation + 1,
                    target.Radius * 0.62f,
                    75f + target.Generation * 12f,
                    target.Variant + 1,
                    target.Velocity + perpendicular * 32f
                );
                AddAsteroid(
                    target.Position,
                    target.Generation + 1,
                    target.Radius * 0.62f,
                    75f + target.Generation * 12f,
                    target.Variant + 3,
                    target.Velocity - perpendicular * 32f
                );
            }
        }

        if (_invulnerability <= 0f)
        {
            var hit = _asteroids.Any(asteroid =>
                DistanceSquared(_shipPosition, asteroid.Position)
                <= MathF.Pow(ShipRadius + asteroid.Radius, 2f)
            );
            if (hit)
            {
                _lives--;
                if (_lives == 0)
                    _finished = true;
                else
                    RespawnShip();
            }
        }

        if (_asteroids.Count == 0)
            _finished = true;
    }

    /// <summary>Adds a bullet at the ship's transformed muzzle with inherited velocity.</summary>
    private void AddBullet()
    {
        var forward = Forward();
        var muzzleTransform = _muzzleNode.WorldTransform;
        var position = new Vector2D<float>(muzzleTransform.M41, muzzleTransform.M42);
        var bullet = new Body(
            position,
            _shipVelocity + forward * 560f,
            6f,
            0f,
            1f,
            BulletMesh,
            BulletColor
        )
        {
            Lifetime = BulletLifetime,
        };
        AddBody(_bullets, bullet);
    }

    /// <summary>Adds one asteroid fragment with a shared mesh variant.</summary>
    private void AddAsteroid(
        Vector2D<float> position,
        int generation,
        float radius,
        float speed,
        int variant,
        Vector2D<float>? velocity = null
    )
    {
        var angle = (variant * 1.71f) % (MathF.PI * 2f);
        var body = new Body(
            position,
            velocity ?? new Vector2D<float>(MathF.Cos(angle), MathF.Sin(angle)) * speed,
            radius,
            angle,
            0.3f + variant * 0.07f,
            AsteroidMeshes[variant % AsteroidMeshes.Length],
            AsteroidColor
        )
        {
            Generation = generation,
            Variant = variant,
        };
        AddBody(_asteroids, body);
    }

    /// <summary>Adds a body node and its drawable to the scene hierarchy.</summary>
    private void AddBody(List<Body> collection, Body body)
    {
        body.Node.AddComponent(body.Renderer);
        Children.Add(body.Node);
        collection.Add(body);
    }

    /// <summary>Removes a body and its associated renderer without leaving a stale drawable.</summary>
    private void RemoveBody(List<Body> collection, Body body)
    {
        collection.Remove(body);
        Children.Remove(body.Node);
    }

    /// <summary>Updates all transforms, HUD text, and terminal status.</summary>
    private void RenderState()
    {
        _shipNode.Position = _shipPosition;
        _shipNode.Rotation = _shipRotation;
        _shipRenderer.Transform =
            Matrix4X4.CreateScale(_fieldScaleX, _fieldScaleY, 1f)
            * Matrix4X4.CreateScale(22f, 22f, 1f)
            * _shipNode.WorldTransform;
        _shipRenderer.Color =
            _invulnerability > 0f && (int)(_invulnerability * 12f) % 2 == 0
                ? Colors.Red
                : ShipColor;
        foreach (var body in _asteroids.Concat(_bullets))
            body.Renderer.Transform =
                Matrix4X4.CreateScale(_fieldScaleX, _fieldScaleY, 1f)
                * Transform(
                    body.Position,
                    body.Rotation,
                    body.Mesh == BulletMesh ? 1f : body.Radius
                );
        _scoreText.Text = $"Score: {_score}";
        _livesText.Text = $"Lives: {_lives}";
        _statusText.Text = _finished
            ? (
                _lives == 0
                    ? "Game Over - Press R to restart"
                    : "Field Cleared - Press R to restart"
            )
            : string.Empty;
    }

    /// <summary>Updates the camera, background, and HUD placement for the current window.</summary>
    /// <param name="windowSize">The current window size.</param>
    private void UpdateLayout(Vector2D<int> windowSize)
    {
        _fieldScaleX = MathF.Max(1f, windowSize.X) / DesignWidth;
        _fieldScaleY = MathF.Max(1f, windowSize.Y) / DesignHeight;
        _camera.SetViewportSize(MathF.Max(1f, windowSize.X), MathF.Max(1f, windowSize.Y));
        _scoreText.Width = windowSize.X;
        _scoreText.Height = 48f;
        _scoreText.Margins = new Margins(20f, 0f, 14f, 0f);
        _livesText.Width = windowSize.X;
        _livesText.Height = 48f;
        _livesText.HorizontalAlignment = AlignHorizontal.Right;
        _livesText.Margins = new Margins(0f, 20f, 14f, 0f);
        _statusText.Width = windowSize.X;
        _statusText.Height = 100f;
        _statusText.Color = Colors.AliceBlue;
        _statusText.HorizontalAlignment = AlignHorizontal.Center;
        _statusText.VerticalAlignment = AlignVertical.Center;
        _statusText.Margins = default;
    }

    /// <summary>Creates a built-in-font HUD element.</summary>
    private static TextElement CreateText(ITextStyleRegistry styles, float size) =>
        new()
        {
            Style = styles.GetOrCreate(BuiltInFonts.Default, size),
            Color = Colors.White,
            HorizontalAlignment = AlignHorizontal.Left,
            VerticalAlignment = AlignVertical.Top,
        };

    /// <summary>Creates deterministic irregular asteroid fan meshes.</summary>
    private static Mesh[] CreateAsteroidMeshes()
    {
        var meshes = new Mesh[4];
        for (var variant = 0; variant < meshes.Length; variant++)
        {
            var random = new Random(variant + 41);
            var vertices = new Vertex[13];
            vertices[0] = new(new(0f, 0f, 0f));
            var indices = new uint[36];
            for (var index = 0; index < 12; index++)
            {
                var angle = index * MathF.PI * 2f / 12f;
                var radius = 0.78f + (float)random.NextDouble() * 0.42f;
                vertices[index + 1] = new(
                    new(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius, 0f)
                );
                indices[index * 3] = 0;
                indices[index * 3 + 1] = (uint)(index + 1);
                indices[index * 3 + 2] = (uint)(index == 11 ? 1 : index + 2);
            }
            meshes[variant] = new Mesh(
                $"Asteroid{variant}",
                PrimitiveTopologyEnum.TriangleList,
                vertices,
                indices
            );
        }
        return meshes;
    }

    /// <summary>Builds a local-to-world transform for a custom mesh.</summary>
    private static Matrix4X4<float> Transform(
        Vector2D<float> position,
        float rotation,
        float scale
    ) =>
        Matrix4X4.CreateScale(scale, scale, 1f)
        * Matrix4X4.CreateRotationZ(rotation)
        * Matrix4X4.CreateTranslation(position.X, position.Y, 0f);

    /// <summary>Gets the ship's forward unit vector.</summary>
    private Vector2D<float> Forward() => new(MathF.Cos(_shipRotation), MathF.Sin(_shipRotation));

    /// <summary>Wraps a center point across the design playfield.</summary>
    private static void Wrap(ref Vector2D<float> position)
    {
        if (position.X < 0f)
            position.X += DesignWidth;
        if (position.X >= DesignWidth)
            position.X -= DesignWidth;
        if (position.Y < 0f)
            position.Y += DesignHeight;
        if (position.Y >= DesignHeight)
            position.Y -= DesignHeight;
    }

    private static float Length(Vector2D<float> value) =>
        MathF.Sqrt(value.X * value.X + value.Y * value.Y);

    private static float DistanceSquared(Vector2D<float> left, Vector2D<float> right)
    {
        var x = left.X - right.X;
        var y = left.Y - right.Y;
        return x * x + y * y;
    }

    private static Vector2D<float> Normalize(Vector2D<float> value)
    {
        var length = Length(value);
        return length < 0.001f ? new(1f, 0f) : value * (1f / length);
    }

    /// <summary>Stores gameplay and rendering state for one dynamic object.</summary>
    private sealed class Body
    {
        public Body(
            Vector2D<float> position,
            Vector2D<float> velocity,
            float radius,
            float rotation,
            float spin,
            Mesh mesh,
            Color color
        )
        {
            Position = position;
            Velocity = velocity;
            Radius = radius;
            Rotation = rotation;
            Spin = spin;
            Mesh = mesh;
            Node = new GameObject2D();
            Renderer = new UniformColorMeshRenderer
            {
                Mesh = mesh,
                Color = color,
                DrawOrder = 110,
            };
        }

        public GameObject2D Node { get; }
        public UniformColorMeshRenderer Renderer { get; }
        public Mesh Mesh { get; }
        public Vector2D<float> Position { get; set; }
        public Vector2D<float> Velocity { get; }
        public float Radius { get; }
        public float Rotation { get; set; }
        public float Spin { get; }
        public float Lifetime { get; set; } = float.PositiveInfinity;
        public int Generation { get; set; }
        public int Variant { get; set; }
    }
}
