# Inventory and reaction grants

Open `Assets/Scenes/Bootstrap.unity` and enter Play mode. Bootstrap compiles its assigned spell text asset before opening Inventory. New profiles receive six blocks in the left 10x8 inventory. Drag blocks into the right 5x5 active grid, then click **Enter Game**. Only fundamental blocks on the right enable element placement. An empty loadout is allowed and cannot place any elements; Blink remains available. Opening Inventory or gameplay directly in the Editor initializes the default spell source before building a loadout.

Click any occupied cell to drag the whole shape. The translucent ghost keeps the item's color for valid snapped destinations and turns red for invalid destinations while preserving the clicked cell's position within the shape. Invalid drops, right-click, Escape, and losing focus cancel the drag without changing either grid. Rotation is not supported.

## Authoring items

Starter assets are in `Assets/Resources/InventoryItems`. Create more with **Create > Grid Mage > Inventory > Reaction Block** and put them in that resource folder. Definitions have:

- `itemId`: stable unique save identifier. Keep this unchanged when renaming artwork or display names.
- `displayName`, `color`, `sprite`: presentation data. The initial Tilemap display uses the shared square sprite and the item's color; custom item artwork is reserved for a later presentation pass.
- `reactionIds`: explicit list of individual reaction IDs from the static catalog. A block can grant any combination, including reactions from different elements.
- `placementElement`: element the block unlocks for placement: `0` = none, `100` = fire, `200` = water, `300` = electricity, `400` = stone. This is independent of its reaction list.
- `shape`: a serialized list of integer cell offsets. X increases right, Y increases down from the top-left bounding corner. For example `(0,0), (0,1), (1,1)` is an L. The corner itself may be empty; at least one cell must touch each of the top and left bounding edges. Offsets must be unique and nonnegative.

Width/height are derived from the offsets. Occupied cells, rather than bounding rectangles, determine collisions, so shapes can interlock. Definition changes are checked when an inventory loads. A new profile gets one of each definition; adding definitions does not inject free items into an existing profile.

The scene's Grid/Tilemap layers and UI are saved as editable scene objects. **Tools > Grid Mage > Inventory > Set up inventory scene** creates missing starter assets and initial wiring. It preserves existing definitions and does not rebuild an already wired scene.

## Reaction catalog

Edit `Assets/Resources/Spells.txt`. Unity imports this as a `TextAsset`, assigned to the Bootstrap component's `spellSource`; another `.txt` asset can be assigned there. The catalog uses element headers and stable IDs for individual reactions:

```text
FIRE
R 1000 V Fire_Fire_Cardinal_Spread I (1,0,100) O (1,0,0) (1,0,101) (1,1,104) (1,-1,104) (2,0,104) D (0,1,2,3) E
```

Reaction ID thousands identify the element. Each block lists the exact IDs it grants in `reactionIds`; there is no intermediate grouping. All directional expansions of one rule share its reaction ID and are enabled together. The `D` directions rotate combat patterns; inventory shapes never rotate. VFX names after `V` are independent of reaction IDs.

### Set expressions

Each comma-separated tuple field is a set expression. An integer is a singleton set; `+` unions sets and `-` subtracts them, evaluated left to right. The initial `+` is optional. A `.` matches exactly one decimal digit; `20.` replaces the old `200*` syntax. Patterns match valid values for the field (defined tile IDs, or directions 0 through 3). Integers have no leading zeroes and are not implicitly padded.

| Expression | Meaning |
|---|---|
| `100+200` | Either tile ID |
| `20.-203` | Defined water stages 200–207 except 203 |
| `.00` | Base tile types 100, 200, 300, 400 |
| `-1+0+1` | Three signed offsets: -1, 0, 1 |
| `1+-2` | Union of 1 and -2, not arithmetic addition |
| `-.-0` | Negative offsets -9 through -1 |
| `D (0+1+2+3-2)` | Rotations 0, 1, 3 |

`I (1+2,0,20.-203)` requires both neighboring cells to contain any accepted water type. Coordinate sets expand as Cartesian products; all input positions must match. Input type sets are alternatives at each position. Separate input tuples are conjunctive, including when they refer to the same position. `O (1+2,0,101+102)` emits each output type at each coordinate; competing writes use priority. Values within an expression expand in ascending numeric order; tuple order is preserved. Whitespace inside tuples and trailing `#` comments are supported. Reaction IDs themselves remain single unique integers.

`O (x,y,type)` uses `ElementDefinitions.Stage.ReactionPriority` for the concrete output type. `O (x,y,type,priority)` overrides that default; the priority field also accepts set expressions. Higher priority wins, followed by later insertion order on ties. Default priorities were chosen from the existing rules; explicit overrides retain special spread/decay behavior. To adjust a tile's default, edit its stage definition.

### Captured variables

Each input field containing at least one `.` receives a variable, in tuple order and then x, y, type order. Names run from `a` through `x` (24 per reaction), restarting for each reaction. A variable captures the entire resolved number, including literal alternatives in that field's set. Multiple patterns in one field still create just one variable.

