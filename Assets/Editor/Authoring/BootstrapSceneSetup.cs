using System;
using System.Linq;
using Assets.Scripts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BootstrapSceneSetup
{
    [MenuItem("Tools/Grid Mage/Spells/Set up Bootstrap scene")]
    public static void Create()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run Bootstrap setup outside Play mode.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before setup.");
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<Bootstrap>();
            if (bootstrap == null) bootstrap = new GameObject("Bootstrap", typeof(Bootstrap)).GetComponent<Bootstrap>();
            if (bootstrap.spellSource == null)
                bootstrap.spellSource = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Spells.txt");
            if (bootstrap.spellSource == null) throw new InvalidOperationException("Spells.txt is missing.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var paths = new[] { "Assets/Scenes/Bootstrap.unity", "Assets/Scenes/Inventory.unity", "Assets/Scenes/In Game.unity" };
            EditorBuildSettings.scenes = paths.Select(p => new EditorBuildSettingsScene(p, true))
                .Concat(EditorBuildSettings.scenes.Where(s => !paths.Contains(s.path))).ToArray();
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
    }
}
