# Nexus.Network

Network currently contains synchronization contracts and data types rather than a working network system or transport implementation.

## Main types and scope

- `INetworkSync` describes state synchronization, property selection, ownership, authority, target clients, conflict resolution, pause/resume, and prediction.
- `NetworkUpdate`, `NetworkStatistics`, and `NetworkValidationResult` represent updates, diagnostics, and validation results.
- Events describe state transfer, connection changes, client membership, ownership, errors, and conflicts.
- Enums describe synchronization mode/direction, reliability, priority, and conflict policy.

These APIs express a proposed capability surface. No concrete transport, synchronization implementation, or runtime registration is present in this folder. Method declarations such as delta updates and prediction do not establish functioning implementations.

## Dependencies and boundaries

The .NET 10 project declares no package or project references. Types currently use the namespace `Nexus.Runtime.Network`, despite the project being named `Nexus.Network`; namespace ownership should be resolved before treating this as a stable API. Runtime does not directly reference this project.

Transport selection, serialization, scheduling, authority/security policy, and integration with scene objects need explicit contracts and validation.

## Development

```sh
dotnet build src/Network/Nexus.Network.csproj
```

Run from the repository root. The build command was not executed for this documentation change. See the [architecture baseline](../../README.md), which leaves Network's integration and dependency hierarchy open.
