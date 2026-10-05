namespace Nexus.Physics.Components;

using Nexus.Physics;

/// <summary>Associates local collision geometry with a 2D component owner.</summary>
public sealed class PhysicsCollider2D : Component, IPhysicsComponent
{
    private PhysicsWorldId _worldId;
    private IPhysicsShape2D? _shape;

    /// <summary>Gets or sets the simulation world.</summary>
    /// <exception cref="InvalidOperationException">The collider is already activated.</exception>
    public PhysicsWorldId WorldId
    {
        get => _worldId;
        set
        {
            if (IsActivated && value != _worldId)
                throw new InvalidOperationException("WorldId cannot change while the collider is activated.");
            _worldId = value;
        }
    }

    /// <summary>Gets or sets the category bit or bits assigned to this collider.</summary>
    public uint CollisionCategory { get; set; } = 1;

    /// <summary>
    /// Gets or sets the categories this collider can contact. A pair is permitted only when
    /// each collider's mask includes the other collider's category.
    /// </summary>
    public uint CollisionMask { get; set; } = uint.MaxValue;

    /// <summary>Gets or sets the local-space collision shape.</summary>
    public IPhysicsShape2D Shape
    {
        get => _shape ?? throw new InvalidOperationException("A collider shape has not been configured.");
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var previous = _shape;
            _shape = value;
            if (previous is not null && !ReferenceEquals(previous, value))
                ShapeChanged?.Invoke(this, previous, value);
        }
    }

    /// <summary>Occurs when the local collision shape is replaced.</summary>
    public event Action<PhysicsCollider2D, IPhysicsShape2D, IPhysicsShape2D>? ShapeChanged;

    /// <summary>Validates that the owner supplies the required 2D transform.</summary>
    /// <returns><see langword="true"/> when the owner and shape are valid.</returns>
    public override bool CanActivate() => base.CanActivate() && Owner is IGameObject2D && _shape is not null;
}
