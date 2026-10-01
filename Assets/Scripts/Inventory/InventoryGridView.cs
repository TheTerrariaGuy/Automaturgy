using UnityEngine;
using UnityEngine.Tilemaps;
using SquareTile = UnityEngine.Tilemaps.Tile;

namespace Assets.Scripts.Inventory
{
    public sealed class InventoryGridView : MonoBehaviour
    {
        public InventoryGrid grid;
        public Tilemap background, items, ghost;
        public SquareTile square;
        public static Vector3Int Cell(int x, int y) => new(x, -y - 1, 0);

        public void DrawBackground()
        {
            background.ClearAllTiles();
            for (int y = 0; y < InventoryState.Height(grid); y++)
                for (int x = 0; x < InventoryState.Width(grid); x++)
                    Paint(background, Cell(x, y), new Color(.16f, .20f, .27f), .96f);
        }

        public Vector2Int Pick(Camera camera, Vector2 screen)
        {
            Ray ray = camera.ScreenPointToRay(screen);
            var plane = new Plane(transform.forward, transform.position);
            if (!plane.Raycast(ray, out float distance)) return new Vector2Int(int.MinValue, int.MinValue);
            var cell = background.WorldToCell(ray.GetPoint(distance));
            return new Vector2Int(cell.x, -cell.y - 1);
        }
        public bool Contains(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 &&
            cell.x < InventoryState.Width(grid) && cell.y < InventoryState.Height(grid);

        public void Draw(InventoryState state, ItemPlacement dragging = null)
        {
            items.ClearAllTiles();
            foreach (var item in state.Items)
            {
                if (item.grid != grid) continue;
                var definition = state.Definition(item);
                Color color = definition.color;
                if (ReferenceEquals(item, dragging)) color.a = .25f;
                foreach (var offset in definition.shape)
                    Paint(items, Cell(item.x + offset.x, item.y + offset.y), color, .88f);
            }
        }
        public void Preview(InventoryItemDefinition definition, Vector2Int origin, bool valid)
        {
            ghost.ClearAllTiles();
            Color color = valid ? definition.color : new Color(1f, .3f, .35f);
            color.a = .55f;
            foreach (var offset in definition.shape)
                Paint(ghost, Cell(origin.x + offset.x, origin.y + offset.y), color, .92f);
        }
        private void Paint(Tilemap map, Vector3Int cell, Color color, float size)
        {
            map.SetTile(cell, square);
            map.SetTileFlags(cell, TileFlags.None);
            map.SetColor(cell, color);
            map.SetTransformMatrix(cell, Matrix4x4.Scale(new Vector3(size, size, 1f)));
        }
    }
}

