namespace Nexus.Graphics.Components;

/// <summary>Converts sparse tile-map cells into one batched textured-quad drawable.</summary>
public sealed class TileMapRenderer : Component, IRenderer
{
    private TileMapData? _map;
    private TileMapDrawable? _drawable;
    private IReadOnlyList<IDrawable> _drawables = Array.Empty<IDrawable>();
    private ISpatialObject? _spatialOwner;
    private Vector2D<float> _cellSize = new(1f, 1f);
    private ITexture _texture = BuiltInTextures.Invalid;
    private ISamplingBehavior _samplingBehavior = SamplingBehaviors.Smooth;
    private VertexShader? _vertexShader = BuiltInShaders.TexturedQuadVertexShader;
    private FragmentShader? _fragmentShader = BuiltInShaders.TexturedQuadFragmentShader;
    private ulong _renderLayerMask = RenderLayers.All;
    private int _drawOrder;
    private bool _isVisible = true;
    private int _isDirty = 1;

    /// <inheritdoc />
    public event EventHandler<DrawableEventArgs>? DrawableAdded;

    /// <inheritdoc />
    public event EventHandler<DrawableEventArgs>? DrawableRemoved;

    /// <inheritdoc />
    public event Action<bool, bool>? IsVisibleChanged;

    /// <inheritdoc />
    public IReadOnlyList<IDrawable> Drawables => _drawables;

    /// <summary>Gets or sets the map data interpreted by this component.</summary>
    public TileMapData? Map
    {
        get => _map;
        set
        {
            if (ReferenceEquals(_map, value))
                return;

            if (IsActivated && _map is not null)
                _map.Changed -= OnMapChanged;
            _map = value;
            if (IsActivated && _map is not null)
                _map.Changed += OnMapChanged;
            MarkDirty();
        }
    }

