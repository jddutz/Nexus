# Nexus.Physics

Physics defines a system/component integration surface for simulation. Its current default implementation is a scaffold.

## Main types and status

- `IPhysicsSystem` defines initialization, update, and component activation/deactivation.
- `PhysicsSystem.Initialize` registers the system with EventHub.
- `PhysicsSystem.Update` is empty; activation and deactivation currently return `false`.
- `Components/IPhysicsComponent` defines the physics-component boundary.
- `AddPhysicsServices` registers the default implementation.

The source does not establish a working collision detector, rigid-body solver, or simulation backend. These responsibilities and their interfaces remain to be defined.

## Dependencies and integration

The .NET 10 project references [Core](../Core/README.md) and declares Microsoft DI, Silk.NET.Maths, and Silk.NET.Windowing packages. [Runtime](../Runtime/README.md) initializes Physics and updates it after Game. Game-specific simulation rules belong in game code; engine-level physical simulation belongs here.

## Development

```sh
dotnet build src/Physics/Nexus.Physics.csproj
```

Run from the repository root. The build command was not executed for this documentation change. See the [architecture baseline](../../README.md) for the outstanding physics-contract and scheduling questions.
