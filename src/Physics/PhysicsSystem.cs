namespace Nexus.Physics;

using Microsoft.Extensions.Logging;

/// <summary>
/// Provides the default physics system implementation.
/// </summary>
public sealed class PhysicsSystem : IPhysicsSystem
{
    private readonly IEventHub _eventHub;
    private readonly ILogger<PhysicsSystem>? _logger;
    private readonly List<PhysicsWorld2D> _worlds = [];
    private readonly IReadOnlyCollection<PhysicsWorld2D> _readOnlyWorlds;
    private readonly Dictionary<IPhysicsComponent, PhysicsWorld2D> _registeredComponents =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>Creates a physics system without any simulation worlds.</summary>
    /// <param name="eventHub">The event hub used for lifecycle and collision events.</param>
    /// <param name="logger">The optional logger used to report rejected registrations.</param>
    public PhysicsSystem(IEventHub eventHub, ILogger<PhysicsSystem>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(eventHub);
        _eventHub = eventHub;
        _logger = logger;
        _readOnlyWorlds = _worlds.AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<PhysicsWorld2D> Worlds => _readOnlyWorlds;

    /// <inheritdoc />
    public void Initialize()
    {
        _eventHub.Register(this);
    }

    /// <inheritdoc />
    public void Update(double deltaTime)
    {
        if (!double.IsFinite(deltaTime) || deltaTime < 0d || deltaTime > float.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
        var elapsed = (float)deltaTime;
        if (!float.IsFinite(elapsed))
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
        ValidateAssociations();
        foreach (var world in _worlds)
            foreach (var result in world.Step(elapsed))
                _eventHub.Publish(new PhysicsCollisionEvent(result.First, result.Second, result.Contact));
    }

    /// <inheritdoc />
    public PhysicsWorld2D CreateWorld2D()
    {
        var world = new PhysicsWorld2D(PhysicsWorldId.New());
        _worlds.Add(world);
        return world;
    }

    /// <inheritdoc />
    public bool RemoveWorld(PhysicsWorld2D world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.HasParticipants)
            return false;
        return _worlds.Remove(world);
    }

    /// <inheritdoc />
    public bool Activate<TComponent>(TComponent component)
        where TComponent : class, IPhysicsComponent
    {
        ArgumentNullException.ThrowIfNull(component);
        if (component.WorldId == PhysicsWorldId.Invalid)
        {
            _logger?.LogWarning(
                "Physics component {ComponentId} cannot be activated because its WorldId is invalid.",
                component.Id
            );
            return false;
        }
        var world = _worlds.FirstOrDefault(candidate => candidate.Id == component.WorldId);
        if (world is null)
        {
            _logger?.LogWarning(
                "Physics component {ComponentId} cannot be activated because world {WorldId} is unknown.",
                component.Id,
                component.WorldId
            );
            return false;
        }

        if (_registeredComponents.ContainsKey(component))
        {
            _logger?.LogWarning("Physics component {ComponentId} is already registered.", component.Id);
            return false;
        }

        if (HasAssociationConflict(component, world))
        {
            _logger?.LogWarning(
                "Physics component {ComponentId} cannot be activated because its owner association conflicts with another registered component.",
                component.Id
            );
            return false;
        }

        switch (component)
        {
            case PhysicsBody2D body:
                world.Add(body);
                break;
            case PhysicsCollider2D collider:
                world.Add(collider);
                break;
            default:
                _logger?.LogWarning(
                    "Physics component type {ComponentType} is not supported.",
                    component.GetType().FullName
                );
                return false;
        }

        _registeredComponents.Add(component, world);
        return true;
    }

    private bool HasAssociationConflict(IPhysicsComponent component, PhysicsWorld2D targetWorld)
    {
        if (component.Owner is not IGameObject2D owner)
            return false;

        foreach (var (registered, world) in _registeredComponents)
        {
            if (ReferenceEquals(component, registered))
                continue;
            if (registered.Owner is not IGameObject2D registeredOwner)
                continue;
            if (component is PhysicsBody2D
                && registered is PhysicsBody2D
                && ReferenceEquals(owner, registeredOwner))
                return true;
            if (!ReferenceEquals(world, targetWorld)
                && ReferenceEquals(owner, registeredOwner))
                return true;
            if (component is PhysicsBody2D
                && registered is PhysicsBody2D
                && !ReferenceEquals(world, targetWorld)
                && IsAncestorOrDescendant(owner, registeredOwner))
                return true;
        }

        return false;
    }

    private void ValidateAssociations()
    {
        var registered = _registeredComponents.ToArray();
        foreach (var (component, world) in registered)
            if (HasAssociationConflict(component, world))
                throw new InvalidOperationException(
                    "Physics bodies and same-owner components must remain in compatible simulation worlds. "
                    + "Deactivate components before changing their hierarchy or world association."
                );
    }

    private static bool IsAncestorOrDescendant(IGameObject2D first, IGameObject2D second)
    {
        return IsAncestor(first, second) || IsAncestor(second, first);
    }

    private static bool IsAncestor(IGameObject2D ancestor, IGameObject2D node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
            if (ReferenceEquals(current, ancestor))
                return true;
        return false;
    }

    /// <inheritdoc />
    public bool Deactivate<TComponent>(TComponent component)
        where TComponent : class, IPhysicsComponent
    {
        ArgumentNullException.ThrowIfNull(component);
        if (!_registeredComponents.Remove(component, out var world))
            return false;
        switch (component)
        {
            case PhysicsBody2D body:
                world.Remove(body);
                return true;
            case PhysicsCollider2D collider:
                world.Remove(collider);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Registers an activated physics component with its configured world.</summary>
    /// <param name="message">The component activation event.</param>
    public void Handle(ComponentActivatedEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Component is IPhysicsComponent component)
            _ = Activate(component);
    }

    /// <summary>Removes a deactivated physics component from its registered world.</summary>
    /// <param name="message">The component deactivation event.</param>
    public void Handle(ComponentDeactivatedEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Component is IPhysicsComponent component)
            Deactivate(component);
    }
}
