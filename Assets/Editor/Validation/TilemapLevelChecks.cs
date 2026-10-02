using static CheckSupport;
using System;
using Assets.Scripts;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public static class TilemapLevelChecks
{
    [MenuItem("Tools/Automaturgy/Levels/Check tilemap parsing")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run parsing checks outside Play mode.");
        var root = new GameObject("Temporary level checks", typeof(Grid), typeof(TilemapLevel));
        var floor = ScriptableObject.CreateInstance<LevelMarkerTile>();
        var player = ScriptableObject.CreateInstance<LevelMarkerTile>();
        var unknown = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        try
        {
            player.spawnKind = LevelSpawnKind.Player;
            root.transform.position = new Vector3(17f, -11f, 0f);
            root.transform.localScale = Vector3.one * 1.25f;
            var mapObject = new GameObject("Logic", typeof(Tilemap), typeof(TilemapRenderer));
            mapObject.transform.SetParent(root.transform, false);
            var map = mapObject.GetComponent<Tilemap>();
            var level = root.GetComponent<TilemapLevel>();
            level.logic = map;
            map.SetTile(new Vector3Int(-4, 3, 0), player);
            map.SetTile(new Vector3Int(-2, 1, 0), floor);
            // Painting and erasing far away must not enlarge the runtime arrays.
            map.SetTile(new Vector3Int(30, 30, 0), floor);
            map.SetTile(new Vector3Int(30, 30, 0), null);
            var layout = level.Read(root.transform);
            Require(layout.exists.GetLength(0) == 3 && layout.exists.GetLength(1) == 3, "Bounds must use occupied markers.");
            Require(layout.exists[0, 0] && layout.exists[2, 2] && !layout.exists[1, 1], "Holes must stay absent.");
            Require(layout.player == Vector2Int.zero, "Top-left player conversion.");
            Require(layout.ToCell(2, 2) == new Vector3Int(-2, 1, 0), "Negative-coordinate inverse conversion.");
            Require((root.transform.TransformPoint(layout.origin) - map.GetCellCenterWorld(layout.ToCell(0, 0))).sqrMagnitude < .00001f,
                "Translated and scaled grid centers must align.");
            floor.walkable = false;
            Require(layout.walkable[2, 2], "Runtime flags must be copied from marker assets.");
            level.HideMarkers();
            Require(!map.GetComponent<TilemapRenderer>().enabled && map.GetTile(layout.ToCell(0, 0)) == player,
                "Hiding must retain the authored data.");
            Require(level.Read(root.transform).exists[2, 2], "Hidden maps must be reloadable.");
            map.SetTile(new Vector3Int(-3, 2, 0), player);
            Reject(() => level.Read(root.transform), "Duplicate player spawns");
            map.SetTile(new Vector3Int(-3, 2, 0), unknown);
            Reject(() => level.Read(root.transform), "Unknown marker type");
            map.SetTile(new Vector3Int(-3, 2, 0), null);
            root.GetComponent<Grid>().cellSize = new Vector3(2f, 1f, 1f);
            Reject(() => level.Read(root.transform), "Non-square cells");
            root.GetComponent<Grid>().cellSize = Vector3.one;
            root.GetComponent<Grid>().cellGap = new Vector3(.2f, .2f, 0f);
            Reject(() => level.Read(root.transform), "Cell gaps");
            root.GetComponent<Grid>().cellGap = Vector3.zero;
            map.SetTile(new Vector3Int(-4, 3, 0), floor);
            Reject(() => level.Read(root.transform), "Missing player spawn");
            map.ClearAllTiles();
            Reject(() => level.Read(root.transform), "Empty level");
            Debug.Log("PASS: tilemap bounds, holes, coordinate conversion, shared assets, hiding/reload, and invalid authoring checks.");
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(floor);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(unknown);
        }
    }

    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new Exception(message + " must be rejected.");
    }

}
