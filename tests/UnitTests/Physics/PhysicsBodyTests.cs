namespace Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Core;
using Nexus.Core.Events;
using Nexus.Game;
using Nexus.Physics;
using Nexus.Physics.Components;
using Nexus.Graphics.Cameras;
using Silk.NET.Maths;

/// <summary>Verifies lifecycle and pose behavior for 2D physics bodies.</summary>
public sealed class PhysicsBodyTests
{
    /// <summary>
    /// Verifies reactivation captures an owner's pose changed while the body was inactive.
    /// </summary>
    [Fact]
    public void Reactivation_capturesCurrentOwnerWorldPose()
    {
        var eventHub = new EventHub();
        var physics = new PhysicsSystem(eventHub);
        var world = physics.CreateWorld2D();
        var owner = new GameObject2D { Position = new(10f, 20f) };
        var body = new PhysicsBody2D { WorldId = world.Id };
        owner.AddComponent(body);

        Assert.True(physics.Activate(body));
        physics.Update(0);
        Assert.Equal(new(10f, 20f), owner.Position);

        body.Deactivate();
        Assert.True(physics.Deactivate(body));
        owner.Position = new(40f, 50f);

        body.Activate();
        Assert.True(physics.Activate(body));
        physics.Update(0);

        Assert.Equal(new(40f, 50f), owner.Position);
    }

    /// <summary>
    /// Verifies scene deactivation unregisters physics participants while retaining the world for reactivation.
    /// </summary>
    [Fact]
    public void SceneDeactivation_unregistersPhysicsParticipantsAndRetainsWorld()
    {
        var eventHub = new EventHub();
        var physics = new PhysicsSystem(eventHub);
        physics.Initialize();
        var world = physics.CreateWorld2D();
        var body = new PhysicsBody2D { WorldId = world.Id };
        var collider = new PhysicsCollider2D
        {
            WorldId = world.Id,
            Shape = new CircleShape2D(1f),
        };
        var owner = new GameObject2D();
        owner.AddComponent(body);
        owner.AddComponent(collider);
        var scene = new Scene(NodeId.New()) { MainCamera = new StaticCamera() };
        scene.Children.Add(owner);
        var gameSystem = new GameSystem(
            eventHub,
            NullLogger<GameSystem>.Instance
        );
        gameSystem.LoadScene(scene);

        gameSystem.Initialize();
        gameSystem.Update(0);
        eventHub.Drain();

        Assert.True(body.IsActivated);
        Assert.True(collider.IsActivated);
        Assert.False(physics.RemoveWorld(world));

        gameSystem.CurrentScene = null;
        eventHub.Drain();

        Assert.False(body.IsActivated);
        Assert.False(collider.IsActivated);
        Assert.False(physics.Deactivate(body));
        Assert.False(physics.Deactivate(collider));
        Assert.Contains(world, physics.Worlds);

        gameSystem.LoadScene(scene);
        gameSystem.Update(0);
        eventHub.Drain();

        Assert.True(body.IsActivated);
        Assert.True(collider.IsActivated);
        Assert.False(physics.RemoveWorld(world));
    }

    /// <summary>Verifies a fast body stops at the earliest swept rectangle contact.</summary>
    [Fact]
    public void SweptContact_stopsBodyAndPublishesNormal()
    {
        var eventHub = new EventHub();
        var physics = new PhysicsSystem(eventHub);
        physics.Initialize();
        var world = physics.CreateWorld2D();
        var movingOwner = new GameObject2D { Position = new(0f, 0f), Scale = new(1f, 1f) };
        var movingBody = new PhysicsBody2D
        {
            WorldId = world.Id,
            Velocity = new(100f, 0f),
        };
        var movingCollider = new PhysicsCollider2D
        {
            WorldId = world.Id,
            Shape = new RectangleShape2D(new(1f, 1f), new(0.5f, 0.5f)),
        };
        movingOwner.AddComponent(movingBody);
        movingOwner.AddComponent(movingCollider);

        var targetOwner = new GameObject2D { Position = new(5f, 0f), Scale = new(1f, 1f) };
        var targetCollider = new PhysicsCollider2D
        {
            WorldId = world.Id,
            Shape = new RectangleShape2D(new(1f, 1f), new(0.5f, 0.5f)),
        };
        targetOwner.AddComponent(targetCollider);
        var recorder = new CollisionRecorder();
        eventHub.Register(recorder);

        Assert.True(physics.Activate(movingBody));
        Assert.True(physics.Activate(movingCollider));
        Assert.True(physics.Activate(targetCollider));
        physics.Update(0.1d);
        eventHub.Drain();

        Assert.NotNull(recorder.Event);
        Assert.NotNull(recorder.Event!.Contact);
        Assert.Equal(4f, movingOwner.Position.X, 3);
        Assert.Equal(0f, movingBody.Velocity.X);
        Assert.Same(movingBody, recorder.Event.Contact.Value.Body);
        Assert.Equal(new(-1f, 0f), recorder.Event.Contact.Value.Normal);
        Assert.Equal(0.4f, recorder.Event.Contact.Value.Fraction, 3);
    }

