namespace Nexus.Samples.Landfall.Tests;

public sealed class LandfallGameTests
{
    [Fact]
    public void BuildingHidesResourceWithoutRemovingItsAssignmentOrChangingTerrain()
    {
        var game = new LandfallGame(new MonominoRandom());
        var resource = new TileFeature("test-resource", new(1, 1));
        var building = new TileFeature("test-building", new(2, 1));
        game.AssignResource(9, 9, resource);
        Assert.True(game.IsResourceVisible(9, 9));
        var terrain = game[9, 9];
        var pieces = game.Pieces.ToArray();
        Assert.True(game.TryPlaceBuilding(9, 9, building));
        Assert.False(game.IsResourceVisible(9, 9));
        Assert.Same(resource, game.GetResource(9, 9));
        Assert.Same(building, game.GetBuilding(9, 9));
        Assert.Equal(terrain, game[9, 9]);
        for (var i = 0; i < 5; i++) Assert.Same(pieces[i], game.Pieces[i]);
        Assert.False(game.TryPlaceBuilding(9, 9, new("replacement", new(3, 1))));
        Assert.Same(building, game.GetBuilding(9, 9));
        game.AssignResource(9, 9, new("other-resource", new(4, 1)));
        Assert.False(game.IsResourceVisible(9, 9));
    }

    [Fact]
    public void ResourcesFollowFogAndTerrainPlacementPreservesAssignments()
    {
        var game = new LandfallGame(new MonominoRandom());
        var resource = new TileFeature("test-resource", new(1, 1));
        game.AssignResource(7, 9, resource);
        game.AssignResource(8, 9, resource);
        Assert.False(game.IsResourceVisible(7, 9));
        Assert.True(game.IsResourceVisible(8, 9));
        Assert.True(game.TryPlace(0, new(8, 9)));
        Assert.True(game.IsResourceVisible(7, 9));
        Assert.True(game.IsResourceVisible(8, 9));
        Assert.Same(resource, game.GetResource(8, 9));
        Assert.True(game.TryPlaceBuilding(8, 9, new("test-building", new(2, 1))));
        Assert.False(game.IsResourceVisible(8, 9));
        Assert.True(game.IsResourceVisible(7, 9));
        game.Restart();
        Assert.Null(game.GetResource(7, 9));
        Assert.Null(game.GetResource(8, 9));
        Assert.Null(game.GetBuilding(8, 9));
    }

