# Inventory verification

Edit-mode checks (after the inventory scene/assets have been created):

```powershell
unity command eval --code 'SpellVariableChecks.Run(); SpellCompilerChecks.Run(); InventoryChecks.Run(); RefactoringChecks.Run(); return true;' --timeout 60 --json
```

Play-mode checks start from a fresh starter layout in `Assets/Scenes/Inventory.unity`. The script redirects runtime saves to a unique test directory under `Temp/InventoryChecks` before changing anything. It does not overwrite the real profile. If testing a project with an existing customized profile, use an isolated copy of that profile with the six starter blocks in storage.

To also verify startup, open `Assets/Scenes/Bootstrap.unity` before entering Play mode, wait for Inventory to load, then execute `Tests/Inventory/Bootstrap.cs`. Run it again in a second Play session to verify cache reset with domain reload disabled. The compiler suite checks expressions, wildcards, rotations, priorities, expansion bounds, file diagnostics, failed initialization, and reuse of compiled mappings.

```powershell
unity command editor_play --json
unity command eval --code 'Application.runInBackground = true; UnityEditor.EditorApplication.QueuePlayerLoopUpdate(); return true;' --json
unity command eval_file --file Tests/Inventory/Runtime.cs --timeout 60000 --json
# Wait for In Game to finish loading and its first Start frame, then:
unity command eval_file --file Tests/Inventory/Gameplay.cs --timeout 60000 --json
unity command editor_stop --json
```

`Runtime.cs` feeds a synthetic mouse through the production pointer code, verifies a grab from a non-origin cell, item-colored/red translucent previews, failed drops, cancellation, autosaving, UI raycasting, and the loadout snapshot. It captures `Temp/InventoryChecks/ValidGhost.png` and enters gameplay with fire and stone. Input devices/settings are restored in `finally`.

`Gameplay.cs` checks selected reaction and placement grants, fire decay, exclusion of cross-element reactions, keyboard selection permissions, and direct casting permission checks. Stop Play mode afterwards to discard the test's runtime world state and reset the isolated save store.

Inventory checks cover starter grants, selecting individual reactions with their rotations, shared grants, unequipping, and independent placement permissions. The combat baseline compares 160 cases and 480 phase snapshots.

Bootstrap startup checks verify that spells compile once before Inventory and that gameplay reuses the compiled mappings. Run two consecutive Play sessions with domain reload disabled to check cache reset.

Spell variable checks cover whole-number capture, x/y/type assignment order, rotations, last-detected wins and logging, output unions/differences and priorities, overlap snapshots, isolation between reactions, all 24 variable names, and rejection of unsupported wildcards, invalid bindings, and excessive expansion. Run the compiler, inventory, and combat suites with these checks to exercise variable output tables through the full pipeline.

See the [validation guide](../../Docs/Validation.md) for suite entry points, fixtures, and output paths.
