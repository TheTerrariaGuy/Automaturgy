using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Inventory
{
    public enum InventoryGrid { Storage, Active }

    [Serializable]
    public sealed class ItemPlacement
    {
        public string instanceId;
        public string itemId;
        public InventoryGrid grid;
        public int x, y;
        public ItemPlacement Copy() => (ItemPlacement)MemberwiseClone();
    }

    [Serializable]
    public sealed class InventorySaveData
    {
        public int version = 1;
        public List<ItemPlacement> items = new();
        public List<ItemPlacement> recovery = new();
    }

    /// <summary>Owns placements; tilemaps are views. Moves between grids commit as one operation.</summary>
    public sealed class InventoryState
    {
        public const int StorageWidth = 10, StorageHeight = 8, ActiveWidth = 5, ActiveHeight = 5;
        private readonly Dictionary<string, InventoryItemDefinition> definitions;
        private readonly List<ItemPlacement> items = new();
        private readonly List<ItemPlacement> recovery = new();
        private readonly ItemPlacement[,] storage = new ItemPlacement[StorageWidth, StorageHeight];
        private readonly ItemPlacement[,] active = new ItemPlacement[ActiveWidth, ActiveHeight];
        public IReadOnlyList<ItemPlacement> Items => items.AsReadOnly();
        public IReadOnlyList<ItemPlacement> Recovery => recovery.AsReadOnly();
        public event Action Changed;
        public InventoryItemDefinition Definition(ItemPlacement item) => definitions[item.itemId];
        public static int Width(InventoryGrid grid) => grid == InventoryGrid.Storage ? StorageWidth : ActiveWidth;
        public static int Height(InventoryGrid grid) => grid == InventoryGrid.Storage ? StorageHeight : ActiveHeight;
        private static bool ValidGrid(InventoryGrid grid) => grid == InventoryGrid.Storage || grid == InventoryGrid.Active;
        private ItemPlacement[,] Cells(InventoryGrid grid) => grid == InventoryGrid.Storage ? storage : active;

        public InventoryState(IEnumerable<InventoryItemDefinition> catalog, InventorySaveData data)
        {
            definitions = new Dictionary<string, InventoryItemDefinition>(StringComparer.Ordinal);
            foreach (var definition in catalog)
            {
                definition.Validate();
                if (!definitions.TryAdd(definition.itemId, definition))
                    throw new InvalidOperationException("Duplicate item definition ID: " + definition.itemId);
            }
            if (data == null || data.version != 1 || data.items == null || data.recovery == null)
                throw new InvalidOperationException("Unsupported or invalid inventory save.");
            var instances = new HashSet<string>();
            foreach (var saved in data.items)
            {
                if (saved == null) throw new InvalidOperationException("Inventory contains a null item record.");
                var item = saved.Copy();
                if (string.IsNullOrWhiteSpace(item.instanceId) || !instances.Add(item.instanceId) ||
                    !CanPlace(item, item.grid, item.x, item.y)) recovery.Add(item);
                else { items.Add(item); Fill(item); }
            }
            recovery.AddRange(data.recovery.Select(p => p?.Copy() ?? throw new InvalidOperationException("Null recovery record.")));
        }

        public ItemPlacement At(InventoryGrid grid, int x, int y) =>
            ValidGrid(grid) && x >= 0 && y >= 0 && x < Width(grid) && y < Height(grid) ? Cells(grid)[x, y] : null;

        public bool CanPlace(ItemPlacement item, InventoryGrid grid, int x, int y)
        {
            if (item == null || !ValidGrid(grid) || item.itemId == null || !definitions.TryGetValue(item.itemId, out var definition)) return false;
            if (x < 0 || y < 0 || x > Width(grid) - definition.Width || y > Height(grid) - definition.Height) return false;
            foreach (var offset in definition.shape)
            {
                var occupied = At(grid, x + offset.x, y + offset.y);
                if (occupied != null && !ReferenceEquals(occupied, item)) return false;
            }
            return true;
        }

        public bool Move(ItemPlacement item, InventoryGrid grid, int x, int y)
        {
            if (!items.Contains(item) || !CanPlace(item, grid, x, y)) return false;
            if (item.grid == grid && item.x == x && item.y == y) return true;
            foreach (var offset in Definition(item).shape) Cells(item.grid)[item.x + offset.x, item.y + offset.y] = null;
            item.grid = grid; item.x = x; item.y = y;
            Fill(item);
            Changed?.Invoke();
            return true;
        }

        public int RecoverAvailable()
        {
            int count = 0;
            foreach (var item in recovery.ToArray())
            {
                if (string.IsNullOrWhiteSpace(item.instanceId) || items.Any(p => p.instanceId == item.instanceId)) continue;
                bool placed = false;
                for (int y = 0; y < StorageHeight && !placed; y++)
                    for (int x = 0; x < StorageWidth && !placed; x++)
                        if (CanPlace(item, InventoryGrid.Storage, x, y))
                        {
                            item.grid = InventoryGrid.Storage; item.x = x; item.y = y;
                            items.Add(item); recovery.Remove(item); Fill(item); count++; placed = true;
                        }
            }
            if (count > 0) Changed?.Invoke();
            return count;
        }

        private void Fill(ItemPlacement item)
        {
            foreach (var offset in Definition(item).shape) Cells(item.grid)[item.x + offset.x, item.y + offset.y] = item;
        }
        public RunLoadout BuildLoadout() => new(items.Where(i => i.grid == InventoryGrid.Active).Select(i => Definition(i).packageId));
        public InventorySaveData Snapshot() => new()
        {
            items = items.Select(i => i.Copy()).ToList(), recovery = recovery.Select(i => i.Copy()).ToList()
        };

        public static InventorySaveData NewProfile(IEnumerable<InventoryItemDefinition> catalog)
        {
            var state = new InventoryState(catalog, new InventorySaveData());
            foreach (var definition in state.definitions.Values.OrderBy(d => d.packageId))
                state.recovery.Add(new ItemPlacement { instanceId = Guid.NewGuid().ToString("N"), itemId = definition.itemId });
            state.RecoverAvailable();
            return state.Snapshot();
        }
    }
}
