namespace Nexus.Graphics.Components;

/// <summary>Exposes one custom uniform-color mesh drawable to the graphics system.</summary>
public sealed class UniformColorMeshRenderer : Component, IGraphicsComponent
{
    private UniformColorMesh? _drawable;
    private Mesh? _mesh;
    private Matrix4X4<float> _transform = Matrix4X4<float>.Identity;
    private Color _color = Colors.White;
    private ISpatialObject? _spatialOwner;

    /// <inheritdoc />
    public event EventHandler<DrawableEventArgs>? DrawableAdded;

    /// <inheritdoc />
    public event EventHandler<DrawableEventArgs>? DrawableRemoved;

    /// <inheritdoc />
    public IReadOnlyList<IDrawable> Drawables =>
        _drawable is null ? Array.Empty<IDrawable>() : [_drawable];

    /// <summary>Gets or sets the mesh exposed by this renderer.</summary>
    public Mesh? Mesh
    {
        get => _mesh;
        set
        {
            _mesh = value;
            if (IsActivated)
                Synchronize();
        }
    }

    /// <summary>Gets or sets the transform applied to the mesh.</summary>
    public Matrix4X4<float> Transform
    {
        get => _transform;
        set
        {
            _transform = value;
            if (_drawable is not null)
                _drawable.Transform = value;
        }
    }

    /// <summary>Gets or sets the mesh's uniform color.</summary>
    public Color Color
    {
        get => _color;
        set
        {
            _color = value;
            if (_drawable is not null)
                _drawable.Color = value;
        }
    }

    /// <inheritdoc />
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

    private ulong _renderLayerMask = RenderLayers.All;

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

    private int _drawOrder;

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();
    }

    /// <inheritdoc />
    public override void Activate()
    {
        base.Activate();
        if (IsActivated)
            Synchronize();
    }

    /// <inheritdoc />
    public override void Deactivate()
    {
        Unregister();
        base.Deactivate();
    }

    /// <inheritdoc />
    protected override void OnOwnerChanged()
    {
        if (_spatialOwner is not null)
            _spatialOwner.WorldTransformChanged -= OnOwnerWorldTransformChanged;

        _spatialOwner = Owner as ISpatialObject;
        if (_spatialOwner is not null)
        {
            _spatialOwner.WorldTransformChanged += OnOwnerWorldTransformChanged;
            Transform = _spatialOwner.WorldTransform;
        }

        base.OnOwnerChanged();
    }

    /// <summary>Copies the owner's latest world transform into the drawable transform.</summary>
    /// <param name="previousValue">The previous owner world transform.</param>
    /// <param name="value">The updated owner world transform.</param>
    private void OnOwnerWorldTransformChanged(
        Matrix4X4<float> previousValue,
        Matrix4X4<float> value
    ) => Transform = value;

    /// <summary>Creates or removes the drawable to match the current mesh.</summary>
    private void Synchronize()
    {
        if (!IsActivated)
            return;

        if (_mesh is null)
        {
            Unregister();
            return;
        }

        if (_drawable is null)
        {
            _drawable = new UniformColorMesh(_mesh)
            {
                Transform = Transform,
                Color = Color,
                RenderLayerMask = RenderLayerMask,
                DrawOrder = DrawOrder,
            };
            DrawableAdded?.Invoke(this, new DrawableEventArgs(_drawable));
        }
        else
        {
            _drawable.Mesh = _mesh;
        }
    }

    /// <summary>Removes the current drawable and reports its lifecycle change.</summary>
    private void Unregister()
    {
        if (_drawable is null)
            return;

        var drawable = _drawable;
        _drawable = null;
        DrawableRemoved?.Invoke(this, new DrawableEventArgs(drawable));
    }
}
