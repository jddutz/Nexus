# Nexus.GUI

GUI provides screen-space elements, measurement and arrangement, focus management, and input routing. Elements express visual output through graphics components.

## Main types and behavior

- `IElement` and `Element` define element hierarchy, sizing, alignment, visibility, enabled state, and layout behavior.
- `ImageElement`, `TextElement`, and `TextButton` provide image, text, and button presentation.
- `GraphicalUserInterface` tracks elements through lifecycle events, invalidates layout, and measures/arranges active layout roots using the main window size.
- `FocusDirection`, alignment enums, `SizingMode`, and `TextAlignment` represent GUI-specific policy.
- `TextElement` and `TextButton` use Graphics' `TextComponent` for text output; text preparation remains stubbed.

Layout work is driven by invalidation. `GraphicalUserInterface.Update` skips layout when there is no scene, window service, or pending invalidation. Focus targets must be active, registered, visible, enabled, and focusable. GUI interprets input using its own geometry and interaction state.

## Dependencies and boundaries

The .NET 10 project references [Core](../Core/README.md), [Graphics](../Graphics/README.md), [Input](../Input/README.md), and the source-generator analyzer. `AddNexusGui` registers GUI services.

GUI owns layout and interaction; Input supplies device state and events, and the rendering backend consumes graphics data. GUI enums must not become Graphics or backend dependencies. Text layout and drawable output are not yet implemented by Graphics' `TextComponent`. Precise parent/view-relative rectangle conventions remain an open architecture question.

## Development

```sh
dotnet build src/GUI/Nexus.GUI.csproj
```

Run from the repository root. [GUI tests](../../tests/UnitTests/GUI) cover elements, text, images, buttons, and GUI coordination. See the [architecture baseline](../../README.md) for accepted layout and input decisions.
