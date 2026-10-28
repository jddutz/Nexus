namespace Nexus.Samples.Landfall;

public readonly record struct Cell(int X, int Y)
{
    public static Cell operator +(Cell a, Cell b) => new(a.X + b.X, a.Y + b.Y);
}

public enum Terrain { Grassland, Forest, Mountain, Water }
public enum PlacementResult { Valid, OutsideBoard, Overlap, Disconnected }
public enum BuildingPlacementResult { Valid, OutsideBoard, NeedsTerrain, Overlap }

/// <summary>A rectangular building footprint displaying one atlas tile across its whole area.</summary>
public sealed record BuildingDefinition
{
    public TileFeature Feature { get; }
    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public bool PlayerPlaceable { get; }

    public BuildingDefinition(TileFeature feature, string name, int width, int height, bool playerPlaceable = true)
    {
        ArgumentNullException.ThrowIfNull(feature);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (width is < 1 or > LandfallGame.BoardSize) throw new ArgumentOutOfRangeException(nameof(width));
        if (height is < 1 or > LandfallGame.BoardSize) throw new ArgumentOutOfRangeException(nameof(height));
        Feature = feature;
        Name = name;
        Width = width;
        Height = height;
        PlayerPlaceable = playerPlaceable;
    }
}

public sealed record BuildingPlacement(BuildingDefinition Definition, Cell Anchor);

public static class Buildings
{
    public static BuildingDefinition Settlement { get; } = new(new("settlement", new(0, 0)), "Settlement", 2, 2,
        playerPlaceable: false);
    public static BuildingDefinition Farm { get; } = new(TileFeature.FromRegion("farm", "farm"), "Farm", 1, 1);
    public static IReadOnlyList<BuildingDefinition> Available { get; } = Array.AsReadOnly(new[] { Farm });
}

/// <summary>A resource or building type and its tile in its layer's 8×8 atlas.</summary>
public sealed record TileFeature
{
    public string TypeId { get; }
    public Cell AtlasTile { get; }
    public string? RegionName { get; private init; }

    /// <summary>References named artwork from the generated manifest instead of duplicating coordinates.</summary>
    public static TileFeature FromRegion(string typeId, string regionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionName);
        return new(typeId, new(0, 0)) { RegionName = regionName };
    }

    public TileFeature(string typeId, Cell atlasTile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
        if (atlasTile.X is < 0 or >= 8 || atlasTile.Y is < 0 or >= 8)
            throw new ArgumentOutOfRangeException(nameof(atlasTile));
        TypeId = typeId;
        AtlasTile = atlasTile;
    }
}

/// <summary>A terrain footprint whose first tile stays anchored through rotation.</summary>
public sealed class Piece
{
    private readonly Cell[] _shape;
    private Cell[] _rotated;
    public Terrain Terrain { get; }
    public int Rotation { get; private set; }
    public IReadOnlyList<Cell> Tiles => Array.AsReadOnly(_rotated);

    public Piece(IEnumerable<Cell> shape, Terrain terrain)
    {
        _shape = shape.ToArray();
        if (_shape.Length is < 1 or > 5 || _shape.Distinct().Count() != _shape.Length)
            throw new ArgumentException("A piece needs one to five distinct tiles.", nameof(shape));
        var anchor = _shape[0];
        _shape = _shape.Select(c => new Cell(c.X - anchor.X, c.Y - anchor.Y)).ToArray();
        _rotated = _shape.ToArray();
        Terrain = terrain;
    }

    public void Rotate(int quarterTurns)
    {
        Rotation = ((Rotation + quarterTurns) % 4 + 4) % 4;
        _rotated = AtRotation(Rotation);
    }

    public Cell[] AtRotation(int rotation) => _shape.Select(c => ((rotation % 4 + 4) % 4) switch
    {
        1 => new Cell(-c.Y, c.X),
        2 => new Cell(-c.X, -c.Y),
        3 => new Cell(c.Y, -c.X),
        _ => c,
    }).ToArray();
}

