namespace Nexus.Samples.Asteroids;

using Nexus.Physics;
using Nexus.Physics.Components;

/// <summary>Runs a single-wave Asteroids round using indexed uniform-color meshes.</summary>
[Scene("Asteroids")]
public sealed class AsteroidsScene : Scene
{
    private const float BulletLifetime = 1.2f;
    private const float FireCooldown = 0.18f;
    private const int StartingLives = 3;
    private const uint ShipCollisionCategory = 1u << 0;
    private const uint BulletCollisionCategory = 1u << 1;
    private const uint AsteroidCollisionCategory = 1u << 2;

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
    private static readonly TriangleMeshShape2D ShipShape = CreatePhysicsShape(ShipMesh);
    private static readonly TriangleMeshShape2D BulletShape = CreatePhysicsShape(BulletMesh);
    private static readonly TriangleMeshShape2D[] AsteroidShapes =
        AsteroidMeshes.Select(CreatePhysicsShape).ToArray();

    private readonly IWindowService _windowService;
    private readonly IEventHub _eventHub;
    private readonly PhysicsWorldId _worldId;
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
    /// <param name="windowService">Provides the current window size.</param>
    /// <param name="physics">Owns the scene's simulation world.</param>
    public AsteroidsScene(
        ITextStyleRegistry textStyles,
        IEventHub eventHub,
        IWindowService windowService,
        IPhysicsSystem physics
    )
    {
        _windowService = windowService;
        _eventHub = eventHub;
        _worldId = physics.CreateWorld2D().Id;
        MainCamera = _camera;
        _scoreText = CreateText(textStyles, 24);
        _livesText = CreateText(textStyles, 24);
        _statusText = CreateText(textStyles, 28);
        _statusText.MaximumLines = 2;
        _lastWindowSize = windowService.GetMainWindow().Size;
        InputMap = CreateInputMap(eventHub);
        _ship = new Ship(_shipInput, ShipMesh, ShipColor, _worldId);
        _ship.Fired += OnShipFired;
        UpdateLayout(_lastWindowSize);
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

        base.Update(deltaTime);

        _statusText.Text = _finished
            ? (_lives == 0
                ? "Game Over - Press R to restart"
                : "Field Cleared - Press R to restart")
            : string.Empty;
        _scoreText.Text = $"Score: {_score}";
        _livesText.Text = $"Lives: {_lives}";
    }

    /// <summary>Applies gameplay reactions to a collision reported by PhysicsSystem.</summary>
    /// <param name="message">The deferred collision result.</param>
    public void Handle(PhysicsCollisionEvent message)
    {
        if (_finished)
            return;

        var firstOwner = message.First.Owner;
        var secondOwner = message.Second.Owner;
        if (
            (firstOwner is Bullet && secondOwner is Asteroid)
            || (secondOwner is Bullet && firstOwner is Asteroid)
        )
        {
            var bullet = firstOwner as Bullet ?? secondOwner as Bullet;
            var asteroid = firstOwner as Asteroid ?? secondOwner as Asteroid;
            if (bullet is not null && asteroid is not null)
                HandleBulletHit(bullet, asteroid);
        }
        else if (
            (firstOwner is Ship && secondOwner is Asteroid)
            || (secondOwner is Ship && firstOwner is Asteroid)
        )
        {
            var ship = firstOwner as Ship ?? secondOwner as Ship;
            var asteroid = firstOwner as Asteroid ?? secondOwner as Asteroid;
            if (ReferenceEquals(ship, _ship) && asteroid is not null && _asteroids.Contains(asteroid))
                HandleShipHit();
        }
    }

    /// <summary>Creates restart and quit bindings.</summary>
    /// <param name="eventHub">The event hub used by the input map.</param>
    /// <returns>The configured input map.</returns>
    private InputMap CreateInputMap(IEventHub eventHub)
    {
        var map = new InputMap(eventHub);
        BindHeld(map, KeyEnum.Left, value => _shipInput.Left = value);
        BindHeld(map, KeyEnum.Right, value => _shipInput.Right = value);
        BindHeld(map, KeyEnum.A, value => _shipInput.A = value);
        BindHeld(map, KeyEnum.D, value => _shipInput.D = value);
        BindHeld(map, KeyEnum.Up, value => _shipInput.Up = value);
        BindHeld(map, KeyEnum.W, value => _shipInput.W = value);
        BindHeld(map, KeyEnum.Space, value => _shipInput.Fire = value);
        map.OnKeyPressed(KeyEnum.R).Invoke(() => HandlePressed(KeyEnum.R));
        map.OnKeyPressed(KeyEnum.Escape).Invoke(() => HandlePressed(KeyEnum.Escape));
        map.OnKeyReleased(KeyEnum.R).Invoke(() => _pressedKeys.Remove(KeyEnum.R));
        map.OnKeyReleased(KeyEnum.Escape).Invoke(() => _pressedKeys.Remove(KeyEnum.Escape));
        return map;
    }

