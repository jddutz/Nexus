namespace Nexus.Graphics;

using System.Collections.ObjectModel;

/// <summary>Describes the texture region and tint used to render one occupied tile-map cell.</summary>
public readonly record struct TileMapCell
{
    /// <summary>Gets the normalized texture region sampled for this cell.</summary>
    public Vector4D<float> TextureRegion { get; }

    /// <summary>Gets the tint applied to this cell.</summary>
    public Color Color { get; }

    /// <summary>Creates a cell with a normalized texture region and tint.</summary>
    /// <param name="textureRegion">The normalized atlas region in (x, y, width, height) form.</param>
    /// <param name="color">The normalized color tint.</param>
    /// <exception cref="ArgumentOutOfRangeException">A region or color channel is invalid.</exception>
    public TileMapCell(Vector4D<float> textureRegion, Color color)
    {
        TextureRegion = textureRegion;
        Color = color;
        EnsureValid();
    }

    /// <summary>Rejects the invalid default value and validates normalized render inputs.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A region or color channel is invalid.</exception>
    internal void EnsureValid()
    {
        if (
            !float.IsFinite(TextureRegion.X)
            || !float.IsFinite(TextureRegion.Y)
            || !float.IsFinite(TextureRegion.Z)
            || !float.IsFinite(TextureRegion.W)
            || TextureRegion.X < 0f
            || TextureRegion.Y < 0f
            || TextureRegion.Z <= 0f
            || TextureRegion.W <= 0f
            || TextureRegion.X + TextureRegion.Z > 1f
            || TextureRegion.Y + TextureRegion.W > 1f
        )
            throw new ArgumentOutOfRangeException(nameof(TextureRegion));

        if (
            !float.IsFinite(Color.R)
            || !float.IsFinite(Color.G)
            || !float.IsFinite(Color.B)
            || !float.IsFinite(Color.A)
            || Color.R is < 0f or > 1f
            || Color.G is < 0f or > 1f
            || Color.B is < 0f or > 1f
            || Color.A is < 0f or > 1f
        )
            throw new ArgumentOutOfRangeException(nameof(Color));
    }
}

/// <summary>
/// Stores sparse tile contents inside finite, declared, half-open cell bounds.
/// </summary>
public sealed class TileMapData
{
    private readonly object _gate = new();
    private readonly Dictionary<Vector2D<int>, TileMapCell> _cells = [];
    private Rectangle<int> _cellBounds;
    private long _revision;

    /// <summary>Occurs once after a successful data mutation.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the revision number, advanced once for each effective mutation.</summary>
    public long Revision
    {
        get
        {
            lock (_gate)
                return _revision;
        }
    }

    /// <summary>Gets or sets the permitted half-open cell rectangle.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The bounds have a negative size.</exception>
    public Rectangle<int> CellBounds
    {
        get
        {
            lock (_gate)
                return _cellBounds;
        }
        set
        {
            ValidateBounds(value);
            var changed = false;
            lock (_gate)
            {
                if (_cellBounds == value)
                    return;

                _cellBounds = value;
                foreach (var coordinate in _cells.Keys.ToArray())
                    if (!Contains(value, coordinate.X, coordinate.Y))
                        _cells.Remove(coordinate);

                AdvanceRevision();
                changed = true;
            }

            if (changed)
                Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets a read-only copy of the occupied cell collection.</summary>
    public IReadOnlyDictionary<Vector2D<int>, TileMapCell> Cells => CaptureSnapshot().Cells;

    /// <summary>Creates an empty map with the specified finite bounds.</summary>
    /// <param name="cellBounds">The permitted half-open cell rectangle.</param>
    /// <exception cref="ArgumentOutOfRangeException">The bounds have a negative size.</exception>
    public TileMapData(Rectangle<int> cellBounds)
    {
        ValidateBounds(cellBounds);
        _cellBounds = cellBounds;
    }

    /// <summary>Inserts or replaces the contents of an in-bounds cell.</summary>
    /// <param name="column">The cell's horizontal coordinate.</param>
    /// <param name="row">The cell's vertical coordinate.</param>
    /// <param name="cell">The cell's texture region and tint.</param>
    /// <exception cref="ArgumentOutOfRangeException">The coordinate or cell render data is invalid.</exception>
    public void SetCell(int column, int row, TileMapCell cell)
    {
        cell.EnsureValid();
        lock (_gate)
        {
            if (!Contains(_cellBounds, column, row))
                throw new ArgumentOutOfRangeException(
                    nameof(column),
                    $"Cell ({column}, {row}) is outside the declared cell bounds."
                );

            var coordinate = new Vector2D<int>(column, row);
            if (_cells.TryGetValue(coordinate, out var existing) && existing == cell)
                return;

            _cells[coordinate] = cell;
            AdvanceRevision();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Removes an occupied cell, leaving empty cells and bounds unchanged.</summary>
    /// <param name="column">The cell's horizontal coordinate.</param>
    /// <param name="row">The cell's vertical coordinate.</param>
    /// <returns><see langword="true"/> if an occupied cell was removed.</returns>
    public bool RemoveCell(int column, int row)
    {
        lock (_gate)
        {
            if (!_cells.Remove(new Vector2D<int>(column, row)))
                return false;

            AdvanceRevision();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Removes every cell without changing the declared bounds.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            if (_cells.Count == 0)
                return;

            _cells.Clear();
            AdvanceRevision();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Captures bounds, revision, and contents as one coherent snapshot.</summary>
    /// <returns>An immutable view of the map's current state.</returns>
    internal TileMapDataSnapshot CaptureSnapshot()
    {
        lock (_gate)
        {
            var cells = new ReadOnlyDictionary<Vector2D<int>, TileMapCell>(
                new Dictionary<Vector2D<int>, TileMapCell>(_cells)
            );
            return new TileMapDataSnapshot(_cellBounds, _revision, cells);
        }
    }

    /// <summary>Checks whether a coordinate belongs to the half-open cell rectangle.</summary>
    /// <param name="bounds">The rectangle being checked.</param>
    /// <param name="column">The horizontal coordinate.</param>
    /// <param name="row">The vertical coordinate.</param>
    /// <returns><see langword="true"/> when the coordinate is permitted.</returns>
    private static bool Contains(Rectangle<int> bounds, int column, int row) =>
        column >= bounds.Origin.X
        && row >= bounds.Origin.Y
        && (long)column < (long)bounds.Origin.X + bounds.Size.X
        && (long)row < (long)bounds.Origin.Y + bounds.Size.Y;

    /// <summary>Validates that a bounds rectangle has nonnegative dimensions.</summary>
    /// <param name="bounds">The rectangle to validate.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is negative.</exception>
    private static void ValidateBounds(Rectangle<int> bounds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bounds.Size.X);
        ArgumentOutOfRangeException.ThrowIfNegative(bounds.Size.Y);
    }

    /// <summary>Advances the revision while the map lock is held.</summary>
    private void AdvanceRevision() => _revision = checked(_revision + 1);
}

/// <summary>Represents a thread-safe snapshot captured from tile-map data.</summary>
/// <param name="CellBounds">The declared cell bounds.</param>
/// <param name="Revision">The revision represented by the snapshot.</param>
/// <param name="Cells">The occupied cell contents.</param>
internal sealed record TileMapDataSnapshot(
    Rectangle<int> CellBounds,
    long Revision,
    IReadOnlyDictionary<Vector2D<int>, TileMapCell> Cells
);
