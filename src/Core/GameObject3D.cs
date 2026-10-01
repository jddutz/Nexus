namespace Nexus.Core;

/// <summary>
/// Provides a three-dimensional game object.
/// </summary>
public partial class GameObject3D : GameObject, IGameObject3D
{
    private ISpatialObject? _spatialAncestor;

    /// <summary>Initializes a 3D game object without components.</summary>
    public GameObject3D()
    {
        UpdateLocal();
    }

    /// <summary>Initializes a 3D game object with the specified components.</summary>
    /// <param name="components">The components owned by this game object.</param>
    public GameObject3D(IEnumerable<IComponent> components)
        : base(components) { }

    /// <inheritdoc/>
    [Observable(PublicSetter = false)]
    private Matrix4X4<float> _worldTransform = Matrix4X4<float>.Identity;

    /// <summary>Composes the local transform with the nearest spatial ancestor.</summary>
    private void UpdateWorldTransform()
    {
        var ancestor = FindSpatialAncestor(this);

        if (ancestor == null)
        {
            WorldTransform = LocalTransform;
        }
        else
        {
            WorldTransform = LocalTransform * ancestor.WorldTransform;
        }
    }

    /// <inheritdoc/>
    [Observable(PublicSetter = false)]
    private Matrix4X4<float> _localTransform = Matrix4X4<float>.Identity;

    /// <summary>Rebuilds the world transform after the local transform changes.</summary>
    /// <param name="previousValue">The previous local transform.</param>
    protected virtual partial void AfterLocalTransformChanges(Matrix4X4<float> previousValue) =>
        UpdateWorldTransform();

    /// <summary>Rebuilds the local transform from the local position, rotation, and scale.</summary>
    private void UpdateLocal()
    {
        LocalTransform =
            Matrix4X4.CreateScale(Scale.X, Scale.Y, Scale.Z)
            * Matrix4X4.CreateFromQuaternion(Quaternion)
            * Matrix4X4.CreateTranslation(Position.X, Position.Y, Position.Z);
    }

    [Observable(PublicSetter = true)]
    private Vector3D<float> _position;

    /// <summary>Rebuilds the local transform after the position changes.</summary>
    /// <param name="previousValue">The previous position.</param>
    protected virtual partial void AfterPositionChanges(Vector3D<float> previousValue) =>
        UpdateLocal();

    [Observable(PublicSetter = true)]
    private Quaternion<float> _quaternion = Quaternion<float>.Identity;

    /// <summary>Rebuilds the local transform after the quaternion changes.</summary>
    /// <param name="previousValue">The previous quaternion.</param>
    protected virtual partial void AfterQuaternionChanges(Quaternion<float> previousValue) =>
        UpdateLocal();

    [Observable(PublicSetter = true)]
    private Vector3D<float> _scale = new(1f, 1f, 1f);

    /// <summary>Rebuilds the local transform after the scale changes.</summary>
    /// <param name="previousValue">The previous scale.</param>
    protected virtual partial void AfterScaleChanges(Vector3D<float> previousValue) =>
        UpdateLocal();

    private static ISpatialObject? FindSpatialAncestor(ISceneNode node)
    {
        if (node.Parent is null)
            return null;

        if (node.Parent is ISpatialObject result)
            return result;

        return FindSpatialAncestor(node.Parent);
    }

    /// <summary>Updates this object's world transform after an ancestor changes.</summary>
    /// <param name="previousValue">The ancestor's previous world transform.</param>
    /// <param name="value">The ancestor's new world transform.</param>
    private void OnParentWorldTransformChanged(
        Matrix4X4<float> previousValue,
        Matrix4X4<float> value
    )
    {
        UpdateWorldTransform();
    }

    /// <inheritdoc/>
    public override void Activate()
    {
        if (IsActivated || !CanActivate())
            return;

        UpdateLocal();

        _spatialAncestor = FindSpatialAncestor(this);
        if (_spatialAncestor != null)
        {
            _spatialAncestor.WorldTransformChanged += OnParentWorldTransformChanged;
            UpdateWorldTransform();
        }

        base.Activate();
    }

    /// <inheritdoc/>
    public override void Deactivate()
    {
        _spatialAncestor?.WorldTransformChanged -= OnParentWorldTransformChanged;

        base.Deactivate();
    }

    /// <inheritdoc/>
    public override void OnSceneHierarchyChanged()
    {
        _spatialAncestor?.WorldTransformChanged -= OnParentWorldTransformChanged;

        _spatialAncestor = FindSpatialAncestor(this);
        if (
            _spatialAncestor is not null
            && (IsActivated || (_spatialAncestor as IManagedEntity)?.IsActivated == true)
        )
            _spatialAncestor.WorldTransformChanged += OnParentWorldTransformChanged;

        UpdateWorldTransform();
        base.OnSceneHierarchyChanged();
    }
}
