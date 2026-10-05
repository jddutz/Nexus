namespace Nexus.Samples.Asteroids;

/// <summary>Runs a single-wave Asteroids round using indexed uniform-color meshes.</summary>
[Scene("Asteroids")]
public sealed class AsteroidsScene : Scene
{
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

    private readonly IWindowService _windowService;
    private readonly StaticCamera _camera = new();
    private readonly ShipInput _shipInput = new();
    private readonly List<Asteroid> _asteroids = [];
    private readonly List<Bullet> _bullets = [];
    private readonly HashSet<KeyEnum> _pressedKeys = [];
    private readonly TextElement _scoreText;
    private readonly TextElement _livesText;
    private readonly TextElement _statusText;
    private readonly Ship _ship;
    private Vector2D<int> _lastWindowSize;
    private int _lives;
    private int _score;
    private bool _finished;

    /// <summary>Creates the scene and its input, camera, and HUD services.</summary>
    /// <param name="textStyles">Provides built-in font styles.</param>
    /// <param name="eventHub">Dispatches key transitions.</param>
    /// <param name="input">Provides keyboard state.</param>
    /// <param name="windowService">Provides the current window size.</param>
    public AsteroidsScene(
        ITextStyleRegistry textStyles,
        IEventHub eventHub,
        IInputSystem input,
        IWindowService windowService
    )
    {
        _windowService = windowService;
        MainCamera = _camera;
        _scoreText = CreateText(textStyles, 24);
        _livesText = CreateText(textStyles, 24);
        _statusText = CreateText(textStyles, 28);
        _statusText.MaximumLines = 2;
        _lastWindowSize = windowService.GetMainWindow().Size;
        InputMap = CreateInputMap(eventHub);
        _ship = new Ship(_shipInput, ShipMesh, ShipColor);
        _ship.Fired += OnShipFired;
        UpdateLayout(_lastWindowSize);
    }

    /// <summary>Initializes the view, HUD, ship, and initial asteroid family.</summary>
    public override void Initialize()
    {
        base.Initialize();
        Children.Add(new View { Camera = _camera, PreserveDrawOrder = true });
        Children.Add(_ship);
        Children.Add(_scoreText);
        Children.Add(_livesText);
        Children.Add(_statusText);
        ResetRound();
    }

    /// <summary>Resolves collisions, updates HUD state, and handles responsive layout.</summary>
    /// <param name="deltaTime">Elapsed frame time in seconds.</param>
    public override void Update(double deltaTime)
    {
        base.Update(deltaTime);
        var size = _windowService.GetMainWindow().Size;
        if (size != _lastWindowSize)
        {
            _lastWindowSize = size;
            UpdateLayout(size);
        }

        var bounds = new Vector2D<float>(MathF.Max(1f, size.X), MathF.Max(1f, size.Y));
        _ship.Bounds = bounds;
        foreach (var asteroid in _asteroids)
            asteroid.Bounds = bounds;
        foreach (var bullet in _bullets)
            bullet.Bounds = bounds;

        if (!_finished)
            CheckCollisions();

        _statusText.Text = _finished
            ? (_lives == 0
                ? "Game Over - Press R to restart"
                : "Field Cleared - Press R to restart")
            : string.Empty;
        _scoreText.Text = $"Score: {_score}";
        _livesText.Text = $"Lives: {_lives}";
    }

    /// <summary>Creates restart and quit bindings.</summary>
    /// <param name="eventHub">The event hub used by the input map.</param>
    /// <returns>The configured input map.</returns>
    private InputMap CreateInputMap(IEventHub eventHub)
    {
        var map = new InputMap(eventHub);
        BindHeld(map, KeyEnum.Left, () => _shipInput.RotateLeft = true, () => _shipInput.RotateLeft = false);
        BindHeld(map, KeyEnum.Right, () => _shipInput.RotateRight = true, () => _shipInput.RotateRight = false);
        BindHeld(map, KeyEnum.A, () => _shipInput.RotateLeft = true, () => _shipInput.RotateLeft = false);
        BindHeld(map, KeyEnum.D, () => _shipInput.RotateRight = true, () => _shipInput.RotateRight = false);
        BindHeld(map, KeyEnum.Up, () => _shipInput.Thrust = true, () => _shipInput.Thrust = false);
        BindHeld(map, KeyEnum.W, () => _shipInput.Thrust = true, () => _shipInput.Thrust = false);
        BindHeld(map, KeyEnum.Space, () => _shipInput.Fire = true, () => _shipInput.Fire = false);
        map.OnKeyPressed(KeyEnum.R).Invoke(() => HandlePressed(KeyEnum.R));
        map.OnKeyPressed(KeyEnum.Escape).Invoke(() => HandlePressed(KeyEnum.Escape));
        map.OnKeyReleased(KeyEnum.R).Invoke(() => _pressedKeys.Remove(KeyEnum.R));
        map.OnKeyReleased(KeyEnum.Escape).Invoke(() => _pressedKeys.Remove(KeyEnum.Escape));
        return map;
    }