/// <summary>Builds all free polyominoes; reflection is used for catalog deduplication only.</summary>
public static class Shapes
{
    private static readonly Cell[] Neighbors = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];
    public static IReadOnlyList<IReadOnlyList<IReadOnlyList<Cell>>> BySize { get; } = Build();

    private static IReadOnlyList<IReadOnlyList<IReadOnlyList<Cell>>> Build()
    {
        var result = new List<IReadOnlyList<IReadOnlyList<Cell>>>();
        Cell[][] previous = [[new Cell(0, 0)]];
        for (var size = 1; size <= 5; size++)
        {
            if (size > 1)
            {
                var next = new SortedDictionary<string, Cell[]>(StringComparer.Ordinal);
                foreach (var shape in previous)
                foreach (var tile in shape)
                foreach (var direction in Neighbors)
                {
                    var candidate = tile + direction;
                    if (shape.Contains(candidate)) continue;
                    var canonical = Canonical(shape.Append(candidate).ToArray());
                    next.TryAdd(Key(canonical), canonical);
                }
                previous = next.Values.ToArray();
            }
            result.Add(Array.AsReadOnly(previous.Select(s => (IReadOnlyList<Cell>)Array.AsReadOnly(s)).ToArray()));
        }
        return result.AsReadOnly();
    }

    private static Cell[] Canonical(Cell[] shape)
    {
        var variants = new List<Cell[]>();
        for (var mirror = 0; mirror < 2; mirror++)
        for (var rotation = 0; rotation < 4; rotation++)
        {
            var transformed = shape.Select(c => new Cell(mirror == 0 ? c.X : -c.X, c.Y)).ToArray();
            for (var turn = 0; turn < rotation; turn++)
                transformed = transformed.Select(c => new Cell(-c.Y, c.X)).ToArray();
            var minX = transformed.Min(c => c.X);
            var minY = transformed.Min(c => c.Y);
            variants.Add(transformed.Select(c => new Cell(c.X - minX, c.Y - minY))
                .OrderBy(c => c.Y).ThenBy(c => c.X).ToArray());
        }
        return variants.OrderBy(Key, StringComparer.Ordinal).First();
    }

    private static string Key(Cell[] shape) => string.Join(";", shape.Select(c => $"{c.X},{c.Y}"));
}

