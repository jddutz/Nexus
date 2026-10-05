namespace Nexus.Samples.Snake;

using System.Collections.ObjectModel;

/// <summary>Identifies a tile on the Snake game board.</summary>
/// <param name="X">The zero-based horizontal tile coordinate.</param>
/// <param name="Y">The zero-based vertical tile coordinate.</param>
public readonly record struct SnakeTile(int X, int Y);

/// <summary>Describes a cardinal direction of movement.</summary>
public enum SnakeDirection
{
    /// <summary>Move toward decreasing vertical coordinates.</summary>
    Up,

    /// <summary>Move toward increasing vertical coordinates.</summary>
    Down,

    /// <summary>Move toward decreasing horizontal coordinates.</summary>
    Left,

    /// <summary>Move toward increasing horizontal coordinates.</summary>
    Right,
}

/// <summary>Describes the current Snake game phase.</summary>
public enum SnakeGameStatus
{
    /// <summary>The game is waiting for a fresh start input.</summary>
    Waiting,

    /// <summary>The snake is moving and can be steered.</summary>
    Playing,

    /// <summary>A collision ended the game.</summary>
    GameOver,

    /// <summary>The snake filled the board.</summary>
    Won,
}

/// <summary>Owns Snake rules and timing independently of rendering.</summary>
public sealed class SnakeGameState
{
    /// <summary>Gets the number of rows and columns on the square board.</summary>
    public const int BoardSize = 15;

    /// <summary>Gets the movement interval at the beginning of a game, in milliseconds.</summary>
    public const double InitialMovementIntervalMilliseconds = 250d;

    /// <summary>Gets the minimum movement interval, in milliseconds.</summary>
    public const double MinimumMovementIntervalMilliseconds = 60d;

    private const int MaximumTurns = 2;
    private const int MaximumTicksPerUpdate = 4;
    private readonly Random _random;
    private readonly List<SnakeTile> _snake = [];
    private readonly ReadOnlyCollection<SnakeTile> _snakeView;
    private readonly Queue<SnakeDirection> _turns = [];
    private SnakeTile? _target;
    private SnakeDirection _direction;
    private SnakeGameStatus _status;
    private double _movementIntervalMilliseconds;
    private double _movementAccumulatorSeconds;
    private int _score;

    /// <summary>Occurs after a visible game-state change.</summary>
    public event Action? StateChanged;

    /// <summary>Gets the snake tiles in head-first order.</summary>
    public IReadOnlyList<SnakeTile> Snake => _snakeView;

    /// <summary>Gets the current target, or null after a loss or win.</summary>
    public SnakeTile? Target => _target;

    /// <summary>Gets the current travel direction.</summary>
    public SnakeDirection Direction => _direction;

    /// <summary>Gets the current game phase.</summary>
    public SnakeGameStatus Status => _status;

    /// <summary>Gets the current score.</summary>
    public int Score => _score;

    /// <summary>Gets the movement interval in milliseconds.</summary>
    public double MovementIntervalMilliseconds => _movementIntervalMilliseconds;

    /// <summary>Creates a waiting game with a randomly selected empty target tile.</summary>
    /// <param name="random">Optional random source for target selection.</param>
    public SnakeGameState(Random? random = null)
    {
        _random = random ?? Random.Shared;
        _snakeView = _snake.AsReadOnly();
        Reset();
    }

    /// <summary>Resets all game data and returns to the waiting phase.</summary>
    public void Reset()
    {
        _snake.Clear();
        _snake.AddRange([new SnakeTile(7, 7), new SnakeTile(6, 7), new SnakeTile(5, 7)]);
        _direction = SnakeDirection.Right;
        _turns.Clear();
        _score = 0;
        _movementIntervalMilliseconds = InitialMovementIntervalMilliseconds;
        _movementAccumulatorSeconds = 0d;
        _target = SelectEmptyTile();
        _status = SnakeGameStatus.Waiting;
        StateChanged?.Invoke();
    }

    /// <summary>Starts a fresh game, consuming the input that initiated it.</summary>
    public void Start()
    {
        Reset();
        _status = SnakeGameStatus.Playing;
        StateChanged?.Invoke();
    }

    /// <summary>Queues a legal direction request while the game is playing.</summary>
    /// <param name="direction">The requested cardinal direction.</param>
    /// <returns>True if the request was queued.</returns>
    public bool RequestDirection(SnakeDirection direction)
    {
        if (!Enum.IsDefined(direction))
            throw new ArgumentOutOfRangeException(nameof(direction));
        if (_status != SnakeGameStatus.Playing || _turns.Count >= MaximumTurns)
            return false;

        var referenceDirection = _turns.Count > 0 ? _turns.Last() : _direction;
        if (direction == referenceDirection || IsOpposite(direction, referenceDirection))
            return false;

        _turns.Enqueue(direction);
        return true;
    }

    /// <summary>Advances game time, processing no more than four movement ticks.</summary>
    /// <param name="elapsedSeconds">The elapsed time since the previous update.</param>
    public void Advance(double elapsedSeconds)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (_status != SnakeGameStatus.Playing)
            return;

        _movementAccumulatorSeconds += elapsedSeconds;
        var ticks = 0;
        var stateChanged = false;
        while (
            _status == SnakeGameStatus.Playing
            && ticks < MaximumTicksPerUpdate
            && _movementAccumulatorSeconds >= _movementIntervalMilliseconds / 1000d
        )
        {
            _movementAccumulatorSeconds -= _movementIntervalMilliseconds / 1000d;
            MoveOneTile();
            ticks++;
            stateChanged = true;
        }

