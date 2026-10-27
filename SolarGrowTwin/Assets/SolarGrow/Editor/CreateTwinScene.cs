using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SolarGrow
{
    /// <summary>Menu SolarGrow: create the twin scene, create or reset the default config, rebuild the houses.</summary>
    public static class CreateTwinScene
    {
        const string ScenePath = "Assets/SolarGrow/Scenes/Twin.unity";

        [MenuItem("SolarGrow/Create Twin Scene", false, 1)]
        public static void CreateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("SolarGrow", "Stop Play mode first.", "OK");
                return;
            }
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("SolarGrow", ScenePath + " exists. Replace it with a new twin scene?", "Replace", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            GreenhouseConfig config = GetOrCreateConfigAsset();
            EnsureFolder("Assets/SolarGrow/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            TwinController controller = TwinSceneFactory.CreateTwin(config, new string[] { "A", "B" });
            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene, ScenePath))
            {
                AddToBuildSettings(ScenePath);
                Debug.Log("SolarGrow: saved " + ScenePath + ". Press Play; the simulated source starts by itself.");
            }
            else
            {
                Debug.LogError("SolarGrow: could not save " + ScenePath);
            }
            Selection.activeGameObject = controller.gameObject;
        }

        [MenuItem("SolarGrow/Create Default Config", false, 2)]
        public static void CreateDefaultConfig()
        {
            GreenhouseConfig existing = AssetDatabase.LoadAssetAtPath<GreenhouseConfig>(GreenhouseConfig.DefaultAssetPath);
            if (existing != null)
            {
                if (EditorUtility.DisplayDialog("SolarGrow", GreenhouseConfig.DefaultAssetPath + " exists. Reset every value to the defaults?", "Reset", "Keep"))
                {
                    string keepName = existing.name;
                    GreenhouseConfig fresh = ScriptableObject.CreateInstance<GreenhouseConfig>();
                    EditorUtility.CopySerialized(fresh, existing);
                    Object.DestroyImmediate(fresh);
                    existing.name = keepName;
                    existing.NotifyChanged();
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssets();
                }
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }
            GreenhouseConfig created = GetOrCreateConfigAsset();
            Selection.activeObject = created;
            EditorGUIUtility.PingObject(created);
        }

        [MenuItem("SolarGrow/Rebuild Greenhouses", false, 3)]
        public static void RebuildAll()
        {
            GreenhouseBuilder[] builders = Object.FindObjectsByType<GreenhouseBuilder>(FindObjectsSortMode.None);
            for (int i = 0; i < builders.Length; i++) builders[i].Rebuild();
            if (!EditorApplication.isPlaying)
            {
                for (int i = 0; i < builders.Length; i++)
                    if (builders[i].gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(builders[i].gameObject.scene);
            }
            Debug.Log("SolarGrow: rebuilt " + builders.Length + " greenhouse(s).");
        }

        static GreenhouseConfig GetOrCreateConfigAsset()
        {
            GreenhouseConfig existing = AssetDatabase.LoadAssetAtPath<GreenhouseConfig>(GreenhouseConfig.DefaultAssetPath);
            if (existing != null) return existing;
            EnsureFolder(Path.GetDirectoryName(GreenhouseConfig.DefaultAssetPath).Replace('\\', '/'));
            GreenhouseConfig config = ScriptableObject.CreateInstance<GreenhouseConfig>();
            AssetDatabase.CreateAsset(config, GreenhouseConfig.DefaultAssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("SolarGrow: created " + GreenhouseConfig.DefaultAssetPath);
            return config;
        }

        static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            if (slash <= 0) return;
            string parent = path.Substring(0, slash);
            string name = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        static void AddToBuildSettings(string path)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
                if (scenes[i].path == path) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