For example, `I (1,0,20.) O (0,1,a) D (0) E` copies the matched water type to the output cell: matching `203` gives `a = 203`. With `I (.,0,20.)`, `a` captures the x offset and `b` captures the tile type. All expanded input positions must still match. Detection visits tuples in authored order, then ascending x and ascending y within each tuple. If a field is captured more than once, the last detected value wins; a successful reaction logs a warning with the variable, count, and selected value.

Outputs can use variables as whole set terms in coordinates, type, or explicit priority, including `-a`, `a+300`, and `a-201`. These remain set operations. A substitution that leaves an output set empty emits no writes for that tuple. Captured coordinates are relative to the authored, unrotated reaction; output rotation is applied once. Variables are unavailable in input fields and directions, and cannot be embedded inside a number (`2a`). Every possible binding must be valid for its output field.

Compilation reports the source, line, and offending field. Empty static results, unknown literal tile IDs, malformed expressions, unbound variables, and the old `*` syntax are rejected. Coordinates are bounded to ±1024; expressions allow at most 1024 values, each rule 4096 mappings before rotations, and a catalog 262144 mappings including rotations. Variable combinations and empty output rows count toward these limits. Each output tuple only expands the variables it actually uses.

### Startup compilation

Bootstrap is first in build settings. It compiles every reaction's coordinates, accepted-type lookups, variable output tables, rotations, and output priorities once before loading Inventory. During combat, captured numbers select prepared output rows. Compilation failure stays in Bootstrap with an error, rather than using partially compiled rules. The cache resets on each Play session, including when domain reload is disabled. Re-equipping items reuses compiled objects; the resolver performs no set parsing or wildcard evaluation. Direct Editor scene entry uses the default `Resources/Spells` asset. Use **Tools > Grid Mage > Spells > Set up Bootstrap scene** to repair the scene wiring and build order.

| Starter block | Placement element | Reaction IDs |
|---|---|---|
| Fundamental Fire | 100 | 1000, 1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008 |
| Fire Interactions | None | 1009, 1010, 1011, 1012, 1013, 1014 |
| Fundamental Water | 200 | 2000 |
| Fundamental Electricity | 300 | 3000, 3001, 3002, 3003 |
| Electricity Interactions | None | 3004, 3005 |
| Fundamental Stone | 400 | None |

All existing reaction tuples, priorities, effects, and direction ordering are preserved. A block can unlock placement without granting reactions, as stone does, or grant reactions without unlocking placement. Existing special-effect fading remains in `ReactionResolver`.

`ReactionParser` is static. `ReactionCatalog` caches the compiled individual reactions; `RunLoadout` selects the equipped reaction IDs and supplies the resolver's lookup by element family. Duplicate grants activate once, including overlaps between different blocks. Unequipping a block removes a reaction only when no remaining equipped block grants it. Catalog source order determines ties, regardless of inventory position or drag history. `Indexing` remains a compatibility adapter for existing scene/test references; it no longer loads or owns rules. `Assets/Data/Elements/Reactions.txt` is retained only as the pre-migration comparison fixture, not the authoring source.

On **Enter Game**, `InventorySession.PrepareRun` creates an immutable snapshot of selected reaction IDs, reactions, and placement permissions, saves both grids, and hands it to the gameplay scene. `GameLogic` enforces placement permissions independently of keyboard selection. Opening gameplay directly in the editor uses the saved loadout, rather than enabling everything implicitly. Validation fixtures explicitly supply all reactions and placement elements for old combat tests.

## Saves

Both grids live in `Application.persistentDataPath/inventory-v1.json`. Records contain instance ID, item definition ID, grid, and top-left X/Y; they do not duplicate shapes or reaction text. The initial version uses fixed grid sizes.

Successful moves save both grids together. Saving writes and flushes a temporary file before replacing the primary, keeping `.bak`. Loading can restore a valid backup and preserve the bad primary as `.corrupt-<timestamp>`. If neither save can be read, the inventory displays the error and preserves the files instead of resetting progress. Save failures leave the in-memory layout intact and provide **Retry Save**; entering gameplay requires a successful save.

Unknown items, overlapping placements, and shapes that no longer fit remain in a saved recovery list. **Recover** moves known items into available storage cells. Unrecognized IDs and conflicting identities remain preserved for a content/save repair.

## Verification

Run **Tools > Grid Mage > Validation > Check spell compiler**, **Check inventory**, and **Check refactoring regressions** in Edit mode. The compiler suite covers expressions, defaults and overrides, expansion limits, initialization failures, and cache reuse. The inventory suite checks individual reaction grants, asset migration, ordering, shape collisions, transfers, persistence, and backup recovery. The combat baseline exercises the new catalog against 160 captured cases and 480 phase snapshots.

See `Tests/Inventory/README.md` for Play-mode pointer, preview, scene handoff, and casting checks. Test saves go under `Temp/InventoryChecks`, never the player's save directory.
