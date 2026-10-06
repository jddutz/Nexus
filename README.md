# Nexus Game Engine

Nexus Engine is a C#/.NET game engine targeting desktop and mobile platforms. It is architected around an authored Game Model with separate replacable systems for graphics, input, audio, and physics. Nexus also provides an offline asset pipeline for importing authored content.

The vision is for game developers to exclusively use VS Code for coding and development while supporting custom authoring pipelines through modern CI-CD.

The current development stack uses .NET 10 and Silk.NET, with Vulkan as the active rendering backend.

The samples now include Breakout, SnakeGame, and Asteroids alongside HelloNexus. Asteroids
adds indexed 32-bit custom meshes, a uniform-color triangle-list pipeline, transformable
mesh drawables, continuous movement, and runtime drawable removal. Breakout uses
GameObject2D-owned transforms and a scene-owned physics world for ball integration and
rectangle overlap and swept-contact detection, while keeping bounce and round rules in the
scene. The Physics module provides CPU-only 2D body integration, contact correction, and
collision events, which both samples use while retaining their own gameplay responses.

> **Document status:** Initial architecture baseline, 2026-10-02. Established decisions below record prior design agreements; they do not certify that every implementation already conforms. Proposed boundaries and unresolved questions are explicitly identified. The source tree has not been audited for this draft.

## Contents

