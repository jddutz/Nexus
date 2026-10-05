namespace Nexus.Physics;

/// <summary>
/// Reports a collider pair that overlapped after a physics step or had a resolved swept contact.
/// Swept-contact pairs may not overlap at the final pose. This is a per-step notification, not a
/// collision-enter event; pair ordering has no semantic meaning.
/// </summary>
/// <param name="first">One collider in the reported pair.</param>
/// <param name="second">The other collider in the reported pair.</param>
/// <param name="contact">Optional data for a resolved swept contact.</param>
public sealed class PhysicsCollisionEvent(
    PhysicsCollider2D first,
    PhysicsCollider2D second,
    PhysicsCollisionContact? contact = null
) : IEvent
{
    /// <summary>Gets one collider in the reported pair.</summary>
    public PhysicsCollider2D First { get; } = first ?? throw new ArgumentNullException(nameof(first));

    /// <summary>Gets the other collider in the reported pair.</summary>
    public PhysicsCollider2D Second { get; } = second ?? throw new ArgumentNullException(nameof(second));

    /// <summary>Gets swept contact data when motion reached this pair during the current step.</summary>
    public PhysicsCollisionContact? Contact { get; } = contact;
}
