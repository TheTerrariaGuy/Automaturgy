using System;
using System.Linq;
using Assets.Scripts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public static class ElevationLevelSetup
{
    private const string Folder = "Assets/Levels/Markers/Elevation";
    private const string Sheet = "Assets/Textures/__Technical/Elevation.png";

    [MenuItem("Tools/Automaturgy/Levels/Use elevation tiles")]
    public static void Configure()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Configure elevation outside Play mode.");
        var game = Object.FindAnyObjectByType<GameLogic>();
        if (game == null || game.Level == null) throw new InvalidOperationException("Assign a TilemapLevel first.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Levels/Markers", "Elevation");
        var tiles = new LevelMarkerTile[16];
        var sprites = AssetDatabase.LoadAllAssetsAtPath(Sheet).OfType<Sprite>().ToArray();
        for (int i = 0; i < tiles.Length; i++)
        {
            // Rows: white 1..4, blue 1..4, white 5..8, blue 5..8.
            float height = 1 + (i / 8) * 4 + i % 4 + ((i / 4) % 2 == 1 ? .5f : 0f);
            string name = "Elevation " + height.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            string path = Folder + "/" + name + ".asset";
            var marker = AssetDatabase.LoadAssetAtPath<LevelMarkerTile>(path);
            if (marker == null)
            {
                marker = ScriptableObject.CreateInstance<LevelMarkerTile>();
                marker.name = name;
                marker.sprite = sprites.Single(s => s.name == "Elevation_" + i);
                marker.elevation = height;
                marker.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;
                marker.transform = Matrix4x4.Scale(new Vector3(1f / marker.sprite.bounds.size.x,
                    1f / marker.sprite.bounds.size.y, 1f));
                AssetDatabase.CreateAsset(marker, path);
            }
            tiles[i] = marker;
        }
        CreatePalette(tiles);
        var logic = game.Level.logic;
        var flat = AssetDatabase.LoadAssetAtPath<LevelMarkerTile>("Assets/Levels/Markers/Floor.asset");
        Undo.RegisterCompleteObjectUndo(logic, "Replace flat floor markers with elevation tiles");
        foreach (var cell in logic.cellBounds.allPositionsWithin)
            if (logic.GetTile(cell) == flat) logic.SetTile(cell, tiles[0]);
        WorldRenderingSetup.ConfigureCastableOutline(game);
        game.Level.Read(game.gridParent);
        EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = logic.gameObject;
        Debug.Log("Elevation tiles and blue/yellow borders configured. Paint heights on Logic using Elevation Palette.");
    }

    private static void CreatePalette(LevelMarkerTile[] tiles)
    {
        PaletteAuthoring.Edit("Assets/Levels/Palettes", "Elevation Palette", map =>
        {
            for (int i = 0; i < tiles.Length; i++)
                map.SetTile(new Vector3Int((i / 8) * 4 + i % 4, -(i / 4 % 2), 0), tiles[i]);
            string[] names = { "Wall", "Player Spawn", "Enemy Spawn", "Enemy Spawner" };
            for (int i = 0; i < names.Length; i++)
                map.SetTile(new Vector3Int(i, -3, 0),
                    AssetDatabase.LoadAssetAtPath<LevelMarkerTile>("Assets/Levels/Markers/" + names[i] + ".asset"));
        });
    }
}