- [Project status](#project-status)
- [Getting started](#getting-started)
- [Repository structure](#repository-structure)
- [Architecture](#architecture)
- [Established decisions](#established-decisions)
- [Open architectural questions](#open-architectural-questions)
- [Maintaining the architecture](#maintaining-the-architecture)
- [Contributing](#contributing)
- [License](#license)

## Project status

Nexus is under active development. Current work centers on scene lifecycle, GUI layout capabilities, and graphics components.

Vulkan is the active backend. The presence of folders for OpenGL, Audio, Network, and other systems does not establish their implementation completeness or supported platform coverage.

## Quickstart

Create a C# console project and add the engine package:

```bash
dotnet new console -n HelloNexus
cd HelloNexus
dotnet add package Nexus.GameEngine
```

Replace `Program.cs` with:

```csharp
using Microsoft.Extensions.Configuration;
using Nexus.Runtime;

try
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json")
        .AddCommandLine(args)
        .Build();

    using var application = new Application(configuration);
    application.Run();

    return 0;
}
catch (Exception exception)
{
    var error = exception.ToString();
    Console.Error.WriteLine(error);
    Debug.WriteLine(error);
    return 1;
}
```

Add `appsettings.json`:

```json
{
  "Application": {
    "ApplicationName": "Hello Nexus",
    "ApplicationVersion": "1.0.0"
  },
  "Game": {
    "StartSceneId": "WelcomeScreen"
  },
  "Window": {
    "Title": "Hello Nexus",
    "Width": 1280,
    "Height": 720,
    "VSync": true,
    "Resizable": true,
    "Mode": "Normal"
  }
}
```

Concrete `IScene` implementations in the entry assembly are discovered by default; `SceneAttribute` can override a scene's class-name-based id. At startup, `NexusRuntime` resolves `Game:StartSceneId` through `ISceneRegistry`, or uses the sole registered scene when that setting is absent, and clears setup events before loading the scene. `GameSystem.LoadScene` initializes and activates the scene hierarchy synchronously and publishes `SceneLoadedEvent`; unload must be explicit before another scene can be loaded. Multiple scenes require an explicit start scene.

Configure the project to copy the settings and built content into its output directory:

```xml
<ItemGroup>
  <None Update="appsettings*.json"
        CopyToOutputDirectory="PreserveNewest" />
  <None Update=".content/**/*"
        CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

Set `DOTNET_ENVIRONMENT=Development` to load optional `appsettings.Development.json`
overrides after the base settings. Keep development-only diagnostics and logging
levels in that file; the VS Code sample debug profiles set this environment automatically.

Build your content with NAP:

```bash
nap build
```

Run the project from VS Code with **F5** after configuring a .NET launch profile, or from the terminal:

```bash
dotnet run
```

## Repository structure

The following names match the supplied repository view. Folder names describe physical organization; they do not automatically define namespace names or dependency direction.

| Path | Responsibility or documentation status |
| --- | --- |
| `src/Core` | Shared foundational contracts and utilities. |
| `src/Runtime` | Application composition and runtime orchestration; exact ownership boundaries require confirmation. |
| `src/Game` | Scenes, templates, and managed lifecycle concepts; conceptually the GameModel discussed in design. Exact project mapping requires confirmation. |
| `src/Graphics` | Graphics contracts, drawable data, views, and graphics components. |
| `src/Vulkan` | Vulkan implementation of graphics functionality. |
| `src/OpenGL` | OpenGL backend area; implementation status unverified. |
| `src/GUI` | Elements, layout, interaction, and GUI-facing presentation semantics. |
| `src/Input` | Device input and input event production. |
| `src/Platform` | Platform services, including window services. |
| `src/Physics` | PhysicsSystem integration layer and isolated 2D simulation context. |
| `src/Audio` | Audio system area; detailed contracts not yet captured here. |
| `src/Network` | Networking system area; detailed contracts not yet captured here. |
| `src/AssetPipeline` | Offline content processing, including font generation. |
| `src/Assets` | Asset-related project area; its distinction from AssetPipeline requires confirmation. |
| `src/Testing`, `tests` | Testing support and tests; exact division requires confirmation. |
| `codegen` | Code generation area, including observable infrastructure where applicable. |
| `docs` | Detailed specifications and architectural records. |
| `scripts` | Development and build automation. |

## Architecture

### Three distinct relationships

Nexus has three relationships that must be documented independently:

1. **Module dependencies:** which system may reference another system's contracts.
2. **Scene hierarchy:** which scene node owns a child node and how ancestry affects lifecycle and transforms.
3. **Runtime orchestration:** which system schedules work and in what order.

A scene parent does not create a module dependency. A runtime coordinator calling several systems does not make those systems depend on one another. A component consuming a view does not make its owning game object responsible for rendering.

### Template and behavior terminology

| Term | Meaning |
| --- | --- |
| **Template** | A reusable definition of an object hierarchy, its configuration, and attached behaviors. |
| **Template instance** | A live hierarchy constructed from a template, with its own identity and mutable state. |
| **Template parameters** | The supported choices supplied when instantiating a template. |
| **Template variant** | A reusable specialization of another template, with changed defaults, structure, or behaviors. |
| **Template registry** | The project's catalog of templates, retrieved by stable identifiers. |
| **Behavior** | Reusable logic attached to an object, with configuration and per-instance runtime state. |
| **Binding** | An explicit connection between behavior and the objects or data it operates on. |

### Established dependency boundaries

| Boundary | Contract |
| --- | --- |
| GUI → Graphics | GUI elements use graphics components to express their visual output. Graphics does not consume GUI enums or depend on GUI layout types. |
| Graphics → backend implementation | Graphics describes rendering requirements. Vulkan provides backend implementation. Backend resources and command recording remain backend concerns. |
| GUI → Input | GUI interprets device input through hit testing, focus, interaction state, and routing. Input does not perform GUI hit testing. |
| Game code → engine | Faction rules, campaign progression, tactical resolution, and castle simulation belong to game code. |
| Asset generation → runtime content | NAP builds content ahead of execution. Runtime consumes prepared assets and their manifest. Runtime font rasterization is deferred. |

Graphics layout parameters should use numeric values where needed to avoid importing GUI-specific alignment enums. This does not authorize copying GUI layout policy into the backend.

### Proposed module policy

The following policy makes the intended hierarchy explicit but needs confirmation against project references before adoption:

- Core is independent of higher-level engine systems and game code.
- Game model contracts do not depend on GUI or concrete rendering backends.
- Graphics may consume shared and game model contracts needed for its components, while avoiding GUI and concrete backend dependencies.
- Input and Platform avoid GUI, game-specific behavior, and concrete renderer dependencies.
- GUI composes game model, graphics, and input functionality.
- Runtime is the composition boundary that selects implementations and schedules systems.
- Physics, Audio, and Network consume only the shared contracts they require; integration occurs through explicitly defined interfaces.
- Backend implementations consume Graphics contracts. Platform integration must not introduce a reverse dependency from the graphics API into a backend.

These are architectural dependency directions, not a complete list of permitted project references. Transitive references and shared assemblies need a source audit. Move a contract into Core only when it is genuinely foundational; moving it there merely to resolve a cycle obscures ownership.

### Responsibility at the GUI and graphics boundary

| Layer | Owns |
| --- | --- |
| GUI element | Measurement, arrangement, bounds, GUI alignment semantics, focus, hit testing, and interaction state. |
| Graphics component | Visual state and conversion of that state into drawable output. TextComponent owns text layout needed to produce positioned glyph output. |
| Drawable | Rendering data and the transform required by its graphics contract. |
| View / camera | Conversion of view coordinates to clip coordinates. |
| Backend | Resource allocation, uploads, bindings, command recording, and submission. |

An element supplies the arranged visual placement to its graphics component. A drawable requiring a matrix does not automatically mean that the component must expose a mutable `WorldTransform` as its public placement API.

## Established decisions

### Scene model and lifecycle

- `ISceneNode` represents hierarchy membership through `Parent`, `Children`, and `Root`. `Root` replaces the earlier `Scene` property.
- `IManagedEntity` defines `Initialize`, `Activate`, `Update`, and `Deactivate`, with initialization and activation state.
- A game object **has** components; it is not itself a component collection.
- Game objects and scenes support the engine's observable contract. Components use their own modification notifications; changes do not automatically propagate upward.
- Scene and game object lifecycle methods are virtual. Their default behavior delegates to children.
- GameSystem subscribes to scene-node child collections before publishing activation events. Game objects added beneath active parents are initialized and activated immediately, then updated in the next parent-first traversal without duplicate activation.
- Components added to active game objects are observed by GameSystem and queued through EventHub for activation at the next frame boundary.
- The scheduler and default lifecycle delegation must compose without updating a node twice. The implementation must make traversal ownership explicit.
- Duplicate children are disallowed. Transfer between scenes requires detachment before attaching to the destination.
- Hierarchy changes propagate down the affected subtree through `OnSceneHierarchyChanged`. Root changes alone do not describe every ancestry change.

### Transforms and layout

- `IGameObject2D` exposes local and world transforms alongside position, rotation, and scale. The 3D contract uses 3D position and scale with quaternion rotation.
- Transform recomputation is driven by relevant property and hierarchy changes. Recomputing every object's matrices every frame is unnecessary.
- GUI elements operate in screen space and implement the 2D spatial contract. Element size is applied when constructing visual drawable transforms.
- Concrete element classes own their internal virtual measurement and arrangement behavior. The earlier reusable layout-rule model was discarded.
- Grid tracks support absolute, content-sized auto, and relative sizing. Auto columns are measured before auto rows so wrapped content can determine row heights at resolved column widths.
- GUI view conversion is handled by the camera. DPI-related conversion must not be independently duplicated in components without an explicit requirement.

### Graphics and rendering

- Graphics components include TextRenderer, TextureRenderer, NinePatchRenderer, TileMapRenderer, and ViewRenderer. TileMapRenderer owns a separate batched path for sparse tile-map data; it does not derive from TextureRenderer.
- The agreed simple image model is one ImageElement owning one TextureRenderer, owning one TexturedQuad with one instance.
- Nine-patch rendering uses nine quad instances and reuses geometry.
- Tile maps use bounded sparse data and one persistent textured-quad drawable, independent of GUI and game-specific rules.
- Drawable GUI renderers remain attached to their elements; `IsVisible` gates submission when elements are hidden or their geometry is unrenderable.
- Views own the render policy (draw-order preservation, blending, and depth state) for contents selected by their layer mask; render layers only classify contents and render-pass membership. `RenderLayers` centralizes the common `DefaultUI` and `All` masks, and view/drawable masks default to `All`.
- Scenes do not create a default render view. Scene composition creates views and attaches the scene's static camera to the chosen view owner; view masks can narrow the default all-layer selection. `GameSystem` forwards main-window size changes to that camera.
- Vulkan rendering follows `PrepareFrame`, per-view `Begin`, per-pass `Record`, per-view `Finalize`, then `Submit`.
- Draw commands execute inside their rendering pass. Resource uploads occur before the pass.
- Sticky draw commands are retained. Drawables expose an ascending `DrawOrder`; the default batch strategy groups by pipeline before drawable identity, while views can explicitly select draw-order preservation.
- Vertex buffers, images, samplers, pipelines, and per-drawable instance buffers have backend registry responsibilities.

### Text

- TextComponent consumes source text and style information and produces drawable text output.
- TextSpan represents **output from TextComponent**, rather than an editable source-text container.
- TextSpan should be mostly immutable. Rebuilding output is preferred when changes would require maintaining interdependent layout state.
- GUI alignment enums remain GUI concerns. Graphics uses numeric layout parameters instead of referencing those enums.
- Fonts are generated offline by the managed asset pipeline. Runtime uses prebuilt font assets.
- MSDF glyph rendering uses the median RGB distance, derivative-based antialiasing, and source-alpha blending.

The revised TextComponent specification must define the exact source API, output replacement behavior, and acceptance criteria. This README records the ownership decisions without inventing those contracts.

### Physics

- PhysicsSystem owns simulation-world membership and timing. Game code configures lifecycle-managed physics components and never registers bodies or calls integration directly.
- Physics components carry an explicit PhysicsWorldId. PhysicsSystem creates and owns isolated PhysicsWorld2D contexts; activation rejects invalid or unknown world IDs rather than routing them to a default.
- PhysicsWorld2D advances active bodies, filters collider pairs by reciprocal category masks, resolves the earliest translating axis-aligned rectangle sweep against unchanged bodyless targets, detects overlaps, and returns results to PhysicsSystem. Equal-time contact ties use target then moving collider ID; unsupported sweeps remain overlap notifications. PhysicsSystem queues collision events for the next EventHub drain.

### Input

- Input wraps Silk.NET.Input and exposes device state and input events.
- Input events and deferred system feedback are drained through EventHub at the start of update. GameSystem updates first, followed by Physics, GUI, Audio, and Input. Physics collision results and gameplay feedback are intentionally delivered by the next update's drain; rendering consumes the latest completed update state.
- GUI routing proceeds from root to leaf; invisible or disabled branches are pruned. Composite elements can suppress child input.
- GUI owns focus and mouse-over gating. Input remains independent of GUI geometry.

### Observability and construction

- Nexus uses its own `IObservable` rather than `System.ComponentModel.INotifyPropertyChanged`.
- Property changes use `Action<string>` notifications. Observable collections expose singular `ItemAdded` and `ItemRemoved` notifications; batch events are deferred.
- `[Observable]` fields generate properties, mutation methods, and change hooks. An analyzer warns about direct accesses to observable backing fields.
- Generated setters are nonvirtual. `SetPropertyName` is protected and virtual.
- `[Observable(PublicSetter = false)]` generates a protected property setter; otherwise the property setter is public. `SetPropertyName` remains protected and virtual.
- Observable generation applies only to classes implementing or inheriting `IObservable`. `PropertyChanged` is authored, and derived classes raise it through generated protected, nonvirtual `NotifyPropertyChanged`.
- The complete agreed property wrapper and partial after-change hook pattern is documented in [codegen/README.md](codegen/README.md). This replaces the obsolete `docs/Code Generation.md` specification; implementation conformity remains to be verified.
- Deferred property mutation is not currently an established requirement.
- Templates separate construction recipes from created objects, with optional initialization callbacks and dependency-injected construction.
- Concrete GUI classes such as TextButton are preferred for GUI behavior. Templates remain available for authoring and composition.
- GUI elements support parameterless construction and property-based initialization; text visuals tolerate styles or textures being assigned later.

## Open architectural questions

These items must remain explicit until resolved:

| Question | Required resolution |
| --- | --- |
| Physical projects versus conceptual systems | Audit project references and namespaces, particularly `Game` versus GameModel and backend folder names. |
| Runtime versus GameSystem | Define composition, scheduling, traversal ownership, and component lifecycle integration. |
| Assets versus AssetPipeline | Define runtime asset contracts, content loading ownership, and dependency direction. |
| TextComponent revision | Complete the source/output contract and immutable TextSpan acceptance criteria. |
| Font atlas orientation | Verify the full builder → upload → UV → shader path. Current orientation must not be declared correct without evidence. |
| GUI rectangle coordinates | State the precise parent-relative or view-relative contract at measurement, arrangement, and graphics handoff. |
| Physics, Audio, Network | Capture each system's public contracts and integration points before asserting a hierarchy. |
| Backend and platform support | Document verified capabilities and launch requirements per platform. |

## Maintaining the architecture

This README is the architecture entry point. Detailed specifications and architecture decision records belong under `docs`; add relative links as those files are created. Do not link to documents that do not yet exist.

For each significant architectural decision, create a record under `docs/decisions/` with:

1. A stable identifier and descriptive title.
2. Status: **Proposed**, **Accepted**, **Superseded**, or **Rejected**.
3. Context and the concrete problem.
4. The decision, including ownership and dependency direction.
5. Consequences and relevant alternatives.
6. Acceptance criteria or evidence of implementation conformity.
7. Links to related specifications and any decision it supersedes.

The established decisions above are the initial baseline to migrate into individual records. Proposed policies remain proposed until explicitly accepted.

When changing architecture, update the relevant record and this README in the same change. Preserve superseded records so their rationale remains available. Implementation discrepancies should be recorded and corrected deliberately; existing code does not silently supersede an accepted decision.

Before introducing a new reference or shared contract, identify its owning system, its consumers, and whether it creates a cycle or transfers policy into a lower-level system. Future design discussions and coding-agent tasks should reference this baseline and the relevant detailed specification.

## Contributing

Keep changes within the owning system, document cross-system contract changes, and run checks appropriate to the change. Add focused tests for lifecycle ordering, coordinate conversion, layout behavior, and rendering data where correctness requires them.

Repository-specific contribution procedures and verified validation commands remain to be documented.

## License

See [LICENSE](LICENSE) for the repository's license terms.
