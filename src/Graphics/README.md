# Nexus.Graphics

Graphics describes visual state and rendering data independently of a concrete rendering backend. It supplies graphics components, drawables, cameras, textures, geometry, shader contracts, render layers, and window contracts.

## Main areas

- `Components/`: `IRenderer`, `TextureComponent`, `NinePatchComponent`, `TileMapRenderer`, `TextComponent`, and `ViewComponent`.
- `Drawables/`: `IDrawable`, `TexturedQuad`, `NinePatch`, `TileMapDrawable`, and `TextSpan`. Drawables serialize instance and uniform data into caller-owned buffers through `WriteInstanceDataTo` and `WriteUniformDataTo`.
- `Cameras/`: static, orthographic, and perspective camera implementations and view-frustum data.
- `Geometry/`, `Textures/`, and `Shaders/`: resource descriptions and rendering contracts consumed by backends.
- `Text/`: `ITextStyle` and `TextStyle`, pairing generated font metrics, glyphs, kerning, and MSDF metadata with an atlas texture and visual settings. `BuiltInFonts` provides embedded decoded outlines for all 16 Aileron faces. `TextStyleRegistry` rasterizes these at the requested generation size without a font file or font manifest entry.
- `IGraphicsSystem`, render-layer types, and `IWindowService`: system-level rendering and window abstractions.

## Dependencies and ownership

The .NET 10 project references [Core](../Core/README.md), [Assets](../Assets/README.md), and the source-generator analyzer. It uses Silk.NET math/windowing, Microsoft configuration/DI, and StbImageSharp. `AddGraphicsServices` registers graphics services; the runtime separately supplies a backend implementation.

GUI owns measurement, arrangement, focus, and interaction. Graphics should accept numeric layout parameters without depending on GUI enums. Backends own resource allocation, uploads, bindings, recording, and submission.

## Current status

The public `TextComponent` is unfinished: its drawable collection is empty, span creation returns an empty span, and drawable synchronization is not implemented. GUI's `TextElement` and `TextButton` now use this component, but text layout and rendering are not expected to work until its implementation is completed. Do not treat the intended TextComponent source/output contract as implemented.

`TileMapData` stores sparse occupied cells within declared bounds. `TileMapRenderer` derives their local transforms and publishes complete instance snapshots through its own `TileMapDrawable`; it remains independent of `TextureRenderer`, GUI, and game rules.

## Texture atlas regions

`ITextureRegistry.GetOrCreate(contentId)` loads both the image and its NAP manifest regions. `ITexture.Regions` is a read-only list; `GetRegion(name)` looks up a case-sensitive name and throws `KeyNotFoundException` if it is absent. Standalone textures, direct file loads, and fallback textures have an empty region list.

NAP defaults to 2D RGBA8 sRGB KTX2 textures with embedded mipmaps. `ITexture.MipLevelCount` includes the base level; `WriteMipLevel` serializes a complete level, while `Count` and `WriteTo` continue to refer to the base image. The registry also loads NAP's PNG/JPEG mip companions via `MipmapsFilePath`. Texture identity includes dimensions, sampling format, and all mip levels. Vulkan allocates and uploads the full chain. `SamplingBehaviors.Smooth` uses trilinear mip filtering, with base-level sampling for single-level textures; explicit `MinFilterEnum.Linear` disables mip sampling. See [NAP texture settings](../AssetPipeline/README.md#texture-formats-and-mipmaps) for format and mip overrides.

```csharp
var texture = textureRegistry.GetOrCreate((ContentId)"panels");
var region = texture.GetRegion("region-0000");

imageElement.Texture = texture;
imageElement.SourceRegion = region.Bounds;

spriteRenderer.Texture = texture;
spriteRenderer.Add(new SpriteInstance
{
    TexCoord = new Vector4D<float>(
        region.TexCoords.Origin.X, region.TexCoords.Origin.Y,
        region.TexCoords.Size.X, region.TexCoords.Size.Y)
});
```

Pixel bounds and normalized UVs use top-left XYWH coordinates. Each region references the original texture; loading an atlas does not create separate texture resources for its regions. Nine-patch insets remain separate explicit metadata.

## Image clipping masks

`ImageElement.ClippingMask` forwards to its owned `TextureRenderer`. Set `ClippingMask.Diamond` to keep a full diamond, or `ClippingMask.LowerHalfDiamond` to leave the upper half visible and taper only the lower half. `None` is the default. Masks operate in normalized displayed-image coordinates (top-left origin), independently of atlas texture coordinates, and do not alter layout size or hit testing.

```csharp
var portrait = new ImageElement
{
    Texture = portraitTexture,
    SizingMode = ImageSizingMode.Stretch,
    ClippingMask = ClippingMask.LowerHalfDiamond,
};
```

The Vulkan masked-image shader discards fragments outside the shape in a single image draw. The default textured-quad shaders are required when enabling a mask; custom shader mask support and alpha-texture masks are not implemented.

## Development

```sh
dotnet build src/Graphics/Nexus.Graphics.csproj
```

Run from the repository root. See [Graphics tests](../../tests/UnitTests/Graphics), [Vulkan](../Vulkan/README.md), and the [architecture baseline](../../README.md). The build command was not executed for this documentation change.
