# Inventory verification

Edit-mode checks (after the inventory scene/assets have been created):

```powershell
unity command eval --code 'InventoryChecks.Run(); RefactoringChecks.Run(); return true;' --timeout 60000 --json
```

Play-mode checks start from a fresh starter layout in `Assets/Scenes/Inventory.unity`. The script redirects runtime saves to a unique test directory under `Temp/InventoryChecks` before changing anything. It does not overwrite the real profile. If testing a project with an existing customized profile, use an isolated copy of that profile with the six starter blocks in storage.

```powershell
unity command editor_play --json
unity command eval --code 'Application.runInBackground = true; UnityEditor.EditorApplication.QueuePlayerLoopUpdate(); return true;' --json
unity command eval_file --file Tests/Inventory/Runtime.cs --timeout 60000 --json
# Wait for In Game to finish loading and its first Start frame, then:
unity command eval_file --file Tests/Inventory/Gameplay.cs --timeout 60000 --json
unity command editor_stop --json
```

`Runtime.cs` feeds a synthetic mouse through the production pointer code, verifies a grab from a non-origin cell, green/red translucent previews, failed drops, cancellation, autosaving, UI raycasting, and the loadout snapshot. It captures `Temp/InventoryChecks/ValidGhost.png` and enters gameplay with fire and stone. Input devices/settings are restored in `finally`.

`Gameplay.cs` checks selected packages, fire decay, exclusion of cross-element reactions, keyboard selection permissions, and direct casting permission checks. Stop Play mode afterwards to discard the test's runtime world state and reset the isolated save store.

Implementation verification: 160 inventory/catalog/save assertions, 25 pointer/preview/save/handoff assertions, 11 gameplay assertions, the 160-case combat baseline, and the existing Blink/input integration suite passed in Unity 6000.6.0f1. The inventory and ghost screenshots were inspected visually.
