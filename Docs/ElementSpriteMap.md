# Element sprites

`Assets/Rendering/ElementSprites.asset` (SpriteCatalog) uses keys `tile type * 100 + variant`. Variant 0 is the default sprite, 2 is the isolated blob, and 2–48 use the neighborhood order documented in [StoneSpriteMap.md](StoneSpriteMap.md). `TileSpriteLayout` selects these variants for all four elements.

Water joins all eight neighbors like stone, including filled 2×2 corners. `Water_0` through `Water_46` correspond directly to variants 2–48, except for L, T, and four-way junctions. L-junctions use filled corner artwork on both connected sides regardless of diagonals:

| Variants | Sprite | Connections |
|---|---|---|
| 5, 6 | Water_4 | N, E |
| 9, 12 | Water_10 | E, S |
| 16, 36 | Water_34 | N, W |
| 20, 28 | Water_26 | S, W |

T-junctions use thick artwork on all three connected sides regardless of diagonals:

| Variants | Sprite | Connections |
|---|---|---|
| 10, 11, 13, 14 | Water_12 | N, E, S |
| 18, 19, 37, 38 | Water_36 | N, E, W |
| 21, 29, 39, 44 | Water_42 | N, S, W |
| 22, 25, 30, 33 | Water_31 | E, S, W |

Four-way water junctions always use the full `Water_46` tile, regardless of diagonals. This covers `+` shapes and the shared tile where two 2×2 blocks meet at a corner. The affected variants are 23, 24, 26, 27, 31, 32, 34, 35, 40, 41, 42, 43, 45, 46, 47, and 48.

Water uses the ground sorting layer.

Steam stages `210/211` use explicit shape entries from `Steam.png`, with `Steam_12` as the default and isolated preview. The 16 sprites follow the same cardinal layout as the fire/lightning table below. All variants 2–48 are mapped for both stages; variants with the same cardinal connections share a steam sprite regardless of diagonals. Steam connects only within the `210/211` group. Stage definitions control opacity, and the particle catalog controls effects.

Fire and lightning join cardinal neighbors only. Diagonals never fill their corners, including in a solid 2×2 block. Both sheets use this mapping:

| Variant | Sprite suffix | Connections |
|---|---|---|
| 2 | 12 | Isolated |
| 3 | 8 | N |
| 4 | 13 | E |
| 5 | 9 | N, E |
| 7 | 0 | S |
| 8 | 4 | N, S |
| 9 | 1 | E, S |
| 10 | 5 | N, E, S |
| 15 | 15 | W |
| 16 | 11 | N, W |
| 17 | 14 | E, W |
| 18 | 10 | N, E, W |
| 20 | 3 | S, W |
| 21 | 7 | N, S, W |
| 22 | 2 | E, S, W |
| 23 | 6 | N, E, S, W |

Base, spent, and fading states below suffix 10 connect within their element family. Fire, water, and lightning reaction states connect only to reaction states in the same family (110/111, 210/211, or 310/311), separately from stages below suffix 10. Shape entries on the base type apply to all connected stages; a stage-specific shape entry overrides them. Stone reaction states (410/411) stay separate and use isolated fire artwork. Particle effects are configured independently of sprite connections.

Hover previews, queued previews, and the spell selector always use the isolated sprite, regardless of neighbors: `Fire_12`, `Water_0`, `Lightning_12`, or `RockWall_34`. Hover opacity is configured on GridPointer; queued opacity is configured on the tile prefab.

`ElementDefinitions` stage alpha is editable in code. All entries use white RGB. Base states have alpha 255; spent fire/water/lightning start at 240, with lower alpha at subsequent fading stages. Spent stone and second reaction stages use 170. Alpha is applied to the artwork, not to the tile's underlying grass or particles.
