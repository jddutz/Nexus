# Font assets

A font asset is a TrueType (`.ttf`) or OpenType (`.otf`) file. NAP copies that file into the content library and records its relative path in the manifest:

```yaml
root: ./Assets
assets:
  - assetType: font
    contentId: ui.default
    source: Fonts/DejaVuSans.ttf
    includeMsdf: true
```

  Set `includeMsdf: true` to generate a printable-ASCII MSDF atlas and write it as
  `fonts/ui.default.png` beside the copied font. The default is `false`, so ordinary font imports
  remain copy-only.

The source must be explicitly supplied; NAP does not discover system fonts or inspect, rasterize, or otherwise transform the font.

For this definition, NAP copies the font to `fonts/ui.default.ttf` and writes this entry to `content-manifest.json`:

```json
"Fonts": {
  "Content": {
    "ui.default": {
      "FilePath": "fonts/ui.default.ttf"
    }
  }
}
```

The runtime loads the font file when a text style is realized. It determines the glyphs and rasterization settings from the styles and text actually needed at runtime, then creates or reuses the corresponding glyph data and texture atlas. NAP must not choose a fixed glyph repertoire or generation size, and the content manifest contains no rasterization metadata.

HelloNexus resolves `ui.default` from the manifest during `GameSystem` initialization, builds its ASCII glyph atlas at runtime, and creates a Roboto text style at size 18 in off-white. A `TextComponent` on a `GameObject2D` uses that style for the sample label. The pipeline test continues to verify NAP's copy and manifest behavior independently of runtime rasterization.
