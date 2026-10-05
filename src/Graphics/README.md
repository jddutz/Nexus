# Nexus.Graphics

Graphics describes visual state and rendering data independently of a concrete rendering backend. It supplies graphics components, drawables, cameras, textures, geometry, shader contracts, render layers, and window contracts.

## Main areas

- `Components/`: `IGraphicsComponent`, `TextureComponent`, `NinePatchComponent`, `TextComponent`, and `ViewComponent`.
- `Drawables/`: `IDrawable`, `TexturedQuad`, `NinePatch`, and `TextSpan`. Drawables serialize instance and uniform data into caller-owned buffers through `WriteInstanceDataTo` and `WriteUniformDataTo`.
- `Cameras/`: static, orthographic, and perspective camera implementations and view-frustum data.
- `Geometry/`, `Textures/`, and `Shaders/`: resource descriptions and rendering contracts consumed by backends.
- `Text/`: `ITextStyle` and `TextStyle`, pairing generated font metrics, glyphs, kerning, and MSDF metadata with an atlas texture and visual settings. `BuiltInFonts` provides embedded decoded outlines for all 16 Aileron faces. `TextStyleRegistry` rasterizes these at the requested generation size without a font file or font manifest entry.
- `IGraphicsSystem`, render-layer types, and `IWindowService`: system-level rendering and window abstractions.

## Dependencies and ownership

The .NET 10 project references [Core](../Core/README.md), [Assets](../Assets/README.md), and the source-generator analyzer. It uses Silk.NET math/windowing, Microsoft configuration/DI, and StbImageSharp. `AddGraphicsServices` registers graphics services; the runtime separately supplies a backend implementation.

GUI owns measurement, arrangement, focus, and interaction. Graphics should accept numeric layout parameters without depending on GUI enums. Backends own resource allocation, uploads, bindings, recording, and submission.

## Current status

The public `TextComponent` is unfinished: its drawable collection is empty, span creation returns an empty span, and drawable synchronization is not implemented. GUI's `TextElement` and `TextButton` now use this component, but text layout and rendering are not expected to work until its implementation is completed. Do not treat the intended TextComponent source/output contract as implemented.

## Development

```sh
dotnet build src/Graphics/Nexus.Graphics.csproj
```

Run from the repository root. See [Graphics tests](../../tests/UnitTests/Graphics), [Vulkan](../Vulkan/README.md), and the [architecture baseline](../../README.md). The build command was not executed for this documentation change.