    /// <summary>Registers pressed and released actions for a held control.</summary>
    /// <param name="map">The input map receiving the bindings.</param>
    /// <param name="key">The bound key.</param>
    /// <param name="pressed">The pressed action.</param>
    /// <param name="released">The released action.</param>
    private static void BindHeld(InputMap map, KeyEnum key, Action pressed, Action released)
    {
        map.OnKeyPressed(key).Invoke(pressed);
        map.OnKeyReleased(key).Invoke(released);
    }

    /// <summary>Handles restart and quit controls.</summary>
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

    /// <summary>Resets every gameplay object and counter.</summary>
    private void ResetRound()
    {
        foreach (var asteroid in _asteroids.ToArray())
            RemoveAsteroid(asteroid);
        foreach (var bullet in _bullets.ToArray())
            RemoveBullet(bullet);

        _lives = StartingLives;
        _score = 0;
        _finished = false;
        _ship.Reset(new(_lastWindowSize.X / 2f, _lastWindowSize.Y / 2f), invulnerable: false);

        var width = MathF.Max(1f, _lastWindowSize.X);
        var height = MathF.Max(1f, _lastWindowSize.Y);
        AddAsteroid(new(0.12f * width, 0.18f * height), 0, 68f, 55f, 0);
        AddAsteroid(new(0.88f * width, 0.22f * height), 0, 68f, 72f, 1);
        AddAsteroid(new(0.5f * width, 0.82f * height), 0, 68f, 89f, 2);
    }

    /// <summary>Receives a firing request after the ship has updated its transform.</summary>
    /// <param name="spawn">The transformed muzzle state.</param>
    private void OnShipFired(ShipSpawn spawn)
    {
        var bullet = new Bullet(BulletMesh, BulletColor, spawn.Position, spawn.Rotation, spawn.Velocity)
        {
            Bounds = _ship.Bounds,
        };
        bullet.Expired += RemoveBullet;
        _bullets.Add(bullet);
        Children.Add(bullet);
    }

    /// <summary>Checks bullet impacts and ship contact using circular bounds.</summary>
    private void CheckCollisions()
    {
        foreach (var bullet in _bullets.ToArray())
        {
            var target = _asteroids.FirstOrDefault(asteroid =>
                DistanceSquared(bullet.Position, asteroid.Position)
                    <= MathF.Pow(bullet.CollisionRadius + asteroid.CollisionRadius, 2f)
            );
            if (target is null)
                continue;

            RemoveBullet(bullet);
            RemoveAsteroid(target);
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

        if (!_ship.IsInvulnerable)
        {
            var hit = _asteroids.Any(asteroid =>
                DistanceSquared(_ship.Position, asteroid.Position)
                    <= MathF.Pow(ShipRadius + asteroid.CollisionRadius, 2f)
            );
            if (hit)
            {
                _lives--;
                if (_lives == 0)
                    _finished = true;
                else
                    _ship.Reset(new(_lastWindowSize.X / 2f, _lastWindowSize.Y / 2f), invulnerable: true);
            }
        }

        if (_asteroids.Count == 0)
            _finished = true;
    }

    /// <summary>Adds an asteroid game object to the scene hierarchy.</summary>
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
        var asteroid = new Asteroid(
            AsteroidMeshes[variant % AsteroidMeshes.Length],
            AsteroidColor,
            position,
            radius,
            velocity ?? new Vector2D<float>(MathF.Cos(angle), MathF.Sin(angle)) * speed,
            angle,
            generation,
            variant
        )
        {
            Bounds = _ship.Bounds,
        };
        _asteroids.Add(asteroid);
        Children.Add(asteroid);
    }

    /// <summary>Removes an asteroid game object and its drawable.</summary>
    /// <param name="asteroid">The asteroid to remove.</param>
    private void RemoveAsteroid(Asteroid asteroid)
    {
        _asteroids.Remove(asteroid);
        Children.Remove(asteroid);
    }

    /// <summary>Removes a bullet game object and its drawable.</summary>
    /// <param name="bullet">The bullet to remove.</param>
    private void RemoveBullet(Bullet bullet)
    {
        _bullets.Remove(bullet);
        Children.Remove(bullet);
    }

    /// <summary>Updates the full-window camera and HUD layout.</summary>
    /// <param name="windowSize">The current window size.</param>
    private void UpdateLayout(Vector2D<int> windowSize)
    {
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
        _statusText.HorizontalAlignment = AlignHorizontal.Center;
        _statusText.VerticalAlignment = AlignVertical.Center;
        _statusText.Margins = default;
    }