    /// <summary>Verifies a sweep stops at entry even when the integrated pose ends inside the target.</summary>
    [Fact]
    public void SweptContact_stopsAtEntryWhenBodyEndsInsideTarget()
    {
        var (eventHub, physics, world, recorder) = CreatePhysicsContext();
        var movingOwner = new GameObject2D { Position = new(0f, 0f) };
        var movingBody = new PhysicsBody2D { WorldId = world.Id, Velocity = new(45f, 0f) };
        movingOwner.AddComponent(movingBody);
        var movingCollider = AddUnitRectangleCollider(movingOwner, world.Id);

        var targetOwner = new GameObject2D { Position = new(5f, 0f) };
        var targetCollider = AddUnitRectangleCollider(targetOwner, world.Id);

        ActivatePhysics(physics, movingBody, movingCollider, targetCollider);
        physics.Update(0.1d);
        eventHub.Drain();

        var collision = Assert.Single(recorder.Events);
        Assert.NotNull(collision.Contact);
        Assert.Equal(4f, movingOwner.Position.X, 3);
        Assert.Equal(0f, movingBody.Velocity.X);
        Assert.Equal(45f, collision.Contact.Value.IncomingVelocity.X);
        Assert.Equal(8f / 9f, collision.Contact.Value.Fraction, 3);
        Assert.Equal(new(-1f, 0f), collision.Contact.Value.Normal);
    }

    /// <summary>Verifies only the nearest swept target stops a moving body.</summary>
    [Fact]
    public void SweptContact_resolvesNearestOfMultipleTargets()
    {
        var (eventHub, physics, world, recorder) = CreatePhysicsContext();
        var movingOwner = new GameObject2D { Position = new(0f, 0f) };
        var movingBody = new PhysicsBody2D { WorldId = world.Id, Velocity = new(100f, 0f) };
        movingOwner.AddComponent(movingBody);
        var movingCollider = AddUnitRectangleCollider(movingOwner, world.Id);

        var fartherOwner = new GameObject2D { Position = new(8f, 0f) };
        var fartherCollider = AddUnitRectangleCollider(fartherOwner, world.Id);
        var nearerOwner = new GameObject2D { Position = new(5f, 0f) };
        var nearerCollider = AddUnitRectangleCollider(nearerOwner, world.Id);

        ActivatePhysics(physics, movingBody, movingCollider, fartherCollider, nearerCollider);
        physics.Update(0.1d);
        eventHub.Drain();

        var collision = Assert.Single(recorder.Events);
        Assert.NotNull(collision.Contact);
        Assert.Equal(4f, movingOwner.Position.X, 3);
        Assert.Contains(
            new[] { collision.First.Owner, collision.Second.Owner },
            owner => ReferenceEquals(owner, nearerOwner)
        );
        Assert.DoesNotContain(
            new[] { collision.First.Owner, collision.Second.Owner },
            owner => ReferenceEquals(owner, fartherOwner)
        );
    }

