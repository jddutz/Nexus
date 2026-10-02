# Nexus.Input

Input adapts Silk.NET.Input devices into engine device contracts, state snapshots, and events. GUI and game code interpret those events according to their own interaction rules.

## Main areas

- `IInputSystem` and `InputSystem` expose keyboard/mouse state and connected controllers, manage device subscriptions, and publish device events.
- `IInputAdapter` and `InputAdapter` isolate the underlying input context and update it.
- `Devices/` contains keyboard, mouse, controller, button, analog, and touch-screen contracts, identifiers, and controller profiles.
- `Events/` contains connection, disconnection, key, pointer, wheel, button, and controller analog events.
- `InputMap` and `IGameInputAction` provide input-mapping contracts.

Initialization registers connected devices and connection-change handlers. Updates forward to the input adapter. Disposal releases subscriptions and input resources. A touch-screen interface alone does not establish a working touch implementation.

## Dependencies and integration

The .NET 10 project references [Core](../Core/README.md) and uses Silk.NET.Input, Maths, Windowing, and Microsoft dependency injection. `AddInputServices` registers its services.

Events are delivered through Core's EventHub. The current runtime drains events before system updates and updates Input before GUI; the root baseline describes input updating at the end, so exact ordering needs reconciliation. Input does not own GUI hit testing, focus, or screen geometry.

## Development

```sh
dotnet build src/Input/Nexus.Input.csproj
```

Run from the repository root. See [Input tests](../../tests/UnitTests/Input), the [controller integration guide](../../tests/HelloNexus/ControllerIntegration.md), and the [architecture baseline](../../README.md). The build command was not executed for this documentation change.
