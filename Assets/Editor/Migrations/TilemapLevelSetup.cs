using System;
using System.Linq;
using Assets.Scripts;
using Assets.Scripts.ScriptableObjects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public static class TilemapLevelSetup
{
    private const string Folder = "Assets/Levels/Markers";

    [MenuItem("Tools/Automaturgy/Levels/Create tilemaps from current grid")]
    public static void Create()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Create level tilemaps outside Play mode.");
        var game = Object.FindAnyObjectByType<GameLogic>();
        var player = Object.FindAnyObjectByType<PlayerHandler>();
        if (game == null || player == null || game.gridParent == null)
            throw new InvalidOperationException("The scene needs GameLogic, a grid parent, and PlayerHandler.");
        if (game.Level != null) { Selection.activeGameObject = game.Level.logic.gameObject; return; }
        var gameData = new SerializedObject(game);
        int rows = gameData.FindProperty("rows").intValue, cols = gameData.FindProperty("cols").intValue;
        if (rows <= 0 || cols <= 0 || game.spacing <= 0f || player.r < 0 || player.r >= rows || player.c < 0 || player.c >= cols)
            throw new InvalidOperationException("The existing grid dimensions and player coordinates must be valid.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Levels", "Markers");
        var prefab = (GameObject)gameData.FindProperty("tile").objectReferenceValue;
        var baseRenderer = prefab.GetComponentsInChildren<SpriteRenderer>().First(r => r.name == "Base");
        Sprite markerSprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Textures/__Player/Border.png").OfType<Sprite>().First();
        var floor = Marker("Floor", markerSprite, new Color(.25f, 1f, .4f, .7f));
        var wall = Marker("Wall", markerSprite, new Color(.5f, .5f, .5f, .9f), walkable: false);
        var start = Marker("Player Spawn", markerSprite, new Color(.2f, .7f, 1f, .9f), spawn: LevelSpawnKind.Player);
        var enemy = Marker("Enemy Spawn", markerSprite, new Color(1f, .3f, .2f, .9f), spawn: LevelSpawnKind.Enemy);
        var spawner = Marker("Enemy Spawner", markerSprite, new Color(1f, .3f, 1f, .9f), spawn: LevelSpawnKind.Enemy, interval: 5f);
        var backgroundTile = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>("Assets/Levels/Tiles/Background Floor.asset");
        if (backgroundTile == null)
        {
            backgroundTile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            backgroundTile.sprite = baseRenderer.sprite;
            backgroundTile.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;
            backgroundTile.transform = FitSprite(backgroundTile.sprite);
            AssetDatabase.CreateAsset(backgroundTile, "Assets/Levels/Tiles/Background Floor.asset");
        }
        CreatePalette(new TileBase[] { floor, wall, start, enemy, spawner, backgroundTile });

        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create level tilemaps");
        var root = new GameObject("Level", typeof(Grid), typeof(TilemapLevel));
        Undo.RegisterCreatedObjectUndo(root, "Create level tilemaps");
        root.transform.SetParent(game.gridParent, false);
        root.transform.localPosition = new Vector3(game.offset - game.spacing * .5f, game.offset - game.spacing * .5f, 0f);
        root.GetComponent<Grid>().cellSize = new Vector3(game.spacing, game.spacing, 1f);
        var level = root.GetComponent<TilemapLevel>();
        var background = Map(root.transform, "Background", WorldSorting.Ground, -2, baseRenderer.sharedMaterial);
        level.logic = Map(root.transform, "Logic", WorldSorting.Foreground, 10, baseRenderer.sharedMaterial);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var cell = new Vector3Int(c, -r, 0);
                background.SetTile(cell, backgroundTile);
                level.logic.SetTile(cell, floor);
            }
        foreach (var cell in LegacyLevelDefaults.EnemyCells)
            if (cell.col < cols && cell.row < rows && (cell.col != player.c || cell.row != player.r))
                level.logic.SetTile(new Vector3Int(cell.col, -cell.row, 0), enemy);
        level.logic.SetTile(new Vector3Int(player.c, -player.r, 0), start);
        gameData.FindProperty("level").objectReferenceValue = level;
        gameData.ApplyModifiedProperties();
        level.Read(game.gridParent);
        EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        Undo.CollapseUndoOperations(undo);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = level.logic.gameObject;
    }

    private static Tilemap Map(Transform parent, string name, string sortingLayer, int order, Material material)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(parent, false);
        var renderer = go.GetComponent<TilemapRenderer>();
        renderer.sortingLayerName = sortingLayer;
        renderer.sortingOrder = order;
        renderer.sharedMaterial = material;
        return go.GetComponent<Tilemap>();
    }

    private static LevelMarkerTile Marker(string name, Sprite sprite, Color color, bool walkable = true,
        LevelSpawnKind spawn = LevelSpawnKind.None, float interval = 0f)
    {
        string path = Folder + "/" + name + ".asset";
        var marker = AssetDatabase.LoadAssetAtPath<LevelMarkerTile>(path);
        if (marker != null) return marker;
        marker = ScriptableObject.CreateInstance<LevelMarkerTile>();
        marker.sprite = sprite;
        marker.color = color;
        marker.transform = FitSprite(sprite);
        marker.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;
        marker.walkable = walkable;
        marker.blocksSight = !walkable;
        marker.allowsSpells = walkable;
        marker.spawnKind = spawn;
        marker.spawnInterval = interval;
        if (spawn == LevelSpawnKind.Enemy)
            marker.enemy = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Normal.asset");
        AssetDatabase.CreateAsset(marker, path);
        return marker;
    }

    private static Matrix4x4 FitSprite(Sprite sprite)
    {
        Vector3 scale = new Vector3(1f / sprite.bounds.size.x, 1f / sprite.bounds.size.y, 1f);
        return Matrix4x4.TRS(-Vector3.Scale(sprite.bounds.center, scale), Quaternion.identity, scale);
    }

    private static void CreatePalette(TileBase[] tiles)
    {
        PaletteAuthoring.Edit("Assets/Levels/Palettes", "Level Palette", map =>
        {
            for (int i = 0; i < tiles.Length; i++) map.SetTile(new Vector3Int(i, 0, 0), tiles[i]);
        }, skipExisting: true);
    }
}