/// <summary>Owns the board and five persistent slots independently of rendering and input.</summary>
public sealed class LandfallGame
{
    public const int BoardSize = 20;
    public const int SlotCount = 5;
    private readonly Random _random;
    private readonly Terrain?[,] _board = new Terrain?[BoardSize, BoardSize];
    private readonly TileFeature?[,] _resources = new TileFeature?[BoardSize, BoardSize];
    private readonly BuildingPlacement?[,] _buildings = new BuildingPlacement?[BoardSize, BoardSize];
    public long LayerRevision { get; private set; }
    private readonly Piece[] _pieces = new Piece[SlotCount];
    public IReadOnlyList<Piece> Pieces => Array.AsReadOnly(_pieces);
    public bool IsGameOver { get; private set; }
    public Terrain? this[int x, int y] => _board[x, y];
    private static readonly Cell[] Neighbors = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];

    public LandfallGame(Random? random = null)
    {
        _random = random ?? new Random();
        Restart();
    }

    public void Restart()
    {
        Array.Clear(_board);
        Array.Clear(_resources);
        Array.Clear(_buildings);
        LayerRevision++;
        for (var y = 9; y <= 10; y++)
        for (var x = 9; x <= 10; x++)
            _board[x, y] = Terrain.Forest; // Atlas tile (0,0).
        for (var slot = 0; slot < SlotCount; slot++) _pieces[slot] = Generate();
        IsGameOver = !HasLegalPlacement();
    }

    private Piece Generate()
    {
        var shapes = Shapes.BySize[_random.Next(5)];
        var shape = shapes[_random.Next(shapes.Count)];
        var terrain = (Terrain)_random.Next(4);
        return new Piece(shape, terrain);
    }

    public static bool InBounds(Cell cell) => cell.X is >= 0 and < BoardSize && cell.Y is >= 0 and < BoardSize;

    public TileFeature? GetResource(int x, int y) => _resources[x, y];
    public TileFeature? GetBuilding(int x, int y) => _buildings[x, y]?.Definition.Feature;
    public BuildingPlacement? GetBuildingPlacement(int x, int y) => _buildings[x, y];

    /// <summary>Assigns a resource independently of terrain, allowing future map generation under fog.</summary>
    public void AssignResource(int x, int y, TileFeature resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (!InBounds(new(x, y))) throw new ArgumentOutOfRangeException(nameof(x));
        if (_resources[x, y] == resource) return;
        _resources[x, y] = resource;
        LayerRevision++;
    }

    /// <summary>Places one building on occupied terrain without consuming the cell's resource.</summary>
    public bool TryPlaceBuilding(int x, int y, TileFeature building)
    {
        ArgumentNullException.ThrowIfNull(building);
        return TryPlaceBuilding(new(building, building.TypeId, 1, 1), new(x, y));
    }

    public BuildingPlacementResult ValidateBuilding(BuildingDefinition building, Cell anchor)
    {
        ArgumentNullException.ThrowIfNull(building);
        if (anchor.X < 0 || anchor.Y < 0 || anchor.X > BoardSize - building.Width
            || anchor.Y > BoardSize - building.Height) return BuildingPlacementResult.OutsideBoard;
        for (var y = 0; y < building.Height; y++)
        for (var x = 0; x < building.Width; x++)
            if (_buildings[anchor.X + x, anchor.Y + y] is not null) return BuildingPlacementResult.Overlap;
        for (var y = 0; y < building.Height; y++)
        for (var x = 0; x < building.Width; x++)
            if (!_board[anchor.X + x, anchor.Y + y].HasValue) return BuildingPlacementResult.NeedsTerrain;
        return BuildingPlacementResult.Valid;
    }

    /// <summary>Commits the entire footprint atomically, retaining resources underneath every cell.</summary>
    public bool TryPlaceBuilding(BuildingDefinition building, Cell anchor)
    {
        if (ValidateBuilding(building, anchor) != BuildingPlacementResult.Valid) return false;
        var placement = new BuildingPlacement(building, anchor);
        for (var y = 0; y < building.Height; y++)
        for (var x = 0; x < building.Width; x++)
            _buildings[anchor.X + x, anchor.Y + y] = placement;
        LayerRevision++;
        return true;
    }

    public bool IsResourceVisible(int x, int y) => InBounds(new(x, y))
        && IsRevealed(x, y) && _resources[x, y] is not null && _buildings[x, y] is null;

    /// <summary>Reveals occupied cells and their eight immediate neighbors, including diagonals.</summary>
    /// <remarks>Terrain is permanent, so visibility persists until the board is restarted.</remarks>
    public bool IsRevealed(int x, int y)
    {
        if (!InBounds(new(x, y))) return false;
        for (var nearbyY = Math.Max(0, y - 1); nearbyY <= Math.Min(BoardSize - 1, y + 1); nearbyY++)
        for (var nearbyX = Math.Max(0, x - 1); nearbyX <= Math.Min(BoardSize - 1, x + 1); nearbyX++)
            if (_board[nearbyX, nearbyY].HasValue) return true;
        return false;
    }

    public PlacementResult Validate(IReadOnlyList<Cell> tiles, Cell anchor)
    {
        foreach (var tile in tiles)
            if (!InBounds(tile + anchor)) return PlacementResult.OutsideBoard;
        foreach (var tile in tiles)
            if (_board[tile.X + anchor.X, tile.Y + anchor.Y].HasValue) return PlacementResult.Overlap;
        foreach (var tile in tiles)
        foreach (var direction in Neighbors)
        {
            var adjacent = tile + anchor + direction;
            if (InBounds(adjacent) && _board[adjacent.X, adjacent.Y].HasValue)
                return PlacementResult.Valid;
        }
        return PlacementResult.Disconnected;
    }

    public bool TryPlace(int slot, Cell anchor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, SlotCount);
        var piece = _pieces[slot];
        if (IsGameOver || Validate(piece.Tiles, anchor) != PlacementResult.Valid) return false;
        foreach (var tile in piece.Tiles)
            _board[tile.X + anchor.X, tile.Y + anchor.Y] = piece.Terrain;
        _pieces[slot] = Generate();
        IsGameOver = !HasLegalPlacement();
        return true;
    }

    public bool HasLegalPlacement()
    {
        foreach (var piece in _pieces)
        for (var rotation = 0; rotation < 4; rotation++)
        {
            var tiles = piece.AtRotation(rotation);
            for (var y = 0; y < BoardSize; y++)
            for (var x = 0; x < BoardSize; x++)
                if (Validate(tiles, new Cell(x, y)) == PlacementResult.Valid) return true;
        }
        return false;
    }
}
