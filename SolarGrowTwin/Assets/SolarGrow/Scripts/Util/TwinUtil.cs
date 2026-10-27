using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SolarGrow
{
    /// <summary>
    /// Main-thread helpers: materials made in code (cached by colour), IMGUI scale, world labels and the
    /// screen areas covered by the panels (so the camera ignores mouse input there).
    /// </summary>
    public static class TwinUtil
    {
        static int colorId, emissionId;
        static bool idsReady;

        /// <summary>Shader property ids, looked up on first use (never from a static constructor).</summary>
        public static int ColorId { get { EnsureIds(); return colorId; } }
        public static int EmissionId { get { EnsureIds(); return emissionId; } }

        static void EnsureIds()
        {
            if (idsReady) return;
            colorId = Shader.PropertyToID("_Color");
            emissionId = Shader.PropertyToID("_EmissionColor");
            idsReady = true;
        }

        /// <summary>IMGUI scale factor (1 = 100 %); both panels and the labels use it.</summary>
        public static float UiScale = 1f;
        public static bool ShowSensorLabels = true;
        public static bool ShowDeviceLabels = true;

        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static Shader litShader;
        static readonly Rect[] uiRects = new Rect[4];
        static readonly GUIContent tmpContent = new GUIContent();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Needed when "Enter Play Mode Options" skip the domain reload.
            for (int i = 0; i < uiRects.Length; i++) uiRects[i] = new Rect(0, 0, 0, 0);
            materials.Clear();
        }

        // ------------------------------------------------------------------ materials

        static Shader LitShader
        {
            get
            {
                if (litShader == null) litShader = Shader.Find("Standard");
                if (litShader == null) litShader = Shader.Find("Legacy Shaders/Diffuse");
                if (litShader == null) litShader = Shader.Find("Unlit/Color");
                if (litShader == null) litShader = Shader.Find("Sprites/Default");
                return litShader;
            }
        }

        /// <summary>Opaque Standard material; emissive = the _EMISSION keyword is on so a property block can light it.</summary>
        public static Material GetMaterial(Color color, bool emissive = false)
        {
            return Get(color, false, emissive, 0.3f);
        }

        /// <summary>Transparent Standard material (rendering mode Transparent) for glass.</summary>
        public static Material GetGlassMaterial(Color color)
        {
            return Get(color, true, false, 0.85f);
        }

        static Material Get(Color color, bool transparent, bool emissive, float smoothness)
        {
            string key = ColorUtility.ToHtmlStringRGBA(color) + (transparent ? "t" : "o") + (emissive ? "e" : "n");
            Material m;
            if (materials.TryGetValue(key, out m) && m != null) return m;
            m = Create(color, transparent, emissive, smoothness);
            m.name = "SolarGrow " + key;
            materials[key] = m;
            return m;
        }

        static Material Create(Color color, bool transparent, bool emissive, float smoothness)
        {
            Shader shader = LitShader;
            Material m = new Material(shader);
            m.color = color;
            if (shader != null && shader.name == "Standard")
            {
                m.SetFloat("_Glossiness", smoothness);
                m.SetFloat("_Metallic", 0f);
                if (transparent) MakeTransparent(m);
                if (emissive)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", Color.black);
                }
            }
            return m;
        }

        /// <summary>The same steps the Standard shader inspector takes for Rendering Mode = Transparent.</summary>
        public static void MakeTransparent(Material m)
        {
            m.SetFloat("_Mode", 3f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        /// <summary>Tint renderers through a property block (no new materials).</summary>
        public static void Tint(Renderer[] renderers, MaterialPropertyBlock block, Color color, Color emission)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(ColorId, color);
                block.SetColor(EmissionId, emission);
                r.SetPropertyBlock(block);
            }
        }

        // ------------------------------------------------------------------ IMGUI

        public static void ApplyGuiScale()
        {
            float s = Mathf.Clamp(UiScale, 0.5f, 3f);
            GUI.matrix = Mathf.Abs(s - 1f) < 0.001f
                ? Matrix4x4.identity
                : Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
        }

        public static float GuiWidth { get { return Screen.width / Mathf.Clamp(UiScale, 0.5f, 3f); } }
        public static float GuiHeight { get { return Screen.height / Mathf.Clamp(UiScale, 0.5f, 3f); } }

        /// <summary>Suggested scale from the screen DPI (96 dpi = 1).</summary>
        public static float SuggestedUiScale()
        {
            float dpi = Screen.dpi;
            if (dpi <= 0f) return 1f;
            return Mathf.Clamp(Mathf.Round(dpi / 96f * 4f) / 4f, 1f, 2f);
        }

        /// <summary>Remember the GUI-space rect a panel covers (slot 0-3); an empty rect clears it.</summary>
        public static void SetUiRect(int slot, Rect rect)
        {
            if (slot >= 0 && slot < uiRects.Length) uiRects[slot] = rect;
        }

        /// <summary>True when the mouse (Input.mousePosition) is over a panel or an IMGUI control is being dragged.</summary>
        public static bool IsPointerOverUi(Vector3 mousePosition)
        {
            if (GUIUtility.hotControl != 0) return true;
            float s = Mathf.Clamp(UiScale, 0.5f, 3f);
            Vector2 p = new Vector2(mousePosition.x / s, (Screen.height - mousePosition.y) / s);
            for (int i = 0; i < uiRects.Length; i++)
                if (uiRects[i].width > 0f && uiRects[i].Contains(p)) return true;
            return false;
        }

        /// <summary>Draw text in a box at a world position (call from OnGUI after ApplyGuiScale).</summary>
        public static void DrawWorldLabel(Camera cam, Vector3 world, string text, GUIStyle style)
        {
            if (cam == null || string.IsNullOrEmpty(text) || style == null) return;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f) return;
            float s = Mathf.Clamp(UiScale, 0.5f, 3f);
            float x = sp.x / s;
            float y = (Screen.height - sp.y) / s;
            tmpContent.text = text;
            Vector2 size = style.CalcSize(tmpContent);
            Rect box = new Rect(x - size.x * 0.5f - 4f, y - size.y - 6f, size.x + 8f, size.y + 4f);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x + 4f, box.y + 2f, size.x, size.y), tmpContent, style);
        }

        /// <summary>"12 s ago", "3 min ago", "2.1 h ago".</summary>
        public static string FormatAge(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) return "?";
            if (seconds < 0) return "in the future";
            if (seconds < 90) return ((int)seconds).ToString() + " s ago";
            if (seconds < 5400) return ((int)(seconds / 60)).ToString() + " min ago";
            return (seconds / 3600).ToString("0.0") + " h ago";
        }
    }
}
