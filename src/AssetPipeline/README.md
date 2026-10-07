# Nexus Asset Pipeline (NAP)

AssetPipeline is the offline command-line entry point for preparing content and writing `content-manifest.json`. Its executable assembly is named `nap`.

## Current behavior

`Program.cs` defines `build` and `clean`. `Pipeline` reads camel-case YAML definitions in ordinal input-file order. Each definition has a `root` resolved relative to its definition file and an `assets` array.

- Texture entries use `path`, `files`, and optional `groupName`; files are copied. Grouped texture IDs use `groupName.filename` including the extension (for example, `portraits.anarchist_female.png`). Entries without `groupName` retain filename-stem IDs for compatibility.
  `files` accepts literal filenames and filename wildcards: `*` matches zero or more characters and `?` matches one character. Quote wildcard patterns in YAML, such as `files: ["*.png"]`. A literal subfolder prefix is supported (`"Portraits/*.png"`); directory wildcards and recursive `**` patterns are not supported. Matches are deduplicated and processed in ordinal order. A wildcard matching no files fails the build. Matching uses the operating system's case rules, and paths must stay inside the asset's source folder. Group names namespace texture IDs; use unique filenames within each group. Duplicate texture IDs fail the build instead of overwriting an earlier entry.
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

## Alpha-island atlas import

To enable region extraction with all defaults, add `regions: {}` to a texture asset:

```yaml
assets:
  - assetType: texture
    files: [panels.png]
    regions: {}
```

**No options inside `regions` are required.** The empty mapping `{}` creates the settings with extraction enabled and all defaults. You can also use `regions: { enabled: true }`. Omit `regions` or set `regions: { enabled: false }` to disable extraction; a bare `regions:` is null and does not enable it.

For this minimal texture import, specify `assetType: texture` and `files`. `path` is optional and defaults to the source root; `root` defaults to the YAML file's directory. `groupName` is also optional.

| Region option | Default | Accepted values / behavior |
| --- | --- | --- |
| `enabled` | `true` | Enables extraction when a `regions` mapping is present. |
| `alphaThreshold` | `16` | Integer 0–255; only alpha strictly above this value is occupied. |
| `minimumIslandArea` | `64` | Integer at least 1; minimum occupied pixel count per island. |
| `mergeDistance` | `0` | Nonnegative integer; 0 disables automatic merging. |
| `padding` | `2` | Nonnegative integer; expands bounds by this many pixels on every side. |
| `groups` | `{}` | Optional named lists of candidate indices. |
| `namedBounds` | `{}` | Optional named pixel rectangles. Each rectangle needs positive `width` and `height`; `x` and `y` default to 0. |

Set only the options you want to change. For example, `regions: { padding: 4 }` enables extraction with four pixels of padding and leaves every other option at its default.

The defaults use an alpha threshold of 16, a minimum island area of 64 pixels, and two pixels of padding to suppress faint transparency artifacts and tiny speckles. Review the resulting bounds and lower the threshold or minimum area when importing small or faint assets. Increasing the minimum area alone removes speckles but cannot separate assets joined by faint bridges; increase the alpha threshold to remove those bridges. Padding preserves a margin around the detected bounds without joining regions. These values are a starting point, not a guaranteed asset count for every image.

Full definition with custom settings and overrides:

```yaml
root: ./source-assets
assets:
  - assetType: texture
    path: images
    groupName: UI
    files: [panels.png]
    regions:
      enabled: true
      alphaThreshold: 16
      minimumIslandArea: 8
      mergeDistance: 0
      padding: 2
      groups:
        decoratedPanel: [0, 2]
      namedBounds:
        region-0001: { x: 120, y: 0, width: 100, height: 80 }
```

Alpha must be strictly above `alphaThreshold` (0–255, default 16). Eight-connected occupied pixels form islands; `minimumIslandArea` counts occupied pixels (default 64), filtering before merging. A positive `mergeDistance` joins island bounding rectangles transitively using the maximum horizontal/vertical gap in pixels; zero disables merging. Padding expands the final bounds and clamps them to the image, without influencing detection or grouping.

Candidates are sorted by top then left and named `region-0000`, etc. Group indices refer to this zero-based order after automatic merging and before padding. Groups consume their candidates and create a named union; a candidate may belong to only one group. `namedBounds` replaces a matching candidate/group name or adds a new region, with exact unpadded bounds. For touching shadows, override the detected candidate and add explicit bounds for the other panel. Settings apply independently to every file in the texture asset; use separate entries for per-image overrides. Candidate names can change when source pixels or detection settings change; named overrides provide stable application names.

The texture manifest entry retains `FilePath` and adds `Regions`, keyed by region name. Each region has scalar `Bounds` and `TexCoords` objects with `X`, `Y`, `Width`, and `Height`; coordinates start at the top left. UVs use normalized XYWH, not right/bottom endpoints.

Runtime lookup: `textureRegistry.GetOrCreate(textureContentId).GetRegion("decoratedPanel")` (namespace `Nexus.Graphics.Textures`); the loaded `ITexture.Regions` list contains all regions. Manifest-only lookup remains available as `manifest.GetTextureRegion(textureContentId, "decoratedPanel")`. Assign `region.Bounds` to `ImageElement.SourceRegion`, or assign `new Vector4D<float>(region.TexCoords.Origin.X, region.TexCoords.Origin.Y, region.TexCoords.Size.X, region.TexCoords.Size.Y)` to `SpriteInstance.TexCoord`. Regions are candidates for review, not semantic asset recognition. Nine-patch stretch insets remain explicit metadata.
