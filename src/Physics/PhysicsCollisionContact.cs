namespace Nexus.Physics;

using Nexus.Physics.Components;
using Silk.NET.Maths;

/// <summary>
/// Describes a swept contact resolved during a physics step. The contact may precede overlap at
/// the final pose; one earliest contact is reported per moving body, with equal-fraction ties
/// selected by target collider ID and then moving collider ID.
/// </summary>
public readonly record struct PhysicsCollisionContact(
    PhysicsBody2D Body,
    Vector2D<float> Normal,
    float Fraction,
    Vector2D<float> IncomingVelocity
)
{
    /// <summary>Gets the body whose motion reached the contact.</summary>
    public PhysicsBody2D Body { get; } = Body ?? throw new ArgumentNullException(nameof(Body));

    /// <summary>
    /// Gets the separation direction for <see cref="Body"/>. Reflection uses this
    /// normal to direct the body away from the contacted surface.
    /// </summary>
    public Vector2D<float> Normal { get; } = Normal;

    /// <summary>Gets the fraction of the current step at which contact occurred.</summary>
    public float Fraction { get; } = Fraction;

    /// <summary>Gets the body's velocity immediately before contact resolution.</summary>
    public Vector2D<float> IncomingVelocity { get; } = IncomingVelocity;
}
