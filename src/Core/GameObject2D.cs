namespace Nexus.Core;

/// <summary>
/// Provides a two-dimensional game object.
/// </summary>
public class GameObject2D : GameObject, IGameObject2D
{
    private Vector2D<float> _position = Vector2D<float>.Zero;
    private float _rotation;
    private Vector2D<float> _scale = Vector2D<float>.One;
    private Matrix4X4<float> _transformationMatrix = Matrix4X4<float>.Identity;
    private IGameObject? _spatialParent;

    /// <inheritdoc/>
    public Matrix4X4<float> LocalTransform => _transformationMatrix;

    /// <inheritdoc/>
    public Matrix4X4<float> WorldTransform
    {
        get
        {
            var spatialAncestor = FindNearestSpatialAncestor();
            return spatialAncestor switch
            {
                IGameObject2D parent2D => LocalTransform * parent2D.WorldTransform,
                IGameObject3D parent3D => LocalTransform * parent3D.WorldTransform,
                _ => LocalTransform,
            };
        }
    }

    /// <inheritdoc/>
    protected override void OnHierarchyChanged()
    {
        base.OnHierarchyChanged();
        if (IsActive)
            UpdateSpatialParentSubscription(true);
    }

    /// <inheritdoc/>
    protected override void OnActivated()
    {
        base.OnActivated();
        UpdateSpatialParentSubscription(true);
    }

    /// <inheritdoc/>
    protected override void OnDeactivated()
    {
        UpdateSpatialParentSubscription(false);
        base.OnDeactivated();
    }

    /// <summary>
    /// Finds the nearest spatial ancestor in this object's parent chain.
    /// </summary>
    /// <returns>The nearest spatial ancestor, or <see langword="null"/>.</returns>
    private IGameObject? FindNearestSpatialAncestor()
    {
        var ancestor = Parent;
        while (
            ancestor is not null && ancestor is not IGameObject2D && ancestor is not IGameObject3D
        )
            ancestor = ancestor.Parent;

        return ancestor;
    }

    /// <summary>
    /// Subscribes to the nearest spatial ancestor while this object is active.
    /// </summary>
    /// <param name="notifyWorldTransformChanged">Whether to notify when the ancestor changes.</param>
    private void UpdateSpatialParentSubscription(bool notifyWorldTransformChanged)
    {
        var spatialParent = IsActive ? FindNearestSpatialAncestor() : null;
        if (ReferenceEquals(_spatialParent, spatialParent))
            return;

        if (_spatialParent is not null)
            _spatialParent.PropertyChanged -= OnSpatialParentPropertyChanged;

        _spatialParent = spatialParent;
        if (_spatialParent is not null)
            _spatialParent.PropertyChanged += OnSpatialParentPropertyChanged;

        if (notifyWorldTransformChanged)
            OnPropertyChanged(nameof(WorldTransform));
    }

    /// <inheritdoc/>
    public Vector2D<float> Position
    {
        get => _position;
        set
        {
            if (SetProperty(ref _position, value))
                UpdateTransformationMatrix();
        }
    }

    /// <inheritdoc/>
    public float Rotation
    {
        get => _rotation;
        set
        {
            if (SetProperty(ref _rotation, value))
                UpdateTransformationMatrix();
        }
    }

    /// <inheritdoc/>
    public Vector2D<float> Scale
    {
        get => _scale;
        set
        {
            if (SetProperty(ref _scale, value))
                UpdateTransformationMatrix();
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameObject2D"/> class.
    /// </summary>
    public GameObject2D() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameObject2D"/> class with the specified identifier.
    /// </summary>
    /// <param name="id">The identifier for the game object.</param>
    public GameObject2D(uint id)
        : base(id) { }

    /// <summary>
    /// Updates the transformation matrix from the position, rotation, and scale.
    /// </summary>
    private void UpdateTransformationMatrix()
    {
        var scale = Matrix4X4.CreateScale(_scale.X, _scale.Y, 1.0f);
        var rotation = Matrix4X4.CreateRotationZ(_rotation);
        var translation = Matrix4X4.CreateTranslation(_position.X, _position.Y, 0.0f);
        _transformationMatrix = scale * rotation * translation;
        OnPropertyChanged(nameof(LocalTransform));
        OnPropertyChanged(nameof(WorldTransform));
    }

    /// <summary>
    /// Propagates a nearest spatial ancestor's world-transform change.
    /// </summary>
    /// <param name="sender">The spatial ancestor that changed.</param>
    /// <param name="e">The property change event data.</param>
    private void OnSpatialParentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IGameObject2D.WorldTransform))
            OnPropertyChanged(nameof(WorldTransform));
    }
}
