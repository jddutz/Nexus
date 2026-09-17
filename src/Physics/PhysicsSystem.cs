namespace Nexus.Physics;

/// <summary>
/// Provides the default physics system implementation.
/// </summary>
public sealed class PhysicsSystem : IPhysicsSystem
{
    public void Initialize() { }

    /// <inheritdoc />
    public void Update(double deltaTime) { }

    public bool Activate<TComponent>(TComponent component)
        where TComponent : class, IPhysicsComponent
    {
        return false;
    }

    public bool Deactivate<TComponent>(TComponent component)
        where TComponent : class, IPhysicsComponent
    {
        return false;
    }
}
