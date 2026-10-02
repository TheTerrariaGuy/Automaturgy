using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GridMage.Workshop.Editor
{
    public static class WorkshopSceneSetup
    {
        public const string ScenePath = "Assets/Workshop/Scenes/Workshop.unity";
        [MenuItem("Tools/Automaturgy/Workshop/Create scene")]
        public static void Create()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Create the workshop outside Play mode.");
            if (File.Exists(ScenePath)) return;
            Directory.CreateDirectory("Assets/Workshop/Scenes");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var camera = new GameObject("Workshop Camera").AddComponent<Camera>();
                camera.tag = "MainCamera"; camera.orthographic = true;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(13, 19, 29, 255);
                var light = new GameObject("Workshop Light").AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 0;
                var root = new GameObject("Workshop");
                var tab = root.AddComponent<ReactionWorkshopTab>();
                tab.Configure(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Spells.txt"));
                root.AddComponent<WorkshopController>().Configure(camera, tab);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.Refresh();
        }
        [MenuItem("Tools/Automaturgy/Workshop/Open scene")]
        public static void Open()
        {
            Create();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scene edits before opening Workshop alone.");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }
}
