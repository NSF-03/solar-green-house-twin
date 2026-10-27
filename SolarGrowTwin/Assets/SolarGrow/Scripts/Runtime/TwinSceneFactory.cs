using System.Collections.Generic;
using UnityEngine;

namespace SolarGrow
{
    /// <summary>
    /// Creates the twin objects (camera, light, houses, controller, panels). Used by the editor menu
    /// SolarGrow/Create Twin Scene and by TwinBootstrap when Play starts in a scene without a TwinController.
    /// </summary>
    public static class TwinSceneFactory
    {
        /// <summary>The config asset at GreenhouseConfig.DefaultAssetPath (editor only), else a fresh instance with defaults.</summary>
        public static GreenhouseConfig LoadDefaultConfig()
        {
#if UNITY_EDITOR
            GreenhouseConfig asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GreenhouseConfig>(GreenhouseConfig.DefaultAssetPath);
            if (asset != null) return asset;
#endif
            GreenhouseConfig c = ScriptableObject.CreateInstance<GreenhouseConfig>();
            c.name = "GreenhouseConfig (built-in defaults)";
            return c;
        }

        /// <summary>A new house: index 0 at the origin with ground/trees/compass, index n at n x houseBOffset.</summary>
        public static GreenhouseBuilder CreateHouse(string id, int index, GreenhouseConfig config)
        {
            GameObject go = new GameObject("Greenhouse " + id);
            GreenhouseBuilder b = go.AddComponent<GreenhouseBuilder>();
            b.greenhouseId = id;
            b.placementIndex = index;
            b.includeSurroundings = index == 0;
            b.config = config;
            b.Rebuild();
            return b;
        }

        static GreenhouseBuilder FindHouse(string id)
        {
            GreenhouseBuilder[] all = UnityEngine.Object.FindObjectsByType<GreenhouseBuilder>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i].greenhouseId == id) return all[i];
            return null;
        }

        static bool HasDirectionalLight()
        {
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
                if (lights[i].type == LightType.Directional) return true;
            return false;
        }

        /// <summary>Build the whole twin in the active scene and return its controller.</summary>
        public static TwinController CreateTwin(GreenhouseConfig config, IList<string> ids)
        {
            if (config == null) config = LoadDefaultConfig();
            if (ids == null || ids.Count == 0) ids = new string[] { "A", "B" };

            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;
            OrbitCamera orbit = cam.GetComponent<OrbitCamera>();
            if (orbit == null) orbit = cam.gameObject.AddComponent<OrbitCamera>();

            if (!HasDirectionalLight())
            {
                GameObject lightGo = new GameObject("Directional Light");
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.shadows = LightShadows.Soft;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            List<GreenhouseBuilder> builders = new List<GreenhouseBuilder>();
            for (int i = 0; i < ids.Count; i++)
            {
                GreenhouseBuilder b = FindHouse(ids[i]);
                if (b == null) b = CreateHouse(ids[i], i, config);
                else if (b.config == null)
                {
                    b.config = config;
                    b.Rebuild();
                }
                builders.Add(b);
            }

            GameObject controllerGo = new GameObject("Twin Controller");
            TwinController controller = controllerGo.AddComponent<TwinController>();
            controller.config = config;
            controller.greenhouseIds = new List<string>(ids);
            controller.houses = builders;
            AdjustPanel panel = controllerGo.AddComponent<AdjustPanel>();
            panel.controller = controller;
            HudPanel hud = controllerGo.AddComponent<HudPanel>();
            hud.controller = controller;

            orbit.controller = controller;
            Quaternion heading = Quaternion.Euler(0f, config.orientationDeg, 0f);
            Vector3 middle = heading * (config.houseBOffset * ((ids.Count - 1) * 0.5f)) + new Vector3(0f, 1f, 0f);
            float span = config.houseBOffset.magnitude * (ids.Count - 1) + config.length;
            orbit.SetView(middle, Mathf.Clamp(span * 1.4f + 3f, 4f, 60f));
            return controller;
        }
    }
}
