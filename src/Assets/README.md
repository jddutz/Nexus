# Nexus.Assets

Assets supplies managed font parsing, geometry conversion, MSDF generation, atlas packing, and the font data consumed by text rendering. It is a reusable library; [AssetPipeline](../AssetPipeline/README.md) provides the command-line content workflow.

## Main areas

- `Fonts/`: `IFontBuilder`, `FontBuilder`, `FontProcessor`, `FontDefinition`, `FontBuildResult`, metrics, glyph bounds, kerning pairs, and atlas writing.
- `Typography/FontReader/`: font-domain types and TrueType/SFNT parsing, including character maps, outlines, metrics, legacy kerning, and selected GPOS pair positioning.
- `Typography/Geometry/`: conversion to line and quadratic curve segments and geometry distance calculations.
- `Typography/DistanceFields/`: glyph-level MSDF bitmap generation and settings.
- `Typography/Atlas/`: atlas construction and placement results.

`FontBuilder` composes parsing, repertoire selection, geometry conversion, distance-field generation, and packing into a `FontBuildResult`. Graphics `TextStyle` consumes that result together with an atlas texture. Accepting an `.otf` filename does not imply support for every OpenType outline or shaping feature.

## Dependencies and boundaries

The .NET 10 project declares no package or project references. Font generation uses managed code and does not require a native font generator or graphics backend. Font data and atlas generation belong here; GPU upload and visual styling belong to consumers.

The [Fonts guide](Fonts/README.md) and [Typography guide](Typography/README.md) provide detailed contracts and supported parsing scope. Those guides include runtime-generation discussion that differs from the root baseline's offline-font decision; font generation/loading ownership remains to be reconciled. The code's capabilities should not be mistaken for an accepted runtime policy.

## Development

```sh
dotnet build src/Assets/Nexus.Assets.csproj
```

Run from the repository root. See [font tests](../../tests/UnitTests/AssetPipeline/Fonts), [typography tests](../../tests/UnitTests/AssetPipeline/Typography), and the [architecture baseline](../../README.md). The build command was not executed for this documentation change.