    /// <summary>Verifies equal-fraction contacts select the lowest target collider ID.</summary>
    [Fact]
    public void SweptContact_breaksEqualFractionTiesByTargetColliderId()
    {
        var (eventHub, physics, world, recorder) = CreatePhysicsContext();
        var movingOwner = new GameObject2D { Position = new(0f, 0f) };
        var movingBody = new PhysicsBody2D { WorldId = world.Id, Velocity = new(100f, 0f) };
        movingOwner.AddComponent(movingBody);
        var movingCollider = AddUnitRectangleCollider(movingOwner, world.Id);

        var firstTargetOwner = new GameObject2D { Position = new(5f, 0f) };
        var firstTargetCollider = AddUnitRectangleCollider(firstTargetOwner, world.Id);
        var secondTargetOwner = new GameObject2D { Position = new(5f, 0f) };
        var secondTargetCollider = AddUnitRectangleCollider(secondTargetOwner, world.Id);
        var expectedTarget = firstTargetCollider.Id.Value < secondTargetCollider.Id.Value
            ? firstTargetOwner
            : secondTargetOwner;

        ActivatePhysics(physics, movingBody, movingCollider, firstTargetCollider, secondTargetCollider);
        physics.Update(0.1d);
        eventHub.Drain();

        var collision = Assert.Single(recorder.Events, message =>
            ReferenceEquals(message.First.Owner, movingOwner)
            || ReferenceEquals(message.Second.Owner, movingOwner)
        );
        Assert.NotNull(collision.Contact);
        Assert.Contains(
            new[] { collision.First.Owner, collision.Second.Owner },
            owner => ReferenceEquals(owner, expectedTarget)
        );
    }

    /// <summary>Verifies a body touching a target while departing receives no swept contact.</summary>
    [Fact]
    public void SweptContact_doesNotStopBodyDepartingFromBoundary()
    {
        var (eventHub, physics, world, recorder) = CreatePhysicsContext();
        var movingOwner = new GameObject2D { Position = new(4f, 0f) };
        var movingBody = new PhysicsBody2D { WorldId = world.Id, Velocity = new(-10f, 0f) };
        movingOwner.AddComponent(movingBody);
        var movingCollider = AddUnitRectangleCollider(movingOwner, world.Id);
        var targetOwner = new GameObject2D { Position = new(5f, 0f) };
        var targetCollider = AddUnitRectangleCollider(targetOwner, world.Id);

        ActivatePhysics(physics, movingBody, movingCollider, targetCollider);
        physics.Update(0.1d);
        eventHub.Drain();

        Assert.Empty(recorder.Events);
        Assert.Equal(3f, movingOwner.Position.X, 3);
        Assert.Equal(-10f, movingBody.Velocity.X);
    }

    /// <summary>Verifies zero displacement does not produce a swept contact.</summary>
    [Fact]
    public void SweptContact_doesNotReportZeroMotion()
    {
        var (eventHub, physics, world, recorder) = CreatePhysicsContext();
        var movingOwner = new GameObject2D { Position = new(0f, 0f) };
        var movingBody = new PhysicsBody2D { WorldId = world.Id };
        movingOwner.AddComponent(movingBody);
        var movingCollider = AddUnitRectangleCollider(movingOwner, world.Id);
        var targetOwner = new GameObject2D { Position = new(5f, 0f) };
        var targetCollider = AddUnitRectangleCollider(targetOwner, world.Id);

        ActivatePhysics(physics, movingBody, movingCollider, targetCollider);
        physics.Update(0.1d);
        eventHub.Drain();

        Assert.Empty(recorder.Events);
        Assert.Equal(new(0f, 0f), movingOwner.Position);
        Assert.Equal(default, movingBody.Velocity);
    }

    /// <summary>Verifies initial penetration remains an ordinary overlap without swept contact data.</summary>
    [Fact]
    public void InitialPenetration_isReportedAsOverlapWithoutContact()
    {
        var (eventHub, physics, world, recorder) = CreatePhysicsContext();
        var movingOwner = new GameObject2D { Position = new(4.5f, 0f) };
        var movingBody = new PhysicsBody2D { WorldId = world.Id, Velocity = new(10f, 0f) };
        movingOwner.AddComponent(movingBody);
        var movingCollider = AddUnitRectangleCollider(movingOwner, world.Id);
        var targetOwner = new GameObject2D { Position = new(5f, 0f) };
        var targetCollider = AddUnitRectangleCollider(targetOwner, world.Id);

        ActivatePhysics(physics, movingBody, movingCollider, targetCollider);
        physics.Update(0.1d);
        eventHub.Drain();

        var collision = Assert.Single(recorder.Events);
        Assert.Null(collision.Contact);
        Assert.Equal(5.5f, movingOwner.Position.X, 3);
        Assert.Equal(10f, movingBody.Velocity.X);
    }