    /// <summary>Registers pressed and released actions for a held control.</summary>
    /// <param name="map">The input map receiving the bindings.</param>
    /// <param name="key">The bound key.</param>
    /// <param name="setHeld">The action that updates the held state.</param>
    /// <param name="released">The released action.</param>
    private static void BindHeld(InputMap map, KeyEnum key, Action<bool> setHeld)
    {
        map.OnKeyPressed(key).Invoke(() => setHeld(true));
        map.OnKeyReleased(key).Invoke(() => setHeld(false));
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
        var bullet = new Bullet(BulletMesh, BulletColor, spawn.Position, spawn.Rotation, spawn.Velocity, _worldId)
        {
            Bounds = _ship.Bounds,
        };
        bullet.Expired += RemoveBullet;
        _bullets.Add(bullet);
        Children.Add(bullet);
    }

    /// <summary>Applies a deferred bullet-to-asteroid collision.</summary>
    private void HandleBulletHit(Bullet bullet, Asteroid target)
    {
        if (!_bullets.Contains(bullet) || !_asteroids.Contains(target))
            return;

        RemoveBullet(bullet);
        RemoveAsteroid(target);
        _score += 100;
        if (target.Generation < 4)
        {
            var direction = Normalize(target.Velocity);
            var perpendicular = new Vector2D<float>(-direction.Y, direction.X);
            AddAsteroid(target.Position, target.Generation + 1, target.Radius * 0.62f,
                75f + target.Generation * 12f, target.Variant + 1,
                target.Velocity + perpendicular * 32f);
            AddAsteroid(target.Position, target.Generation + 1, target.Radius * 0.62f,
                75f + target.Generation * 12f, target.Variant + 3,
                target.Velocity - perpendicular * 32f);
        }

        if (_asteroids.Count == 0)
            FinishRound();
    }

    /// <summary>Applies a deferred ship-to-asteroid collision.</summary>
    private void HandleShipHit()
    {
        if (_ship.IsInvulnerable)
            return;

        _lives--;
        if (_lives == 0)
            FinishRound();
        else
            _ship.Reset(new(_lastWindowSize.X / 2f, _lastWindowSize.Y / 2f), invulnerable: true);
    }

