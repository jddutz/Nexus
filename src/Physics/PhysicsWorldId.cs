namespace Nexus.Physics;

/// <summary>Identifies a physics simulation world.</summary>
public readonly record struct PhysicsWorldId(ulong Value) : IEquatable<PhysicsWorldId>, IUniqueId
{
    /// <summary>Converts a numeric value to a physics-world identifier.</summary>
    /// <param name="value">The identifier value.</param>
    public static implicit operator PhysicsWorldId(ulong value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();

    /// <inheritdoc />
    public bool Equals(IUniqueId? other) => other is not null && Value == other.Value;

    /// <summary>Creates a new physics-world identifier.</summary>
    /// <returns>A new identifier.</returns>
    public static PhysicsWorldId New() =>
        new IdentityHashBuilder("PhysicsWorld").Add(Guid.NewGuid()).Compute();

    /// <summary>Gets the invalid physics-world identifier.</summary>
    public static readonly PhysicsWorldId Invalid = new(0);
}
