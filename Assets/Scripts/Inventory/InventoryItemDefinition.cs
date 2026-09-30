using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Inventory
{
    [CreateAssetMenu(fileName = "Reaction Block", menuName = "Grid Mage/Inventory/Reaction Block")]
    public sealed class InventoryItemDefinition : ScriptableObject
    {
        public string itemId;
        public string displayName;
        public int packageId;
        public Color color = Color.white;
        public Sprite sprite;
        [Tooltip("Occupied cells from the top-left bounding corner. X right, Y down. No rotation.")]
        public List<Vector2Int> shape = new() { Vector2Int.zero };
        public int Width => shape.Max(p => p.x) + 1;
        public int Height => shape.Max(p => p.y) + 1;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(itemId) || string.IsNullOrWhiteSpace(displayName))
                throw new InvalidOperationException(name + ": item ID and name are required.");
            if (!ReactionCatalog.Contains(packageId))
                throw new InvalidOperationException(name + ": unknown package " + packageId);
            if (shape == null || shape.Count == 0 || shape.Distinct().Count() != shape.Count ||
                shape.Any(p => p.x < 0 || p.y < 0) || shape.Min(p => p.x) != 0 || shape.Min(p => p.y) != 0)
                throw new InvalidOperationException(name + ": shape must contain unique nonnegative offsets normalized to its top-left bounds.");
        }
    }
}
