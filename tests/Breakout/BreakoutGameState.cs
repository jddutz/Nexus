namespace Nexus.Samples.Breakout;

using Nexus.Graphics;
using Silk.NET.Maths;

/// <summary>Identifies the phase of a Breakout round.</summary>
public enum BreakoutRoundState
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

/// <summary>Describes one removable brick in the logical playfield.</summary>
public sealed class BreakoutBrick
{
    /// <summary>Gets the stable identifier used by the renderable scene child.</summary>
    public int Id { get; }

    /// <summary>Gets the brick rectangle in logical playfield coordinates.</summary>
    public Rectangle<float> Bounds { get; }

    /// <summary>Gets the brick tint.</summary>
    public Color Color { get; }

    /// <summary>Creates a brick with a rectangle and tint.</summary>
    /// <param name="bounds">The logical rectangle.</param>
    /// <param name="color">The display tint.</param>
    public BreakoutBrick(int id, Rectangle<float> bounds, Color color)
    {
        Id = id;
        Bounds = bounds;
        Color = color;
    }
}

/// <summary>Owns Breakout timing, movement, collision, scoring, and round transitions.</summary>
public sealed class BreakoutGameState
{
    /// <summary>Gets the logical playfield width.</summary>
    public const float FieldWidth = 1280f;
    /// <summary>Gets the logical playfield height.</summary>
    public const float FieldHeight = 720f;
    /// <summary>Gets the fixed simulation step in seconds.</summary>
    public const double FixedStep = 1d / 120d;
    /// <summary>Gets the paddle width.</summary>
    public const float PaddleWidth = 120f;
    /// <summary>Gets the paddle height.</summary>
    public const float PaddleHeight = 20f;
    /// <summary>Gets the ball size.</summary>
    public const float BallSize = 12f;
    /// <summary>Gets the paddle speed.</summary>
    public const float PaddleSpeed = 600f;
    /// <summary>Gets the ball speed.</summary>
    public const float BallSpeed = 400f;

    private const float PaddleY = FieldHeight - 50f;
    private const int MaximumCatchUpSteps = 8;
    private readonly List<BreakoutBrick> _bricks = [];
    private double _accumulator;
    private Vector2D<float> _ballVelocity;

    /// <summary>Gets the current round state.</summary>
    public BreakoutRoundState State { get; private set; }
    /// <summary>Gets the current score.</summary>
    public int Score { get; private set; }
    /// <summary>Gets the remaining lives.</summary>
    public int Lives { get; private set; }
    /// <summary>Gets the paddle rectangle.</summary>
    public Rectangle<float> Paddle { get; private set; }
    /// <summary>Gets the ball rectangle.</summary>
    public Rectangle<float> Ball { get; private set; }
    /// <summary>Gets the active bricks.</summary>
    public IReadOnlyList<BreakoutBrick> Bricks => _bricks;

    /// <summary>Creates a new round in the ready state.</summary>
    public BreakoutGameState() => Reset();

    /// <summary>Resets the complete game, including score, lives, ball, paddle, and bricks.</summary>
    public void Reset()
    {
        _accumulator = 0d;
        Score = 0;
        Lives = 3;
        Paddle = new Rectangle<float>(
            (FieldWidth - PaddleWidth) / 2f,
            PaddleY,
            PaddleWidth,
            PaddleHeight
        );
        Ball = new Rectangle<float>(
            FieldWidth / 2f - BallSize / 2f,
            PaddleY - BallSize - 2f,
            BallSize,
            BallSize
        );
        _ballVelocity = Vector2D<float>.Zero;
        State = BreakoutRoundState.Ready;
        CreateBricks();
    }

    /// <summary>Launches the ready ball with a small horizontal component.</summary>
    public void Launch()
    {
        if (State != BreakoutRoundState.Ready)
            return;
        _ballVelocity = new Vector2D<float>(120f, -MathF.Sqrt(BallSpeed * BallSpeed - 120f * 120f));
        State = BreakoutRoundState.Playing;
    }

    /// <summary>Toggles pause while the round is active, clearing stale elapsed time.</summary>
    public void TogglePause()
    {
        if (State == BreakoutRoundState.Playing)
        {
            State = BreakoutRoundState.Paused;
            _accumulator = 0d;
        }
        else if (State == BreakoutRoundState.Paused)
        {
            State = BreakoutRoundState.Playing;
            _accumulator = 0d;
        }
    }

    /// <summary>Advances the fixed-step simulation using the currently held paddle inputs.</summary>
    /// <param name="elapsedSeconds">The rendered frame duration.</param>
    /// <param name="moveLeft">Whether left movement is held.</param>
    /// <param name="moveRight">Whether right movement is held.</param>
    public void Advance(double elapsedSeconds, bool moveLeft, bool moveRight)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (State == BreakoutRoundState.Paused || State is BreakoutRoundState.Won or BreakoutRoundState.Lost)
            return;

