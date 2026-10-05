# Graphics loading diagnostics

All graphics collectors use `IOptions<DiagnosticsSettings>` from Core, bound to
the top-level `Diagnostics` section. Vulkan validation controls remain in `Vulkan`.
Move existing `Vulkan.EnableDiagnostics` and `Vulkan.EnablePerformanceMetrics`
configuration into this section:

```json
"Diagnostics": {
  "Debugging": false,
  "EnableDiagnostics": true,
  "EnablePerformanceMetrics": true,
  "EnableProfiling": false
}
```

Performance collection requires `Debugging || EnablePerformanceMetrics`.
`EnableDiagnostics` alone enables diagnostic snapshots and tooling without load
timings or performance counters. All three default to false, including in Debug builds. `EnableProfiling`
remains the separate frame-profiler setting. Disabled load scopes skip clocks,
allocation readings, aggregate collection, metrics emission, and telemetry logging.

`Nexus.Graphics.GraphicsProfiler` emits Information logs for font build totals,
embedded-data initialization, glyph batches, atlas packing, atlas pixel conversion
and texture hashing, and texture file loading/decoding. Per-glyph timings and cache
hits/misses use Debug logs. Enable that log category for individual glyph detail:

```json
"Logging": {
  "LogLevel": {
    "Nexus.Graphics.GraphicsProfiler": "Debug"
  }
}
```

Resolve `IGraphicsProfiler` from DI and call `GetSnapshot()` to inspect startup
aggregates even if startup happened before the first frame. Samples group by
operation, resource, and generation size; they include counts, total/maximum
milliseconds, current-thread allocated bytes, work units, and the slowest glyph's
codepoint. Cache `.hit`/`.miss` sample counts show reuse. At most 2048 distinct
aggregate groups are retained. `Reset()` clears these local aggregates.

The .NET meter `Nexus.Graphics.Loading` exposes
`nexus.graphics.load.duration` (ms), `nexus.graphics.load.allocated` (bytes), and
`nexus.graphics.cache.requests` (hit/miss counts), and `nexus.graphics.load.work`
(operation-specific units). The duration/allocations/work use
operation, resource, and em-size tags; glyph codepoints are kept out of metric tags.

Timings are inclusive: parent operations include child work, so their totals and
allocated bytes must not be summed together. Scope durations also capture time
spent before an exception; they do not imply successful completion. Glyph MSDF
work units count contour edges; atlas texture work units count pixels; build and
packing work units count glyphs. Allocation readings cover synchronous work on
the calling thread. Vulkan allocation, staging copies, serialization, and upload
command preparation have CPU timings. GPU execution and transfer completion are
outside these load timings.

Additional coverage:

| Operation | Work measured |
|---|---|
| `startup.application` | Window setup, deferred service construction, runtime initialization; excludes the run loop |
| `startup.window.create`, `startup.window.initialize`, `startup.services.resolve` | Window creation/initialization and deferred DI construction |
| `startup.runtime.initialize`, `startup.system.initialize` | Total runtime startup and physics, audio, input, game, graphics, GUI stages |
| `registry.content.load` | Configuration-backed manifest construction; configuration is already supplied |
| `geometry.registry.initialize` | Built-in geometry registration; content-backed geometry loading remains unimplemented |
| `startup.vulkan.context`, `startup.vulkan.swapchain` | Backend setup, with initial swapchain, render-pass, depth and framebuffer stages |
| `shader.realize`, `shader.file.read`, `shader.module.create` | Shader cache requests, SPIR-V read, module creation (bytes) |
| `pipeline.realize`, `pipeline.create`, `sampler.realize` | Cache reuse and Vulkan creation |
| `geometry.buffer.realize`, `geometry.buffer.update`, `geometry.serialize`, `geometry.buffer.allocate.upload` | Vertices, serialized bytes, buffer allocation and host copy |
| `geometry.instances.realize`, `geometry.instances.serialize` | Instance count and serialized bytes |
| `texture.image.realize`, `texture.image.update`, `texture.image.allocate`, `texture.image.view.create` | Image realization/allocation (pixels) and view creation |
| `texture.serialize`, `texture.staging.allocate.upload` | Serialized bytes and staging allocation/host copy |
| `texture.rgba.realize` | Decoded color conversion and CPU texture construction (pixels) |
| `font.file.read`, `font.source.parse`, `font.bounds.glyph` | Source bytes, parsing, and per-glyph bounds alongside existing MSDF stages |

Work units are meaningful within an operation. Do not combine byte, pixel,
vertex, instance, edge, and glyph counts. Realization scopes include cache
lookups; separate creation scopes isolate misses. The runtime shares one
collector through `IGraphicsProfiler` and `IPerformanceTelemetry`.
