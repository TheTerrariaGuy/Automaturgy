# Inventory and reaction packages

Open `Assets/Scenes/Inventory.unity` and enter Play mode. New profiles receive six blocks in the left 10x8 inventory. Drag blocks into the right 5x5 active grid, then click **Enter Game**. Only fundamental blocks on the right enable element placement. An empty loadout is allowed and cannot place any elements; Blink remains available.

Click any occupied cell to drag the whole shape. The translucent green/red ghost shows a valid/invalid snapped destination while preserving the clicked cell's position within the shape. Invalid drops, right-click, Escape, and losing focus cancel the drag without changing either grid. Rotation is not supported.

## Authoring items

Starter assets are in `Assets/Resources/InventoryItems`. Create more with **Create > Grid Mage > Inventory > Reaction Block** and put them in that resource folder. Definitions have:

- `itemId`: stable unique save identifier. Keep this unchanged when renaming artwork or display names.
- `displayName`, `color`, `sprite`: presentation data. The initial Tilemap display uses the shared square sprite and the item's color; custom item artwork is reserved for a later presentation pass.
- `packageId`: reference into the static reaction catalog, not an index into an array and not a shared SO.
- `shape`: a serialized list of integer cell offsets. X increases right, Y increases down from the top-left bounding corner. For example `(0,0), (0,1), (1,1)` is an L. The corner itself may be empty; at least one cell must touch each of the top and left bounding edges. Offsets must be unique and nonnegative.

Width/height are derived from the offsets. Occupied cells, rather than bounding rectangles, determine collisions, so shapes can interlock. Definition changes are checked when an inventory loads. A new profile gets one of each definition; adding definitions does not inject free items into an existing profile.

The scene's Grid/Tilemap layers and UI are saved as editable scene objects. **Tools > Grid Mage > Inventory > Set up inventory scene** creates missing starter assets and initial wiring. It preserves existing definitions and does not rebuild an already wired scene.

## Reaction catalog

Edit `Assets/Scripts/Gameplay/Combat/ReactionCatalog.cs`, in `Definitions`. The catalog is a hardcoded string with the original reaction grammar plus package headers and stable reaction IDs:

```text
PACKAGE 100 FIRE
R 1000 V Fire_Fire_Cardinal_Spread I (1,0,100) O (1,0,101,51) D (0,1,2,3) E
PACKAGE 400 STONE
```

Package hundreds identify the element. Packages ending in `00` enable placement. Individual reaction thousands identify the element. The `D` directions still rotate combat patterns; inventory shapes never rotate. All directional expansions of one rule share its reaction ID. VFX names after `V` are independent of reaction IDs.

| Package | Role | Reaction IDs |
|---|---|---|
| 100 | Fundamental fire: placement, self-reactions, decay | 1000-1008 |
| 101 | Fire interactions with other elements | 1009-1014 |
| 200 | Fundamental water: placement and self-reaction | 2000 |
| 300 | Fundamental electricity: placement, self-reactions, decay | 3000-3003 |
| 301 | Electricity interactions with water | 3004-3005 |
| 400 | Fundamental stone: placement only | none |

All existing reaction tuples, priorities, effects, and direction ordering were preserved. Package 400 demonstrates that a placement package may contain no rules. Existing special-effect fading remains in `ReactionResolver`.

`ReactionParser` is static. `ReactionCatalog` parses and caches immutable package definitions; `RunLoadout` selects the equipped packages and supplies the resolver's lookup by element family. Duplicate grants activate once. Catalog source order determines ties, regardless of inventory position or drag history. `Indexing` remains a compatibility adapter for existing scene/test references; it no longer loads or owns rules. `Assets/Data/Elements/Reactions.txt` is retained only as the pre-migration comparison fixture, not the authoring source.

On **Enter Game**, `InventorySession.PrepareRun` creates an immutable snapshot of selected package IDs, reactions, and placement permissions, saves both grids, and hands it to the gameplay scene. `GameLogic` enforces placement permissions independently of keyboard selection. Opening gameplay directly in the editor uses the saved loadout, rather than enabling everything implicitly. Validation fixtures explicitly supply all packages for old combat tests.

## Saves

Both grids live in `Application.persistentDataPath/inventory-v1.json`. Records contain instance ID, item definition ID, grid, and top-left X/Y; they do not duplicate shapes or reaction text. The initial version uses fixed grid sizes.

Successful moves save both grids together. Saving writes and flushes a temporary file before replacing the primary, keeping `.bak`. Loading can restore a valid backup and preserve the bad primary as `.corrupt-<timestamp>`. If neither save can be read, the inventory displays the error and preserves the files instead of resetting progress. Save failures leave the in-memory layout intact and provide **Retry Save**; entering gameplay requires a successful save.

Unknown items, overlapping placements, and shapes that no longer fit remain in a saved recovery list. **Recover** moves known items into available storage cells. Unrecognized IDs and conflicting identities remain preserved for a content/save repair.

## Verification

Run **Tools > Grid Mage > Validation > Check inventory** and **Check refactoring regressions** in Edit mode. The inventory suite checks package migration, ordering, shape collisions, transfers, persistence, and backup recovery. The combat baseline exercises the new catalog against 160 captured cases and 480 phase snapshots.

See `Tests/Inventory/README.md` for Play-mode pointer, preview, scene handoff, and casting checks. Test saves go under `Temp/InventoryChecks`, never the player's save directory.
