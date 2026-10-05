# Nexus.Physics

Physics defines the system/component integration surface for simulation. `PhysicsSystem` owns explicitly created 2D simulation worlds and is the only engine service that registers active physics components or advances the simulation.

## Main types and status

- `IPhysicsSystem` defines initialization, update, world creation/removal, and component activation/deactivation.
- `PhysicsSystem` subscribes to component lifecycle events, resolves each component's explicit `WorldId`, routes supported components to the registered `PhysicsWorld2D`, advances every owned world once per update, and queues `PhysicsCollisionEvent` results. World removal is rejected while participants remain registered.
- `PhysicsWorld2D` is an isolated stateful 2D simulation context. It owns body/collider membership, integrates registered bodies exactly once per step, resolves earliest translating axis-aligned rectangle sweeps against unchanged bodyless targets, and performs exact overlap detection for all supported shapes. Equal-fraction candidates for one body select the lowest target collider ID, then the lowest moving collider ID.
- `PhysicsSystem.CreateWorld2D()` creates and registers a usable world; `RemoveWorld()` removes only empty worlds. The system exposes owned worlds through a cached read-only collection.
- `PhysicsBody2D` and `PhysicsCollider2D` are lifecycle-managed physics components. Collider categories and reciprocal masks filter pairs before overlap and swept-contact detection. Their authored/runtime state is consumed by the system; game code does not register them with a world or call integration directly.
- `PhysicsCollisionEvent` reports a final-pose overlap or swept-contact pair for one completed physics step; swept pairs may not overlap at the final pose. Optional `PhysicsCollisionContact` data identifies the stopped body, contact normal, incoming velocity, and step fraction. It is not a collision-enter notification, and the two collider properties have no meaningful ordering.
- `IPhysicsShape2D` describes local-space geometry. The initial shapes are `CircleShape2D`, `RectangleShape2D`, `CapsuleShape2D`, `ConvexPolygonShape2D`, and `TriangleMeshShape2D`; collision algorithms remain in the physics layer.
- Replacing an activated collider's `Shape` raises its direct `ShapeChanged` subscription. PhysicsWorld2D uses that notification to refresh cached local geometry; shape changes are not global EventHub events.
- `Components/IPhysicsComponent` defines the physics-component boundary.
- `AddPhysicsServices` registers the default implementation.

The default implementation provides CPU 2D integration, limited axis-aligned rectangle swept-contact correction, and existing overlap detection. Initial penetration and unsupported sweep cases remain ordinary end-of-step overlap notifications without contact data. Game code owns contact response; moving-target sweeps and a 3D world remain future boundaries.

## Dependencies and integration

The .NET 10 project references [Core](../Core/README.md) and declares Microsoft DI and Silk.NET.Maths packages. Physics components require an `IGameObject2D` owner; collider geometry follows that owner's world transform. Body velocity, acceleration, and simulated pose are world-space values. On each step, the world integrates the pose and derives the owner's local transform from the final parent transform. Simulated bodies support translation and rotation beneath rigid parents; scale, shear, and non-invertible parents are rejected. Use `Teleport` for explicit external pose changes. Physics excludes collider pairs with the same owner, supports stationary colliders without bodies, and delivers overlap events through the next normal EventHub drain. [Runtime](../Runtime/README.md) initializes Physics and updates it after Game. Game-specific simulation rules belong in game code; engine-level physical simulation belongs here.

## Development

```sh
dotnet build src/Physics/Nexus.Physics.csproj
```

Run from the repository root. The build command was not executed for this documentation change. See the [architecture baseline](../../README.md) for the outstanding physics-contract and scheduling questions.