        _accumulator += Math.Min(elapsedSeconds, 0.25d);
        var steps = 0;
        while (_accumulator >= FixedStep && steps++ < MaximumCatchUpSteps)
        {
            _accumulator -= FixedStep;
            Step((moveRight ? 1f : 0f) - (moveLeft ? 1f : 0f));
        }
        if (steps > MaximumCatchUpSteps)
            _accumulator = 0d;
    }

    /// <summary>Processes one fixed simulation step.</summary>
    /// <param name="direction">The opposing-input-resolved paddle direction.</param>
    private void Step(float direction)
    {
        var paddleX = Math.Clamp(Paddle.Origin.X + direction * PaddleSpeed * (float)FixedStep, 0f, FieldWidth - PaddleWidth);
        Paddle = new Rectangle<float>(paddleX, PaddleY, PaddleWidth, PaddleHeight);
        if (State == BreakoutRoundState.Ready)
        {
            Ball = new Rectangle<float>(Paddle.Origin.X + (PaddleWidth - BallSize) / 2f, PaddleY - BallSize - 2f, BallSize, BallSize);
            return;
        }

        var previous = Ball;
        Ball = new Rectangle<float>(Ball.Origin.X + _ballVelocity.X * (float)FixedStep, Ball.Origin.Y + _ballVelocity.Y * (float)FixedStep, BallSize, BallSize);
        ResolveWalls();
        if (_ballVelocity.Y > 0f && Intersects(Ball, Paddle))
        {
            Ball = new Rectangle<float>(Ball.Origin.X, Paddle.Origin.Y - BallSize, BallSize, BallSize);
            var offset = Math.Clamp((Ball.Origin.X + BallSize / 2f - (Paddle.Origin.X + PaddleWidth / 2f)) / (PaddleWidth / 2f), -1f, 1f);
            var angle = offset * MathF.PI / 3f;
            _ballVelocity = new Vector2D<float>(MathF.Sin(angle) * BallSpeed, -MathF.Cos(angle) * BallSpeed);
        }
        else
        {
            ResolveBrick(previous);
        }

        if (Ball.Origin.Y > FieldHeight)
        {
            Lives--;
            _accumulator = 0d;
            if (Lives == 0)
            {
                State = BreakoutRoundState.Lost;
                return;
            }
            State = BreakoutRoundState.Ready;
            _ballVelocity = Vector2D<float>.Zero;
            Ball = new Rectangle<float>(Paddle.Origin.X + (PaddleWidth - BallSize) / 2f, PaddleY - BallSize - 2f, BallSize, BallSize);
        }
    }

    /// <summary>Reflects the ball from the visible playfield walls and resolves penetration.</summary>
    private void ResolveWalls()
    {
        if (Ball.Origin.X < 0f)
        {
            Ball = new Rectangle<float>(0f, Ball.Origin.Y, BallSize, BallSize);
            _ballVelocity = new Vector2D<float>(MathF.Abs(_ballVelocity.X), _ballVelocity.Y);
        }
        else if (Ball.Max.X > FieldWidth)
        {
            Ball = new Rectangle<float>(FieldWidth - BallSize, Ball.Origin.Y, BallSize, BallSize);
            _ballVelocity = new Vector2D<float>(-MathF.Abs(_ballVelocity.X), _ballVelocity.Y);
        }
        if (Ball.Origin.Y < 0f)
        {
            Ball = new Rectangle<float>(Ball.Origin.X, 0f, BallSize, BallSize);
            _ballVelocity = new Vector2D<float>(_ballVelocity.X, MathF.Abs(_ballVelocity.Y));
        }
    }

    /// <summary>Removes and scores the first brick intersected by the ball.</summary>
    /// <param name="previous">The ball rectangle before this step.</param>
    private void ResolveBrick(Rectangle<float> previous)
    {
        for (var index = 0; index < _bricks.Count; index++)
        {
            var brick = _bricks[index];
            if (!Intersects(Ball, brick.Bounds))
                continue;
            var vertical = previous.Max.Y <= brick.Bounds.Origin.Y || previous.Origin.Y >= brick.Bounds.Max.Y;
            if (vertical)
            {
                var y = _ballVelocity.Y > 0f ? brick.Bounds.Origin.Y - BallSize : brick.Bounds.Max.Y;
                Ball = new Rectangle<float>(Ball.Origin.X, y, BallSize, BallSize);
                _ballVelocity = new Vector2D<float>(_ballVelocity.X, -_ballVelocity.Y);
            }
            else
            {
                var x = _ballVelocity.X > 0f ? brick.Bounds.Origin.X - BallSize : brick.Bounds.Max.X;
                Ball = new Rectangle<float>(x, Ball.Origin.Y, BallSize, BallSize);
                _ballVelocity = new Vector2D<float>(-_ballVelocity.X, _ballVelocity.Y);
            }
            _bricks.RemoveAt(index);
            Score += 10;
            if (_bricks.Count == 0)
                State = BreakoutRoundState.Won;
            return;
        }
    }

    /// <summary>Creates the authored five-by-ten brick layout.</summary>
    private void CreateBricks()
    {
        _bricks.Clear();
        var colors = new[] { Colors.Red, Colors.Orange, Colors.Yellow, Colors.Green, Colors.Blue };
        const float width = 108f;
        const float height = 28f;
        const float gap = 10f;
        const float startX = 55f;
        const float startY = 80f;
        for (var row = 0; row < 5; row++)
        for (var column = 0; column < 10; column++)
            _bricks.Add(new BreakoutBrick(row * 10 + column, new Rectangle<float>(startX + column * (width + gap), startY + row * (height + gap), width, height), colors[row]));
    }

    /// <summary>Determines whether two rectangles overlap.</summary>
    /// <param name="first">The first rectangle.</param>
    /// <param name="second">The second rectangle.</param>
    /// <returns><see langword="true"/> when the rectangles overlap.</returns>
    private static bool Intersects(Rectangle<float> first, Rectangle<float> second) =>
        first.Origin.X < second.Max.X && first.Max.X > second.Origin.X
        && first.Origin.Y < second.Max.Y && first.Max.Y > second.Origin.Y;
}
