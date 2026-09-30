# Authoring and asset organization

## Folder map

| Path | Contents |
|---|---|
| Assets/Scripts/Core/Grid | Grid math and scene coordinate adapter |
| Assets/Scripts/Gameplay | Game coordinator |
| Assets/Scripts/Gameplay/Combat | Board, queue, resolver, rules, stage definitions |
| Assets/Scripts/Gameplay/Actors | Player, enemy management, enemy data type |
| Assets/Scripts/Levels | Level reader and marker type |
| Assets/Scripts/Presentation | Tiles, sprite selection, outlines, sorting, grass |
| Assets/Scripts/Presentation/Particles | Particle catalog, pattern, pixelation, playback |
| Assets/Scripts/Input | Pointer interaction |
| Assets/Scripts/UI | Selection presentation and mana bar |
| Assets/Data/Elements | Legacy reaction comparison fixture |
| Assets/Scripts/Inventory | Inventory state, persistence, Tilemap views, and dragging |
| Assets/Resources/InventoryItems | Editable reaction block definitions |
| Assets/Data/Enemies | EnemyData assets |
| Assets/Levels/Tiles | Background tile assets, including Spring |
| Assets/Levels/Markers | Terrain/spawn markers, including Elevation |
| Assets/Levels/Palettes | Painting palettes |
| Assets/Rendering | Sprite catalog and shared rendering assets |
| Assets/Rendering/Particles | Prefabs, catalog, materials, meshes, shader |
| Assets/Editor/Authoring | Reusable authoring helpers and builders |
| Assets/Editor/Migrations | Existing-level conversion and rendering setup |
| Assets/Editor/Validation | Regression checks and temporary fixtures |

The painted level is still serialized inside In Game.unity. No empty Maps folder or duplicated map asset was introduced. Third-party artwork and TextMesh Pro remain in their existing folders.

## Add or tune an element stage

Edit ElementDefinitions once for alpha, damage, placement cost, or decay effect. Use the existing family encoding unless intentionally changing the protocol.

If the stage has special artwork, add an override to Assets/Rendering/ElementSprites.asset. Otherwise let it inherit its family shape. If it emits particles, add/update its ParticleCatalog tile entry.

A stage needs a positive ManaCost and an equipped fundamental package to be an available queued placement type. The selector exposes the existing four element keys, restricted by the active loadout.

Player cast range, Blink range, cooldown, and mana cost are serialized on PlayerHandler. The migration copied their prior values from Indexing/GameLogic. Blink animation duration remains the existing blinkSpeed field.

## Author a reaction

Edit the hardcoded Definitions string in Assets/Scripts/Gameplay/Combat/ReactionCatalog.cs. See [inventory and reaction packages](../Inventory.md) for package ownership and item authoring. Reactions.txt is retained as a legacy comparison fixture.

Each rule occupies one line below a PACKAGE ID ELEMENT header:

```text
PACKAGE 101 FIRE
R 1015 V Effect_Name I (1,0,200*) O (0,0,0,0) (1,0,210,51) D (0,1,2,3) E
```

- R provides a unique reaction ID in the element's thousands range.
- V is optional and names a ParticleCatalog reaction effect.
- I tuples contain x, y, and required type. A trailing * matches reactive states in a family.
- O tuples contain x, y, exact output type, and priority.
- D contains quarter-turn directions 0 through 3.
- E ends the rule.
- Empty lines, START/END lines, and lines beginning with # are accepted.

The parser validates tuple structure, known types, family requirements, directions, required sections, and trailing tokens. Errors include the line number.

Add the corresponding prefab/catalog entry when using a new V identifier. Catalog validation checks those cross-references before accepting authored content. A missing effect should be intentional, represented by omitting V.

## Sprite catalog

SpriteCatalog stores key/value entries and constructs its lookup on demand. Keys remain type * 100 + variant:

- 0: default surface.
- 2: isolated preview.
- 2–48: connected shapes, following the existing sprite-map documents.

TextureHandler first checks the exact type/variant, then a connected family's variant, then the exact type's default, then its fallback sprite.

The migration removed 72 entries that already matched the family fallback, leaving 229. In particular, all 48 spent-stone entries inherit stone artwork. Explicit steam-stage entries and other actual differences remain.

Validate reports duplicate keys, null sprites, and missing isolated previews for the four selectable elements. TextureHandler handles world fitting, color, and sorting; catalog authoring does not resize colliders.

## Particle authoring

All effect prefabs now live beside the particle catalog under Assets/Rendering/Particles/Prefabs. Shared tile-stage prefabs and their pooling behavior are unchanged.

ParticlePrefabAuthoring.Bake adds ground anchors to new hierarchies. Existing baked hierarchies are left intact. Keep multi-cell links split into one-cell edges and keep authored anchors synchronized with pattern cells.

EnemyDamageParticleBuilder rebuilds the four damage effects and updates their catalog entries. Rebuilding intentionally replaces those generated prefabs.

The retired grass-particle material/mesh and inactive water-diagonal reaction were removed. GrassWind is the active tile-based grass animation. The particle shader no longer contains its unused grass-sway parameters or calculations.

## Levels and editor commands

Continue painting artwork separately from terrain/spawn markers. Marker elevation remains a finite multiple of 0.5, and a valid level has exactly one player marker.

PaletteAuthoring centralizes loading, painting, saving, and unloading palette prefabs. TilemapLevelSetup remains a conversion tool for a rectangular fallback board. ElevationLevelSetup creates elevation markers and the palette and can replace standard floor markers.

WorldRenderingSetup now acts on the active gameplay scene and checks required objects, sprites, prefabs, renderer data, and shader before changing settings. It marks the scene dirty for review rather than automatically saving it. Its asset/prefab changes remain explicit effects of running the setup command.

Do not use a setup/migration command as a prerequisite for ordinary regression checks. Existing authored content is already configured; validation builds a temporary fixture.
