namespace Nexus.Core;

/// <summary>
/// Provides a three-dimensional game object.
/// </summary>
public partial class GameObject3D : GameObject, IGameObject3D
{
    /// <summary>Initializes a 3D game object without components.</summary>
    public GameObject3D()
    {
        UpdateTransformationMatrix();
    }

    /// <summary>Initializes a 3D game object with the specified components.</summary>
    /// <param name="components">The components owned by this game object.</param>
    public GameObject3D(IEnumerable<IComponent> components)
        : base(components)
    {
        UpdateTransformationMatrix();
    }

    /// <inheritdoc/>
    [Observable(PublicSetter = false)]
    private Matrix4X4<float> _localTransform = Matrix4X4<float>.Identity;

    /// <inheritdoc/>
    [Observable(PublicSetter = false)]
    private Matrix4X4<float> _worldTransform = Matrix4X4<float>.Identity;

    [Observable(PublicSetter = true)]
    private Vector3D<float> _position;

    [Observable]
    private Quaternion<float> _quaternion = Quaternion<float>.Identity;

    [Observable(PublicSetter = true)]
    private Vector3D<float> _scale = new(1f, 1f, 1f);

    private IGameObject2D? _spatialAncestor2D;
    private IGameObject3D? _spatialAncestor3D;
    private IGameObject? _spatialAncestor;

    /// <summary>
    /// Finds the nearest spatial ancestor in this object's parent chain.
    /// </summary>
    /// <returns>The nearest spatial ancestor, or <see langword="null"/>.</returns>
    private IGameObject? FindNearestSpatialAncestor()
    {
        var ancestor = Parent as IGameObject;
        while (
            ancestor is not null && ancestor is not IGameObject2D && ancestor is not IGameObject3D
        )
            ancestor = ancestor.Parent as IGameObject;

        return ancestor;
    }

    /// <inheritdoc/>
    public override void Activate()
    {
        if (IsActivated || !CanActivate())
            return;

        SubscribeToSpatialAncestor();
        UpdateWorldTransform();
        base.Activate();
    }

    /// <inheritdoc/>
    public override void Deactivate()
    {
        UnsubscribeFromSpatialAncestor();
        base.Deactivate();
    }

    /// <inheritdoc/>
    protected override void OnHierarchyChanged()
    {
        base.OnHierarchyChanged();
        UpdateWorldTransform();

        UnsubscribeFromSpatialAncestor();
        SubscribeToSpatialAncestor();
    }

    /// <summary>Rebuilds the local transform from the local position, rotation, and scale.</summary>
    private void UpdateTransformationMatrix()
    {
        SetLocalTransform(
            Matrix4X4.CreateScale(Scale.X, Scale.Y, Scale.Z)
                * Matrix4X4.CreateFromQuaternion(Quaternion)
                * Matrix4X4.CreateTranslation(Position.X, Position.Y, Position.Z)
        );
    }

    /// <summary>Composes the local transform with the nearest spatial ancestor.</summary>
    private void UpdateWorldTransform()
    {
        var ancestor = FindNearestSpatialAncestor();
        var ancestorTransform = ancestor switch
        {
            IGameObject2D gameObject2D => gameObject2D.WorldTransform,
            IGameObject3D gameObject3D => gameObject3D.WorldTransform,
            _ => Matrix4X4<float>.Identity,
        };

        SetWorldTransform(LocalTransform * ancestorTransform);
    }

    /// <summary>Subscribes to the nearest spatial ancestor's world-transform changes.</summary>
    private void SubscribeToSpatialAncestor()
    {
        _spatialAncestor = FindNearestSpatialAncestor();
        if (_spatialAncestor is null)
            return;

        _spatialAncestor.PropertyChanged += OnSpatialAncestorPropertyChanged;
        switch (_spatialAncestor)
        {
            case IGameObject2D gameObject2D:
                _spatialAncestor2D = gameObject2D;
                gameObject2D.WorldTransformChanged += OnSpatialAncestorWorldTransformChanged;
                break;
            case IGameObject3D gameObject3D:
                _spatialAncestor3D = gameObject3D;
                gameObject3D.WorldTransformChanged += OnSpatialAncestorWorldTransformChanged;
                break;
        }
    }

    /// <summary>Removes the current spatial ancestor subscriptions.</summary>
    private void UnsubscribeFromSpatialAncestor()
    {
        if (_spatialAncestor2D is not null)
            _spatialAncestor2D.WorldTransformChanged -= OnSpatialAncestorWorldTransformChanged;
        if (_spatialAncestor3D is not null)
            _spatialAncestor3D.WorldTransformChanged -= OnSpatialAncestorWorldTransformChanged;
        if (_spatialAncestor is not null)
            _spatialAncestor.PropertyChanged -= OnSpatialAncestorPropertyChanged;

        _spatialAncestor2D = null;
        _spatialAncestor3D = null;
        _spatialAncestor = null;
    }

    /// <summary>Updates this object's world transform after an ancestor changes.</summary>
    /// <param name="previousValue">The ancestor's previous world transform.</param>
    /// <param name="value">The ancestor's new world transform.</param>
    private void OnSpatialAncestorWorldTransformChanged(
        Matrix4X4<float> previousValue,
        Matrix4X4<float> value
    )
    {
        if (_spatialAncestor?.IsActivated == true)
            UpdateWorldTransform();
    }

    /// <summary>Starts tracking an ancestor when its activation state changes.</summary>
    /// <param name="propertyName">The changed ancestor property name.</param>
    private void OnSpatialAncestorPropertyChanged(string propertyName)
    {
        if (propertyName == nameof(IManagedEntity.IsActivated))
            UpdateWorldTransform();
    }

    /// <summary>Rebuilds the local transform after the position changes.</summary>
    /// <param name="previousValue">The previous position.</param>
    private void AfterPositionChanges(Vector3D<float> previousValue) =>
        UpdateTransformationMatrix();

    /// <summary>Rebuilds the local transform after the quaternion changes.</summary>
    /// <param name="previousValue">The previous quaternion.</param>
    private void AfterQuaternionChanges(Quaternion<float> previousValue) =>
        UpdateTransformationMatrix();

    /// <summary>Rebuilds the local transform after the scale changes.</summary>
    /// <param name="previousValue">The previous scale.</param>
    private void AfterScaleChanges(Vector3D<float> previousValue) => UpdateTransformationMatrix();

    /// <summary>Rebuilds the world transform after the local transform changes.</summary>
    /// <param name="previousValue">The previous local transform.</param>
    private void AfterLocalTransformChanges(Matrix4X4<float> previousValue) =>
        UpdateWorldTransform();
}
