using Nexus.Samples.SnakeGame;

namespace SnakeGame.Tests;

/// <summary>Checks Snake rule boundaries without requiring a rendering runtime.</summary>
public sealed class SnakeGameStateTests
{
    /// <summary>Verifies initial waiting state and the first full movement interval.</summary>
    [Fact]
    public void NewGameWaitsAndMovesOnlyAfterFullInterval()
    {
        var game = new SnakeGameState(new Random(1));

        Assert.Equal(SnakeGameStatus.Waiting, game.Status);
        Assert.Equal(
            [new SnakeTile(7, 7), new SnakeTile(6, 7), new SnakeTile(5, 7)],
            game.Snake
        );
        Assert.NotNull(game.Target);

        game.Start();
        game.Advance(0.249d);

        Assert.Equal(new SnakeTile(7, 7), game.Snake[0]);
        game.Advance(0.001d);

        Assert.Equal(new SnakeTile(8, 7), game.Snake[0]);
    }

    /// <summary>Verifies wrapping at the left edge.</summary>
    [Fact]
    public void MovementWrapsAcrossBoardEdges()
    {
        var game = new SnakeGameState(new Random(2));
        game.ConfigureForTesting(
            [new SnakeTile(0, 7), new SnakeTile(1, 7), new SnakeTile(2, 7)],
            SnakeDirection.Left,
            new SnakeTile(10, 10)
        );

        game.Advance(0.25d);

        Assert.Equal(new SnakeTile(14, 7), game.Snake[0]);
    }

    /// <summary>Verifies direct reversal rejection, a two-turn queue, and one turn per tick.</summary>
    [Fact]
    public void DirectionQueueRejectsReversalAndAppliesOneTurnPerTick()
    {
        var game = new SnakeGameState(new Random(3));
        game.Start();

        Assert.False(game.RequestDirection(SnakeDirection.Left));
        Assert.True(game.RequestDirection(SnakeDirection.Up));
        Assert.False(game.RequestDirection(SnakeDirection.Up));
        Assert.False(game.RequestDirection(SnakeDirection.Down));
        Assert.True(game.RequestDirection(SnakeDirection.Left));
        Assert.False(game.RequestDirection(SnakeDirection.Down));

        game.Advance(0.25d);
        Assert.Equal(SnakeDirection.Up, game.Direction);
        Assert.Equal(new SnakeTile(7, 6), game.Snake[0]);

        game.Advance(0.25d);
        Assert.Equal(SnakeDirection.Left, game.Direction);
        Assert.Equal(new SnakeTile(6, 6), game.Snake[0]);
    }

    /// <summary>Verifies entering the departing tail tile does not count as self-collision.</summary>
    [Fact]
    public void MovingIntoDepartingTailTileIsLegal()
    {
        var game = new SnakeGameState(new Random(4));
        game.ConfigureForTesting(
            [
                new SnakeTile(2, 1),
                new SnakeTile(1, 1),
                new SnakeTile(1, 2),
                new SnakeTile(2, 2),
            ],
            SnakeDirection.Down,
            new SnakeTile(10, 10)
        );

        game.Advance(0.25d);

        Assert.Equal(SnakeGameStatus.Playing, game.Status);
        Assert.Equal(new SnakeTile(2, 2), game.Snake[0]);
        Assert.Equal(new SnakeTile(1, 2), game.Snake[^1]);
    }

    /// <summary>Verifies body collision ends the game without retaining the board.</summary>
    [Fact]
    public void BodyCollisionEndsGameAndClearsBoard()
    {
        var game = new SnakeGameState(new Random(5));
        game.ConfigureForTesting(
            [
                new SnakeTile(2, 1),
                new SnakeTile(2, 2),
                new SnakeTile(3, 2),
                new SnakeTile(3, 1),
            ],
            SnakeDirection.Down,
            new SnakeTile(10, 10)
        );

        game.Advance(0.25d);

        Assert.Equal(SnakeGameStatus.GameOver, game.Status);
        Assert.Empty(game.Snake);
        Assert.Null(game.Target);
    }

