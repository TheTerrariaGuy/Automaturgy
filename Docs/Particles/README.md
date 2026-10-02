# Tile and reaction particles

`Assets/Rendering/Particles/Prefabs` contains seven looping tile family prefabs (27 catalog stages), 19 one-shot reaction prefabs and four enemy damage prefabs, authored for the XY grid. All visible children use layer `PixelVFX` (6), which renders alongside sprites through `Main Camera`. Their shared `Element Particle` shader pixelates effects on a screen-aligned grid.

Shared meshes, materials, shader and catalog live in `Assets/Rendering/Particles`. Authoring scripts live in `Assets/Editor/Authoring`; validation scripts live in `Assets/Editor/Validation`. Documentation and previews live here in `Docs/Particles`. None of the authoring tools or previews are required for playback.

Preview sheets: [tiles](Previews/Tiles.png), [pixelated tiles](Previews/Tiles_Pixel.png), [reactions](Previews/Reactions.png). Entries run left to right, top to bottom, in filename order; matching `.txt` files list each entry. These are artwork references and can include effects outside the active catalog. Use the catalog tables below for playback configuration. The shader measures cells in game pixels at 16 pixels per world unit, scaling with camera zoom and resolution; see the [rendering example](../Rendering/Particle6px.png).

Tile effects occupy a centered 1 × 1 square. Place a tile prefab under its tile with local position zero, rotation identity and scale one; it inherits the tile's transform. Emission covers the square surface, with restrained drift and particle sizes suitable for the pixelated display. Shared unlit materials use particle color and preserve render-target alpha. Mesh silhouettes provide flames, blades, droplets, wavelets, rings and angular bolts without texture dependencies.

All tile emitters are centered at tile-local X/Y zero. Every active emission shape is an unrotated 1 × 1 rectangle centered at zero, spanning −0.5 to +0.5 on both axes. Particle size and movement can extend beyond the emission area. Runtime playback preserves the prefab's authored root scale.

Grass (tile type 0) intentionally has no particle catalog entry. Returning an elemental tile to grass releases its effect. Missing prefab references are treated as no effect during playback.

## Tiles

| Tile IDs | Shared prefab under `Assets/Rendering/Particles/Prefabs/` | Appearance |
| --- | --- | --- |
| 0 | No prefab | Grass uses its tile sprite without particles |
| 100–107 | `Tile_100_Fire` | Flame bed, hot cores and embers, progressively fading |
| 110, 111 | `Tile_110_Fire_Flare` | Golden flare and its fading state |
| 200–207 | `Tile_200_Water` | Wavelets, ripples and glints, progressively fading |
| 210, 211 | `Tile_210_Steam_Geyser` | Steam wisps and boiling droplets |
| 300–302 | `Tile_300_Electricity` | Violet arcs and pale ion sparks, progressively fading |
| 310, 311 | `Tile_310_Electricity_Charged` | Pink plasma wisps, violet coronas, bright filaments and white-hot knots |
| 410, 411 | `Tile_410_Lava` | Orange molten pools, bubbling currents and growing dark crust |
| 400, 401 | No prefab | Ordinary stone emits no particles |

`ParticleCatalog` defines each stage's appearance using the seven shared family prefabs.

## Reactions

Names below have the `Reaction_` prefix and `.prefab` extension, under `Assets/Rendering/Particles/Prefabs/`. Cardinal and diagonal layouts preserve the actual canonical output cells in `Assets/Resources/Spells.txt`. `(x,y)` below uses those grid coordinates, with positive y going down on screen.

