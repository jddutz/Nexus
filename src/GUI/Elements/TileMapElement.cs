namespace Nexus.GUI.Elements;

using Nexus.Graphics.Shaders;

/// <summary>Measures, arranges, and renders a tile map as a screen-space element.</summary>
public sealed class TileMapElement : Element
{
    private readonly GameObject2D _renderObject;
    private readonly TileMapRenderer _renderer;
    private readonly List<IObservable> _visibilityAncestors = [];
    private TileMapData? _map;
    private Vector2D<float> _cellSize = new(1f, 1f);
    private Rectangle<float> _contentAllocation;

    /// <summary>Gets or sets the tile data rendered by this element.</summary>
    public TileMapData? Map
    {
        get => _map;
        set
        {
            if (ReferenceEquals(_map, value))
                return;

            if (_map is not null)
                _map.Changed -= OnMapChanged;
            _map = value;
            _renderer.Map = value;
            if (_map is not null)
                _map.Changed += OnMapChanged;

            NotifyPropertyChanged(nameof(Map));
            InvalidateLayout();
            SynchronizeRenderer();
        }
    }

    /// <summary>Gets or sets the nominal width and height of a tile before layout fitting.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is non-finite or not positive.</exception>
    public Vector2D<float> CellSize
    {
        get => _cellSize;
        set
        {
            ValidateCellSize(value, nameof(value));
            if (_cellSize == value)
                return;

            _cellSize = value;
            NotifyPropertyChanged(nameof(CellSize));
            InvalidateLayout();
            SynchronizeRenderer();
        }
    }

    /// <summary>Gets or sets the texture sampled by the occupied cells.</summary>
    public ITexture Texture
    {
        get => _renderer.Texture;
        set => _renderer.Texture = value;
    }

    /// <summary>Gets or sets the sampling behavior used by the tile texture.</summary>
    public ISamplingBehavior SamplingBehavior
    {
        get => _renderer.SamplingBehavior;
        set => _renderer.SamplingBehavior = value;
    }

    /// <summary>Gets or sets the render-layer mask shared by the tiles.</summary>
    public ulong RenderLayerMask
    {
        get => _renderer.RenderLayerMask;
        set => _renderer.RenderLayerMask = value;
    }

    /// <summary>Gets or sets the vertex shader used to render the tiles.</summary>
    public VertexShader? VertexShader
    {
        get => _renderer.VertexShader;
        set => _renderer.VertexShader = value;
    }

    /// <summary>Gets or sets the fragment shader used to render the tiles.</summary>
    public FragmentShader? FragmentShader
    {
        get => _renderer.FragmentShader;
        set => _renderer.FragmentShader = value;
    }

    /// <summary>Initializes an empty element with an owned tile-map renderer.</summary>
    public TileMapElement()
    {
        _renderer = new TileMapRenderer { RenderLayerMask = RenderLayers.DefaultUI };
        _renderObject = new GameObject2D([_renderer]);
        AddChild(_renderObject);
        PropertyChanged += OnElementPropertyChanged;
        SynchronizeRenderer();
    }

    /// <inheritdoc />
    public override Vector2D<float> Measure(Vector2D<float> constraint)
    {
        ValidateAvailableSize(constraint, nameof(constraint));
        if (!IsEffectivelyVisible || _map is null || !TryGetDesiredMapSize(out var desiredSize))
            return Vector2D<float>.Zero;

        var availableSize = new Vector2D<float>(
            MathF.Max(0f, constraint.X - Margins.Left - Margins.Right),
            MathF.Max(0f, constraint.Y - Margins.Top - Margins.Bottom)
        );
        availableSize = new(
            MathF.Min(availableSize.X, Width ?? availableSize.X),
            MathF.Min(availableSize.Y, Height ?? availableSize.Y)
        );
        var scale = GetFitScale(desiredSize, availableSize);

        return new Vector2D<float>(desiredSize.X * scale, desiredSize.Y * scale) + Margins;
    }

