# Embedded Aileron font data

Each face is generated directly from `RasterizerInput` in `.content/content-manifest.json`.
The C# files construct the existing font records, preserving coordinates, glyph and
contour order, nullable bounds, empty outlines, and the exported kerning list.
They contain no source font paths. Each face initializes lazily when requested.

Regenerate from the repository root after exporting rasterizer input through NAP:

```powershell
dotnet run --project src/AssetPipeline/Nexus.AssetPipeline.csproj -- --output src/Graphics/Text/Generated generate-fonts --manifest .content/content-manifest.json
```

`BuiltInFonts.TryGetRasterizerInput` resolves these records by content identifier.
`TextStyleRegistry` passes them to the decoded-input overload of `IFontBuilder.Build`,
which uses the same distance-field generation, atlas packing, and metadata assembly
as file-based fonts. Existing quadratic curves are reconstructed directly without
another curve conversion.

Exported generation settings are defaults. Callers can override the generation
size, distance range, and padding. Text styles use their requested generation size
and retain the embedded distance-range and padding defaults. The source OTF and
manifest are required only for regeneration, not for runtime font construction.
