namespace Nexus.Physics;

public interface IPhysicsSystem
{
    /// <summary>
    /// Initializes the physics system before the update loop begins.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Updates the physics system for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time in seconds since the previous frame.</param>
    void Update(double deltaTime);

    bool CanActivate<TComponent>(TComponent component)
        where TComponent : class, IComponent;

    bool Activate<TComponent>(TComponent component)
        where TComponent : class, IComponent;

    bool Deactivate<TComponent>(TComponent component)
        where TComponent : class, IComponent;
}
