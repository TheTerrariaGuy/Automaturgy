# Painting levels

In Game is configured with `GridParent/Level/Background` and `GridParent/Level/Logic`.
The conversion tool can create **Level Palette** under Assets/Levels/Palettes. It contains,
left to right: Floor, Wall, Player Spawn, Enemy Spawn, Enemy Spawner, Background Floor.
Terrain/spawn assets live in `Assets/Levels/Markers`; art tiles live in `Assets/Levels/Tiles`.

## Elevation

In Game uses numbered elevation tiles on **Logic**.
Select **Elevation Palette** to paint heights. White `n` means `n`; blue `n`
means `n + 0.5`. The assets in `Assets/Levels/Markers/Elevation` cover 1 through 8.5.
The sheet rows are white 1–4, blue 1–4, white 5–8, blue 5–8. The palette groups
white 1–8 on its first row and blue 1–8 on its second row.
Numbered base floor cells and default spawn markers start at 1; paint ramps and cliffs as needed.
Logic numbers are editor markers, hidden in Play mode.

Every Level Marker, including a spawn or wall marker, has an **Elevation** property.
Use finite multiples of 0.5. Duplicate a spawn marker to give a particular spawn a
different height without changing other cells that share the asset. The loader copies
heights into `GameLogic.elevationGrid[row, col]`; rectangular fallback boards default to 0.

`GridMath.IsElevationDiffOk` permits only -0.5, 0 and +0.5. Blink and reactions visit
cells in straight-line order and compare consecutive heights, so `1 -> 1.5 -> 2`
passes and `1 -> 2 -> 1` fails. They do not search for a route around a cliff. At an
exact grid corner, passage fails only when both side cells block it. For elevation,
a side is open if its height connects to both diagonal cells by valid half-steps;
the direct diagonal height difference must also be valid. Missing side cells count
as blocked. Wall and elevation checks apply independently, including for Blink.
Enemy pathfinding
and wandering apply the same height rule to neighboring movement steps; movement
smoothing cannot cut across an invalid elevation transition.

Elevation checks affect reaction ingredients, outputs, overlap outputs, and decay
spread. Spell placement and the blue cast region are independent of elevation.

The **blue** mesh outlines the cast region. The **yellow** mesh outlines valid Blink
destinations, excluding the player cell, blocked rays and cliffs. Enemy-occupied cells
remain valid Blink destinations.
Both the cast visibility range and Blink range limit movement destinations.
These are spatial regions; mana and cooldown are checked when casting. Yellow is
slightly inset so coincident blue/yellow edges remain visible. Board changes and Blink
arrival refresh both regions. Enemy movement does not change destination eligibility.

For another scene, **Tools > Automaturgy > Levels > Use elevation tiles** creates the
palette, replaces the standard Floor markers with Elevation 1, and configures both
outlines. It preserves existing height tiles and spawn/wall markers. Save the scene afterward.

Regression checks:

```powershell
unity command eval_file --file Tests/Elevation/Rules.cs --json
unity command eval_file --file Tests/Elevation/Corners.cs --json
unity command editor_play --json
unity command eval_file --file Tests/Elevation/Runtime.cs --json
unity command editor_stop --json
```

The runtime checks create a temporary board in Play mode; stop Play mode afterward.

## Painting terrain and spawns

1. Select **Background** as the active tilemap and paint the visible artwork. The supplied
   Background Floor tile uses the existing floor sprite; other regular tile assets work too.
2. Select **Logic** and paint Floor everywhere gameplay should exist. Erase Logic tiles
   to create holes or irregular borders. Background art alone does not create game cells.
3. Replace one Floor tile with **Player Spawn**. There must be exactly one.
4. Paint **Enemy Spawn** for an enemy present at load, or **Enemy Spawner** for an enemy
   at load and subsequent attempts every five seconds. Both markers also define walkable floor.
5. Paint **Wall** for an existing cell that blocks movement, sight, and spells. Paint its
   appearance separately on Background: the marker itself is hidden in the game.
