namespace Nexus.Core;

/// <summary>
/// Provides a two-dimensional game object.
/// </summary>
public partial class GameObject2D : GameObject, IGameObject2D
{
    /// <summary>Initializes a 2D game object without components.</summary>
    public GameObject2D() { }

    /// <summary>Initializes a 2D game object with the specified components.</summary>
    /// <param name="components">The components owned by this game object.</param>
    public GameObject2D(IEnumerable<IComponent> components)
        : base(components) { }

    /// <inheritdoc/>
    [Observable(PublicSetter = false)]
    private Matrix4X4<float> _localTransform;

    /// <inheritdoc/>
    [Observable(PublicSetter = false)]
    private Matrix4X4<float> _worldTransform;

    [Observable(PublicSetter = true)]
    private Vector2D<float> _position;

    [Observable(PublicSetter = true)]
    private float _rotation;

    [Observable(PublicSetter = true)]
    private Vector2D<float> _scale;

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
