# NAP native font boundary

NAP uses a small, versioned-by-layout C ABI rather than exposing C++ or FreeType handles to managed code. The shared library loads a caller-supplied font with FreeType through `msdfgen-ext`, extracts glyph metrics and kerning, generates deterministic RGB MSDF cells with `msdfgen`, and copies one owned result to managed memory. `nap_font_result_free` releases the complete native object; no native allocation escapes a single `NativeFontRasterizer.Rasterize` call.

## Build and deployment

Build `msdfgen` with its FreeType extension enabled, then configure this directory with its CMake package on `CMAKE_PREFIX_PATH`:

```sh
cmake -S src/AssetPipeline/native -B artifacts/font-native \
  -DCMAKE_BUILD_TYPE=Release -DCMAKE_PREFIX_PATH=/path/to/msdfgen/install
cmake --build artifacts/font-native --config Release
cmake --install artifacts/font-native --prefix artifacts/font-native/install
```

Ship the resulting `nexus_font_native.dll`, `libnexus_font_native.so`, or `libnexus_font_native.dylib` beside `nap`, together with the matching architecture's dynamically linked dependencies. Release packaging must build separately for each supported RID (`win-x64`, `linux-x64`, `osx-x64`, `osx-arm64`, and so on); it must not fall back to a system font or executable.

`msdfgen` and `msdf-atlas-gen` use the MIT license. FreeType uses the FreeType License or GPLv2 at the distributor's option. Binary distributions must carry the selected dependency notices. NAP uses only `msdfgen`'s library API because atlas policy and the serialized package are Nexus-owned; it never starts the `msdf-atlas-gen` CLI.
