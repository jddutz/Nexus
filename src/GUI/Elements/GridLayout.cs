namespace Nexus.GUI.Elements;

/// <summary>
/// Allocates one rectangle per cell. Occupants interpret their own margins and alignment.
/// Assign occupants through SetCell; ordinary child additions have no cell placement.
/// </summary>
public class GridLayout : Element
{
    // TODO: establish a 1:1 lookup between the layout's children and their grid positions (Vector2D<uint>)
    private readonly List<GridSize> _rows = [];
    private readonly List<GridSize> _cols = [];
    private IElement?[,] _cells = new IElement?[0, 0];

    /// <summary>Gets or sets the row track definitions.</summary>
    public GridSize[] Rows
    {
        get => [.. _rows];
        set
        {
            _rows.Clear();
            _rows.AddRange(value);
            ResizeCells(RowCount, ColumnCount);
            InvalidateLayout();
        }
    }

    /// <summary>Gets or sets the column track definitions.</summary>
    public GridSize[] Columns
    {
        get => [.. _cols];
        set
        {
            _cols.Clear();
            _cols.AddRange(value);
            ResizeCells(RowCount, ColumnCount);
            InvalidateLayout();
        }
    }

    /// <summary>Gets the number of row tracks.</summary>
    public int RowCount => _rows.Count;

    /// <summary>Gets the number of column tracks.</summary>
    public int ColumnCount => _cols.Count;

    /// <summary>Gets the occupant at the specified cell, or <see langword="null"/> if it is empty.</summary>
    public IElement? GetCell(int row, int column)
    {
        ValidateCell(row, column);
        var element = _cells[row, column];
        // Direct removal or reparenting must not leave a live placement behind.
        if (element is not null && !ReferenceEquals(element.Parent, this))
            _cells[row, column] = element = null;
        return element;
    }

    /// <summary>Replaces an occupant. Pass <see langword="null"/> to clear the cell.</summary>
    public void SetCell(int row, int column, IElement? element)
    {
        ValidateCell(row, column);
        var previous = GetCell(row, column);
        if (ReferenceEquals(previous, element))
            return;

        if (element is not null)
        {
            for (ISceneNode? ancestor = this; ancestor is not null; ancestor = ancestor.Parent)
                if (ReferenceEquals(ancestor, element))
                    throw new ArgumentException(
                        "A grid cannot contain itself or an ancestor.",
                        nameof(element)
                    );
            if (element.Parent is not null)
                throw new ArgumentException(
                    "Detach the element from its current parent first.",
                    nameof(element)
                );
            AddChild(element);
        }

        _cells[row, column] = element;
        if (previous is not null)
            RemoveChild(previous);
        InvalidateLayout();
    }

    /// <inheritdoc/>
    public override Vector2D<float> Measure(Vector2D<float> constraint)
    {
        ValidateSize(constraint);
        ValidateExplicitSize();
        if (!IsEffectivelyVisible)
            return Vector2D<float>.Zero;

        var available = GetContentConstraint(constraint);
        var size = new Vector2D<float>(
            DesiredLength(_cols, available.X, Width),
            DesiredLength(_rows, available.Y, Height)
        );
        var columnTracks = new TrackAllocator(_cols, size.X);
        var rowTracks = new TrackAllocator(_rows, size.Y);
        for (var row = 0; row < RowCount; row++)
        {
            var rowSize = rowTracks.Next();
            var columns = columnTracks;
            for (var column = 0; column < ColumnCount; column++)
            {
                var columnSize = columns.Next();
                GetCell(row, column)?.Measure(new(columnSize, rowSize));
            }
        }
        // Track definitions, rather than child content, determine desired size.
        return IncludeMargins(size);
    }

