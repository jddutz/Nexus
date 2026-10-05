namespace Nexus.Physics;

public interface IPhysicsSystem
{
    /// <summary>Gets the simulation worlds owned by this system.</summary>
    IReadOnlyCollection<PhysicsWorld2D> Worlds { get; }

    /// <summary>
    /// Initializes the physics system before the update loop begins.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Updates the physics system for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time in seconds since the previous frame.</param>
    void Update(double deltaTime);

    /// <summary>Creates and registers a 2D simulation world.</summary>
    /// <returns>The newly registered simulation world.</returns>
    PhysicsWorld2D CreateWorld2D();

    /// <summary>
    /// Removes a registered 2D simulation world. Worlds with registered participants cannot be removed.
    /// </summary>
    /// <param name="world">The world to remove.</param>
    /// <returns><see langword="true"/> when the empty world was removed.</returns>
    bool RemoveWorld(PhysicsWorld2D world);

    /// <summary>
    /// Registers an activated component with its configured simulation world.
    /// Registration is rejected when the world ID is invalid or unknown, the component
    /// type is unsupported, or the component is already registered.
    /// </summary>
    /// <param name="component">The physics component to register.</param>
    /// <returns><see langword="true"/> when the component is registered.</returns>
    bool Activate<TComponent>(TComponent component)
        where TComponent : class, IPhysicsComponent;

    /// <summary>
    /// Unregisters a deactivated component from its registered simulation world.
    /// </summary>
    /// <param name="component">The physics component to unregister.</param>
    /// <returns><see langword="true"/> when the component was registered and removed.</returns>
    bool Deactivate<TComponent>(TComponent component)
        where TComponent : class, IPhysicsComponent;
}
