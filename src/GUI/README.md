# Nexus.GUI

GUI provides screen-space elements, measurement and arrangement, focus management, and input routing. Elements express visual output through graphics components.

## Main types and behavior

- `IElement` and `Element` define element hierarchy, sizing, alignment, visibility, enabled state, and layout behavior.
- `ImageElement`, `TextElement`, and `TextButton` provide image, text, and button presentation.
- `GraphicalUserInterface` tracks elements through lifecycle events, invalidates layout, and measures/arranges active layout roots using the main window size.
- `FocusDirection`, alignment enums, `SizingMode`, and `TextAlignment` represent GUI-specific policy.
- `TextElement` and `TextButton` delegate text measurement, fitting, and drawable preparation to Graphics' `TextComponent`.

Layout work is driven by invalidation. `GraphicalUserInterface.Update` skips layout when there is no scene, window service, or pending invalidation. Focus targets must be active, registered, visible, enabled, and focusable. GUI interprets input using its own geometry and interaction state.

The rectangle passed to `Arrange` is an allocation; `Bounds` is the hit-test rectangle. Text elements use rendered glyph bounds, images use their clipped image rectangle, and text buttons use their full button rectangle. Hidden elements have zero-size hit-test bounds while retaining their allocation for restoration.

Drawable elements create their renderer components with the element and retain them while hidden or unrenderable. Each renderer's `IsVisible` reflects effective element visibility and valid drawable geometry.

## Dependencies and boundaries

The .NET 10 project references [Core](../Core/README.md), [Graphics](../Graphics/README.md), [Input](../Input/README.md), and the source-generator analyzer. `AddNexusGui` registers GUI services.

GUI owns layout and interaction; Input supplies device state and events, and the rendering backend consumes graphics data. GUI enums must not become Graphics or backend dependencies. Precise parent/view-relative rectangle conventions remain an open architecture question.

## Development

```sh
dotnet build src/GUI/Nexus.GUI.csproj
```

Run from the repository root. [GUI tests](../../tests/UnitTests/GUI) cover elements, text, images, buttons, and GUI coordination. See the [architecture baseline](../../README.md) for accepted layout and input decisions.
