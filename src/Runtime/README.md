# Nexus.Runtime

Runtime composes engine services and coordinates application startup, update, rendering, and shutdown using configuration and dependency injection.

## Main types

- `Application` / `IApplication`: application execution and service-provider ownership.
- `RuntimeBuilder` / `IRuntimeBuilder`: add configuration and services, then construct an `INexusRuntime`.
- `NexusRuntime`: initialize participating systems and handle window update/render callbacks.
- `ApplicationSettings`, `RunMode`, and `ExitReason`: application configuration and execution state.
- `AddNexusServices`: compose the standard systems; `AddRuntimeServices`: register runtime orchestration separately.

## Current orchestration

At startup, NexusRuntime resolves `Game:StartSceneId` through `ISceneRegistry`, or selects the sole registered scene when the setting is absent, then loads it with `GameSystem.LoadScene` before initializing Input, Physics, Audio, Graphics, GUI, and GameSystem. Multiple registered scenes require `Game:StartSceneId`. On update, the runtime drains queued events once, then updates GameSystem, Physics, GUI, Audio, and Input. Physics collision results and gameplay feedback are delivered by the next update's drain. Graphics has no update phase; `graphics.Render` consumes the latest completed update state. Configured frame-count or elapsed-time limits can close the window.

Default composition registers Vulkan when an `IGraphicsSystem` has not already been supplied. `RuntimeBuilder.UseVulkan` currently returns the builder without additional configuration; `UseOpenGL` throws `NotImplementedException`.

## Dependencies and boundaries

The .NET 10 project references Audio, Core, Game, GUI, Graphics, OpenGL, Vulkan, Input, and Physics. It uses Microsoft configuration, logging, options, DI, and Silk.NET input/windowing.

Runtime owns composition and scheduling; individual systems retain their own policy. Runtime/GameSystem traversal ownership and the baseline's intended end-of-update input ordering still need reconciliation with code. Network and Platform are not direct project references here.

## Development

```sh
dotnet build src/Runtime/Nexus.Runtime.csproj
```

Run from the repository root. See [runtime tests](../../tests/UnitTests/Runtime), [HelloNexus](../../tests/HelloNexus), [Testing](../Testing/README.md), and the [architecture baseline](../../README.md). The build command was not executed for this documentation change.
