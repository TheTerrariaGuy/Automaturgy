using System;
using System.IO;
using System.Linq;
using Assets.Scripts.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using SquareTile = UnityEngine.Tilemaps.Tile;

/// <summary>Creates the editable inventory scene and starter assets without replacing existing item definitions.</summary>
public static class InventorySceneSetup
{
    private const string ScenePath = "Assets/Scenes/Inventory.unity";
    private const string ItemFolder = "Assets/Resources/InventoryItems";
    private const string ArtFolder = "Assets/Rendering/Inventory";

    [MenuItem("Tools/Grid Mage/Inventory/Set up inventory scene")]
    public static void Create()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run inventory setup outside Play mode.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before setup.");
        Directory.CreateDirectory(ItemFolder); Directory.CreateDirectory(ArtFolder);
        AssetDatabase.Refresh();
        var square = CreateSquare();
        CreateItem("fire", "Fundamental Fire", 100, new[] { 1000, 1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008 }, new Color(1f, .42f, .22f), square.sprite, (0,0), (0,1), (1,1));
        CreateItem("fire_interactions", "Fire Interactions", 0, new[] { 1009, 1010, 1011, 1012, 1013, 1014 }, new Color(1f, .68f, .28f), square.sprite, (0,0), (1,0), (2,0), (1,1));
        CreateItem("water", "Fundamental Water", 200, new[] { 2000 }, new Color(.28f, .65f, 1f), square.sprite, (0,0), (0,1));
        CreateItem("electricity", "Fundamental Electricity", 300, new[] { 3000, 3001, 3002, 3003 }, new Color(.78f, .53f, 1f), square.sprite, (1,0), (0,1), (1,1));
        CreateItem("electricity_interactions", "Electricity Interactions", 0, new[] { 3004, 3005 }, new Color(1f, .87f, .4f), square.sprite, (0,0), (1,0), (1,1), (2,1));
        CreateItem("stone", "Fundamental Stone", 400, Array.Empty<int>(), new Color(.57f, .76f, .65f), square.sprite, (0,0), (1,0), (0,1), (1,1));

