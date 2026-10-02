# Automaturgy documentation

Automaturgy is a grid-based elemental combat game built in Unity. Open `Assets/Scenes/Bootstrap.unity` and enter Play mode to configure an inventory loadout and start a run.

## Development guides

- [Architecture and ownership](Architecture.md): runtime components, board state, initialization, data flow, and coordinates.
- [Combat rules](Combat.md): reaction phases, priorities, mana reservations, movement, and visual ownership.
- [Authoring and asset organization](Authoring.md): asset locations, element stages, reactions, sprites, particles, and editor tools.
- [Validation](Validation.md): test suites, fixtures, commands, outputs, and failure diagnosis.

## System and artwork references

- [Inventory, item shapes, reaction grants, and saves](Inventory.md)
- [Level authoring and coordinates](TilemapLevels.md)
- [World rendering and sorting](WorldRendering.md)
- [Element sprite mapping](ElementSpriteMap.md)
- [Stone sprite mapping](StoneSpriteMap.md)
- [Particle authoring and catalog](Particles/README.md)