6. Save the scene and enter Play mode. Logic's renderer is hidden, its data is retained,
   and the parsed mask determines where runtime cells are created.

For another existing grid scene, use **Tools > Automaturgy > Levels > Create tilemaps from
current grid**. It creates and assigns aligned maps, fills the current rectangular board,
and copies the player coordinates and valid fallback enemy positions. It does not overwrite
an already assigned level. Save the scene afterward. Scenes without an assigned Level on
GameLogic use rectangular fallback initialization.

## Marker properties

Create variants with **Assets > Create > Automaturgy > Level Marker**, or duplicate an
existing marker asset. Add them to the palette as needed. Each painted marker creates an
existing cell and independently specifies:

- **Walkable:** whether actors may occupy it, subject to runtime stone walls.
- **Blocks Sight:** whether targeting and reaction rays can pass through it.
- **Allows Spells:** whether spells may be placed or written into it.
- **Spawn Kind:** none, player, or enemy.
- **Enemy:** the EnemyData asset for that spawn.
- **Spawn Interval:** zero for a single attempt at load, positive seconds for repeated attempts.

A spawn must be walkable. Repeating spawners skip attempts while occupied, blocked by a
runtime wall, occupied by the player, or disconnected from the player. They retry at the
next interval, with no accumulated backlog. Timers use scaled game time.

Marker assets are shared definitions. Parsing copies their flags and spawn settings into
runtime data. Gameplay spell state stays in `GameLogic.grid`; it never modifies the assets.
One coordinate holds one marker, so a spawn marker includes its terrain properties.

## Coordinates and runtime data

`GameLogic.cellExists[row, col]` masks the existing `int[,] grid` and `Tile[,] tilesGrid`.
Absent cells have no Tile object and are blocked for movement, sight, and spell writes.
Use `HasCell`, `CanWalk`, or `AllowsSpells` for gameplay checks; array bounds alone do not
prove that a cell exists. Permanent terrain flags stay separate from changing elemental state.

Array bounds come from occupied Logic cells, including walls and spawns, with the top-left
cell at `[0, 0]`. Negative tilemap coordinates are supported. `LevelLayout.ToCell` and
`ToIndex` convert between coordinates, and `GridHelper.TryGetTileOn` resolves world positions.
Rows increase downward; tilemap Y increases upward. Do not hardcode array indices as saved
level positions: adding cells above or to the left changes the array origin.

Use square rectangular cells at Z = 0 with no gap. Keep the maps aligned with each other
and with GridParent's local axes. GridParent itself can be translated and uniformly scaled.
The supplied palette artwork fits unit cells; use a cell size of 1 and scale GridParent to
resize the whole level. The loader rejects empty maps, unknown Logic tile types, invalid
spawns and unsupported grid geometry. Background artwork is independent of the loader;
keep its cell centers aligned with Logic when painting matching terrain.

Runtime Tile objects retain colliders, elemental artwork, queued previews, and particles.
Their normal floor artwork is suppressed when a TilemapLevel is assigned. Authored static
walls are permanent terrain; they do not become elemental stone and do not participate in
stone reactions. Missing cells block sight as well as movement in this implementation.

`GameLogic.InitializeGrid()` rereads the retained Logic map, rebuilds masks/cells, clears
queued spells, and restores authored player/enemy spawns. It is a board reload, not a full
reset of health, mana, or the combat clock. Editing the mask directly at runtime is not an
authoring workflow; edit the Logic map and reload the board.

## Checks

- **Tools > Automaturgy > Levels > Check tilemap parsing** runs temporary Edit mode checks.
- In a fresh In Game Play session, run
  `unity command eval_file --file Tests/TilemapLevel/Runtime.cs --json`, then stop Play mode.
  The runtime check temporarily replaces the board to exercise holes, negative coordinates,
  spawns, pathfinding, spell propagation, rendering flags, and reloads.
- Existing Blink checks remain under `Tests/Blink`.

Initialization and reset ownership are documented in [Architecture](Architecture.md).