    /// <summary>Verifies eating grows immediately, awards score, speeds up, and chooses an empty target.</summary>
    [Fact]
    public void EatingGrowsScoresSpeedsUpAndPlacesTargetOnEmptyTile()
    {
        var game = new SnakeGameState(new Random(6));
        game.ConfigureForTesting(
            [new SnakeTile(7, 7), new SnakeTile(6, 7), new SnakeTile(5, 7)],
            SnakeDirection.Right,
            new SnakeTile(8, 7)
        );

        game.Advance(0.25d);

        Assert.Equal(4, game.Snake.Count);
        Assert.Equal(1, game.Score);
        Assert.Equal(237.5d, game.MovementIntervalMilliseconds);
        Assert.NotNull(game.Target);
        Assert.DoesNotContain(game.Target!.Value, game.Snake);
    }

    /// <summary>Verifies a stalled update processes at most four ticks and discards whole-tick backlog.</summary>
    [Fact]
    public void LongStallIsCappedAndRetainsOnlyFractionalTime()
    {
        var game = new SnakeGameState(new Random(7));
        game.Start();

        game.Advance(2d);

        Assert.Equal(new SnakeTile(11, 7), game.Snake[0]);
        game.Advance(0.249d);
        Assert.Equal(new SnakeTile(11, 7), game.Snake[0]);
        game.Advance(0.001d);
        Assert.Equal(new SnakeTile(12, 7), game.Snake[0]);
    }

    /// <summary>Verifies the last empty target fills the board and enters the won phase.</summary>
    [Fact]
    public void FillingBoardWinsWithoutSelectingAnotherTarget()
    {
        var target = new SnakeTile(14, 14);
        var head = new SnakeTile(13, 14);
        var next = new SnakeTile(12, 14);
        var snake = new[] { head, next }.Concat(Enumerable
            .Range(0, SnakeGameState.BoardSize)
            .SelectMany(y => Enumerable.Range(0, SnakeGameState.BoardSize).Select(x => new SnakeTile(x, y)))
            .Where(tile => tile != target && tile != head && tile != next))
            .ToArray();

        var game = new SnakeGameState(new Random(8));
        game.ConfigureForTesting(snake, SnakeDirection.Right, target);

        game.Advance(0.25d);

        Assert.Equal(SnakeGameStatus.Won, game.Status);
        Assert.Equal(SnakeGameState.BoardSize * SnakeGameState.BoardSize, game.Snake.Count);
        Assert.Null(game.Target);
        Assert.Equal(1, game.Score);
    }

    /// <summary>Verifies reset clears queued input, score, elapsed time, and speed changes.</summary>
    [Fact]
    public void StartingAgainResetsAllGameState()
    {
        var game = new SnakeGameState(new Random(9));
        game.ConfigureForTesting(
            [new SnakeTile(7, 7), new SnakeTile(6, 7), new SnakeTile(5, 7)],
            SnakeDirection.Right,
            new SnakeTile(7, 6)
        );
        game.RequestDirection(SnakeDirection.Up);
        game.Advance(0.25d);
        Assert.Equal(1, game.Score);

        game.Start();

        Assert.Equal(SnakeGameStatus.Playing, game.Status);
        Assert.Equal(0, game.Score);
        Assert.Equal(
            SnakeGameState.InitialMovementIntervalMilliseconds,
            game.MovementIntervalMilliseconds
        );
        Assert.Equal(SnakeDirection.Right, game.Direction);
        Assert.Equal(new SnakeTile(7, 7), game.Snake[0]);

        game.Advance(0.25d);
        Assert.Equal(new SnakeTile(8, 7), game.Snake[0]);
    }

    /// <summary>Verifies repeated target selection never overlaps a snake segment.</summary>
    [Fact]
    public void RepeatedTargetPlacementNeverOverlapsSnake()
    {
        var game = new SnakeGameState(new Random(10));

        for (var attempt = 0; attempt < 100; attempt++)
        {
            game.Reset();
            Assert.NotNull(game.Target);
            Assert.DoesNotContain(game.Target!.Value, game.Snake);
        }
    }
}
