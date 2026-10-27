using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SolarGrow
{
    /// <summary>
    /// Builds one glasshouse from primitives and generated glass meshes under a child named "_Built".
    /// Every part gets a TwinComponent and can be found by id. Rebuilds when the config changes
    /// (edit mode: delayed via EditorApplication.delayCall; play mode: next Update).
    /// The geometry itself is in GreenhouseBuilder.Parts.cs.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public partial class GreenhouseBuilder : MonoBehaviour
    {
        public const string BuiltRootName = "_Built";

        [Tooltip("Shared geometry, sensor and actuator configuration.")]
        public GreenhouseConfig config;

        [Tooltip("Greenhouse id as it appears in the twin/state frames (A, B, ...).")]
        public string greenhouseId = "A";

        [Tooltip("0 = at the origin; n = placed at n x houseBOffset, in the house's own axes.")]
        public int placementIndex = 0;

        [Tooltip("Also build the ground, the trees to the north and the compass (greenhouse A only).")]
        public bool includeSurroundings = true;

        [Tooltip("Let Rebuild set this object's position and heading from the config.")]
        public bool applyPlacement = true;

        [SerializeField, HideInInspector] string builtSignature;

        /// <summary>Raised after every rebuild (the controller re-applies the latest frame).</summary>
        public event Action<GreenhouseBuilder> Rebuilt;

        readonly Dictionary<string, TwinComponent> index = new Dictionary<string, TwinComponent>();
        readonly Dictionary<string, SensorMarker> sensorIndex = new Dictionary<string, SensorMarker>();
        readonly List<TwinComponent> parts = new List<TwinComponent>();
        Transform builtRoot;
        Transform autoSensorRoot;
        int autoSensorCount;
        GreenhouseConfig subscribedConfig;
        bool pendingRebuild;
        bool indexed;

        /// <summary>All parts of the current build.</summary>
        public IList<TwinComponent> Parts { get { return parts; } }

        public bool IsBuilt { get { return builtRoot != null && indexed; } }

        /// <summary>World position of the middle of the house at half eaves height.</summary>
        public Vector3 CenterWorld
        {
            get { return transform.TransformPoint(new Vector3(0f, config != null ? config.eavesHeight * 0.5f : 1f, 0f)); }
        }

        // ------------------------------------------------------------------ lifecycle

        void OnEnable()
        {
            Subscribe();
            if (Application.isPlaying)
            {
                Rebuild();
                return;
            }
#if UNITY_EDITOR
            if (transform.Find(BuiltRootName) == null) ScheduleEditorRebuild();
            else RebuildIndex();
#endif
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        void Update()
        {
            if (pendingRebuild && Application.isPlaying)
            {
                pendingRebuild = false;
                Rebuild();
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying)
            {
                RequestRebuild();
                return;
            }
            Subscribe();
            ScheduleEditorRebuild();
        }

        void ScheduleEditorRebuild()
        {
            UnityEditor.EditorApplication.delayCall -= EditorDelayedRebuild;
            UnityEditor.EditorApplication.delayCall += EditorDelayedRebuild;
        }

        void EditorDelayedRebuild()
        {
            if (this == null || Application.isPlaying || !isActiveAndEnabled) return;
            if (transform.Find(BuiltRootName) != null && builtSignature == Signature())
            {
                if (!indexed) RebuildIndex();
                return;
            }
            Rebuild();
            if (gameObject.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif

        void Subscribe()
        {
            if (subscribedConfig == config) return;
            Unsubscribe();
            if (config != null)
            {
                config.Changed += OnConfigChanged;
                subscribedConfig = config;
            }
        }

        void Unsubscribe()
        {
            if (subscribedConfig != null) subscribedConfig.Changed -= OnConfigChanged;
            subscribedConfig = null;
        }

        void OnConfigChanged()
        {
            RequestRebuild();
        }

        /// <summary>Rebuild at the next Update (play mode) or after the inspector event (edit mode).</summary>
        public void RequestRebuild()
        {
            if (Application.isPlaying)
            {
                pendingRebuild = true;
                return;
            }
#if UNITY_EDITOR
            ScheduleEditorRebuild();
#endif
        }

        string Signature()
        {
            string c = config != null ? JsonUtility.ToJson(config) : "null";
            return c + "|" + greenhouseId + "|" + placementIndex + "|" + includeSurroundings + "|" + applyPlacement;
        }

        /// <summary>Destroy the old parts and build the house again from the config.</summary>
        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            pendingRebuild = false;
            Subscribe();
            ClearBuilt();
            if (config == null)
            {
                builtSignature = "";
                return;
            }
            config.Sanitize();
            ApplyPlacement();
            GameObject rootGo = new GameObject(BuiltRootName);
            builtRoot = rootGo.transform;
            builtRoot.SetParent(transform, false);
            autoSensorRoot = null;
            autoSensorCount = 0;
            BuildAll();
            indexed = true;
            if (!Application.isPlaying) builtSignature = Signature();
            Action<GreenhouseBuilder> handler = Rebuilt;
            if (handler != null) handler(this);
        }

        void ApplyPlacement()
        {
            if (!applyPlacement || config == null) return;
            Quaternion heading = Quaternion.Euler(0f, config.orientationDeg, 0f);
            transform.localRotation = heading;
            transform.localPosition = placementIndex > 0 ? heading * (config.houseBOffset * placementIndex) : Vector3.zero;
        }

        void ClearBuilt()
        {
            index.Clear();
            sensorIndex.Clear();
            parts.Clear();
            indexed = false;
            Transform old = builtRoot != null ? builtRoot : transform.Find(BuiltRootName);
            int guard = 0;
            while (old != null && guard++ < 16)
            {
                DestroyGeneratedMeshes(old);
                if (Application.isPlaying)
                {
                    old.gameObject.SetActive(false);
                    old.SetParent(null, false);
                    Destroy(old.gameObject);
                }
                else
                {
                    DestroyImmediate(old.gameObject);
                }
                old = transform.Find(BuiltRootName);
            }
            builtRoot = null;
            autoSensorRoot = null;
        }

        static void DestroyGeneratedMeshes(Transform root)
        {
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh m = filters[i].sharedMesh;
                if (m == null || !m.name.StartsWith(MeshUtil.GeneratedPrefix, StringComparison.Ordinal)) continue;
                filters[i].sharedMesh = null;
                if (Application.isPlaying) Destroy(m);
                else DestroyImmediate(m);
            }
        }

        /// <summary>Re-read the parts of an existing build (after a domain reload in the editor).</summary>
        void RebuildIndex()
        {
            index.Clear();
            sensorIndex.Clear();
            parts.Clear();
            builtRoot = transform.Find(BuiltRootName);
            if (builtRoot == null)
            {
                indexed = false;
                return;
            }
            TwinComponent[] all = builtRoot.GetComponentsInChildren<TwinComponent>(true);
            for (int i = 0; i < all.Length; i++)
            {
                TwinComponent c = all[i];
                parts.Add(c);
                if (!string.IsNullOrEmpty(c.id) && !index.ContainsKey(c.id)) index[c.id] = c;
                SensorMarker m = c as SensorMarker;
                if (m != null && !string.IsNullOrEmpty(m.id) && !sensorIndex.ContainsKey(m.id)) sensorIndex[m.id] = m;
            }
            autoSensorRoot = builtRoot.Find("auto-sensors");
            autoSensorCount = autoSensorRoot != null ? autoSensorRoot.childCount : 0;
            indexed = true;
        }

        // ------------------------------------------------------------------ lookup

        public TwinComponent FindComponent(string componentId)
        {
            TwinComponent c;
            if (componentId != null && index.TryGetValue(componentId, out c) && c != null) return c;
            return null;
        }

        public SensorMarker FindSensor(string sensorId)
        {
            SensorMarker m;
            if (sensorId != null && sensorIndex.TryGetValue(sensorId, out m) && m != null) return m;
            return null;
        }

        /// <summary>Add every part whose id equals idOrPrefix or starts with idOrPrefix + "-". Returns the count.</summary>
        public int FindComponents(string idOrPrefix, List<TwinComponent> into)
        {
            if (string.IsNullOrEmpty(idOrPrefix) || into == null) return 0;
            int found = 0;
            int n = idOrPrefix.Length;
            for (int i = 0; i < parts.Count; i++)
            {
                TwinComponent c = parts[i];
                if (c == null || c.id == null) continue;
                string cid = c.id;
                if (cid == idOrPrefix || (cid.Length > n && cid[n] == '-' && cid.StartsWith(idOrPrefix, StringComparison.Ordinal)))
                {
                    into.Add(c);
                    found++;
                }
            }
            return found;
        }

        /// <summary>Marker for a sensor id that is not in the config: a row above the ridge, so no reading is lost.</summary>
        public SensorMarker EnsureSensorMarker(string sensorId)
        {
            SensorMarker m = FindSensor(sensorId);
            if (m != null) return m;
            if (builtRoot == null || config == null || string.IsNullOrEmpty(sensorId)) return null;
            if (autoSensorRoot == null) autoSensorRoot = Group("auto-sensors", builtRoot);
            int k = autoSensorCount++;
            float step = 0.35f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((config.length - 0.2f) / step));
            Vector3 pos = new Vector3(-config.length * 0.5f + 0.2f + (k % perRow) * step,
                                      config.ridgeHeight + 0.35f + (k / perRow) * 0.3f, 0f);
            m = CreateSensorMarker(sensorId, SensorKind.Other, pos, sensorId, autoSensorRoot);
            m.autoCreated = true;
            return m;
        }

        // ------------------------------------------------------------------ part helpers

        Transform Group(string groupName, Transform parent)
        {
            GameObject go = new GameObject(groupName);
            go.transform.SetParent(parent != null ? parent : builtRoot, false);
            return go.transform;
        }

        T Register<T>(GameObject go, string partId, TwinKind partKind) where T : TwinComponent
        {
            T c = go.AddComponent<T>();
            c.id = UniqueId(partId);
            c.kind = partKind;
            c.greenhouseId = greenhouseId;
            parts.Add(c);
            index[c.id] = c;
            SensorMarker m = c as SensorMarker;
            if (m != null) sensorIndex[m.id] = m;
            return c;
        }

        string UniqueId(string partId)
        {
            if (string.IsNullOrEmpty(partId)) partId = "part";
            if (!index.ContainsKey(partId)) return partId;
            int k = 2;
            while (index.ContainsKey(partId + "#" + k)) k++;
            return partId + "#" + k;
        }

        static GameObject Prim(PrimitiveType type, string objectName, Transform parent, Vector3 localPos,
                               Quaternion localRot, Vector3 localScale, Material mat, bool castShadows = true)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = objectName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = mat;
                if (!castShadows) r.shadowCastingMode = ShadowCastingMode.Off;
            }
            return go;
        }

        TwinComponent Part(PrimitiveType type, string partId, TwinKind partKind, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = Prim(type, partId, parent, pos, Quaternion.identity, scale, mat);
            return Register<TwinComponent>(go, partId, partKind);
        }

        /// <summary>Square bar from a to b (local coordinates of parent).</summary>
        TwinComponent Bar(string partId, Transform parent, Vector3 a, Vector3 b, float thickness, Material mat)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return null;
            GameObject go = Prim(PrimitiveType.Cube, partId, parent, (a + b) * 0.5f,
                                 Quaternion.FromToRotation(Vector3.up, d / len), new Vector3(thickness, len, thickness), mat);
            return Register<TwinComponent>(go, partId, TwinKind.Frame);
        }

        /// <summary>One object holding glass slabs for several convex polygons (local coordinates of parent).</summary>
        TwinComponent Glass(string partId, Transform parent, List<Vector3[]> polys, Vector3 outwardHint, float thickness, Material mat)
        {
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            for (int i = 0; i < polys.Count; i++) MeshUtil.AddSlab(verts, tris, polys[i], thickness, outwardHint);
            if (verts.Count == 0) return null;
            GameObject go = new GameObject(partId);
            go.transform.SetParent(parent, false);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = MeshUtil.Build(greenhouseId + ":" + partId, verts, tris);
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return Register<TwinComponent>(go, partId, TwinKind.Glass);
        }
    }
}
