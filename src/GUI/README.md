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

## Layout performance capture

Enable `Diagnostics.EnablePerformanceMetrics` in the application configuration (enabled in Prelude).
The existing `IPerformanceTelemetry` / `IGraphicsProfiler` collector records these operations in
its snapshot, debug-level structured logs, and .NET metrics (layout logs use Debug to avoid
per-request Information logging during interactive captures):

- `gui.layout.pass`: complete invalidated layout pass, including root discovery.
- `gui.layout.measure` and `gui.layout.arrange`: inclusive time and thread allocations per root type;
  units count roots processed. Nested layout and text work is included in these timings.
- `gui.layout.wait`: elapsed time from the first pending invalidation to the next layout pass.
  This is scheduling latency, not CPU time or time until presentation.
- `gui.layout.invalidate.<reason>`: request counts for scene, activation, resize, modal, and element changes.
- `gui.layout.invalidate.during-pass`: requests raised while measuring or arranging, grouped by element type.

Timing samples include count, total/maximum milliseconds, and allocated bytes. Parent and child
measurements overlap and must not be summed. Invalidation samples have zero duration and allocations;
their count/units indicate activity. Resources use element types rather than individual identities.
Disabled collection reads no clocks. Layout behavior is unchanged.

For a Prelude capture, compare startup, idle, window resizing, and the interaction exhibiting the delay.
Use a fresh collector snapshot (`Reset`) for each interval when collecting programmatically; compare
pass counts, measure versus arrange time, allocations, and invalidation-during-pass counts.
Prelude also enables Vulkan validation, so record those settings alongside results when comparing runs.

To capture structured console logs from the Nexus repository root in PowerShell:

```powershell
dotnet run --project ../NexusRealms/Prelude -- --Logging:LogLevel:Nexus.Graphics.GraphicsProfiler=Debug *> prelude-performance.log
```

This enables verbose collector logs for that capture only; console logging adds overhead, so prefer
aggregate snapshots or a .NET metrics listener when comparing small timing differences.