    /// <inheritdoc />
    public override void Arrange(Rectangle<float> bounds)
    {
        ValidateBounds(bounds);
        base.Arrange(bounds);
        _contentAllocation = Bounds;
        SynchronizeRenderer();
    }

    /// <inheritdoc />
    public override void OnSceneHierarchyChanged()
    {
        base.OnSceneHierarchyChanged();
        UpdateVisibilityAncestorSubscriptions();
        SynchronizeRenderer();
    }

    /// <summary>Updates renderer visibility after this element's visibility changes.</summary>
    protected override void AfterIsVisibleChanges()
    {
        base.AfterIsVisibleChanges();
        SynchronizeRenderer();
    }

    /// <summary>Invalidates layout and refreshes the render transform after tile data changes.</summary>
    /// <param name="sender">The changed tile map.</param>
    /// <param name="eventArgs">The change event data.</param>
    private void OnMapChanged(object? sender, EventArgs eventArgs)
    {
        InvalidateLayout();
        SynchronizeRenderer();
    }

    /// <summary>Refreshes rendering when sort order changes.</summary>
    /// <param name="propertyName">The changed property name.</param>
    private void OnElementPropertyChanged(string propertyName)
    {
        if (propertyName is "" or nameof(SortOrder))
            SynchronizeRenderer();
    }

    /// <summary>Refreshes renderer visibility when an ancestor's visibility changes.</summary>
    /// <param name="propertyName">The changed property name.</param>
    private void OnAncestorPropertyChanged(string propertyName)
    {
        if (propertyName is "" or nameof(IsVisible))
            SynchronizeRenderer();
    }

