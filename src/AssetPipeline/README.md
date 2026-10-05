# Nexus Asset Pipeline (NAP)

AssetPipeline is the offline command-line entry point for preparing content and writing `content-manifest.json`. Its executable assembly is named `nap`.

## Current behavior

`Program.cs` defines `build` and `clean`. `Pipeline` reads camel-case YAML definitions in ordinal input-file order. Each definition has a `root` resolved relative to its definition file and an `assets` array.

- Texture entries use `path`, `files`, and optional `groupName`; files are copied and identified by filename without extension.
- Font entries use `contentId` and `source`; `.ttf` or `.otf` source files are copied into `fonts/`. `includeMsdf: true` additionally generates an atlas PNG using default font settings. `includeRasterizerInput: true` adds the source metrics, default repertoire's character-to-glyph mappings, glyph layout metrics and converted contours, generation settings, and supported kerning to the font's manifest entry. It is disabled by default, leaving the existing manifest output unchanged.
- The manifest contains Texture and Font paths and currently empty Geometry and Audio content sections.
- Unsupported asset types are logged and skipped. Build failures return exit code 1.

The current implementation copies source files rather than emitting the general `.content` format described in the root baseline. The manifest's font path points to the copied source font, not a serialized complete `FontBuildResult`.

## Usage

From the repository root, replacing the example paths with your own:

```sh
dotnet run --project src/AssetPipeline/Nexus.AssetPipeline.csproj -- --output ./generated-content build --input ./assets.yaml
dotnet run --project src/AssetPipeline/Nexus.AssetPipeline.csproj -- --output ./generated-content clean
```

`--output` is a required root option; `build` requires one or more `--input` files. **Clean recursively deletes the entire selected output directory.** These command forms follow the current CLI declarations; they were not executed for this documentation change.

Example definition:

```yaml
root: ./source-assets
assets:
  - assetType: font
    contentId: interface
    source: interface.ttf
    includeMsdf: true
  - assetType: texture
    path: images
    files: [button.png]
```

## Dependencies and development

The .NET 10 executable references [Graphics](../Graphics/README.md) and [Assets](../Assets/README.md), with System.CommandLine, YamlDotNet, and StbImageSharp packages. Assets owns managed font generation; this project owns CLI processing, file copying, and manifest registration.

See [pipeline and font tests](../../tests/UnitTests/AssetPipeline/Fonts), the [sample content build script](../../tests/HelloNexus/BuildContentLibrary.ps1), and the [architecture baseline](../../README.md).
