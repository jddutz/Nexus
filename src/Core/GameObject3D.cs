namespace Nexus.Core;

/// <summary>
/// Provides a three-dimensional game object.
/// </summary>
public partial class GameObject3D : GameObject, IGameObject3D
{
    /// <summary>Initializes a 3D game object without components.</summary>
    public GameObject3D() { }

    /// <summary>Initializes a 3D game object with the specified components.</summary>
    /// <param name="components">The components owned by this game object.</param>
    public GameObject3D(IEnumerable<IComponent> components)
        : base(components) { }

    /// <inheritdoc/>
    [Observable(PublicSetter = false)]
    private Matrix4X4<float> _localTransform;

    /// <inheritdoc/>
    [Observable(PublicSetter = false)]
    private Matrix4X4<float> _worldTransform;

    [Observable(PublicSetter = true)]
    private Vector3D<float> _position;

    [Observable]
    private Quaternion<float> _quaternion;

    [Observable(PublicSetter = true)]
    private Vector3D<float> _scale;

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
}
