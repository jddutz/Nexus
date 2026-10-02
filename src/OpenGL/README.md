# Nexus.Graphics.OpenGL

OpenGL currently supplies an OpenGL-oriented window service and settings. It is not a complete alternative graphics backend.

## Main types and status

- `OpenGLWindowService` implements `IWindowService` and `IDisposable` using Silk.NET.Windowing.
- Windows are tracked by `WindowId`; the first window becomes the main window.
- Resize and framebuffer changes publish window resize/DPI events through EventHub.
- Closing a window removes and disposes it; disposal releases remaining windows.
- `OpenGLSettings` provides backend settings.

There is no concrete OpenGL `IGraphicsSystem` implementation in this folder. `RuntimeBuilder.UseOpenGL` currently throws `NotImplementedException`; the presence of this project does not establish a selectable rendering backend.

## Dependencies and boundary

The .NET 10 project references [Core](../Core/README.md) and [Graphics](../Graphics/README.md), with Microsoft configuration/DI/options and Silk.NET Maths/Windowing packages. Its project file includes a resource embedding rule.

Future rendering code should consume Graphics contracts and keep OpenGL resources and command execution inside this backend. GUI layout and game rules remain outside it.

## Development

```sh
dotnet build src/OpenGL/Nexus.Graphics.OpenGL.csproj
```

Run from the repository root. The build command was not executed for this documentation change. See [Runtime](../Runtime/README.md), [Vulkan](../Vulkan/README.md), and the [architecture baseline](../../README.md) for current backend composition and outstanding platform validation.