    /// <summary>Subscribes to visibility changes throughout the current ancestor chain.</summary>
    private void UpdateVisibilityAncestorSubscriptions()
    {
        foreach (var ancestor in _visibilityAncestors)
            ancestor.PropertyChanged -= OnAncestorPropertyChanged;
        _visibilityAncestors.Clear();

        for (ISceneNode? ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor is not IObservable observable)
                continue;

            observable.PropertyChanged += OnAncestorPropertyChanged;
            _visibilityAncestors.Add(observable);
        }
    }

    /// <summary>Synchronizes layout geometry, spatial transform, and visibility with the renderer.</summary>
    private void SynchronizeRenderer()
    {
        _renderer.DrawOrder = SortOrder;
        if (
            !IsEffectivelyVisible
            || _map is null
            || !TryGetDesiredMapSize(out var desiredSize)
            || _contentAllocation.Size.X <= 0f
            || _contentAllocation.Size.Y <= 0f
        )
        {
            _renderer.IsVisible = false;
            SetBounds(new Rectangle<float>(_contentAllocation.Origin, Vector2D<float>.Zero));
            return;
        }

        var scale = GetFitScale(desiredSize, _contentAllocation.Size);
        if (scale <= 0f)
        {
            _renderer.IsVisible = false;
            SetBounds(new Rectangle<float>(_contentAllocation.Origin, Vector2D<float>.Zero));
            return;
        }

        var actualCellSize = _cellSize * scale;
        _renderer.CellSize = actualCellSize;
        var displayedSize = desiredSize * scale;
        var destination = AlignContentBounds(_contentAllocation, displayedSize);
        var cellBounds = _map.CellBounds;
        var origin = new Vector2D<float>(
            destination.Origin.X - cellBounds.Origin.X * actualCellSize.X,
            destination.Origin.Y - cellBounds.Origin.Y * actualCellSize.Y
        );
        if (!float.IsFinite(origin.X) || !float.IsFinite(origin.Y))
            throw new InvalidOperationException("The tile-map origin is non-finite.");

        _renderObject.Position = origin;
        SetBounds(destination);
        _renderer.IsVisible = destination.Size.X > 0f && destination.Size.Y > 0f;
    }

    /// <summary>Aligns a fitted map within the content allocation without reapplying margins.</summary>
    /// <param name="bounds">The element's content allocation.</param>
    /// <param name="contentSize">The fitted map dimensions.</param>
    /// <returns>The aligned content rectangle.</returns>
    private Rectangle<float> AlignContentBounds(
        Rectangle<float> bounds,
        Vector2D<float> contentSize
    )
    {
        var horizontalOffset = HorizontalAlignment switch
        {
            AlignHorizontal.Left => 0f,
            AlignHorizontal.Center => (bounds.Size.X - contentSize.X) / 2f,
            AlignHorizontal.Right => bounds.Size.X - contentSize.X,
            _ => throw new InvalidOperationException("The horizontal alignment is invalid."),
        };
        var verticalOffset = VerticalAlignment switch
        {
            AlignVertical.Top => 0f,
            AlignVertical.Center => (bounds.Size.Y - contentSize.Y) / 2f,
            AlignVertical.Bottom => bounds.Size.Y - contentSize.Y,
            _ => throw new InvalidOperationException("The vertical alignment is invalid."),
        };
        return new Rectangle<float>(
            bounds.Origin.X + horizontalOffset,
            bounds.Origin.Y + verticalOffset,
            contentSize.X,
            contentSize.Y
        );
    }

    /// <summary>Gets the map's nominal render size and reports whether it has positive dimensions.</summary>
    /// <param name="size">The nominal map size in element units.</param>
    /// <returns>True when the map has non-zero bounds and finite dimensions.</returns>
    private bool TryGetDesiredMapSize(out Vector2D<float> size)
    {
        var cellBounds = _map!.CellBounds;
        size = new Vector2D<float>(
            cellBounds.Size.X * _cellSize.X,
            cellBounds.Size.Y * _cellSize.Y
        );
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y))
            throw new InvalidOperationException("The tile-map size is non-finite.");

        return size.X > 0f && size.Y > 0f;
    }

    /// <summary>Calculates a uniform scale that fits a map within an available size.</summary>
    /// <param name="desiredSize">The map's nominal dimensions.</param>
    /// <param name="availableSize">The maximum content dimensions.</param>
    /// <returns>A scale from zero through one.</returns>
    private static float GetFitScale(Vector2D<float> desiredSize, Vector2D<float> availableSize)
    {
        if (
            desiredSize.X <= 0f
            || desiredSize.Y <= 0f
            || availableSize.X <= 0f
            || availableSize.Y <= 0f
        )
            return 0f;

        return MathF.Min(
            1f,
            MathF.Min(availableSize.X / desiredSize.X, availableSize.Y / desiredSize.Y)
        );
    }

    /// <summary>Validates that both cell dimensions are positive and finite.</summary>
    /// <param name="cellSize">The proposed cell dimensions.</param>
    /// <param name="parameterName">The argument name used in validation errors.</param>
    private static void ValidateCellSize(Vector2D<float> cellSize, string parameterName)
    {
        if (
            !float.IsFinite(cellSize.X)
            || !float.IsFinite(cellSize.Y)
            || cellSize.X <= 0f
            || cellSize.Y <= 0f
        )
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Both cell dimensions must be positive and finite."
            );
    }

    /// <summary>Validates that an available size is finite and non-negative.</summary>
    /// <param name="size">The proposed available size.</param>
    /// <param name="parameterName">The argument name used in validation errors.</param>
    private static void ValidateAvailableSize(Vector2D<float> size, string parameterName)
    {
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X < 0f || size.Y < 0f)
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Available dimensions must be finite and non-negative."
            );
    }

    /// <summary>Validates finite arranged coordinates and non-negative dimensions.</summary>
    /// <param name="bounds">The proposed arrangement rectangle.</param>
    private static void ValidateBounds(Rectangle<float> bounds)
    {
        ValidateAvailableSize(bounds.Size, nameof(bounds));
        if (
            !float.IsFinite(bounds.Origin.X)
            || !float.IsFinite(bounds.Origin.Y)
            || !float.IsFinite(bounds.Max.X)
            || !float.IsFinite(bounds.Max.Y)
        )
            throw new ArgumentOutOfRangeException(
                nameof(bounds),
                "The arranged rectangle must have finite coordinates."
            );
    }
}