    /// <summary>Creates a built-in-font HUD element.</summary>
    /// <param name="styles">The text style registry.</param>
    /// <param name="size">The font size.</param>
    /// <returns>A configured text element.</returns>
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
                vertices[index + 1] = new(new(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius, 0f));
                indices[index * 3] = 0;
                indices[index * 3 + 1] = (uint)(index + 1);
                indices[index * 3 + 2] = (uint)(index == 11 ? 1 : index + 2);
            }
            meshes[variant] = new Mesh($"Asteroid{variant}", PrimitiveTopologyEnum.TriangleList, vertices, indices);
        }
        return meshes;
    }

    /// <summary>Calculates squared distance between two positions.</summary>
    private static float DistanceSquared(Vector2D<float> left, Vector2D<float> right)
    {
        var x = left.X - right.X;
        var y = left.Y - right.Y;
        return x * x + y * y;
    }

    /// <summary>Normalizes a vector, using a default direction for zero length.</summary>
    private static Vector2D<float> Normalize(Vector2D<float> value)
    {
        var length = MathF.Sqrt(value.X * value.X + value.Y * value.Y);
        return length < 0.001f ? new(1f, 0f) : value * (1f / length);
    }

    /// <summary>Stores mapped ship controls.</summary>
    private sealed class ShipInput
    {
        /// <summary>Gets or sets whether counterclockwise rotation is held.</summary>
        public bool RotateLeft { get; set; }
        /// <summary>Gets or sets whether clockwise rotation is held.</summary>
        public bool RotateRight { get; set; }
        /// <summary>Gets or sets whether thrust is held.</summary>
        public bool Thrust { get; set; }
        /// <summary>Gets or sets whether firing is held.</summary>
        public bool Fire { get; set; }
    }

    /// <summary>Represents the transformed state emitted when the ship fires.</summary>
    private readonly record struct ShipSpawn(
        Vector2D<float> Position,
        Vector2D<float> Velocity,
        float Rotation
    );

    /// <summary>Owns the ship transform, muzzle, movement, and firing behavior.</summary>
    private sealed class Ship : GameObject2D
    {
        private const float ShipScale = 22f;
        private readonly ShipInput _input;
        private readonly GameObject2D _muzzle = new() { Position = new(24f / ShipScale, 0f) };
        private readonly UniformColorMeshRenderer _renderer;
        private float _fireTimer;
        private float _invulnerability;
        private Vector2D<float> _velocity;

        /// <summary>Creates a ship with its renderer and inherited-transform muzzle.</summary>
        /// <param name="input">The mapped control state.</param>
        /// <param name="mesh">The ship mesh.</param>
        /// <param name="color">The ship color.</param>
        public Ship(ShipInput input, Mesh mesh, Color color)
        {
            _input = input;
            _renderer = new UniformColorMeshRenderer { Mesh = mesh, Color = color, DrawOrder = 100 };
            AddComponent(_renderer);
            Scale = new(ShipScale, ShipScale);
            Children.Add(_muzzle);
        }

        /// <summary>Gets or sets the current movement bounds.</summary>
        public Vector2D<float> Bounds { get; set; }
        /// <summary>Gets whether the ship is currently crash-invulnerable.</summary>
        public bool IsInvulnerable => _invulnerability > 0f;
        /// <summary>Raised after the muzzle transform is current and a shot is fired.</summary>
        public event Action<ShipSpawn>? Fired;

        /// <inheritdoc />
        public override void Update(double deltaTime)
        {
            var elapsed = Math.Clamp((float)deltaTime, 0f, 0.1f);
            if (_input.RotateLeft)
                Rotation -= 3.8f * elapsed;
            if (_input.RotateRight)
                Rotation += 3.8f * elapsed;
            if (_input.Thrust)
            {
                var forward = new Vector2D<float>(MathF.Cos(Rotation), MathF.Sin(Rotation));
                _velocity += forward * (260f * elapsed);
                var speed = MathF.Sqrt(_velocity.X * _velocity.X + _velocity.Y * _velocity.Y);
                if (speed > 300f)
                    _velocity *= 300f / speed;
            }

            var position = Position + _velocity * elapsed;
            Wrap(ref position, Bounds);
            Position = position;
            _fireTimer = MathF.Max(0f, _fireTimer - elapsed);
            _invulnerability = MathF.Max(0f, _invulnerability - elapsed);
            _renderer.Color = IsInvulnerable && (int)(_invulnerability * 12f) % 2 == 0
                ? Colors.Red
                : ShipColor;

            if (_input.Fire && _fireTimer <= 0f)
            {
                var forward = new Vector2D<float>(MathF.Cos(Rotation), MathF.Sin(Rotation));
                var muzzlePosition = new Vector2D<float>(_muzzle.WorldTransform.M41, _muzzle.WorldTransform.M42);
                Fired?.Invoke(new(muzzlePosition, _velocity + forward * 560f, Rotation));
                _fireTimer = FireCooldown;
            }
        }

        /// <summary>Resets position, velocity, orientation, and crash protection.</summary>
        /// <param name="position">The new position.</param>
        /// <param name="invulnerable">Whether to grant crash protection.</param>
        public void Reset(Vector2D<float> position, bool invulnerable)
        {
            Position = position;
            Rotation = -MathF.PI / 2f;
            _velocity = default;
            _fireTimer = 0f;
            _invulnerability = invulnerable ? 2f : 0f;
        }
    }

    /// <summary>Owns asteroid movement, spin, scale, and collision state.</summary>
    private sealed class Asteroid : GameObject2D
    {
        private readonly UniformColorMeshRenderer _renderer;

        /// <summary>Creates an asteroid node with its shared mesh.</summary>
        public Asteroid(
            Mesh mesh,
            Color color,
            Vector2D<float> position,
            float radius,
            Vector2D<float> velocity,
            float rotation,
            int generation,
            int variant
        )
        {
            _renderer = new UniformColorMeshRenderer { Mesh = mesh, Color = color, DrawOrder = 5 };
            AddComponent(_renderer);
            Position = position;
            Rotation = rotation;
            Scale = new(radius, radius);
            Velocity = velocity;
            Radius = radius;
            Generation = generation;
            Variant = variant;
        }

        /// <summary>Gets or sets the current movement bounds.</summary>
        public Vector2D<float> Bounds { get; set; }
        /// <summary>Gets the current velocity.</summary>
        public Vector2D<float> Velocity { get; }
        /// <summary>Gets the visual and collision radius.</summary>
        public float Radius { get; }
        /// <summary>Gets the collision radius.</summary>
        public float CollisionRadius => Radius;
        /// <summary>Gets the splitting generation.</summary>
        public int Generation { get; }
        /// <summary>Gets the mesh variant identifier.</summary>
        public int Variant { get; }

        /// <inheritdoc />
        public override void Update(double deltaTime)
        {
            var elapsed = Math.Clamp((float)deltaTime, 0f, 0.1f);
            var position = Position + Velocity * elapsed;
            Rotation += (0.3f + Variant * 0.07f) * elapsed;
            Wrap(ref position, Bounds);
            Position = position;
        }
    }

    /// <summary>Owns bullet movement, rotation, lifetime, and collision state.</summary>
    private sealed class Bullet : GameObject2D
    {
        private readonly UniformColorMeshRenderer _renderer;

        /// <summary>Creates a bullet aligned with its firing direction.</summary>
        public Bullet(
            Mesh mesh,
            Color color,
            Vector2D<float> position,
            float rotation,
            Vector2D<float> velocity
        )
        {
            _renderer = new UniformColorMeshRenderer { Mesh = mesh, Color = color, DrawOrder = 110 };
            AddComponent(_renderer);
            Position = position;
            Rotation = rotation;
            Velocity = velocity;
            Lifetime = BulletLifetime;
        }

        /// <summary>Gets or sets the current movement bounds.</summary>
        public Vector2D<float> Bounds { get; set; }
        /// <summary>Gets the bullet velocity.</summary>
        public Vector2D<float> Velocity { get; }
        /// <summary>Gets the circular collision radius.</summary>
        public float CollisionRadius => 6f;
        /// <summary>Gets the remaining lifetime.</summary>
        public float Lifetime { get; private set; }
        /// <summary>Raised when the bullet reaches the end of its lifetime.</summary>
        public event Action<Bullet>? Expired;

        /// <inheritdoc />
        public override void Update(double deltaTime)
        {
            var elapsed = Math.Clamp((float)deltaTime, 0f, 0.1f);
            var position = Position + Velocity * elapsed;
            Lifetime -= elapsed;
            Wrap(ref position, Bounds);
            Position = position;
            if (Lifetime <= 0f)
                Expired?.Invoke(this);
        }
    }

    /// <summary>Wraps a position across the current screen bounds.</summary>
    /// <param name="position">The position to wrap.</param>
    /// <param name="bounds">The screen bounds.</param>
    private static void Wrap(ref Vector2D<float> position, Vector2D<float> bounds)
    {
        if (position.X < 0f) position.X += bounds.X;
        if (position.X >= bounds.X) position.X -= bounds.X;
        if (position.Y < 0f) position.Y += bounds.Y;
        if (position.Y >= bounds.Y) position.Y -= bounds.Y;
    }
}
