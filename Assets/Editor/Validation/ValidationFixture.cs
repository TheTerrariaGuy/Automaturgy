using System;
using System.Linq;
using Assets.Scripts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Clones production wiring into an unsaved, rectangular test scene and restores the user's scene.</summary>
[InitializeOnLoad]
public static class ValidationFixture
{
    private const string RestoreKey = "GridMage.Validation.RestoreScene";
    public const string GameplayScene = "Assets/Scenes/In Game.unity";
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadValidationSpells()
    {
        if (!string.IsNullOrEmpty(SessionState.GetString(RestoreKey, "")))
            Assets.Scripts.Inventory.InventorySession.SetRun(new RunLoadout(ReactionCatalog.Packages.Select(p => p.Id)));
    }
    static ValidationFixture()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) Restore();
        };
    }
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Start validation in Edit Mode.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save scene edits before running fixture validation.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameplayScene) == null)
            throw new InvalidOperationException("Missing gameplay scene: " + GameplayScene);
        SessionState.SetString(RestoreKey, SceneManager.GetActiveScene().path);
        var source = EditorSceneManager.OpenScene(GameplayScene);
        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        // Clone one hierarchy so Unity remaps references between formerly separate scene roots.
        var bundle = new GameObject("Validation source bundle");
        SceneManager.MoveGameObjectToScene(bundle, source);
        foreach (var root in source.GetRootGameObjects())
            if (root != bundle) root.transform.SetParent(bundle.transform, true);
        // Keep source lights inactive while their copies are enabled in the fixture.
        bundle.SetActive(false);
        var copy = UnityEngine.Object.Instantiate(bundle);
        SceneManager.MoveGameObjectToScene(copy, fixture);
        foreach (Transform child in copy.transform.Cast<Transform>().ToArray()) child.SetParent(null, true);
        UnityEngine.Object.DestroyImmediate(copy);
        EditorSceneManager.CloseScene(source, true);
        SceneManager.SetActiveScene(fixture);
        var game = UnityEngine.Object.FindAnyObjectByType<GameLogic>();
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerHandler>();
        if (game == null || player == null) { Restore(); throw new InvalidOperationException("Gameplay wiring is incomplete."); }
        // Restore root names used by presentation assertions after cloning.
        foreach (var root in fixture.GetRootGameObjects()) root.name = root.name.Replace("(Clone)", "");
        var data = new SerializedObject(game);
        data.FindProperty("level").objectReferenceValue = null;
        data.FindProperty("rows").intValue = 20;
        data.FindProperty("cols").intValue = 20;
        data.ApplyModifiedPropertiesWithoutUndo();
        player.r = player.c = 5;
        player.transform.position = game.gridParent.TransformPoint(new Vector3(5 * game.spacing + game.offset, -5 * game.spacing + game.offset));
        foreach (var level in UnityEngine.Object.FindObjectsByType<TilemapLevel>()) level.gameObject.SetActive(false);
    }
    public static void Restore()
    {
        string path = SessionState.GetString(RestoreKey, "");
        if (string.IsNullOrEmpty(path)) return;
        SessionState.EraseString(RestoreKey);
        EditorSceneManager.OpenScene(path);
    }
}