| Casting family + requirement | Prefab suffix | Interpretation |
| --- | --- | --- |
| Fire + fire `(1,0)` | `Fire_Fire_Cardinal_Spread` | Contact ignition spreads to the three surrounding output cells |
| Fire + fire `(1,1)` | `Fire_Fire_Diagonal_Spread` | Diagonal ignition spreads to the four output cells |
| Water + water `(1,0)` | `Water_Water_Cardinal_Surge` | Four sequential splashes along the row |
| Fire + water family `(1,0)` | `Fire_Water_Cardinal_Geyser` | Five pressure jets with droplets and rising steam |
| Fire + water family `(0,0)` | `Fire_Water_Overlap_SteamRing` | Central eruption and the eight-cell diamond of steam jets |
| Electricity + electricity `(1,0)` | `Electricity_Electricity_Cardinal_Chain` | Contact bolt forks into two diagonal, three-hop chains |
| Electricity + electricity `(1,1)` | `Electricity_Electricity_Diagonal_Chain` | Contact bolt forks into horizontal and vertical chains |
| Electricity + water family `(1,0)` | `Electricity_Water_Cardinal_Conduction` | Water contact splash conducts electricity through two more cells |
| Electricity + water family `(1,1)` | `Electricity_Water_Diagonal_Conduction` | Same conduction along the diagonal |
| Fire + electricity family `(1,0)` | `Fire_Electricity_Cardinal_PlasmaFork` | Hot plasma contact forks into two ionized blooms |
| Fire + electricity family `(0,0)` | `Fire_Electricity_Overlap_PlasmaCross` | Central ignition with four remote pairs of linked plasma blooms |
| Fire + stone family `(1,0)` | `Fire_Stone_Cardinal_LavaFlow` | Contact stone melts into three square pools with molten spatters |
| Fire + stone family `(0,0)` | `Fire_Stone_Overlap_LavaEruption` | Local lava eruption settles into a cooling molten pool |
| Fire `107 → 0` | `Fire_Burnout` | Final embers cool into drifting ash |
| Electricity `302 → 0` | `Electricity_Discharge` | Last arcs snap away, followed by residual ions |
| Fade map `110 → 111` | `Fire_Flare_Decay` | Golden flame puffs in four neighboring cells |
| Fade map `210 → 211` | `Water_Steam_Decay` | Steam dissipates into four neighboring cells |
| Fade map `310 → 311` | `Electricity_Charge_Decay` | Smaller plasma blooms dissipate into four neighboring cells |
| Fade map `410 → 411` | `Lava_Cooling_Decay` | Lava spreads to four neighboring cells and cools into dark crust |

The intermediate fire transitions `101 → 102 → … → 107` and electricity `301 → 302` use the corresponding tile-state prefabs; they do not need a fresh explosion on each tick. Water's commented-out `201 → … → 207 → 0` rules are not active reactions. The commented-out diagonal fire–electricity overlap rule is also excluded.

## Plasma and lava patterns

Fire–electricity reactions produce plasma, and fire–stone reactions produce lava. Their visuals follow each rule's output pattern:

| Casting family + requirement | Actual outputs | Visual treatment |
| --- | --- | --- |
| Fire + electricity family `(1,0)` | Plasma 310 at `(1,0)`, `(2,1)`, `(2,-1)` | White-hot contact, thick pink filaments and forked plasma blooms |
| Fire + electricity family `(0,0)` | 310 at distances 3 and 4 on all four cardinal axes | Central ignition and remote plasma pairs; filaments only connect each 3-to-4 pair, preserving the gaps at distances 1 and 2 |
| Fire + stone family `(1,0)` | Lava 410 at `(1,0)`, `(1,1)`, `(1,-1)` | Three aligned square pools, gold upwelling, orange spatters and cooling crust |
| Fire + stone family `(0,0)` | Lava 410 at the origin | One square molten pool with an eruption and cooling surface |

## Enemy damage

`MobScript.TakeDamage` plays a short hit effect on the enemy for each element that deals positive damage during that tick. Multiple electricity tiles combine into one electrical visual while all their damage still applies. Grass and ordinary stone do not deal damage or trigger a hit effect.

| Family | Prefab | Appearance |
| --- | --- | --- |
| Fire (1) | `Damage_Fire` | Orange flame tongues and rising gold embers |
| Water (2) | `Damage_Water` | Blue droplets and a cyan impact ripple |
| Electricity (3) | `Damage_Electricity` | Violet jagged arcs and bright sparks |
| Stone/lava (4) | `Damage_Lava` | Molten chips, a hot orange ripple and dark smoke |

The `damage` entries in `ParticleCatalog` map element families to these prefabs. Hits use the same pool as other one-shot effects. They follow the enemy's position, sort immediately over its feet anchor in the World layer, and finish at the last position if the enemy dies. Grid reset clears active hits. Typical particle lifetimes are 0.15–0.75 seconds.