        if (_status == SnakeGameStatus.Playing)
        {
            var intervalSeconds = _movementIntervalMilliseconds / 1000d;
            if (_movementAccumulatorSeconds >= intervalSeconds)
                _movementAccumulatorSeconds %= intervalSeconds;
        }

        if (stateChanged)
            StateChanged?.Invoke();
    }

    /// <summary>Loads a controlled board position for rule-boundary tests.</summary>
    /// <param name="snake">The snake tiles in head-first order.</param>
    /// <param name="direction">The snake's current direction.</param>
    /// <param name="target">The tile occupied by the target, if any.</param>
    internal void ConfigureForTesting(
        IEnumerable<SnakeTile> snake,
        SnakeDirection direction,
        SnakeTile? target
    )
    {
        ArgumentNullException.ThrowIfNull(snake);
        if (!Enum.IsDefined(direction))
            throw new ArgumentOutOfRangeException(nameof(direction));

        var tiles = snake.ToArray();
        if (tiles.Length == 0)
            throw new ArgumentException("The snake must contain at least one tile.", nameof(snake));
        if (tiles.Length > BoardSize * BoardSize)
            throw new ArgumentException("The snake cannot exceed the board size.", nameof(snake));
        if (tiles.Any(tile => !IsOnBoard(tile)) || tiles.Distinct().Count() != tiles.Length)
            throw new ArgumentException(
                "Snake tiles must be unique and lie on the board.",
                nameof(snake)
            );
        if (target is { } targetTile && (!IsOnBoard(targetTile) || tiles.Contains(targetTile)))
            throw new ArgumentException(
                "The target must lie on an unoccupied board tile.",
                nameof(target)
            );

        _snake.Clear();
        _snake.AddRange(tiles);
        _direction = direction;
        _turns.Clear();
        _score = 0;
        _movementIntervalMilliseconds = InitialMovementIntervalMilliseconds;
        _movementAccumulatorSeconds = 0d;
        _target = target;
        _status = SnakeGameStatus.Playing;
        StateChanged?.Invoke();
    }

    /// <summary>Moves once, resolving wraparound, collisions, and growth.</summary>
    private void MoveOneTile()
    {
        if (_turns.TryDequeue(out var turn))
            _direction = turn;

        var head = _snake[0];
        var nextHead = _direction switch
        {
            SnakeDirection.Up => new SnakeTile(head.X, (head.Y + BoardSize - 1) % BoardSize),
            SnakeDirection.Down => new SnakeTile(head.X, (head.Y + 1) % BoardSize),
            SnakeDirection.Left => new SnakeTile((head.X + BoardSize - 1) % BoardSize, head.Y),
            SnakeDirection.Right => new SnakeTile((head.X + 1) % BoardSize, head.Y),
            _ => throw new InvalidOperationException("The current direction is invalid."),
        };
        var reachesTarget = _target == nextHead;
        var bodyLengthToCheck = reachesTarget ? _snake.Count : _snake.Count - 1;
        for (var index = 1; index < bodyLengthToCheck; index++)
        {
            if (_snake[index] != nextHead)
                continue;

            _snake.Clear();
            _target = null;
            _turns.Clear();
            _movementAccumulatorSeconds = 0d;
            _status = SnakeGameStatus.GameOver;
            return;
        }

        _snake.Insert(0, nextHead);
        if (!reachesTarget)
        {
            _snake.RemoveAt(_snake.Count - 1);
            return;
        }

        _score++;
        _movementIntervalMilliseconds = Math.Max(
            MinimumMovementIntervalMilliseconds,
            _movementIntervalMilliseconds * 0.95d
        );
        if (_snake.Count == BoardSize * BoardSize)
        {
            _target = null;
            _status = SnakeGameStatus.Won;
            return;
        }

        _target = SelectEmptyTile();
    }

    /// <summary>Selects uniformly from an explicit list of unoccupied board tiles.</summary>
    /// <returns>A uniformly selected empty tile, or null when the board is full.</returns>
    private SnakeTile? SelectEmptyTile()
    {
        var emptyTiles = new List<SnakeTile>(BoardSize * BoardSize - _snake.Count);
        for (var y = 0; y < BoardSize; y++)
        for (var x = 0; x < BoardSize; x++)
        {
            var tile = new SnakeTile(x, y);
            if (!_snake.Contains(tile))
                emptyTiles.Add(tile);
        }

        return emptyTiles.Count == 0 ? null : emptyTiles[_random.Next(emptyTiles.Count)];
    }

    /// <summary>Determines whether two directions are exact reversals.</summary>
    /// <param name="first">The first direction.</param>
    /// <param name="second">The second direction.</param>
    /// <returns>True when the directions point oppositely.</returns>
    private static bool IsOpposite(SnakeDirection first, SnakeDirection second) =>
        (first, second)
            is
                (SnakeDirection.Up, SnakeDirection.Down)
                or
                (SnakeDirection.Down, SnakeDirection.Up)
                or
                (SnakeDirection.Left, SnakeDirection.Right)
                or
                (SnakeDirection.Right, SnakeDirection.Left);

    /// <summary>Checks whether a tile coordinate is within the board.</summary>
    /// <param name="tile">The tile to check.</param>
    /// <returns>True when the tile lies on the board.</returns>
    private static bool IsOnBoard(SnakeTile tile) =>
        (uint)tile.X < BoardSize && (uint)tile.Y < BoardSize;
}
