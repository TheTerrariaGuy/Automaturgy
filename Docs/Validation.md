# Validation guide

## Requirements

Use the project's Unity 6000.6 editor and Windows graphics configuration for GPU checks. The geometry shader needs the graphics device; do not use a headless no-graphics invocation for rendering assertions.

Wait for compilation to finish and inspect Console errors before running suites. Run GPU suites sequentially: they temporarily control camera and shader-global state.

The core and rendering suites are editor checks, not NUnit tests, so a Unity Test Runner discovery command alone does not execute them.

## Suite map

| Entry point | Context | Coverage | Result |
|---|---|---|---|
| SpellVariableChecks.Run / SpellCompilerChecks.Run | Edit Mode | Set expressions, captured variables, rotations, priorities, expansion limits, diagnostics, startup failures, and cache reuse | Temp/SpellCompiler/Variables.txt and Temp/SpellCompiler/Results.txt |
| InventoryChecks.Run | Edit Mode | Reaction grants, ordering, shape collisions, transfers, persistence, and backup recovery | Temp/InventoryChecks/Results.txt |
| RefactoringChecks.Run | Edit Mode | 160 baseline combat cases, all three phase snapshots, surviving visual ownership, malformed rule input, reservations, grid rays, catalogs, tilemap parsing | Temp/RefactoringChecks/Results.txt |
| Tests/Refactoring/Runtime.cs | Fresh rectangular fixture Play | Board ownership, unique sprite refresh counts, path-buffer reuse, live spawn occupancy and reset | Returned PASS string |
| Tests/Refactoring/GrassReset.cs | Gameplay scene, fresh Play | Grass elevation cache on initialization and two authored-height resets | Returned PASS string |
| Tests/Refactoring/AssetReferences.cs | Edit Mode with saved scenes | Prefab/scene scripts and serialized object references in prefabs, game data, levels and rendering assets | Returned PASS string |
| WorldRenderingChecks.Run | Edit Mode; enters/exits Play | Scene wiring, sprites, 27 particle stages, seven shared pools, rotated effects, damage feedback, occlusion, camera picking, reset | Temp/WorldRenderingChecks/Results.txt and PNGs |
| ParticlePixelationChecks.Run | Edit Mode | 48 resolution/rotation/zoom/cell-size cases, seams, native sprites, disabled pixelation, sorting | Temp/ParticlePixelationChecks/Results.txt |
| CastableOutlineChecks.Run | Edit Mode | All 512 3-by-3 masks, geometry coverage, alpha seams, GPU fade, sorting, mesh lifetime | Temp/CastableOutlineChecks/Results.txt |
| CastableOutlineChecks.CheckLifecycle | Play in rectangular fixture | Initialization/reset/Blink/empty range and mesh reuse | Returned PASS string |
| Tests/Blink/Program.cs | Either mode | 2,393 square-range and wall-ray checks | Returned PASS string |
| Tests/Blink/Castable.cs | Fresh rectangular fixture Play | Cached regions, wall occlusion, queued reservations, movement and retained previews | Returned PASS string |
| Tests/Blink/Integration.cs | Fresh rectangular fixture Play | Mana, destination validity, enemies, afterimages, synthetic pointer/keyboard behavior | Returned PASS string |
| Tests/Blink/Animation.cs | Fresh rectangular fixture Play | Easing, cooldown, cancellation, range indicator | Temp/BlinkAnimationResult.txt |
| Tests/Elevation/Corners.cs | Edit Mode | 134 wall/elevation/Blink corner assertions | Returned PASS string |
| Tests/Elevation/Rules.cs | Gameplay scene, Edit Mode | Elevation differences, rays, symmetry, authored glyph/height mapping and palette | Returned PASS string |
| Tests/Elevation/Runtime.cs | Gameplay scene, fresh Play | Painted heights, casting/Blink differences, reactions, paths, occupancy, resets | Returned PASS string |
| Tests/TilemapLevel/Runtime.cs | Gameplay scene, fresh Play | Holes, terrain permissions, coordinates, spawners, reactions, scaling, fallback/reset | Returned PASS string |
| Tests/StoneTileLayout project | .NET CLI | 256 neighborhoods per element, shapes, family/stage separation, boundaries | Console PASS |