        var scene = SceneManager.GetActiveScene().path == ScenePath ? SceneManager.GetActiveScene() : EditorSceneManager.OpenScene(ScenePath);
        var existing = UnityEngine.Object.FindAnyObjectByType<InventoryController>();
        if (existing != null) { ConfigureBuild(); AssetDatabase.SaveAssets(); return; }
        var camera = Camera.main;
        if (camera == null) camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
        camera.orthographic = true; camera.orthographicSize = 6.4f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.055f, .07f, .105f);

        var root = new GameObject("Inventory", typeof(InventoryController));
        var controller = root.GetComponent<InventoryController>();
        controller.viewCamera = camera;
        controller.storage = CreateGrid(root.transform, "Storage 10 x 8", InventoryGrid.Storage, new Vector3(-9, 3.3f), square);
        controller.active = CreateGrid(root.transform, "Active Reactions 5 x 5", InventoryGrid.Active, new Vector3(4, 3.3f), square);

        var canvasObject = new GameObject("Inventory UI", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(root.transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
        canvas.sortingOrder = 20;
        var canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(2040, 1280);
        canvasRect.localScale = Vector3.one * .01f;
        canvasRect.localPosition = new Vector3(0, 0, -1);

        Label(canvas.transform, "Title", "SPELL INVENTORY", new Vector2(0, 545), new Vector2(1800, 75), 42, TextAnchor.MiddleCenter);
        Label(canvas.transform, "Subtitle", "Shape your loadout. Equip fundamental blocks to place elements.", new Vector2(0, 477), new Vector2(1800, 60), 24, TextAnchor.MiddleCenter);
        Label(canvas.transform, "Storage heading", "INVENTORY  /  10 x 8", new Vector2(-400, 382), new Vector2(1000, 60), 28, TextAnchor.MiddleLeft);
        Label(canvas.transform, "Active heading", "ACTIVE REACTIONS  /  5 x 5", new Vector2(650, 382), new Vector2(560, 60), 25, TextAnchor.MiddleLeft);
        controller.activeSummary = Label(canvas.transform, "Enabled elements", "Placement: none — add a fundamental block", new Vector2(660, -245), new Vector2(540, 110), 22, TextAnchor.UpperLeft);
        controller.details = Label(canvas.transform, "Item details", "Drag blocks between grids. Only blocks on the right are active.\nRight-click or Esc cancels a drag.", new Vector2(-390, -545), new Vector2(1040, 115), 22, TextAnchor.UpperLeft);
        controller.status = Label(canvas.transform, "Save status", "Changes save automatically.", new Vector2(640, -555), new Vector2(590, 110), 19, TextAnchor.UpperLeft);
        controller.enterButton = Button(canvas.transform, "Enter Game", new Vector2(650, -360), new Vector2(500, 70), new Color(.2f, .47f, .68f));
        controller.recoverButton = Button(canvas.transform, "Recover", new Vector2(510, -455), new Vector2(220, 55), new Color(.20f, .25f, .34f));
        controller.saveButton = Button(canvas.transform, "Retry Save", new Vector2(790, -455), new Vector2(220, 55), new Color(.20f, .25f, .34f));
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        ConfigureBuild();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
        Debug.Log("Inventory scene and six reaction blocks created.");
    }

    private static SquareTile CreateSquare()
    {
        string texturePath = ArtFolder + "/Square.png";
        if (!File.Exists(texturePath))
        {
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            texture.SetPixels(Enumerable.Repeat(Color.white, 256).ToArray()); texture.Apply();
            File.WriteAllBytes(texturePath, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        string tilePath = ArtFolder + "/Square.asset";
        var tile = AssetDatabase.LoadAssetAtPath<SquareTile>(tilePath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<SquareTile>();
            tile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            tile.colliderType = SquareTile.ColliderType.None; tile.flags = TileFlags.None;
            AssetDatabase.CreateAsset(tile, tilePath);
        }
        return tile;
    }
    private static void CreateItem(string id, string title, int placementElement, int[] reactionIds, Color color, Sprite sprite, params (int x, int y)[] shape)
    {
        string path = ItemFolder + "/" + id + ".asset";
        if (AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(path) != null) return;
        var item = ScriptableObject.CreateInstance<InventoryItemDefinition>();
        item.itemId = id; item.displayName = title; item.placementElement = placementElement;
        item.reactionIds = reactionIds.ToList(); item.color = color; item.sprite = sprite;
        item.shape = shape.Select(p => new Vector2Int(p.x, p.y)).ToList(); item.Validate();
        AssetDatabase.CreateAsset(item, path);
    }
    private static InventoryGridView CreateGrid(Transform parent, string name, InventoryGrid kind, Vector3 origin, SquareTile square)
    {
        var root = new GameObject(name, typeof(Grid), typeof(InventoryGridView));
        root.transform.SetParent(parent, false); root.transform.localPosition = origin;
        var view = root.GetComponent<InventoryGridView>(); view.grid = kind; view.square = square;
        view.background = Map(root.transform, "Background", 0);
        view.items = Map(root.transform, "Items", 1);
        view.ghost = Map(root.transform, "Snapped preview", 2);
        view.DrawBackground(); return view;
    }
    private static Tilemap Map(Transform parent, string name, int order)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(parent, false);
        var renderer = go.GetComponent<TilemapRenderer>(); renderer.sortingOrder = order;
        string materialPath = ArtFolder + "/Inventory Unlit.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        renderer.sharedMaterial = material;
        return go.GetComponent<Tilemap>();
    }
    private static Text Label(Transform parent, string name, string text, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>(); rect.anchoredPosition = position; rect.sizeDelta = size;
        var label = go.GetComponent<Text>(); label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize; label.alignment = alignment; label.color = new Color(.88f, .93f, 1f);
        label.raycastTarget = false; return label;
    }
    private static Button Button(Transform parent, string text, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(text, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>(); rect.anchoredPosition = position; rect.sizeDelta = size;
        var image = go.GetComponent<Image>(); image.color = color;
        var button = go.GetComponent<Button>(); button.targetGraphic = image;
        Label(go.transform, "Label", text, Vector2.zero, size, 24, TextAnchor.MiddleCenter);
        return button;
    }
    private static void ConfigureBuild()
    {
        var paths = new[] { "Assets/Scenes/Bootstrap.unity", ScenePath, "Assets/Scenes/In Game.unity" };
        EditorBuildSettings.scenes = paths.Select(p => new EditorBuildSettingsScene(p, true))
            .Concat(EditorBuildSettings.scenes.Where(s => !paths.Contains(s.path))).ToArray();
    }
}

