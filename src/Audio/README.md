# Nexus.Audio

Audio defines the engine's audio-system and audio-component integration surface. The current default implementation is a scaffold.

## Main types and status

- `IAudioSystem` defines initialization, update, and component activation/deactivation.
- `AudioSystem.Initialize` registers the system with EventHub.
- `AudioSystem.Update` is empty; activation and deactivation currently return `false`.
- `IAudioSource` and `Components/IAudioComponent` provide source/component contracts.
- `AddAudioServices` registers the default implementation.

This project does not currently demonstrate a playback backend, mixing, decoding, or working audio component management. Those contracts and capabilities must be specified and implemented before audio support is claimed.

## Dependencies and integration

The .NET 10 project references [Core](../Core/README.md) and declares Microsoft DI, Silk.NET.Maths, and Silk.NET.Windowing packages. [Runtime](../Runtime/README.md) initializes and updates Audio; game-specific audio choices belong in game code.

## Development

```sh
dotnet build src/Audio/Nexus.Audio.csproj
```

Run from the repository root. The build command was not executed for this documentation change. See the [architecture baseline](../../README.md); audio's detailed public contracts and integration points remain open design work.
