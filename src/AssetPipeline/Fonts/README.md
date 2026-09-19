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

* `atlas.rgb8`: tightly specified RGB8 atlas bytes in row-major order;
* `font.json`: Nexus-owned font/glyph metrics, atlas geometry, kerning, and MSDF metadata.

The source font is not copied. The package contains no Graphics contracts and requires no font library at runtime.

## Integration decision

There is no managed type boundary in the selected native libraries that is both narrow and stable enough for NAP's content model. Directly projecting the C++ APIs with P/Invoke would couple managed code to C++ ABI, STL, compiler, and allocator details. NAP therefore owns a small C ABI in `native/`: one generation call, one result owner, and two matching release calls. It uses `msdfgen` plus its FreeType extension in-process, while atlas layout and serialization remain Nexus policy. Native handles never enter managed code and native results are copied immediately.

Native binaries are RID/architecture artifacts of NAP itself. They are not runtime game dependencies and no executable, command line, temporary JSON, or stdout parsing is involved.
