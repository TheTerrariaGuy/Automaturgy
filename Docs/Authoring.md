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
| Assets/Data/Elements | Reaction comparison fixture |
| Assets/Resources/Spells.txt | Authored reaction rules compiled at startup |
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

The painted level is serialized inside `Assets/Scenes/In Game.unity`. Keep Unity assets paired with their `.meta` files when moving them so serialized references retain their GUIDs.

## Add or tune an element stage

Edit ElementDefinitions for alpha, damage, placement cost, decay effect, or default reaction priority. See [combat rules](Combat.md) for element family and stage encoding.

If the stage has special artwork, add an override to Assets/Rendering/ElementSprites.asset. Otherwise let it inherit its family shape. If it emits particles, add/update its ParticleCatalog tile entry.

A stage needs a positive ManaCost and an equipped block granting that placement element to be an available queued placement type. The selector exposes four element keys, restricted by the active loadout.

Player cast range, Blink range, cooldown, and mana cost are serialized on PlayerHandler. Blink animation duration is controlled by blinkSpeed.

## Author a reaction

Edit Assets/Resources/Spells.txt, the TextAsset assigned to the Bootstrap component. Bootstrap compiles it before opening Inventory. See [inventory and reaction grants](Inventory.md) for individual reaction grants and item authoring. Assets/Data/Elements/Reactions.txt is a test comparison fixture.

Each rule occupies one line below an element header:

```text
FIRE
R 1015 V Effect_Name I (1+2,0,20.-203) O (0,0,0) (1+2,0,210) D (0+1+2+3) E
```

- R provides a unique reaction ID in the element's thousands range.
- V is optional and names a ParticleCatalog reaction effect.
- I tuples contain x, y, and accepted tile types. Each field supports set union (+), difference (-), and a one-digit wildcard (.). All expanded input positions must match; types within a field are alternatives.
- O tuples contain x, y, output type, and an optional priority override. Omitted priorities come from the output tile definition. Set-valued fields expand to concrete writes during compilation.
- D contains quarter-turn directions 0 through 3.
- E ends the rule.
- Empty lines, START/END lines, and lines beginning with # are accepted.

The parser validates tuple structure, known types, family requirements, directions, required sections, and trailing tokens. Errors include source, line number, and field. See the [inventory guide](Inventory.md) for captured variables, signed literals, expansion limits, and startup behavior.

Add the corresponding prefab/catalog entry when using a new V identifier. Catalog validation checks those cross-references before accepting authored content. A missing effect should be intentional, represented by omitting V.

## Sprite catalog

SpriteCatalog stores key/value entries and constructs its lookup on demand. Keys are type * 100 + variant:

- 0: default surface.
- 2: isolated preview.
- 2–48: connected shapes, following the [element](ElementSpriteMap.md) and [stone](StoneSpriteMap.md) sprite maps.

TextureHandler first checks the exact type/variant, then a connected family's variant, then the exact type's default, then its fallback sprite.

Use overrides only where artwork differs from the family fallback. Spent-stone variants inherit stone artwork; steam stages use explicit entries.

Validate reports duplicate keys, null sprites, and missing isolated previews for the four selectable elements. TextureHandler handles world fitting, color, and sorting; catalog authoring does not resize colliders.

## Particle authoring

Effect prefabs live beside the particle catalog under Assets/Rendering/Particles/Prefabs. Tile stages share family prefabs and pools; see [particle authoring and catalog](Particles/README.md).

ParticlePrefabAuthoring.Bake adds ground anchors to new hierarchies. Existing baked hierarchies are left intact. Keep multi-cell links split into one-cell edges and keep authored anchors synchronized with pattern cells.

EnemyDamageParticleBuilder rebuilds the four damage effects and updates their catalog entries. Rebuilding intentionally replaces those generated prefabs.

GrassWind animates grass tiles. Grass has no particle catalog entry.

## Levels and editor commands

Paint artwork separately from terrain/spawn markers. Marker elevation must be a finite multiple of 0.5, and a valid level has exactly one player marker. See [level authoring](TilemapLevels.md) for palettes, terrain flags, and spawn settings.

PaletteAuthoring handles loading, painting, saving, and unloading palette prefabs. TilemapLevelSetup converts a rectangular fallback board to tilemaps. ElevationLevelSetup creates elevation markers and the palette and can replace standard floor markers.

WorldRenderingSetup acts on the active gameplay scene and checks required objects, sprites, prefabs, renderer data, and shader before changing settings. It marks the scene dirty for review and updates the required assets and prefabs. Save the scene after reviewing its changes. See [world rendering](WorldRendering.md) for configuration details.

Regression checks use configured content and temporary fixtures. See [validation](Validation.md) for required scenes, commands, and outputs.
