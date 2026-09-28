namespace Nexus.Core;

/// <summary>
/// Provides a three-dimensional game object.
/// </summary>
public class GameObject3D : GameObject, IGameObject3D
{
    private Vector3D<float> _position = Vector3D<float>.Zero;
    private Quaternion<float> _rotation = Quaternion<float>.Identity;
    private Vector3D<float> _scale = Vector3D<float>.One;
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

    /// <summary>
    /// Gets or sets the position of this game object.
    /// </summary>
    public Vector3D<float> Position
    {
        get => _position;
        set
        {
            if (SetProperty(ref _position, value))
                UpdateTransformationMatrix();
        }
    }

    /// <summary>
    /// Gets or sets the rotation of this game object.
    /// </summary>
    public Quaternion<float> Rotation
    {
        get => _rotation;
        set
        {
            if (SetProperty(ref _rotation, value))
                UpdateTransformationMatrix();
        }
    }

    /// <inheritdoc/>
    public Vector3D<float> Scale
    {
        get => _scale;
        set
        {
            if (SetProperty(ref _scale, value))
                UpdateTransformationMatrix();
        }
    }

    /// <inheritdoc/>
    public Quaternion<float> Quaternion
    {
        get => _rotation;
        set => Rotation = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameObject3D"/> class.
    /// </summary>
    public GameObject3D()
        : this([]) { }

    /// <summary>Initializes a three-dimensional game object with the specified components.</summary>
    /// <param name="components">The components owned by this game object.</param>
    public GameObject3D(IEnumerable<IComponent> components)
        : base(components) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameObject3D"/> class with the specified identifier.
    /// </summary>
    /// <param name="id">The identifier for the game object.</param>
    public GameObject3D(uint id)
        : base(id) { }

    /// <summary>Initializes a three-dimensional game object with the specified identifier and components.</summary>
    /// <param name="id">The identifier for the game object.</param>
    /// <param name="components">The components owned by this game object.</param>
    public GameObject3D(uint id, IEnumerable<IComponent> components)
        : base(id, components) { }

    /// <summary>
    /// Updates the transformation matrix from the position, rotation, and scale.
    /// </summary>
    private void UpdateTransformationMatrix()
    {
        var scale = Matrix4X4.CreateScale(_scale);
        var rotation = Matrix4X4.CreateFromQuaternion(_rotation);
        var translation = Matrix4X4.CreateTranslation(_position);
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
        if (e.PropertyName == nameof(IGameObject3D.WorldTransform))
            OnPropertyChanged(nameof(WorldTransform));
    }
}
