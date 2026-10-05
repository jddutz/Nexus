namespace Nexus.Physics.Components;

/// <summary>
/// Marks a lifecycle-managed component that participates in a physics simulation.
/// </summary>
public interface IPhysicsComponent : IComponent
{
    /// <summary>Gets or sets the simulation world for this component.</summary>
    PhysicsWorldId WorldId { get; set; }
}
