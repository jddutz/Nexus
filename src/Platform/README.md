# Nexus.Platform

Platform is currently a placeholder for platform-specific services.

## Current contents

- `PlatformType` lists Windows, Linux, macOS, Android, and iOS.
- `Class1` is an empty placeholder class.
- `GlobalUsings.cs` and the project file provide project scaffolding.

The enum identifies possible platform categories; it does not certify working support for those platforms. Window contracts currently live in [Graphics](../Graphics/README.md), with implementations in [Vulkan](../Vulkan/README.md) and [OpenGL](../OpenGL/README.md).

## Dependencies and intended boundary

The .NET 10 project declares no package or project references. Runtime does not directly reference it. The root architecture proposes platform services independent of GUI, game-specific policy, and concrete renderer dependencies, but the exact service ownership and migration plan remain to be defined.

## Development

```sh
dotnet build src/Platform/Nexus.Platform.csproj
```

Run from the repository root. The build command was not executed for this documentation change. See the [architecture baseline](../../README.md) for open platform and backend-support questions.