## Running editor suites

Use Tools > Automaturgy for the validation menu items, or execute the following method bodies using MCP execute_code:

```csharp
RefactoringChecks.Run();
```

```csharp
WorldRenderingChecks.Run();
```

For GPU suites, start one and wait for its Results.txt to report PASS or FAIL before starting the next:

```csharp
ParticlePixelationChecks.Run();
// After completion:
CastableOutlineChecks.Run();
```

CheckSupport.Runner handles editor iteration, final result reporting, and iterator disposal. WorldRenderingChecks has a separate reload-aware runner because it crosses the Play Mode boundary.

## Runtime fixtures

WorldRenderingChecks builds its fixture automatically. For scripts marked as requiring a rectangular fixture and the outline lifecycle check, first execute in Edit Mode:

```csharp
ValidationFixture.Open();
```

Then enter Play Mode and evaluate the appropriate file through the Unity Pipeline or MCP:

```powershell
unity command eval_file --file Tests/Blink/Castable.cs --json
```

Stop Play Mode after each scenario to discard its board changes and restore the original scene. Open another fixture for the next scenario. For animation checks, wait for the result file before stopping.

ValidationFixture clones the scene as one hierarchy so references between its roots are remapped. Its board is 20 by 20 with the player at (5,5). It disables authored level artwork/markers and uses the rectangular fallback. Rendering settings, sprites, actors, and particle catalogs come from the actual gameplay wiring.

The fixture rejects unsaved scene edits before opening. It never saves its generated scene over the gameplay scene. If a setup failure interrupts a manual run, execute ValidationFixture.Restore.

Elevation and tilemap runtime scripts instead need an assigned TilemapLevel. Open Assets/Scenes/In Game.unity before their fresh Play sessions. They paint temporary runtime maps and must also be discarded by stopping Play.

For inventory pointer, preview, save, scene handoff, and Bootstrap startup checks, follow the [inventory test instructions](../Tests/Inventory/README.md). These checks use isolated saves under Temp/InventoryChecks.

## Standalone shape checks

```powershell
dotnet run --project Tests/StoneTileLayout/StoneTileLayout.Checks.csproj
```

This project links the actual TileSpriteLayout, GridMath, and ElementState sources. It does not duplicate their implementations.

## Baseline provenance

Tests/Refactoring/CombatBaseline.json contains reference results captured from commit e38d584 using seed 71621. Keep these independently captured expectations separate from the implementation under test.

Each of 160 cases contains a 12-by-12 input, existence/elevation arrays, expected output after each phase, and the surviving visual events with their owned cells. Cases combine all current element stages, missing cells, and height discontinuities.

The baseline tests compare every cell in every phase and every surviving visual event. Do not update the baseline merely to make a changed resolver pass. A deliberate gameplay change needs independent review of expected results.

Additional tests use explicit examples and malformed inputs. These cover behaviors that snapshot comparisons alone would not explain.

## Diagnosing failures

- Compilation failure: fix Console errors before running tools against new types.
- Missing effect: compare active V/decay IDs with ParticleCatalog.
- Sprite catalog failure: inspect duplicate keys, null references, and isolated previews.
- Board mismatch: the result identifies the baseline case and phase. Compare that snapshot before looking at presentation.
- Visual mismatch with matching boards: inspect priority/insertion ordering and ownership removal.
- Spawn occupancy mismatch: check that the enemy is registered, active and enabled, and that its world position maps to the expected tile.
- GPU mismatch: inspect captured PNGs, active rendering settings, and material import errors.
- Fixture failure: stop Play if needed, call Restore, inspect the exception, and do not save temporary test changes.

## Coverage and interpretation

The combat baseline covers 160 cases and 480 phase snapshots. Explicit terrain, corner, queue, and runtime tests supplement it; the sample does not cover every possible board.

Presentation and routing checks verify sprite refresh counts and buffer reuse. They do not measure frame time, memory usage, or target-platform performance. GPU results apply to the graphics configuration used for the run; validate other graphics APIs and standalone builds separately.

Generated result files and rendering captures belong under Temp. Consult each suite's output for the result of a run.
