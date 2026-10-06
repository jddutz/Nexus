# Nexus.Game

Game owns concrete scenes, scene registration, and the game-system lifecycle coordinator. Shared game-object and component contracts and implementations live in [Core](../Core/README.md).

## Main types and behavior

- `Scene` implements a scene root; `View` represents game-side view functionality.
- `ISceneRegistry` and `SceneRegistry` discover concrete scenes from the entry assembly by default and provide named scene loading. `SceneAttribute` optionally overrides a scene's class-name-based registration name.
- `IGameSystem` and `GameSystem` manage the current scene and object/component activation and deactivation.
- `GameSettings` accepts the runtime startup scene name through the `Game` configuration section.

`NexusRuntime` resolves `Game:StartSceneId` through `ISceneRegistry`, or uses the only registered scene when the setting is absent, and passes the loaded scene to `GameSystem.LoadScene` before initializing the game system. Multiple registered scenes require an explicit start scene. `SceneRegistry` provides scene factories; `GameSystem.LoadScene` owns scene replacement and lifecycle. GameSystem subscribes to each node's child collections before publishing its activation event; children added beneath active parents are initialized and activated immediately, then updated in the next parent-first lifecycle traversal. Captured-ownership checks prevent changes during traversal from causing duplicate updates or activations.

## Dependencies and boundaries

The .NET 10 project currently references Audio, Assets, Core, Graphics, Input, Physics, and GUI, plus the source-generator analyzer. `AddGameServices` registers the system and its configuration.

These are current project references, not confirmation of the proposed module policy. In particular, the root README proposes game-model contracts independent of GUI; shared contracts already reside in Core, while this project includes orchestration and references GUI. Dependency cleanup and scheduling ownership remain architectural questions. Game-specific campaign, faction, and simulation rules belong in consuming game code.

## Development

```sh
dotnet build src/Game/Nexus.Game.csproj
```

Run from the repository root. Relevant coverage is in [Game tests](../../tests/UnitTests/Game). Read the [lifecycle specification](../../docs/Game%20System%20Lifecycle.md) and [architecture baseline](../../README.md) before changing traversal or lifecycle delegation. The build command was not executed for this documentation change.