    /// <inheritdoc/>
    public override void Arrange(Rectangle<float> bounds)
    {
        ValidateSize(bounds.Size);
        if (!float.IsFinite(bounds.Origin.X) || !float.IsFinite(bounds.Origin.Y))
            throw new ArgumentOutOfRangeException(nameof(bounds));
        ValidateExplicitSize();

        var available = GetContentBounds(bounds);
        var size = new Vector2D<float>(
            DesiredLength(_cols, available.Size.X, Width),
            DesiredLength(_rows, available.Size.Y, Height)
        );
        var destination = GetAlignedContentBounds(bounds, size);
        SetBounds(
            IsEffectivelyVisible
                ? destination
                : new Rectangle<float>(destination.Origin, Vector2D<float>.Zero)
        );

        var columnTracks = new TrackAllocator(_cols, IsEffectivelyVisible ? size.X : 0f);
        var rowTracks = new TrackAllocator(_rows, IsEffectivelyVisible ? size.Y : 0f);
        var y = destination.Origin.Y;
        for (var row = 0; row < RowCount; row++)
        {
            var rowSize = rowTracks.Next();
            var columns = columnTracks;
            var x = destination.Origin.X;
            for (var column = 0; column < ColumnCount; column++)
            {
                var columnSize = columns.Next();
                GetCell(row, column)?.Arrange(new Rectangle<float>(x, y, columnSize, rowSize));
                x += columnSize;
            }
            y += rowSize;
        }
    }

    /// <summary>Resizes the cell matrix while retaining occupants in cells that still exist.</summary>
    private void ResizeCells(int rowCount, int columnCount)
    {
        var replacement = new IElement?[rowCount, columnCount];
        var removed = new List<IElement>();
        for (var row = 0; row < _cells.GetLength(0); row++)
        for (var column = 0; column < _cells.GetLength(1); column++)
        {
            var element = _cells[row, column];
            if (element is not null && !ReferenceEquals(element.Parent, this))
                continue;
            if (row < rowCount && column < columnCount)
                replacement[row, column] = element;
            else if (element is not null)
                removed.Add(element);
        }
        _cells = replacement;
        foreach (var element in removed)
            RemoveChild(element);
    }

    /// <summary>Validates the specified row and column indexes.</summary>
    private void ValidateCell(int row, int column)
    {
        if ((uint)row >= (uint)RowCount)
            throw new ArgumentOutOfRangeException(nameof(row));
        if ((uint)column >= (uint)ColumnCount)
            throw new ArgumentOutOfRangeException(nameof(column));
    }

    /// <summary>Validates that a requested layout size is finite and non-negative.</summary>
    private static void ValidateSize(Vector2D<float> size)
    {
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X < 0f || size.Y < 0f)
            throw new ArgumentOutOfRangeException(
                nameof(size),
                "Grid allocation must be finite and non-negative."
            );
    }

    /// <summary>Validates any explicitly requested width and height.</summary>
    private void ValidateExplicitSize()
    {
        if (Width is { } width && (!float.IsFinite(width) || width < 0f))
            throw new InvalidOperationException("Width must be finite and non-negative.");
        if (Height is { } height && (!float.IsFinite(height) || height < 0f))
            throw new InvalidOperationException("Height must be finite and non-negative.");
    }

    /// <summary>Calculates the desired length from track definitions and an optional requested size.</summary>
    private static float DesiredLength(List<GridSize> tracks, float available, float? requested)
    {
        if (requested is { } value)
            return MathF.Min(value, available);
        double absolute = 0;
        foreach (var track in tracks)
        {
            if (track.Mode == GridSizeMode.Relative)
                return available;
            absolute += track.Value;
        }
        return (float)Math.Min(absolute, available);
    }

    /// <summary>Enumerates track lengths without allocating a result array.</summary>
    private struct TrackAllocator
    {
        private readonly List<GridSize> _tracks;
        private readonly double _available;
        private readonly double _scale;
        private readonly double _remaining;
        private readonly double _weights;
        private int _index;
        private double _consumed;

        /// <summary>Initializes an allocator for the provided tracks and available length.</summary>
        public TrackAllocator(List<GridSize> tracks, float available)
        {
            _tracks = tracks;
            _available = available;
            _index = 0;
            _consumed = 0;

            double absolute = 0;
            _weights = 0;
            foreach (var track in tracks)
                if (track.Mode == GridSizeMode.Absolute)
                    absolute += track.Value;
                else
                    _weights += track.Value;

            _scale = absolute > available ? available / absolute : 1d;
            _remaining = Math.Max(0d, available - absolute);
        }

        /// <summary>Gets the next track length, bounded by the unallocated remainder.</summary>
        public float Next()
        {
            var track = _tracks[_index++];
            var length =
                track.Mode == GridSizeMode.Absolute ? track.Value * _scale
                : _weights > 0 ? _remaining * track.Value / _weights
                : 0d;
            // Limit cumulative rounding so no track extends past the allocation.
            var result = (float)Math.Max(0d, Math.Min(length, _available - _consumed));
            _consumed += result;
            return result;
        }
    }
}
