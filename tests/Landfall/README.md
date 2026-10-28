# Landfall

Single-player terrain placement sample for Nexus. No scoring, reflections, or board expansion.

Run from the repository root:

```powershell
dotnet run --project tests/Landfall/Landfall.csproj
```

Click a tray slot or press **1–5** to select it. Click the selected slot again or press
**Escape** to deselect. Hover over the board to preview placement, then click to place.
**Q** rotates left; **E** or **R** rotates right. The on-screen rotation controls are clickable.
Each piece remembers its rotation until replaced; the same occupied anchor tile stays under
the pointer during rotation. Successful placement clears the selection; select a slot again
to continue. Invalid placements leave the piece and selection unchanged.

**N** or **New game** requests a restart. Confirm by repeating the action or pressing
**Enter**; **Escape** cancels. After game over, New game restarts immediately.

The 20×20 board begins with four forest tiles at (9,9), (10,9), (9,10), and (10,10).
The supplied 1024×1024 atlas contains 8×8 tiles of 128 pixels each, addressed from the top left:

| Terrain | Atlas coordinate |
| --- | --- |
| Grassland | (3,0) |
| Forest | (7,0) |
| Starting square | (0,0) |
| Mountain | (2,4) |
| Water | (3,6) |

Landfall loads `.content/content-manifest.json` and the generated KTX2 textures through
Nexus's texture registry. The bandit atlas is imported with its Aseprite JSON companion,
so its animation data is included in the manifest. The generated `.content` directory is
copied on build and publish.
Terrain uses `terrain`; resources use the separate `resources` atlas,
also an 8×8 grid of 128-pixel tiles. The terrain mapping lives in `LandfallScene.Tile`.
After changing source images in `.assets`, regenerate content from the repository root:

```powershell
dotnet build src/AssetPipeline/Nexus.AssetPipeline.csproj
./tests/Landfall/BuildContentLibrary.ps1
```

`LandfallGame.cs` contains the independent rules and a deterministic catalog of the 21 free
polyominoes. Generation draws size, shape, and terrain independently. Only rotation is
available during play. Game-over searches every slot, rotation, and board anchor.
`LandfallScene.cs` owns input, responsive layout, and TileMap rendering.

Unrevealed cells are covered by an opaque gray fog TileMap above the dark board background.
The starting square and placed terrain reveal their occupied cells and all eight immediate
neighbors, including diagonals. Revealed cells stay visible. Restarting resets visibility
to the starting square and its neighbors. The separate fog layer leaves room for future special
tiles between the background and fog; placement previews remain visible above the fog.

Resource and Buildings are separate TileMap layers above terrain. The draw order is
background → terrain → resources → buildings → fog → grid → placement preview.
Resources can be assigned to any board cell with `AssignResource`, including cells still
under fog. `TryPlaceBuilding` places one building on an occupied terrain cell. It hides
that cell's resource without removing the assignment; `GetResource` still returns it.
Both layers reset on a new game. `TileFeature` stores a type ID and either a named atlas
region or a tile coordinate. Resource generation and additional building types are deferred.
Resource coordinates address `resources`; buildings use the `buildings` atlas.
The left sidebar lists the player-placeable definitions in `Buildings.Available`.
Select a building, hover over the board for a footprint preview, and click to place.
All footprint cells must have terrain and no existing building; placement is atomic.
Successful placement clears selection and hides resources across the whole footprint.
Escape deselects, and selecting a terrain piece exits building mode. Rotation controls
apply to terrain pieces. A building's atlas tile is displayed across its whole footprint.
Farm is the only player-placeable building and occupies 1×1 cell. Its artwork is named
`farm` in `.assets/nap.yaml`, using atlas tile (0,2): pixel bounds (0,256,128,128).
NAP exports that named region to the content manifest, and the palette, preview, and
placed building all read it by name. Change artwork bounds in YAML and rebuild content;
board footprints remain defined in `Buildings`. Settlement has a 2×2 definition and is
excluded from the player palette.

Run the rule tests, including complete deterministic games checked against an independent
placement search:

```powershell
dotnet test tests/Landfall/Tests/Landfall.Tests.csproj
```
