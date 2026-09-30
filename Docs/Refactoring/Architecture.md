# Architecture and ownership

## Runtime map

| Unit | Responsibility | Dependencies |
|---|---|---|
| GameLogic | Coordinates board lifecycle, clock, mana, commands, masks, and presentation | BoardState, SpellQueue, ReactionResolver, scene adapters |
| BoardState | Owns current cell contents and terrain masks; answers terrain queries | GridMath, ElementState |
| SpellQueue | Owns queued cells, recorded costs, and reserved mana | Plain C# |
| ReactionResolver | Resolves fading, ordinary reactions, and overlaps against snapshots | BoardState, reaction lookup |
| ReactionParser | Turns authored reaction text into rotated rules with diagnostics | GridMath, ElementDefinitions |
| ReactionCatalog / RunLoadout | Static package definitions and equipped reaction/placement snapshot | ReactionParser |
| Indexing | Compatibility adapter to the active loadout | InventorySession |
| InventoryState / InventorySession | Shaped item placements, active packages, saves, and scene handoff | Item definitions, save store |
| InventoryController / InventoryGridView | Dragging, snapped previews, controls, and Tilemap display | InventoryState |
| ElementDefinitions | Shared stage alpha, damage, placement cost, and decay-effect IDs | Plain C# |
| TilePresentation | Detects changed stages and deduplicates sprite-neighbor refreshes | Tile registry, TextureHandler |
| Tile | Cell coordinates, sprite references, queued preview, particle handle | Authoritative BoardState through GameLogic |
| TextureHandler | Sprite selection, fitting, color, and ground/wall sorting | SpriteCatalog, TileSpriteLayout |
| SpriteCatalog | Shared sprite entries and lookup validation | Serialized sprite references |
| MobHandler | Enemy catalog, spawners, randomized path channels, spawn occupancy checks | BoardState, GridHelper, EnemyData |
| MobScript | Per-enemy health, movement, path lookahead, damage feedback | MobHandler, BoardState |
| PlayerHandler | Player position/health, Blink settings, animation, afterimages | Spatial queries and scene adapters |
| GridHelper | Scene/world coordinate adapter and board sight-wall extraction | GameLogic, TilemapLevel |
| GridMath | Bounds, square distance, quarter turns, line stepping, visibility/elevation rules | Plain C# |
| ParticleVFX | Playback, stage switching, lifetime, pooling | ParticleCatalog, ReactionVisual |
| ParticlePattern | Masks and orients authored effect parts | ReactionVisual, GridMath |
| GrassWind | Animates the authored grass tile set and caches level elevations | Tilemap, level-initialized event |
| Selector | Keyboard selection/submission and selected-art presentation | GameLogic selection event |
| GridPointer | Picking, pointer commands, and hover presentation | Camera, input devices, spatial queries |

## Board ownership

BoardState.Cells is the authoritative contents array. GameLogic.grid exposes the same array for existing inspection and test code; it is not another allocation. Similarly, cellExists and elevationGrid expose BoardState's terrain arrays.

Use BoardState.Set for individual production writes and Replace for complete, matching-size snapshots. Direct array edits remain supported by the existing diagnostic scripts, but callers must explicitly refresh presentation and spatial masks afterward. This is not a fully immutable board API.

Tile.type now reads the board. It cannot be assigned independently. The only cached type on a tile is lastRenderedType, used to avoid replaying an unchanged particle stage. A tile's row and col are initialized once and exposed with private setters.

TilemapLevel.Layout and BoardState share the initialized terrain arrays. Marker assets are copied by the level reader first, so gameplay edits do not mutate shared LevelMarkerTile assets.

Existence, walkability, sight blocking, spell permission, and elevation are separate concepts. Do not collapse these arrays into one generic blocked flag.

## Initialization and reset

Singleton scene adapters register during Awake. GameLogic.Start performs the first InitializeGrid. Unity runs Awake for the active scene before this initialization.

InitializeGrid performs:

1. Read and validate the assigned TilemapLevel.
2. Release reaction effects and old tile effects/objects.
3. Clear queued spells and their reservations.
4. Construct BoardState, the resolver, masks, and tile registry.
5. Instantiate and initialize views for existing cells.
6. Hide authoring markers.
7. Reset the player, then enemies and path fields.
8. Synchronize tile presentation and spatial regions.
9. Raise LevelInitialized.

PlayerHandler and MobHandler no longer independently reset themselves in Start. This removes repeated initialization and region rebuilds. GrassWind subscribes to LevelInitialized, refreshing its elevation cache after subsequent resets as well as the initial load.

A scene with no assigned level retains the existing rectangular fallback and legacy enemy placements. LegacyLevelDefaults shares those coordinates with the level-conversion tool, preserving spawn order. The fallback is also used by validation fixtures.

## Combat-to-presentation flow

GameLogic.TickCombat calls ReactionResolver.Resolve. The resolver replaces board snapshots phase by phase and invokes a callback after each phase. GameLogic uses that callback to invalidate queued placements.

After all phases, TilePresentation compares each cell's type with its last presented stage. Changed tiles update their particle handle, and their 3-by-3 neighborhoods enter a HashSet. TextureHandler.ApplyTexture runs once for each unique affected sprite against the final board.

This retains an O(board cells) change-detection scan. It removes repeated sprite work without introducing a fragile dirty flag at every existing array write. A future mutation-only board API could provide a direct changed-cell list.

UpdateTile is the immediate single-cell path, used by authoring checks and diagnostics. UpdateTiles is the batched path used by combat and queue submission.

## Occupancy and enemy routing

Blink eligibility and its outline share GameLogic.moveableGrid, computed from range, visibility, walkability and elevation in MakeCastable. Enemies do not block Blink destinations, so enemy movement does not trigger outline updates.

MobHandler.IsOccupied checks live, active enemies when a spawner attempts a spawn. There is no occupancy cache, version counter or movement invalidation hook. Direct position and enabled-state changes are visible immediately. Spawns still reject cells occupied by an enemy or the player.

Path fields still rebuild on the same ticks and player moves as before, keeping randomized channel behavior. The implementation reuses the path array when dimensions match, a frontier queue, a four-neighbor list, candidate arrays, and a visited array with generation counters. No wall-array clone is needed per channel.

NextStep is an immutable value type. IsValid is false for the default zero step, replacing the old null sentinel. Path disagreement inspection uses enumeration rather than draining and reconstructing a queue.

## Coordinates

- Board indices are [row, col], with row increasing down.
- Vector2Int cell coordinates use x = column, y = row.
- Reaction tuples use x = column offset, y = row offset.
- World XY uses positive Y up.
- TilemapLevel.Layout converts Unity tilemap cells to board indices, including negative authored coordinates.
- GridHelper maps world positions through the initialized level/grid transform.
- GridMath.Rotate performs quarter turns in the reaction coordinate system; presentation rotates its world transform by -90 degrees per turn.

GridMath.Line is a struct iterator. MoveNext exposes the previous and entered cell and whether an exact corner was crossed. Consumers apply their own policies rather than sharing one combined wall/elevation predicate.