    /// <summary>Gets or sets the positive tile dimensions in local units.</summary>
    /// <remarks>Rows increase along the engine's screen-space +Y axis.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is non-finite or not positive.</exception>
    public Vector2D<float> CellSize
    {
        get => _cellSize;
        set
        {
            if (
                !float.IsFinite(value.X)
                || !float.IsFinite(value.Y)
                || value.X <= 0f
                || value.Y <= 0f
            )
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_cellSize == value)
                return;

            _cellSize = value;
            MarkDirty();
        }
    }

    /// <summary>Gets the local rectangle derived from declared map bounds and cell size.</summary>
    /// <exception cref="InvalidOperationException">The resulting rectangle is non-finite.</exception>
    public Rectangle<float> LocalBounds
    {
        get
        {
            var snapshot = _map?.CaptureSnapshot();
            var bounds = snapshot?.CellBounds ?? new Rectangle<int>(0, 0, 0, 0);
            var cellSize = _cellSize;
            var result = new Rectangle<float>(
                bounds.Origin.X * cellSize.X,
                bounds.Origin.Y * cellSize.Y,
                bounds.Size.X * cellSize.X,
                bounds.Size.Y * cellSize.Y
            );
            if (
                !float.IsFinite(result.Origin.X)
                || !float.IsFinite(result.Origin.Y)
                || !float.IsFinite(result.Size.X)
                || !float.IsFinite(result.Size.Y)
            )
                throw new InvalidOperationException("The derived local map bounds are non-finite.");

            return result;
        }
    }

    /// <summary>Gets or sets the shared texture sampled by all occupied cells.</summary>
    public ITexture Texture
    {
        get => _texture;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_texture, value))
                return;
            _texture = value;
            if (_drawable is not null)
                _drawable.Texture = value;
        }
    }

    /// <summary>Gets or sets the sampling behavior shared by all cells.</summary>
    public ISamplingBehavior SamplingBehavior
    {
        get => _samplingBehavior;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_samplingBehavior, value))
                return;
            _samplingBehavior = value;
            if (_drawable is not null)
                _drawable.SamplingBehavior = value;
        }
    }

    /// <summary>Gets or sets the vertex shader used by the tile drawable.</summary>
    public VertexShader? VertexShader
    {
        get => _vertexShader;
        set
        {
            if (ReferenceEquals(_vertexShader, value))
                return;
            _vertexShader = value;
            if (_drawable is not null)
                _drawable.VertexShader = value;
        }
    }

    /// <summary>Gets or sets the fragment shader used by the tile drawable.</summary>
    public FragmentShader? FragmentShader
    {
        get => _fragmentShader;
        set
        {
            if (ReferenceEquals(_fragmentShader, value))
                return;
            _fragmentShader = value;
            if (_drawable is not null)
                _drawable.FragmentShader = value;
        }
    }

    /// <summary>Gets or sets the render-layer mask shared by all tiles.</summary>
    public ulong RenderLayerMask
    {
        get => _renderLayerMask;
        set
        {
            if (_renderLayerMask == value)
                return;
            _renderLayerMask = value;
            if (_drawable is not null)
                _drawable.RenderLayerMask = value;
        }
    }

    /// <inheritdoc />
    public int DrawOrder
    {
        get => _drawOrder;
        set
        {
            if (_drawOrder == value)
                return;
            _drawOrder = value;
            if (_drawable is not null)
                _drawable.DrawOrder = value;
        }
    }

    /// <inheritdoc />
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value)
                return;
            var previousValue = _isVisible;
            _isVisible = value;
            OnPropertyChanged(nameof(IsVisible));
            IsVisibleChanged?.Invoke(previousValue, value);
        }
    }

    /// <inheritdoc />
    public override void Activate()
    {
        var wasActivated = IsActivated;
        base.Activate();
        if (wasActivated || !IsActivated)
            return;

        if (_map is not null)
            _map.Changed += OnMapChanged;
        Interlocked.Exchange(ref _isDirty, 0);
        PublishSnapshot();
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        base.Update(deltaTime);
        if (Interlocked.Exchange(ref _isDirty, 0) != 0)
            PublishSnapshot();
    }

    /// <inheritdoc />
    public override void Deactivate()
    {
        if (_map is not null)
            _map.Changed -= OnMapChanged;
        UnregisterDrawable();
        base.Deactivate();
    }

    /// <inheritdoc />
    protected override void OnOwnerChanged()
    {
        if (_spatialOwner is not null)
            _spatialOwner.WorldTransformChanged -= OnOwnerWorldTransformChanged;

        _spatialOwner = Owner as ISpatialObject;
        if (_spatialOwner is not null)
            _spatialOwner.WorldTransformChanged += OnOwnerWorldTransformChanged;

        MarkDirty();
        base.OnOwnerChanged();
    }

    /// <summary>Marks the complete map snapshot dirty after map contents or bounds change.</summary>
    /// <param name="sender">The map data source.</param>
    /// <param name="eventArgs">The change event data.</param>
    private void OnMapChanged(object? sender, EventArgs eventArgs) => MarkDirty();

    /// <summary>Marks tile transforms dirty after the owner's world transform changes.</summary>
    /// <param name="previousValue">The previous world transform.</param>
    /// <param name="value">The new world transform.</param>
    private void OnOwnerWorldTransformChanged(
        Matrix4X4<float> previousValue,
        Matrix4X4<float> value
    ) => MarkDirty();

    /// <summary>Marks instance data for publication on the next component update.</summary>
    private void MarkDirty() => Interlocked.Exchange(ref _isDirty, 1);

    /// <summary>Builds and atomically publishes the current complete instance snapshot.</summary>
    private void PublishSnapshot()
    {
        var instances = BuildInstances();
        if (_drawable is null)
        {
            var drawable = new TileMapDrawable
            {
                Texture = Texture,
                SamplingBehavior = SamplingBehavior,
                VertexShader = VertexShader,
                FragmentShader = FragmentShader,
                RenderLayerMask = RenderLayerMask,
                DrawOrder = DrawOrder,
            };
            drawable.SetInstances(instances);
            _drawable = drawable;
            _drawables = [drawable];
            DrawableAdded?.Invoke(this, new DrawableEventArgs(drawable));
            return;
        }

        _drawable.SetInstances(instances);
    }

    /// <summary>Creates cell-local transforms combined with the latest owner world transform.</summary>
    /// <returns>The complete ordered tile-instance snapshot.</returns>
    private TileMapInstance[] BuildInstances()
    {
        var snapshot = _map?.CaptureSnapshot();
        if (snapshot is null || snapshot.Cells.Count == 0)
            return [];

        var ownerTransform = _spatialOwner?.WorldTransform ?? Matrix4X4<float>.Identity;
        var cellSize = _cellSize;
        var instances = new TileMapInstance[snapshot.Cells.Count];
        var index = 0;
        foreach (var (coordinate, cell) in snapshot.Cells)
        {
            var localTransform =
                Matrix4X4.CreateScale(cellSize.X, cellSize.Y, 1f)
                * Matrix4X4.CreateTranslation(
                    coordinate.X * cellSize.X,
                    coordinate.Y * cellSize.Y,
                    0f
                );
            instances[index++] = new TileMapInstance(
                localTransform * ownerTransform,
                cell.TextureRegion,
                cell.Color
            );
        }

        return instances;
    }

    /// <summary>Removes the drawable and reports its normal lifecycle removal.</summary>
    private void UnregisterDrawable()
    {
        if (_drawable is null)
            return;

        var drawable = _drawable;
        _drawable = null;
        _drawables = Array.Empty<IDrawable>();
        DrawableRemoved?.Invoke(this, new DrawableEventArgs(drawable));
    }
}