    /// <summary>Freezes game motion after a win or loss while leaving scene traversal active.</summary>
    private void FinishRound()
    {
        if (_finished)
            return;

        _finished = true;
        _ship.StopMotion();
        foreach (var asteroid in _asteroids)
            asteroid.StopMotion();
        foreach (var bullet in _bullets)
            bullet.StopMotion();
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
            AsteroidShapes[variant % AsteroidShapes.Length],
            AsteroidColor,
            position,
            radius,
            velocity ?? new Vector2D<float>(MathF.Cos(angle), MathF.Sin(angle)) * speed,
            angle,
            generation,
            variant,
            _worldId
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

    /// <summary>Normalizes a vector, using a default direction for zero length.</summary>
    private static Vector2D<float> Normalize(Vector2D<float> value)
    {
        var length = MathF.Sqrt(value.X * value.X + value.Y * value.Y);
        return length < 0.001f ? new(1f, 0f) : value * (1f / length);
    }

    /// <summary>Stores mapped ship controls.</summary>
    private sealed class ShipInput
    {
        /// <summary>Gets or sets whether the Left key is held.</summary>
        public bool Left { get; set; }
        /// <summary>Gets or sets whether the Right key is held.</summary>
        public bool Right { get; set; }
        /// <summary>Gets or sets whether the A key is held.</summary>
        public bool A { get; set; }
        /// <summary>Gets or sets whether the D key is held.</summary>
        public bool D { get; set; }
        /// <summary>Gets or sets whether the Up key is held.</summary>
        public bool Up { get; set; }
        /// <summary>Gets or sets whether the W key is held.</summary>
        public bool W { get; set; }
        /// <summary>Gets whether counterclockwise rotation is held.</summary>
        public bool RotateLeft => Left || A;
        /// <summary>Gets whether clockwise rotation is held.</summary>
        public bool RotateRight => Right || D;
        /// <summary>Gets whether thrust is held.</summary>
        public bool Thrust => Up || W;
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
        private readonly GameObject2D _muzzle = new() { Position = new(30f / ShipScale, 0f) };
        private readonly UniformColorMeshRenderer _renderer;
        private readonly PhysicsBody2D _body;
        private readonly PhysicsCollider2D _collider;
        private float _fireTimer;
        private float _invulnerability;
        private bool _isFrozen;

        /// <summary>Creates a ship with its renderer and inherited-transform muzzle.</summary>
        /// <param name="input">The mapped control state.</param>
        /// <param name="mesh">The ship mesh.</param>
        /// <param name="color">The ship color.</param>
        /// <param name="worldId">The physics world identifier.</param>
        public Ship(ShipInput input, Mesh mesh, Color color, PhysicsWorldId worldId)
        {
            _input = input;
            _renderer = new UniformColorMeshRenderer { Mesh = mesh, Color = color, DrawOrder = 100 };
            AddComponent(_renderer);
            Scale = new(ShipScale, ShipScale);
            Children.Add(_muzzle);
            _body = new() { WorldId = worldId };
            _collider = new()
            {
                WorldId = worldId,
                Shape = ShipShape,
                CollisionCategory = ShipCollisionCategory,
                CollisionMask = uint.MaxValue & ~BulletCollisionCategory,
            };
            AddComponent(_body);
            AddComponent(_collider);
        }

        /// <summary>Gets or sets the current movement bounds.</summary>
        public Vector2D<float> Bounds { get; set; }
        /// <summary>Gets whether the ship is currently crash-invulnerable.</summary>
        public bool IsInvulnerable => _invulnerability > 0f;
        /// <summary>Raised after the muzzle transform is current and a shot is fired.</summary>
        public event Action<ShipSpawn>? Fired;
        /// <summary>Gets the CPU mesh collider used for ship contacts.</summary>
        public PhysicsCollider2D Collider => _collider;

        /// <inheritdoc />
        public override void Update(double deltaTime)
        {
            var elapsed = (float)deltaTime;
            _body.AngularVelocity = _isFrozen
                ? 0f
                : ((_input.RotateRight ? 1f : 0f) - (_input.RotateLeft ? 1f : 0f)) * 3.8f;
            _body.Acceleration = default;
            if (!_isFrozen && _input.Thrust)
            {
                var forward = new Vector2D<float>(MathF.Cos(Rotation), MathF.Sin(Rotation));
                _body.Acceleration = forward * 260f;
                var speed = MathF.Sqrt(_body.Velocity.X * _body.Velocity.X + _body.Velocity.Y * _body.Velocity.Y);
                if (speed > 300f)
                    _body.Velocity *= 300f / speed;
            }
            var position = Position;
            if (Wrap(ref position, Bounds))
                _body.Teleport(position, Rotation);
            base.Update(deltaTime);
            _fireTimer = MathF.Max(0f, _fireTimer - elapsed);
            _invulnerability = MathF.Max(0f, _invulnerability - elapsed);
            _renderer.Color = IsInvulnerable && (int)(_invulnerability * 12f) % 2 == 0
                ? Colors.Red
                : ShipColor;

            if (!_isFrozen && _input.Fire && _fireTimer <= 0f)
            {
                var forward = new Vector2D<float>(MathF.Cos(Rotation), MathF.Sin(Rotation));
                var muzzlePosition = new Vector2D<float>(_muzzle.WorldTransform.M41, _muzzle.WorldTransform.M42);
                Fired?.Invoke(new(muzzlePosition, _body.Velocity + forward * 560f, Rotation));
                _fireTimer = FireCooldown;
            }
        }

        /// <summary>Resets position, velocity, orientation, and crash protection.</summary>
        /// <param name="position">The new position.</param>
        /// <param name="invulnerable">Whether to grant crash protection.</param>
        public void Reset(Vector2D<float> position, bool invulnerable)
        {
            const float resetRotation = -MathF.PI / 2f;
            _body.Teleport(position, resetRotation);
            _body.Velocity = default;
            _body.Acceleration = default;
            _body.AngularVelocity = 0f;
            _isFrozen = false;
            _fireTimer = 0f;
            _invulnerability = invulnerable ? 2f : 0f;
        }

        /// <summary>Stops ship motion and prevents input from restarting it.</summary>
        public void StopMotion()
        {
            _isFrozen = true;
            _body.Velocity = default;
            _body.Acceleration = default;
            _body.AngularVelocity = 0f;
        }
    }

    /// <summary>Owns asteroid movement, spin, scale, and collision state.</summary>
    private sealed class Asteroid : GameObject2D
    {
        private readonly UniformColorMeshRenderer _renderer;
        private readonly PhysicsBody2D _body;
        private readonly PhysicsCollider2D _collider;

        /// <summary>Creates an asteroid node with its shared mesh and collision shape.</summary>
        /// <param name="mesh">The shared render mesh.</param>
        /// <param name="shape">The shared collision shape.</param>
        /// <param name="color">The asteroid color.</param>
        /// <param name="position">The initial position.</param>
        /// <param name="radius">The visual and collision radius.</param>
        /// <param name="velocity">The initial velocity.</param>
        /// <param name="rotation">The initial rotation.</param>
        /// <param name="generation">The splitting generation.</param>
        /// <param name="variant">The mesh variant identifier.</param>
        /// <param name="worldId">The physics world identifier.</param>
        public Asteroid(
            Mesh mesh,
            TriangleMeshShape2D shape,
            Color color,
            Vector2D<float> position,
            float radius,
            Vector2D<float> velocity,
            float rotation,
            int generation,
            int variant,
            PhysicsWorldId worldId
        )
        {
            _renderer = new UniformColorMeshRenderer { Mesh = mesh, Color = color, DrawOrder = 5 };
            AddComponent(_renderer);
            Position = position;
            Rotation = rotation;
            Scale = new(radius, radius);
            Radius = radius;
            Generation = generation;
            Variant = variant;
            _body = new() { WorldId = worldId, Velocity = velocity, AngularVelocity = 0.3f + variant * 0.07f };
            _collider = new()
            {
                WorldId = worldId,
                Shape = shape,
                CollisionCategory = AsteroidCollisionCategory,
            };
            AddComponent(_body);
            AddComponent(_collider);
        }

        /// <summary>Gets or sets the current movement bounds.</summary>
        public Vector2D<float> Bounds { get; set; }
        /// <summary>Gets the current velocity.</summary>
        public Vector2D<float> Velocity => _body.Velocity;
        /// <summary>Gets the visual and collision radius.</summary>
        public float Radius { get; }
        /// <summary>Gets the collision radius.</summary>
        public float CollisionRadius => Radius;
        /// <summary>Gets the splitting generation.</summary>
        public int Generation { get; }
        /// <summary>Gets the mesh variant identifier.</summary>
        public int Variant { get; }
        /// <summary>Gets the CPU mesh collider used for asteroid contacts.</summary>
        public PhysicsCollider2D Collider => _collider;

        /// <inheritdoc />
        public override void Update(double deltaTime)
        {
            var position = Position;
            if (Wrap(ref position, Bounds))
                _body.Teleport(position, Rotation);
            base.Update(deltaTime);
        }

        /// <summary>Stops linear and angular motion.</summary>
        public void StopMotion()
        {
            _body.Velocity = default;
            _body.Acceleration = default;
            _body.AngularVelocity = 0f;
        }
    }

    /// <summary>Converts an authored triangle-list mesh into physics geometry.</summary>
    /// <param name="mesh">The authored mesh.</param>
    /// <returns>The local-space physics triangles.</returns>
    private static PhysicsTriangle2D[] ToPhysicsTriangles(Mesh mesh)
    {
        var indices = mesh.Indices.Count == 0
            ? Enumerable.Range(0, checked((int)mesh.VertexCount)).Select(index => (uint)index).ToArray()
            : mesh.Indices;
        if (mesh.Topology != PrimitiveTopologyEnum.TriangleList || indices.Count % 3 != 0)
            throw new ArgumentException("Physics geometry requires a triangle-list mesh.", nameof(mesh));

        var triangles = new PhysicsTriangle2D[indices.Count / 3];
        for (var index = 0; index < triangles.Length; index++)
        {
            var first = mesh.Vertices[checked((int)indices[index * 3])].Position;
            var second = mesh.Vertices[checked((int)indices[index * 3 + 1])].Position;
            var third = mesh.Vertices[checked((int)indices[index * 3 + 2])].Position;
            triangles[index] = new(new(first.X, first.Y), new(second.X, second.Y), new(third.X, third.Y));
        }
        return triangles;
    }

    /// <summary>Creates immutable collision geometry for a shared mesh.</summary>
    /// <param name="mesh">The authored mesh.</param>
    /// <returns>The reusable triangle mesh shape.</returns>
    private static TriangleMeshShape2D CreatePhysicsShape(Mesh mesh) =>
        new(ToPhysicsTriangles(mesh));

    /// <summary>Owns bullet movement, rotation, lifetime, and collision state.</summary>
    private sealed class Bullet : GameObject2D
    {
        private readonly UniformColorMeshRenderer _renderer;
        private readonly PhysicsBody2D _body;
        private readonly PhysicsCollider2D _collider;

        /// <summary>Creates a bullet aligned with its firing direction.</summary>
        /// <param name="mesh">The shared render mesh.</param>
        /// <param name="color">The bullet color.</param>
        /// <param name="position">The initial position.</param>
        /// <param name="rotation">The initial rotation.</param>
        /// <param name="velocity">The initial velocity.</param>
        /// <param name="worldId">The physics world identifier.</param>
        public Bullet(
            Mesh mesh,
            Color color,
            Vector2D<float> position,
            float rotation,
            Vector2D<float> velocity,
            PhysicsWorldId worldId
        )
        {
            _renderer = new UniformColorMeshRenderer { Mesh = mesh, Color = color, DrawOrder = 110 };
            AddComponent(_renderer);
            Position = position;
            Rotation = rotation;
            Lifetime = BulletLifetime;
            _body = new() { WorldId = worldId, Velocity = velocity };
            _collider = new()
            {
                WorldId = worldId,
                Shape = BulletShape,
                CollisionCategory = BulletCollisionCategory,
                CollisionMask = uint.MaxValue & ~ShipCollisionCategory,
            };
            AddComponent(_body);
            AddComponent(_collider);
        }

        /// <summary>Gets or sets the current movement bounds.</summary>
        public Vector2D<float> Bounds { get; set; }
        /// <summary>Gets the bullet velocity.</summary>
        public Vector2D<float> Velocity => _body.Velocity;
        /// <summary>Gets the circular collision radius.</summary>
        public float CollisionRadius => 6f;
        /// <summary>Gets the remaining lifetime.</summary>
        public float Lifetime { get; private set; }
        /// <summary>Raised when the bullet reaches the end of its lifetime.</summary>
        public event Action<Bullet>? Expired;
        /// <summary>Gets the CPU mesh collider used for bullet contacts.</summary>
        public PhysicsCollider2D Collider => _collider;

        /// <inheritdoc />
        public override void Update(double deltaTime)
        {
            var elapsed = (float)deltaTime;
            Lifetime -= elapsed;
            var position = Position;
            if (Wrap(ref position, Bounds))
                _body.Teleport(position, Rotation);
            base.Update(deltaTime);
            if (Lifetime <= 0f)
                Expired?.Invoke(this);
        }

        /// <summary>Stops linear and angular motion.</summary>
        public void StopMotion()
        {
            _body.Velocity = default;
            _body.Acceleration = default;
            _body.AngularVelocity = 0f;
        }
    }

    /// <summary>Wraps a position across the current screen bounds.</summary>
    /// <param name="position">The position to wrap.</param>
    /// <param name="bounds">The screen bounds.</param>
    private static bool Wrap(ref Vector2D<float> position, Vector2D<float> bounds)
    {
        var wrapped = false;
        if (position.X < 0f || position.X >= bounds.X)
        {
            position.X = WrapCoordinate(position.X, bounds.X);
            wrapped = true;
        }
        if (position.Y < 0f || position.Y >= bounds.Y)
        {
            position.Y = WrapCoordinate(position.Y, bounds.Y);
            wrapped = true;
        }
        return wrapped;
    }

    /// <summary>Maps a coordinate into a positive-sized range.</summary>
    /// <param name="coordinate">The coordinate to wrap.</param>
    /// <param name="size">The positive range size.</param>
    /// <returns>The wrapped coordinate.</returns>
    private static float WrapCoordinate(float coordinate, float size) =>
        ((coordinate % size) + size) % size;
}
