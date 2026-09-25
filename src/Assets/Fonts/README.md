# Pre-rasterized Font assets

A Font definition participates in the normal NAP YAML asset list:

```yaml
root: ./Assets
assets:
  - assetType: font
    contentId: ui.default
    source: Fonts/DejaVuSans.ttf
    glyphs:
      repertoire: ascii
      characters: "©"
    generation:
      emSize: 48
      distanceRange: 4
      padding: 2
```

`glyphs` and `generation` are optional. The defaults produce printable ASCII at a 48-pixel generation resolution. `emSize` is the MSDF generation resolution, not a runtime display size. The source must be an explicitly supplied `.ttf` or `.otf`; NAP never discovers system fonts.

The deterministic output is one logical directory, `fonts/<contentId>/`, containing:

* `atlas.rgb8`: tightly specified RGB8 atlas bytes in row-major order.

NAP records the atlas path and generated font metadata in `content-manifest.json`. The source font is not copied. Font generation is performed by the managed Typography implementation; NAP requires no native font libraries, external executables, or runtime font library.

`FontProcessor` adapts NAP asset definitions to `IFontBuilder`. The builder returns a `FontBuildResult`; NAP then writes the atlas and maps the result to its content-manifest representation.