    /// <summary>Verifies unsupported mesh geometry uses overlap detection rather than bounds sweeps.</summary>
    [Fact]
    public void SweptContact_preservesExactOverlapForUnsupportedMeshGeometry()
    {
        var (eventHub, physics, world, recorder) = CreatePhysicsContext();
        var movingOwner = new GameObject2D { Position = new(0f, 0f) };
        var movingBody = new PhysicsBody2D { WorldId = world.Id, Velocity = new(22f, 0f) };
        var movingCollider = new PhysicsCollider2D
        {
            WorldId = world.Id,
            Shape = new TriangleMeshShape2D([
                new(new(0f, 0f), new(2f, 0f), new(0f, 2f)),
            ]),
        };
        movingOwner.AddComponent(movingBody);
        movingOwner.AddComponent(movingCollider);

        var targetOwner = new GameObject2D
        {
            Position = new(3.95f, 1.85f),
            Scale = new(0.1f, 0.1f),
        };
        var targetCollider = AddUnitRectangleCollider(targetOwner, world.Id);

        ActivatePhysics(physics, movingBody, movingCollider, targetCollider);
        physics.Update(0.1d);
        eventHub.Drain();

        Assert.Empty(recorder.Events);
        Assert.Equal(2.2f, movingOwner.Position.X, 3);
        Assert.Equal(22f, movingBody.Velocity.X);
    }

    /// <summary>Verifies a rotated rectangle remains on exact overlap detection instead of a bounds sweep.</summary>
    [Fact]
    public void SweptContact_preservesExactOverlapForRotatedRectangles()
    {
        var (eventHub, physics, world, recorder) = CreatePhysicsContext();
        var movingOwner = new GameObject2D
        {
            Position = new(0f, 0f),
            Rotation = MathF.PI / 4f,
        };
        var movingBody = new PhysicsBody2D { WorldId = world.Id, Velocity = new(2f, 0f) };
        var movingCollider = new PhysicsCollider2D
        {
            WorldId = world.Id,
            Shape = new RectangleShape2D(new(2f, 0.2f), new(1f, 0.1f)),
        };
        movingOwner.AddComponent(movingBody);
        movingOwner.AddComponent(movingCollider);

        var targetOwner = new GameObject2D
        {
            Position = new(1.42f, 1.53f),
            Scale = new(0.02f, 0.02f),
        };
        var targetCollider = AddUnitRectangleCollider(targetOwner, world.Id);

        ActivatePhysics(physics, movingBody, movingCollider, targetCollider);
        physics.Update(0.1d);
        eventHub.Drain();

        Assert.Empty(recorder.Events);
        Assert.Equal(0.2f, movingOwner.Position.X, 3);
        Assert.Equal(2f, movingBody.Velocity.X);
    }

    /// <summary>Verifies moving targets do not participate in static-target sweeps.</summary>
    [Fact]
    public void SweptContact_ignoresMovingTargets()
    {
        var (eventHub, physics, world, recorder) = CreatePhysicsContext();
        var movingOwner = new GameObject2D { Position = new(0f, 0f) };
        var movingBody = new PhysicsBody2D { WorldId = world.Id, Velocity = new(100f, 0f) };
        movingOwner.AddComponent(movingBody);
        var movingCollider = AddUnitRectangleCollider(movingOwner, world.Id);

        var targetOwner = new GameObject2D { Position = new(5f, 0f) };
        var targetBody = new PhysicsBody2D { WorldId = world.Id };
        targetOwner.AddComponent(targetBody);
        var targetCollider = AddUnitRectangleCollider(targetOwner, world.Id);

        ActivatePhysics(physics, movingBody, movingCollider, targetBody, targetCollider);
        physics.Update(0.1d);
        eventHub.Drain();

        Assert.Empty(recorder.Events);
        Assert.Equal(10f, movingOwner.Position.X, 3);
        Assert.Equal(100f, movingBody.Velocity.X);
    }

    /// <summary>Verifies a bodyless target moved externally is not treated as static for that step.</summary>
    [Fact]
    public void SweptContact_ignoresTargetsMovedBetweenSteps()
    {
        var (eventHub, physics, world, recorder) = CreatePhysicsContext();
        var movingOwner = new GameObject2D { Position = new(0f, 0f) };
        var movingBody = new PhysicsBody2D { WorldId = world.Id };
        movingOwner.AddComponent(movingBody);
        var movingCollider = AddUnitRectangleCollider(movingOwner, world.Id);
        var targetOwner = new GameObject2D { Position = new(8f, 0f) };
        var targetCollider = AddUnitRectangleCollider(targetOwner, world.Id);

        ActivatePhysics(physics, movingBody, movingCollider, targetCollider);
        physics.Update(0d);
        eventHub.Drain();
        targetOwner.Position = new(5f, 0f);
        movingBody.Velocity = new(100f, 0f);

        physics.Update(0.1d);
        eventHub.Drain();

        Assert.Empty(recorder.Events);
        Assert.Equal(10f, movingOwner.Position.X, 3);
        Assert.Equal(100f, movingBody.Velocity.X);
    }

