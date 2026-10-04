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

    /// <summary>Gets or sets the occupant at the specified cell.</summary>
    /// <param name="row">The zero-based row index.</param>
    /// <param name="column">The zero-based column index.</param>
    /// <returns>The occupant at the cell, or <see langword="null"/> when it is empty.</returns>
    public IElement? this[int row, int column]
    {
        get => GetCell(row, column);
        set => SetCell(row, column, value);
    }

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

        var available = new Vector2D<float>(
            MathF.Max(0f, constraint.X - Margins.Left - Margins.Right),
            MathF.Max(0f, constraint.Y - Margins.Top - Margins.Bottom)
        );
        var widthConstraint = MathF.Min(Width ?? available.X, available.X);
        var heightConstraint = MathF.Min(Height ?? available.Y, available.Y);
        var autoColumns = MeasureAutoColumns(widthConstraint, heightConstraint);
        var width = DesiredLength(_cols, available.X, Width, autoColumns);
        var columnLengths = AllocateTracks(_cols, width, autoColumns);
        var autoRows = MeasureAutoRows(heightConstraint, width, columnLengths);
        var size = new Vector2D<float>(
            width,
            DesiredLength(_rows, available.Y, Height, autoRows)
        );
        // Relative tracks fill the constraint; absolute and auto tracks determine intrinsic size.
        return size + Margins;
    }

    /// <inheritdoc/>
    public override void Arrange(Rectangle<float> bounds)
    {
        ValidateSize(bounds.Size);
        if (!float.IsFinite(bounds.Origin.X) || !float.IsFinite(bounds.Origin.Y))
            throw new ArgumentOutOfRangeException(nameof(bounds));
        ValidateExplicitSize();

        var available = bounds - Margins;
        var widthConstraint = MathF.Min(Width ?? available.Size.X, available.Size.X);
        var heightConstraint = MathF.Min(Height ?? available.Size.Y, available.Size.Y);
        var autoColumns = IsEffectivelyVisible
            ? MeasureAutoColumns(widthConstraint, heightConstraint)
            : new float[ColumnCount];
        var width = DesiredLength(_cols, available.Size.X, Width, autoColumns);
        var columnLengths = AllocateTracks(
            _cols,
            IsEffectivelyVisible ? width : 0f,
            autoColumns
        );
        var autoRows = IsEffectivelyVisible
            ? MeasureAutoRows(heightConstraint, width, columnLengths)
            : new float[RowCount];
        var size = new Vector2D<float>(
            width,
            DesiredLength(_rows, available.Size.Y, Height, autoRows)
        );
        var destination = GetAlignedContentBounds(bounds, size);
        SetBounds(
            IsEffectivelyVisible
                ? destination
                : new Rectangle<float>(destination.Origin, Vector2D<float>.Zero)
        );

        var columnTracks = new TrackAllocator(
            _cols,
            IsEffectivelyVisible ? size.X : 0f,
            autoColumns
        );
        var rowTracks = new TrackAllocator(_rows, IsEffectivelyVisible ? size.Y : 0f, autoRows);
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

    /// <summary>Measures occupants in auto columns to determine their content widths.</summary>
    /// <param name="availableWidth">The width available after reserving absolute tracks.</param>
    /// <param name="availableHeight">The height constraint supplied to occupants.</param>
    /// <returns>The measured outer width for each column track.</returns>
    private float[] MeasureAutoColumns(float availableWidth, float availableHeight)
    {
        var sizes = new float[ColumnCount];
        var occupantWidth = (float)Math.Max(0d, availableWidth - AbsoluteLength(_cols));
        for (var column = 0; column < ColumnCount; column++)
        {
            if (_cols[column].Mode != GridSizeMode.Auto)
                continue;
            for (var row = 0; row < RowCount; row++)
            {
                var occupant = GetCell(row, column);
                if (occupant is null)
                    continue;
                var measured = occupant.Measure(new(occupantWidth, availableHeight));
                ValidateMeasuredSize(measured);
                sizes[column] = MathF.Max(sizes[column], measured.X);
            }
        }
        return sizes;
    }

    /// <summary>Measures auto rows after resolving column widths, allowing wrapped content to settle.</summary>
    /// <param name="availableHeight">The height available to auto rows after absolute tracks.</param>
    /// <param name="availableWidth">The resolved grid width.</param>
    /// <param name="columnLengths">The allocated width of each column.</param>
    /// <returns>The measured outer height for each row track.</returns>
    private float[] MeasureAutoRows(
        float availableHeight,
        float availableWidth,
        IReadOnlyList<float> columnLengths
    )
    {
        var sizes = new float[RowCount];
        var occupantHeight = (float)Math.Max(0d, availableHeight - AbsoluteLength(_rows));
        var columns = new TrackAllocator(_cols, availableWidth, columnLengths);
        for (var row = 0; row < RowCount; row++)
        {
            if (_rows[row].Mode != GridSizeMode.Auto)
                continue;
            var rowColumns = columns;
            for (var column = 0; column < ColumnCount; column++)
            {
                var occupant = GetCell(row, column);
                var columnLength = rowColumns.Next();
                if (occupant is null)
                    continue;
                var measured = occupant.Measure(new(columnLength, occupantHeight));
                ValidateMeasuredSize(measured);
                sizes[row] = MathF.Max(sizes[row], measured.Y);
            }
        }
        return sizes;
    }

    /// <summary>Calculates the desired length from track definitions and an optional requested size.</summary>
    /// <param name="tracks">The track definitions.</param>
    /// <param name="available">The maximum length available.</param>
    /// <param name="requested">An optional explicit length.</param>
    /// <param name="autoSizes">Measured content lengths for auto tracks.</param>
    /// <returns>The desired length, capped by the available space.</returns>
    private static float DesiredLength(
        List<GridSize> tracks,
        float available,
        float? requested,
        IReadOnlyList<float> autoSizes
    )
    {
        if (requested is { } value)
            return MathF.Min(value, available);
        if (tracks.Any(track => track.Mode == GridSizeMode.Relative))
            return available;

        double desired = 0;
        for (var index = 0; index < tracks.Count; index++)
            desired += tracks[index].Mode switch
            {
                GridSizeMode.Absolute => tracks[index].Value,
                GridSizeMode.Auto => autoSizes[index],
                _ => 0d,
            };
        return (float)Math.Min(desired, available);
    }

    /// <summary>Allocates resolved lengths for the supplied tracks.</summary>
    /// <param name="tracks">The track definitions.</param>
    /// <param name="available">The available length.</param>
    /// <param name="autoSizes">Measured content lengths for auto tracks.</param>
    /// <returns>One allocated length per track.</returns>
    private static float[] AllocateTracks(
        List<GridSize> tracks,
        float available,
        IReadOnlyList<float> autoSizes
    )
    {
        var lengths = new float[tracks.Count];
        var allocator = new TrackAllocator(tracks, available, autoSizes);
        for (var index = 0; index < lengths.Length; index++)
            lengths[index] = allocator.Next();
        return lengths;
    }

    /// <summary>Returns the combined length reserved by absolute tracks.</summary>
    /// <param name="tracks">The track definitions.</param>
    /// <returns>The sum of absolute track lengths.</returns>
    private static double AbsoluteLength(List<GridSize> tracks)
    {
        double length = 0;
        foreach (var track in tracks)
            if (track.Mode == GridSizeMode.Absolute)
                length += track.Value;
        return length;
    }

    /// <summary>Rejects invalid sizes returned by an occupant's measurement implementation.</summary>
    /// <param name="size">The measured occupant size.</param>
    private static void ValidateMeasuredSize(Vector2D<float> size)
    {
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X < 0f || size.Y < 0f)
            throw new InvalidOperationException("An occupant returned an invalid measured size.");
    }

    /// <summary>Enumerates track lengths without allocating a result array.</summary>
    private struct TrackAllocator
    {
        private readonly List<GridSize> _tracks;
        private readonly double _available;
        private readonly double _absoluteScale;
        private readonly IReadOnlyList<float> _autoSizes;
        private readonly double _autoScale;
        private readonly double _remaining;
        private readonly double _weights;
        private int _index;
        private double _consumed;

        /// <summary>Initializes an allocator for the provided tracks and available length.</summary>
        public TrackAllocator(
            List<GridSize> tracks,
            float available,
            IReadOnlyList<float> autoSizes
        )
        {
            _tracks = tracks;
            _available = available;
            _autoSizes = autoSizes;
            _index = 0;
            _consumed = 0;

            double absolute = 0;
            double auto = 0;
            _weights = 0;
            for (var index = 0; index < tracks.Count; index++)
            {
                var track = tracks[index];
                if (track.Mode == GridSizeMode.Absolute)
                    absolute += track.Value;
                else if (track.Mode == GridSizeMode.Auto)
                    auto += autoSizes[index];
                else
                    _weights += track.Value;
            }

            _absoluteScale = absolute > available ? available / absolute : 1d;
            var remainingAfterAbsolute = Math.Max(0d, available - absolute * _absoluteScale);
            _autoScale = auto > remainingAfterAbsolute ? remainingAfterAbsolute / auto : 1d;
            _remaining = Math.Max(0d, remainingAfterAbsolute - auto * _autoScale);
        }

        /// <summary>Gets the next track length, bounded by the unallocated remainder.</summary>
        public float Next()
        {
            var track = _tracks[_index++];
            var length =
                track.Mode == GridSizeMode.Absolute ? track.Value * _absoluteScale
                : track.Mode == GridSizeMode.Auto ? _autoSizes[_index - 1] * _autoScale
                : _weights > 0 ? _remaining * track.Value / _weights
                : 0d;
            // Limit cumulative rounding so no track extends past the allocation.
            var result = (float)Math.Max(0d, Math.Min(length, _available - _consumed));
            _consumed += result;
            return result;
        }
    }
}
