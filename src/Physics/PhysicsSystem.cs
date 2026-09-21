namespace Nexus.Physics;

/// <summary>
/// Provides the default physics system implementation.
/// </summary>
public sealed class PhysicsSystem(IEventHub eventHub) : IPhysicsSystem
{
    public void Initialize()
    {
        eventHub.Register(this);
    }

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
