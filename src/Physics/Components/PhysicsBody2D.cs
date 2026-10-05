namespace Nexus.Physics.Components;

using Silk.NET.Maths;

/// <summary>Stores authoritative world-space motion state for a 2D physics owner.</summary>
public sealed class PhysicsBody2D : Component, IPhysicsComponent
{
    private PhysicsWorldId _worldId;
    private Vector2D<float> _worldPosition;
    private float _worldRotation;
    private bool _poseInitialized;

    /// <summary>Gets or sets the simulation world.</summary>
    /// <exception cref="InvalidOperationException">The body is already activated.</exception>
    public PhysicsWorldId WorldId
    {
        get => _worldId;
        set
        {
            if (IsActivated && value != _worldId)
                throw new InvalidOperationException("WorldId cannot change while the body is activated.");
            _worldId = value;
        }
    }

    /// <summary>Gets or sets the world-space linear velocity.</summary>
    public Vector2D<float> Velocity { get; set; }

    /// <summary>Gets or sets the world-space linear acceleration.</summary>
    public Vector2D<float> Acceleration { get; set; }

    /// <summary>Gets or sets the angular velocity in radians per second.</summary>
    public float AngularVelocity { get; set; }

    /// <summary>Moves the simulated body and its owner to a world-space pose.</summary>
    /// <param name="worldPosition">The new world-space position.</param>
    /// <param name="worldRotation">The new world-space rotation in radians.</param>
    public void Teleport(Vector2D<float> worldPosition, float worldRotation)
    {
        if (!float.IsFinite(worldPosition.X) || !float.IsFinite(worldPosition.Y) || !float.IsFinite(worldRotation))
            throw new ArgumentOutOfRangeException(nameof(worldPosition));
        if (Owner is not IGameObject2D owner)
            throw new InvalidOperationException("A physics body requires an IGameObject2D owner.");

        var local = PhysicsPose.ToLocal(owner, worldPosition, worldRotation);
        _worldPosition = worldPosition;
        _worldRotation = worldRotation;
        _poseInitialized = true;
        owner.Position = local.Position;
        owner.Rotation = local.Rotation;
    }

    internal Vector2D<float> WorldPosition => _worldPosition;

    internal float WorldRotation => _worldRotation;

    internal void StopAtContact(Vector2D<float> worldPosition)
    {
        Teleport(worldPosition, _worldRotation);
        Velocity = default;
    }

    internal void InitializePose()
    {
        if (Owner is not IGameObject2D owner)
            throw new InvalidOperationException("A physics body requires an IGameObject2D owner.");
        if (!PhysicsPose.IsSupportedParent(owner))
            throw new InvalidOperationException("Physics bodies require rigid, invertible parent transforms.");

        var world = owner.WorldTransform;
        _worldPosition = new(world.M41, world.M42);
        _worldRotation = MathF.Atan2(world.M12, world.M11);
        _poseInitialized = true;
    }

    internal void IntegrateWorld(float deltaTime)
    {
        if (!_poseInitialized)
            InitializePose();
        Velocity += Acceleration * deltaTime;
        _worldPosition += Velocity * deltaTime;
        _worldRotation += AngularVelocity * deltaTime;
    }

    internal void WriteBackPose()
    {
        if (Owner is not IGameObject2D owner)
            throw new InvalidOperationException("A physics body requires an IGameObject2D owner.");
        PhysicsPose.WriteLocal(owner, _worldPosition, _worldRotation);
    }

    /// <inheritdoc />
    public override void Deactivate()
    {
        base.Deactivate();
        _poseInitialized = false;
    }

    /// <inheritdoc />
    public override bool CanActivate() => base.CanActivate() && Owner is IGameObject2D;
}
