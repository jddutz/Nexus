# Nexus.Game

Game owns concrete scenes, scene registration, and the game-system lifecycle coordinator. Shared game-object and component contracts and implementations live in [Core](../Core/README.md).

## Main types and behavior

- `Scene` implements a scene root; `View` represents game-side view functionality.
- `ISceneRegistry` and `SceneRegistry` provide named scene registration and loading.
- `IGameSystem` and `GameSystem` manage the current scene and object/component activation and deactivation.
- `GameSettings` accepts an optional initial scene name through the `Game` configuration section.

`GameSystem.Initialize` loads the configured scene when `Game:StartSceneId` is supplied. Otherwise it uses the only registered scene, fails when none are registered, or requires configuration and lists available names when multiple scenes are registered. It does not fall back if the selected scene fails to construct. Updates use a parent-first lifecycle snapshot, check captured ownership before processing an entry, and initialize/activate eligible entities before updating them. Activation events and event-handler registration connect objects and components to other systems. Changes during traversal require careful ownership checks.

## Dependencies and boundaries

The .NET 10 project currently references Audio, Assets, Core, Graphics, Input, Physics, and GUI, plus the source-generator analyzer. `AddGameServices` registers the system and its configuration.

These are current project references, not confirmation of the proposed module policy. In particular, the root README proposes game-model contracts independent of GUI; shared contracts already reside in Core, while this project includes orchestration and references GUI. Dependency cleanup and scheduling ownership remain architectural questions. Game-specific campaign, faction, and simulation rules belong in consuming game code.

## Development

```sh
dotnet build src/Game/Nexus.Game.csproj
```

Run from the repository root. Relevant coverage is in [Game tests](../../tests/UnitTests/Game). Read the [lifecycle specification](../../docs/Game%20System%20Lifecycle.md) and [architecture baseline](../../README.md) before changing traversal or lifecycle delegation. The build command was not executed for this documentation change.
