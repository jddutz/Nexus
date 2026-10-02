# Nexus.Graphics.Vulkan

Vulkan is the active graphics backend. It implements graphics-system and window services and owns Vulkan resources, frame recording, synchronization, and submission.

## Main areas

- `VulkanGraphicsSystem`, `VulkanWindowService`, `Context`, and `SwapChain`: backend and window integration.
- `Rendering/`: renderer, render-pass configuration, batches, and default/depth-sort strategies.
- `Commands/`: draw, bind, upload, viewport/scissor, and uniform-update commands.
- `Geometry/`, `Textures/`, `Pipelines/`, and `Descriptors/`: resource registries, factories, and bindings.
- `Synchronization/`, buffer management, and command-buffer pooling: frame/resource lifetime coordination.
- Validation and performance diagnostics: backend diagnostics and metrics.
- `Shaders/`: shader sources and compiled SPIR-V assets.

The architecture baseline defines frame work as `PrepareFrame`, per-view `Begin`, per-pass `Record`, per-view `Finalize`, then `Submit`. Uploads occur before rendering passes; draw commands execute inside their pass. Changes must preserve resource lifetime and synchronization requirements.

## Dependencies and integration

The .NET 10 project references [Core](../Core/README.md) and [Graphics](../Graphics/README.md). Packages include Silk.NET Vulkan and EXT/KHR extensions, Maths, Windowing, and Microsoft configuration/DI/options. `AddVkGraphicsServices` registers backend services; `AddVkValidation` registers validation services. Runtime selects Vulkan by default when no graphics system was supplied.

Backend code consumes graphics data; GUI measurement, focus, text-source policy, and game rules belong to higher layers.

## Build and shaders

From the repository root:

```powershell
./CompileShaders.ps1
dotnet build src/Vulkan/Nexus.Graphics.Vulkan.csproj
```

The [shader script](../../CompileShaders.ps1) expects `VULKAN_SDK` to identify an SDK containing `Bin/glslc.exe`. It writes `.spv` files to `Shaders/Compiled`; the project copies them to build/publish output under `Shaders/`. `-EnableShaderDebugPrintf` enables the script's shader debug define.

These commands were not executed for this documentation change. Rendering also requires a usable Vulkan driver/device and window environment; platform coverage and full launch requirements remain to be validated.

See [Vulkan unit tests](../../tests/UnitTests/Vulkan), [HelloNexus](../../tests/HelloNexus), and the [architecture baseline](../../README.md). Unit tests alone do not establish successful GPU rendering or font-atlas orientation.