    [Fact]
    public void BuildingPlacementRequiresAnInBoundsOccupiedCell()
    {
        var game = new LandfallGame();
        var building = new TileFeature("test-building", new(2, 1));
        var revision = game.LayerRevision;
        Assert.False(game.TryPlaceBuilding(-1, 0, building));
        Assert.False(game.TryPlaceBuilding(20, 0, building));
        Assert.False(game.TryPlaceBuilding(8, 9, building));
        Assert.Equal(revision, game.LayerRevision);
        Assert.Null(game.GetBuilding(8, 9));
        Assert.Throws<ArgumentOutOfRangeException>(() => game.AssignResource(20, 0, building));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TileFeature("invalid", new(8, 0)));
    }

    [Fact]
    public void FogRevealsImmediateNeighborsAndResetsOnRestart()
    {
        var game = new LandfallGame(new MonominoRandom());
        for (var y = 0; y < 20; y++)
        for (var x = 0; x < 20; x++)
            Assert.Equal(x is >= 8 and <= 11 && y is >= 8 and <= 11, game.IsRevealed(x, y));

        Assert.False(game.TryPlace(0, new(0, 0)));
        Assert.False(game.IsRevealed(0, 0));
        Assert.True(game.TryPlace(0, new(8, 9)));
        Assert.True(game.IsRevealed(7, 8)); // Diagonal from the new tile.
        Assert.True(game.IsRevealed(7, 9));
        Assert.True(game.IsRevealed(7, 10));
        Assert.False(game.IsRevealed(7, 11)); // Outside its immediate neighborhood.
        Assert.Null(game[7, 8]); // Revealing does not occupy a cell.
        for (var x = 7; x >= 0; x--) Assert.True(game.TryPlace(0, new(x, 9)));
        Assert.True(game.IsRevealed(0, 8));
        Assert.True(game.IsRevealed(0, 10));
        Assert.False(game.IsRevealed(-1, 9));
        Assert.False(game.IsRevealed(20, 9));
        game.Restart();
        Assert.False(game.IsRevealed(7, 8));
        Assert.False(game.IsRevealed(0, 9));
        Assert.True(game.IsRevealed(8, 8));
    }

    private sealed class MonominoRandom : Random
    {
        public override int Next(int maxValue) => 0;
    }

    [Fact]
    public void CatalogContainsExactlyThe21ConnectedFreePolyominoes()
    {
        Assert.Equal(new[] { 1, 1, 2, 5, 12 }, Shapes.BySize.Select(s => s.Count));
        var seen = new HashSet<string>();
        for (var size = 1; size <= 5; size++)
        foreach (var shape in Shapes.BySize[size - 1])
        {
            Assert.Equal(size, shape.Count);
            Assert.Equal(size, shape.Distinct().Count());
            var reached = new HashSet<Cell> { shape[0] };
            while (true)
            {
                var oldCount = reached.Count;
                foreach (var tile in shape)
                    if (reached.Any(c => Math.Abs(c.X - tile.X) + Math.Abs(c.Y - tile.Y) == 1))
                        reached.Add(tile);
                if (reached.Count == oldCount) break;
            }
            Assert.Equal(size, reached.Count);
            // Independent canonicalization also checks no mirrored duplicate slipped into the catalog.
            var variants = new List<string>();
            for (var mirror = 0; mirror < 2; mirror++)
            {
                var transformed = shape.Select(c => new Cell(c.X * (mirror == 0 ? 1 : -1), c.Y)).ToArray();
                for (var turn = 0; turn < 4; turn++)
                {
                    var minX = transformed.Min(c => c.X);
                    var minY = transformed.Min(c => c.Y);
                    variants.Add(string.Join(";", transformed.Select(c => new Cell(c.X - minX, c.Y - minY))
                        .OrderBy(c => c.X).ThenBy(c => c.Y)));
                    transformed = transformed.Select(c => new Cell(c.Y, -c.X)).ToArray();
                }
            }
            Assert.True(seen.Add(variants.Min(StringComparer.Ordinal)!));
        }
    }

    [Fact]
    public void NewGameHasOnlyFourCenterCellsAndFiveDefaultPieces()
    {
        var game = new LandfallGame(new Random(10));
        Assert.Equal(4, Occupied(game));
        foreach (var cell in new[] { new Cell(9, 9), new(10, 9), new(9, 10), new(10, 10) })
            Assert.Equal(Terrain.Forest, game[cell.X, cell.Y]);
        Assert.Equal(5, game.Pieces.Count);
        Assert.All(game.Pieces, p => Assert.Equal(0, p.Rotation));
        Assert.False(game.IsGameOver);
    }

    [Fact]
    public void RotationPreservesAnchorAndReturnsToOriginalAfterFourTurns()
    {
        var piece = new Piece([new(2, 1), new(3, 1), new(3, 2)], Terrain.Water);
        var original = piece.Tiles.ToArray();
        piece.Rotate(1);
        Assert.Equal(new[] { new Cell(0, 0), new(0, 1), new(-1, 1) }, piece.Tiles);
        piece.Rotate(-1);
        Assert.Equal(original, piece.Tiles);
        for (var i = 0; i < 4; i++) piece.Rotate(1);
        Assert.Equal(original, piece.Tiles);
    }

    [Fact]
    public void PlacementChecksEdgesOverlapAndOrthogonalContact()
    {
        var game = new LandfallGame(new Random(1));
        Cell[] single = [new(0, 0)];
        Assert.Equal(PlacementResult.OutsideBoard, game.Validate(single, new(-1, 0)));
        Assert.Equal(PlacementResult.OutsideBoard, game.Validate(single, new(20, 19)));
        Assert.Equal(PlacementResult.Overlap, game.Validate(single, new(9, 9)));
        Assert.Equal(PlacementResult.Disconnected, game.Validate(single, new(8, 8)));
        Assert.Equal(PlacementResult.Valid, game.Validate(single, new(8, 9)));
        Assert.Equal(PlacementResult.Valid, game.Validate([new(0, 0), new(1, 0)], new(7, 9)));
    }

    [Fact]
    public void InvalidPlacementPreservesPieceRotationAndBoard()
    {
        var game = new LandfallGame(new Random(2));
        var pieces = game.Pieces.ToArray();
        pieces[0].Rotate(1);
        Assert.False(game.TryPlace(0, new(9, 9)));
        Assert.Equal(4, Occupied(game));
        Assert.Equal(1, pieces[0].Rotation);
        for (var i = 0; i < 5; i++) Assert.Same(pieces[i], game.Pieces[i]);
    }

    [Fact]
    public void PlacementCommitsAllTilesAndReplacesOnlyItsOwnSlot()
    {
        var game = new LandfallGame(new Random(3));
        var pieces = game.Pieces.ToArray();
        var move = FindMove(game)!.Value;
        game.Pieces[move.Slot].Rotate(move.Rotation);
        var tiles = game.Pieces[move.Slot].Tiles.ToArray();
        Assert.True(game.TryPlace(move.Slot, move.Anchor));
        Assert.Equal(4 + tiles.Length, Occupied(game));
        foreach (var tile in tiles)
            Assert.Equal(pieces[move.Slot].Terrain, game[tile.X + move.Anchor.X, tile.Y + move.Anchor.Y]);
        for (var i = 0; i < 5; i++)
            if (i != move.Slot) Assert.Same(pieces[i], game.Pieces[i]);
        Assert.NotSame(pieces[move.Slot], game.Pieces[move.Slot]);
        Assert.Equal(0, game.Pieces[move.Slot].Rotation);
    }

    [Fact]
    public void GenerationUsesIndependentSizeShapeAndTerrainDraws()
    {
        var random = new ScriptedRandom();
        var game = new LandfallGame(random);
        Assert.Equal(Enumerable.Repeat(new[] { 5, 12, 4 }, 5).SelectMany(x => x), random.Bounds);
        Assert.All(game.Pieces, p => { Assert.Equal(5, p.Tiles.Count); Assert.Equal(Terrain.Water, p.Terrain); });
    }

    [Theory]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(197)]
    public void CompleteGamesAgreeWithIndependentExhaustiveSearch(int seed)
    {
        var game = new LandfallGame(new Random(seed));
        var moves = 0;
        while (FindMove(game) is { } move)
        {
            Assert.False(game.IsGameOver);
            game.Pieces[move.Slot].Rotate(move.Rotation - game.Pieces[move.Slot].Rotation);
            Assert.True(game.TryPlace(move.Slot, move.Anchor));
            Assert.True(++moves <= 396);
        }
        Assert.True(game.IsGameOver);
        Assert.False(game.HasLegalPlacement());
        var before = Occupied(game);
        Assert.False(game.TryPlace(0, new(0, 0)));
        Assert.Equal(before, Occupied(game));
        game.Restart();
        Assert.Equal(4, Occupied(game));
        Assert.False(game.IsGameOver);
    }

    // Uses raw board occupancy rather than the implementation's Validate/HasLegalPlacement.
    private static (int Slot, int Rotation, Cell Anchor)? FindMove(LandfallGame game)
    {
        for (var slot = 0; slot < 5; slot++)
        {
            var tiles = game.Pieces[slot].AtRotation(0);
            for (var rotation = 0; rotation < 4; rotation++)
            {
                for (var y = 0; y < 20; y++)
                for (var x = 0; x < 20; x++)
                {
                    var destinations = tiles.Select(c => new Cell(c.X + x, c.Y + y)).ToArray();
                    if (destinations.Any(c => c.X < 0 || c.Y < 0 || c.X >= 20 || c.Y >= 20)) continue;
                    if (destinations.Any(c => game[c.X, c.Y].HasValue)) continue;
                    if (destinations.Any(c =>
                        (c.X > 0 && game[c.X - 1, c.Y].HasValue) ||
                        (c.X < 19 && game[c.X + 1, c.Y].HasValue) ||
                        (c.Y > 0 && game[c.X, c.Y - 1].HasValue) ||
                        (c.Y < 19 && game[c.X, c.Y + 1].HasValue))) return (slot, rotation, new(x, y));
                }
                tiles = tiles.Select(c => new Cell(-c.Y, c.X)).ToArray();
            }
        }
        return null;
    }

    private static int Occupied(LandfallGame game) => Enumerable.Range(0, 400)
        .Count(i => game[i % 20, i / 20].HasValue);

    private sealed class ScriptedRandom : Random
    {
        public List<int> Bounds { get; } = [];
        public override int Next(int maxValue) { Bounds.Add(maxValue); return maxValue - 1; }
    }
}
