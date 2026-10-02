# Nexus.Core

Core contains shared engine contracts and their foundational implementations: scene nodes, game objects, components, identity, events, observability, content lookup, and profiling.

## Main types

- `ISceneNode`, `IScene`, and `IManagedEntity` define hierarchy and lifecycle contracts.
- `GameObject`, `GameObject2D`, `GameObject3D`, and `Component` provide common object and component behavior. Game objects own components separately from their child nodes.
- `IObservable`, `ObservableCollection`, and `[Observable]` support property and collection notifications. The analyzer/source generator lives in [codegen](../../codegen).
- `EventHub` registers handlers, queues events, and drains pending events when the runtime requests it.
- `ContentManifest`, content identifiers, and provider contracts support runtime content lookup.
- `Performance/` contains profiling scopes, samples, reports, and frame profiles.

## Dependencies and integration

The project targets .NET 10. It has no runtime project reference to another engine system; it references the source generator as an analyzer. Package dependencies include Silk.NET.Maths and Microsoft configuration, logging, options, and dependency injection libraries.

`AddCoreServices` registers core services and content configuration. `AddEventHub` can register event infrastructure separately. Higher-level systems consume Core contracts; game rules, GUI layout policy, and concrete graphics resources belong to their owning systems.

## Development

From the repository root:

```sh
dotnet build src/Core/Nexus.Core.csproj
```

Related tests are in [Core tests](../../tests/UnitTests/Core), [game-object tests](../../tests/UnitTests/Game/GameObjectTests.cs), and [source-generator tests](../../tests/SourceGeneratorTests). See [code generation](../../docs/Code%20Generation.md) and the [architecture baseline](../../README.md) for design guidance. Build commands here describe project entry points; they were not executed for this documentation change.