    /// <summary>Verifies reciprocal collision masks suppress overlaps and swept contacts.</summary>
    [Fact]
    public void CollisionMask_excludesPairFromOverlapAndSweepContacts()
    {
        var eventHub = new EventHub();
        var physics = new PhysicsSystem(eventHub);
        var world = physics.CreateWorld2D();
        var movingOwner = new GameObject2D { Position = new(0f, 0f) };
        var movingBody = new PhysicsBody2D
        {
            WorldId = world.Id,
            Velocity = new(100f, 0f),
        };
        var movingCollider = new PhysicsCollider2D
        {
            WorldId = world.Id,
            Shape = new RectangleShape2D(new(1f, 1f), new(0.5f, 0.5f)),
            CollisionCategory = 2,
            CollisionMask = ~1u,
        };
        movingOwner.AddComponent(movingBody);
        movingOwner.AddComponent(movingCollider);

        var targetOwner = new GameObject2D { Position = new(0f, 0f) };
        var targetCollider = new PhysicsCollider2D
        {
            WorldId = world.Id,
            Shape = new RectangleShape2D(new(1f, 1f), new(0.5f, 0.5f)),
            CollisionCategory = 1,
            CollisionMask = ~2u,
        };
        targetOwner.AddComponent(targetCollider);
        var recorder = new CollisionRecorder();
        eventHub.Register(recorder);

        Assert.True(physics.Activate(movingBody));
        Assert.True(physics.Activate(movingCollider));
        Assert.True(physics.Activate(targetCollider));
        physics.Update(0.1d);
        eventHub.Drain();

        Assert.Null(recorder.Event);
        Assert.Equal(10f, movingOwner.Position.X, 3);
        Assert.Equal(100f, movingBody.Velocity.X);
    }

    /// <summary>Creates a physics world with an event recorder.</summary>
    /// <returns>The event hub, physics system, world, and collision recorder.</returns>
    private static (
        EventHub EventHub,
        PhysicsSystem Physics,
        PhysicsWorld2D World,
        CollisionRecorder Recorder
    ) CreatePhysicsContext()
    {
        var eventHub = new EventHub();
        var physics = new PhysicsSystem(eventHub);
        var world = physics.CreateWorld2D();
        var recorder = new CollisionRecorder();
        eventHub.Register(recorder);
        return (eventHub, physics, world, recorder);
    }

    /// <summary>Adds a unit rectangle collider to a game object.</summary>
    /// <param name="owner">The collider's owner.</param>
    /// <param name="worldId">The owning physics world.</param>
    /// <returns>The attached collider.</returns>
    private static PhysicsCollider2D AddUnitRectangleCollider(
        GameObject2D owner,
        PhysicsWorldId worldId
    )
    {
        var collider = new PhysicsCollider2D
        {
            WorldId = worldId,
            Shape = new RectangleShape2D(new(1f, 1f), new(0.5f, 0.5f)),
        };
        owner.AddComponent(collider);
        return collider;
    }

    /// <summary>Registers physics components with their test physics system.</summary>
    /// <param name="physics">The physics system under test.</param>
    /// <param name="components">The components to activate.</param>
    private static void ActivatePhysics(PhysicsSystem physics, params IPhysicsComponent[] components)
    {
        foreach (var component in components)
            Assert.True(physics.Activate(component));
    }

    /// <summary>Collects physics collision events dispatched by the test event hub.</summary>
    private sealed class CollisionRecorder
    {
        /// <summary>Gets all collision events received by the recorder.</summary>
        public List<PhysicsCollisionEvent> Events { get; } = [];

        /// <summary>Gets the most recently received collision event.</summary>
        public PhysicsCollisionEvent? Event => Events.LastOrDefault();

        /// <summary>Records a collision event.</summary>
        /// <param name="message">The event to record.</param>
        public void Handle(PhysicsCollisionEvent message) => Events.Add(message);
    }
}
