namespace Nexus.Physics;

/// <summary>
/// Provides the default physics system implementation.
/// </summary>
public sealed class PhysicsSystem : IPhysicsSystem
{
    public void Initialize() { }

    /// <inheritdoc />
    public void Update(double deltaTime) { }

    public bool CanActivate<TComponent>(TComponent component)
        where TComponent : class, IComponent
    {
        return false;
    }

    public bool Activate<TComponent>(TComponent component)
        where TComponent : class, IComponent
    {
        return false;
    }

    public bool Deactivate<TComponent>(TComponent component)
        where TComponent : class, IComponent
    {
        return false;
    }
}
