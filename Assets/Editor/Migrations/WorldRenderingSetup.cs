using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using ComponentUtility = UnityEditorInternal.ComponentUtility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class WorldRenderingSetup
{
    public static void ConfigureCastableOutline(Assets.Scripts.GameLogic game)
    {
        ConfigureOutline(game, false);
        ConfigureOutline(game, true);
    }

    private static void ConfigureOutline(Assets.Scripts.GameLogic game, bool movement)
    {
        const string materialPath = "Assets/Rendering/CastableOutline.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Grid Mage/Element Particle"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.SetColor("_Tint", Color.white);
        EditorUtility.SetDirty(material);
        string name = movement ? "Moveable Outline" : "Castable Outline";
        var player = game.gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Assets.Scripts.PlayerHandler>(true)).Single();
        var data = new SerializedObject(game);
        var reference = data.FindProperty(movement ? "moveableOutline" : "castableOutline");
        var existing = (CastableOutline)reference.objectReferenceValue;
        var root = existing != null ? existing.transform : player.transform.Find(name) ?? game.gridParent.Find(name);
        if (root == null)
        {
            root = new GameObject(name).transform;
        }
        root.SetParent(player.transform, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;
        root.gameObject.layer = game.gridParent.gameObject.layer;
        var outline = root.GetComponent<CastableOutline>() ?? root.gameObject.AddComponent<CastableOutline>();
        var outlineData = new SerializedObject(outline);
        outlineData.FindProperty("region").enumValueIndex = movement ? 1 : 0;
        outlineData.FindProperty("highlightColor").colorValue = movement ? new Color(1f, .85f, .1f, 1f) : new Color(.15f, .45f, 1f, 1f);
        outlineData.FindProperty("opacity").floatValue = movement ? .7f : .85f;
        outlineData.FindProperty("fadeWidth").floatValue = movement ? .2f : .06f;
        outlineData.FindProperty("inset").floatValue = movement ? .14f : 0f;
        outlineData.ApplyModifiedPropertiesWithoutUndo();
        var renderer = root.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = WorldSorting.Foreground;
        renderer.sortingOrder = movement ? 0 : -1;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        reference.objectReferenceValue = outline;
        data.ApplyModifiedPropertiesWithoutUndo();
        outline.Rebuild(game);
    }

    public static void ConfigureHoverOverlay(GridPointer pointer, Transform gridParent)
    {
        var hover = gridParent.Find("Hover");
        if (hover == null)
        {
            hover = new GameObject("Hover", typeof(SpriteRenderer)).transform;
            hover.SetParent(gridParent, false);
        }
        var overlay = hover.GetComponent<SpriteRenderer>();
        var tile = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tile.prefab").GetComponent<Tile>();
        overlay.sharedMaterial = tile.SurfaceRenderer.sharedMaterial;
        overlay.sortingLayerName = WorldSorting.Foreground;
        overlay.sortingOrder = 1;
        overlay.enabled = false;
        var data = new SerializedObject(pointer);
        overlay.sprite = (Sprite)data.FindProperty("borderSprite").objectReferenceValue;
        overlay.color = new Color(1f, 1f, 1f, data.FindProperty("hoverAlpha").floatValue);
        data.FindProperty("hoverOverlay").objectReferenceValue = overlay;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Tools/Automaturgy/Rendering/Configure Y-sorted world")]
    public static void Configure()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Configure rendering outside Play mode.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var game = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Assets.Scripts.GameLogic>(true)).SingleOrDefault();
        var player = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Assets.Scripts.PlayerHandler>(true)).SingleOrDefault();
        var view = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c => c.CompareTag("MainCamera"));
        var rendererAsset = AssetDatabase.LoadAssetAtPath<Renderer2DData>("Assets/Settings/Renderer2D.asset");
        var tilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tile.prefab");
        var mobPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mob.prefab");
        if (game == null || game.gridParent == null || player == null || view == null || rendererAsset == null ||
            tilePrefab == null || mobPrefab == null || view.GetComponent<Physics2DRaycaster>() == null ||
            Shader.Find("Grid Mage/Element Particle") == null)
            throw new InvalidOperationException("Active scene must contain complete gameplay/camera wiring and required rendering assets.");
        if (!AssetDatabase.LoadAllAssetsAtPath("Assets/Textures/__Player/Border.png").OfType<Sprite>().Any() ||
            !AssetDatabase.LoadAllAssetsAtPath("Assets/Textures/__Player/TeleportTo.png").OfType<Sprite>().Any())
            throw new InvalidOperationException("Missing pointer sprites.");
        ConfigureLayers();
        var renderer = new SerializedObject(rendererAsset);
        renderer.FindProperty("m_TransparencySortMode").intValue = (int)TransparencySortMode.CustomAxis;
        renderer.FindProperty("m_TransparencySortAxis").vector3Value = Vector3.up;
        renderer.ApplyModifiedPropertiesWithoutUndo();

        view.cullingMask = LayerMask.GetMask("Default", "TransparentFX", "PixelVFX", "Lighting", "UI");
        view.targetTexture = null;
        view.eventMask = LayerMask.GetMask("UI");
        view.GetComponent<Physics2DRaycaster>().eventMask = LayerMask.GetMask("UI");
        view.transparencySortMode = TransparencySortMode.CustomAxis;
        view.transparencySortAxis = Vector3.up;
        if (view.GetComponent<ParticlePixelation>() == null) view.gameObject.AddComponent<ParticlePixelation>();
        var pointer = game.GetComponent<GridPointer>() ?? game.gameObject.AddComponent<GridPointer>();
        var pointerData = new SerializedObject(pointer);
        pointerData.FindProperty("viewCamera").objectReferenceValue = view;
        pointerData.FindProperty("borderSprite").objectReferenceValue = AssetDatabase.LoadAllAssetsAtPath("Assets/Textures/__Player/Border.png").OfType<Sprite>().First();
        pointerData.FindProperty("teleportSprite").objectReferenceValue = AssetDatabase.LoadAllAssetsAtPath("Assets/Textures/__Player/TeleportTo.png").OfType<Sprite>().First();
        pointerData.ApplyModifiedPropertiesWithoutUndo();
        ConfigureHoverOverlay(pointer, game.gridParent);
        ConfigureCastableOutline(game);

        foreach (string name in new[] { "ManaBar", "Button", "Clock", "Mana Label" })
        {
            var root = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == name)?.gameObject;
            if (root == null) continue;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LayerMask.NameToLayer("UI");
            foreach (var sprite in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sprite.sortingLayerName = WorldSorting.UI;
                sprite.sortingOrder = Mathf.RoundToInt(-sprite.transform.position.z * 100f);
            }
            foreach (var hudCanvas in root.GetComponentsInChildren<Canvas>(true))
            {
                hudCanvas.sortingLayerName = WorldSorting.UI;
                hudCanvas.sortingOrder = 500;
            }
        }
        ConfigureActor(player.gameObject);
        foreach (var light in Object.FindObjectsByType<Light2D>())
        {
            var lightData = new SerializedObject(light);
            var layers = lightData.FindProperty("m_ApplyToSortingLayers");
            layers.arraySize = SortingLayer.layers.Length;
            for (int i = 0; i < layers.arraySize; i++) layers.GetArrayElementAtIndex(i).intValue = SortingLayer.layers[i].id;
            lightData.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.MarkSceneDirty(scene);

        const string mobPath = "Assets/Prefabs/Mob.prefab";
        var mob = PrefabUtility.LoadPrefabContents(mobPath);
        ConfigureActor(mob);
        PrefabUtility.SaveAsPrefabAsset(mob, mobPath);
        PrefabUtility.UnloadPrefabContents(mob);

        const string tilePath = "Assets/Prefabs/Tile.prefab";
        var tile = PrefabUtility.LoadPrefabContents(tilePath);
        foreach (var t in tile.GetComponentsInChildren<Transform>(true))
            t.localPosition = new Vector3(t.localPosition.x, t.localPosition.y, 0f);
        PrefabUtility.SaveAsPrefabAsset(tile, tilePath);
        PrefabUtility.UnloadPrefabContents(tile);
        AssetDatabase.SaveAssets();
        Debug.Log("World rendering configured: native-resolution world, particle-only screen grid, Y anchors and view-space picking.");
    }

    private static void ConfigureActor(GameObject actor)
    {
        actor.transform.localPosition = new Vector3(actor.transform.localPosition.x, actor.transform.localPosition.y, 0f);
        var original = actor.GetComponent<SpriteRenderer>();
        if (original == null) return; // Already migrated.
        float feet = original.sprite.bounds.min.y;
        var group = WorldSorting.CreateGroup(actor.transform, new Vector3(0, feet, 0), "Feet Y anchor");
        var visual = new GameObject("Sprite");
        visual.layer = actor.layer;
        visual.transform.SetParent(actor.transform, false);
        ComponentUtility.CopyComponent(original);
        ComponentUtility.PasteComponentAsNew(visual);
        WorldSorting.Include(group, visual.GetComponent<SpriteRenderer>(), 0);
        Object.DestroyImmediate(original);
    }

    private static void ConfigureLayers()
    {
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tags.FindProperty("m_SortingLayers");
        string[] names = { WorldSorting.Ground, WorldSorting.World, WorldSorting.Foreground, WorldSorting.UI };
        for (int i = 0; i < names.Length; i++)
        {
            bool exists = false;
            for (int j = 0; j < layers.arraySize; j++)
                exists |= layers.GetArrayElementAtIndex(j).FindPropertyRelative("name").stringValue == names[i];
            if (exists) continue;
            int index = i == 0 ? 0 : layers.arraySize;
            layers.InsertArrayElementAtIndex(index);
            var layer = layers.GetArrayElementAtIndex(index);
            layer.FindPropertyRelative("name").stringValue = names[i];
            layer.FindPropertyRelative("uniqueID").intValue = 1800100 + i;
            layer.FindPropertyRelative("locked").boolValue = false;
        }
        tags.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void ConfigureAndCheck()
    {
        try
        {
            Configure();
            WorldRenderingChecks.Run();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
