using Assets.Scripts.ScriptableObjects;
using UnityEngine;

namespace Assets.Scripts
{
    public enum LevelSpawnKind { None, Player, Enemy }

    [CreateAssetMenu(fileName = "Level Marker", menuName = "Automaturgy/Level Marker")]
    public sealed class LevelMarkerTile : UnityEngine.Tilemaps.Tile
    {
        [Tooltip("Terrain height in half-unit increments. Blue numbers represent n + 0.5.")]
        public float elevation = 1f;
        public bool walkable = true;
        public bool blocksSight;
        public bool allowsSpells = true;
        public LevelSpawnKind spawnKind;
        public EnemyData enemy;
        [Min(0f), Tooltip("Zero spawns once. Positive values repeat at this interval in seconds.")]
        public float spawnInterval;
    }
}