`Tools > Automaturgy > Particles > Rebuild enemy damage effects` regenerates only these four prefabs and updates their catalog entries. The builder is `Assets/Editor/Authoring/EnemyDamageParticleBuilder.cs`. `EnemyDamageChecks` runs with the world rendering checks and covers all four damage sources, overlapping elements, duplicate electrical hits, pool reuse, movement, lethal hits and cleanup.

[Damage preview, left to right: fire, water, electricity, lava](../Rendering/EnemyDamage.png)

## Placement and playback

`In Game/GameHandler` has a `ParticleVFX` component linked to `Assets/Rendering/Particles/ParticleCatalog.asset`. Press Play to use the effects. `ParticlePixelation` on the main camera sets the shared particle cell size in game pixels (component default 6, current scene 4, at 16 pixels per world unit); characters, tiles and HUD retain native rendering. Disable the component for native-resolution particles. Particle destinations and one-cell links have separate world-Y sorting anchors. See [World rendering](../WorldRendering.md) for the shader, camera, sorting and input setup.

`Tile.ChangeType` maintains one pooled tile effect through an opaque handle. Each catalog tile entry selects a shared family prefab and stores root scale plus named emitter settings (start color, lifetime color, emission rate and particle limit). Emitter names must be unique within a tile prefab and match the stage entries. All stages of a family share one pool, including effects first spawned at a faded stage. Unchanged tiles keep playing; fading stages update emission and color on the existing systems. Queued spells use the existing overlay until submitted. Ordinary stone has no effect. Effects follow their tile's active state; resetting the grid releases and reuses them before removing the old tiles.

Reaction rules carry a `V` effect name and direction in `Assets/Resources/Spells.txt`. `ReactionResolver` tracks the owner of each accepted write across fading, normal reactions and overlaps. Only surviving outputs play at the end of the tick. This also removes duplicate symmetric bursts. The four spread/fade effects follow the cells actually accepted by the fading phase.

Each reaction prefab's `ParticlePattern` lists destination cells and bolt endpoints. `ParticleVFX` masks rejected groups before playback, starts only visible systems, and returns completed effects to the pool. A bolt needs both endpoints to survive, except its casting origin. Lava conversions may reach a stone destination; intervening stone still blocks them.

Reaction roots sit at the casting/source cell, not the contacted cell. Their children include grid offsets at spacing 1. Parent the root to `gridParent`, set its local position to the source tile center, and set uniform scale to `GameLogic.spacing`. To match `GridMath.Rotate(x, y, direction)`, rotate the root around Z by **−90 × direction** degrees, since `GridHelper` inverts grid y. Do not scale each child separately.

Geyser reactions (`Fire_Water_Cardinal_Geyser` and `Fire_Water_Overlap_SteamRing`) keep their emitters facing world up. Cast direction rotates their destination layout without tilting the jets or steam. This is controlled by `ParticlePattern.keepEmittersUpright` and reapplied on pooled playback.

Reaction effects play once and finish within 2.8 authored seconds. Their simulation speed follows the combat clock; combat still resolves on its existing tick. For a standalone unmasked preview, call the root ParticleSystem's `Play(true)`. Directional variants are rotations of these prefabs rather than duplicates.

## Authoring

Edit shared emitter geometry and motion in the seven tile prefabs; edit stage scale, colors, particle limits and emission rates in `ParticleCatalog`. Add new stages by referencing a family prefab and supplying settings for each named emitter.

Sorting anchors are saved in every catalog prefab. `ParticlePrefabAuthoring.Bake` builds anchors for a newly authored hierarchy: tile systems share a center anchor, reaction parts use destination or link-midpoint anchors, and damage effects use a single anchor with a `Visuals` child. The enemy damage builder calls this before saving its prefabs. Runtime playback does not create or rearrange sorting groups. When editing a reaction layout, keep its saved anchors aligned with `ParticlePattern` cells.

Run `WorldRenderingChecks.Run` in an isolated project using `-batchmode -executeMethod WorldRenderingChecks.Run -logFile Validation.log` (omit `-quit`; checks exit Unity themselves). Checks cover all 27 tile stages and seven shared pools, saved anchors, rotated reactions, upright geysers, damage effects, movement, lethal hits, cleanup, particle/sprite occlusion, tile picking and grid reset. Results and screenshots are saved under `Temp/WorldRenderingChecks` in that project. See [validation](../Validation.md) for fixture requirements and other suites.

The damage rebuild menu updates prefabs and catalog entries; preview sheets are maintained separately.
