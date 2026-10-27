using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SolarGrow
{
    /// <summary>
    /// Right-hand IMGUI panel: per greenhouse the latest mean T/RH, twin estimate, device and contact states,
    /// energy/power if any sensor reports them, node status and a history + forecast graph.
    /// Also draws the world labels next to sensors and devices.
    /// </summary>
    [DisallowMultipleComponent]
    public class HudPanel : MonoBehaviour
    {
        [Tooltip("Controller whose houses are shown.")]
        public TwinController controller;

        [Tooltip("Show the panel (a small HUD button brings it back).")]
        public bool visible = true;

        [Tooltip("Panel width in GUI units.")]
        public float panelWidth = 360f;

        [Tooltip("Graph height in GUI units.")]
        public float graphHeight = 120f;

        [Tooltip("Minutes of forecast shown right of 'now'.")]
        public float forecastMinutes = 120f;

        [Tooltip("Index of the history window: 0 = 30 min, 1 = 1 h, 2 = 6 h, 3 = 24 h.")]
        [Range(0, 3)] public int historyWindow = 1;

        static readonly string[] WindowNames = { "30 min", "1 h", "6 h", "24 h" };
        static readonly float[] WindowMinutes = { 30f, 60f, 360f, 1440f };
        const string Legend = "orange = mean T, blue = mean RH; solid = measured, dashed = forecast; grey line = now";

        readonly List<ForecastGraph> graphs = new List<ForecastGraph>();
        readonly List<string> texts = new List<string>();
        readonly List<int> textVersions = new List<int>();
        readonly List<float> textTimes = new List<float>();
        readonly StringBuilder sb = new StringBuilder(512);
        readonly List<Action> pending = new List<Action>();
        GUIStyle textStyle, smallStyle, headerStyle, worldStyle, tempAxisStyle, rhAxisStyle;
        Vector2 scroll;

        void OnDestroy()
        {
            for (int i = 0; i < graphs.Count; i++) graphs[i].Dispose();
            graphs.Clear();
            TwinUtil.SetUiRect(1, new Rect(0, 0, 0, 0));
        }

        void OnDisable()
        {
            TwinUtil.SetUiRect(1, new Rect(0, 0, 0, 0));
        }

        void InitStyles()
        {
            if (textStyle != null) return;
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, richText = false };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true, richText = false };
            headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            worldStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = false, richText = false };
            tempAxisStyle = new GUIStyle(smallStyle) { wordWrap = false };
            tempAxisStyle.normal.textColor = new Color(1f, 0.6f, 0.35f, 1f);
            rhAxisStyle = new GUIStyle(smallStyle) { wordWrap = false, alignment = TextAnchor.UpperRight };
            rhAxisStyle.normal.textColor = new Color(0.5f, 0.75f, 1f, 1f);
        }

        void OnGUI()
        {
            if (controller == null) controller = GetComponent<TwinController>();
            if (controller == null) return;
            GUI.depth = 10;
            if (Event.current.type == EventType.Layout && pending.Count > 0)
            {
                for (int i = 0; i < pending.Count; i++) pending[i]();
                pending.Clear();
            }
            TwinUtil.ApplyGuiScale();
            InitStyles();
            if (Event.current.type == EventType.Repaint) DrawWorldLabels();

            float w = TwinUtil.GuiWidth, h = TwinUtil.GuiHeight;
            if (!visible)
            {
                Rect b = new Rect(w - 70f, 10f, 60f, 24f);
                TwinUtil.SetUiRect(1, b);
                if (GUI.Button(b, "HUD")) pending.Add(() => visible = true);
                return;
            }
            float pw = Mathf.Min(panelWidth, w * 0.45f);
            Rect area = new Rect(w - pw - 10f, 10f, pw, h - 20f);
            TwinUtil.SetUiRect(1, area);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Live twin", headerStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Hide", GUILayout.Width(50f))) pending.Add(() => visible = false);
            GUILayout.EndHorizontal();
            GUILayout.Label(controller.SourceDetail(), smallStyle);
            historyWindow = GUILayout.Toolbar(Mathf.Clamp(historyWindow, 0, WindowNames.Length - 1), WindowNames);
            scroll = GUILayout.BeginScrollView(scroll);
            int n = controller.HouseCount;
            if (n == 0) GUILayout.Label("No greenhouse yet.", textStyle);
            for (int i = 0; i < n; i++) DrawHouse(i, controller.GetHouse(i), pw);
            GUILayout.Label(Legend, smallStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawWorldLabels()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            List<TwinComponent> list = TwinComponent.Active;
            for (int i = 0; i < list.Count; i++)
            {
                TwinComponent c = list[i];
                if (c == null) continue;
                string text = c.GetLabelText();
                if (text == null) continue;
                TwinUtil.DrawWorldLabel(cam, c.LabelAnchor, text, worldStyle);
            }
        }

        void DrawHouse(int index, HouseRuntime house, float pw)
        {
            if (house == null) return;
            while (graphs.Count <= index)
            {
                graphs.Add(new ForecastGraph(320, 120));
                texts.Add("");
                textVersions.Add(int.MinValue);
                textTimes.Add(-100f);
            }
            float now = Time.realtimeSinceStartup;
            if (Event.current.type == EventType.Layout && (textVersions[index] != house.Version || now - textTimes[index] > 1f))
            {
                texts[index] = ComposeText(house);
                textVersions[index] = house.Version;
                textTimes[index] = now;
            }

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(texts[index], textStyle);
            Rect r = GUILayoutUtility.GetRect(pw - 40f, graphHeight, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                ForecastGraph g = graphs[index];
                float minutes = WindowMinutes[Mathf.Clamp(historyWindow, 0, WindowMinutes.Length - 1)];
                Texture2D tex = g.Render(house, TwinJson.NowUnixSeconds(), minutes, forecastMinutes);
                GUI.DrawTexture(r, tex);
                if (g.HasData)
                {
                    CultureInfo inv = CultureInfo.InvariantCulture;
                    GUI.Label(new Rect(r.x + 3f, r.y + 1f, 80f, 16f), g.TempMax.ToString("0.0", inv) + " °C", tempAxisStyle);
                    GUI.Label(new Rect(r.x + 3f, r.yMax - 16f, 80f, 16f), g.TempMin.ToString("0.0", inv) + " °C", tempAxisStyle);
                    GUI.Label(new Rect(r.xMax - 83f, r.y + 1f, 80f, 16f), g.RhMax.ToString("0", inv) + " %", rhAxisStyle);
                    GUI.Label(new Rect(r.xMax - 83f, r.yMax - 16f, 80f, 16f), g.RhMin.ToString("0", inv) + " %", rhAxisStyle);
                    float nx = r.x + g.NowFraction * r.width;
                    GUI.Label(new Rect(nx - 14f, r.yMax - 16f, 40f, 16f), "now", smallStyle);
                }
                else
                {
                    GUI.Label(new Rect(r.x + 6f, r.y + 6f, r.width - 12f, 20f), "no mean T/RH yet", smallStyle);
                }
            }
            GUILayout.EndVertical();
        }

        string ComposeText(HouseRuntime h)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            sb.Length = 0;
            sb.Append("Greenhouse ").Append(h.Id);
            TwinStateFrame f = h.LastFrame;
            if (f == null)
            {
                sb.Append(": no data yet");
                return sb.ToString();
            }
            sb.Append("   (").Append(h.FrameCount).Append(" frames, last ")
              .Append(TwinUtil.FormatAge(Time.realtimeSinceStartup - h.LastFrameRealtime)).Append(')');

            DateTimeOffset at;
            float t, rh;
            JObject mean = f.Mean();
            if (f.TryGetMean(out at, out t, out rh))
            {
                sb.Append("\nMean ").Append(t.ToString("0.0", inv)).Append(" °C");
                if (!float.IsNaN(rh)) sb.Append("   ").Append(rh.ToString("0", inv)).Append(" % RH");
                double count, spread;
                if (mean != null && TwinJson.TryGetNumber(mean["sensor_count"], out count))
                    sb.Append("   ").Append(count.ToString("0", inv)).Append(" sensors");
                if (mean != null && TwinJson.TryGetNumber(mean["temperature_spread_c"], out spread))
                    sb.Append(", spread ").Append(spread.ToString("0.0", inv)).Append(" °C");
                sb.Append("\nMeasured ").Append(TwinUtil.FormatAge((DateTimeOffset.UtcNow - at).TotalSeconds));
            }
            else
            {
                sb.Append("\nMean: no fresh sensor");
            }

            JObject twin = f.Twin();
            if (twin != null)
            {
                JObject est = twin["estimated"] as JObject;
                double et, erh;
                if (est != null && TwinJson.TryGetNumber(est["temperature_c"], out et))
                {
                    sb.Append("\nTwin estimate ").Append(et.ToString("0.0", inv)).Append(" °C");
                    if (TwinJson.TryGetNumber(est["relative_humidity_percent"], out erh))
                        sb.Append("   ").Append(erh.ToString("0", inv)).Append(" % RH");
                }
                string status = TwinJson.GetString(twin, "status");
                if (status != null) sb.Append("\nTwin: ").Append(status);
            }

            AppendDevices(f.Devices(), f.EffectiveDevices());
            AppendMap("Contacts", f.Contacts());

            JObject sensors = f.Sensors();
            if (sensors != null)
            {
                foreach (JProperty p in sensors.Properties())
                {
                    JObject e = p.Value as JObject;
                    if (e == null) continue;
                    double kwh, watts;
                    bool hasE = TwinJson.TryGetNumber(e["energy_kwh"], out kwh);
                    bool hasP = TwinJson.TryGetNumber(e["power_w"], out watts);
                    if (!hasE && !hasP) continue;
                    sb.Append("\nEnergy ").Append(p.Name).Append(':');
                    if (hasE) sb.Append(' ').Append(kwh.ToString("0.000", inv)).Append(" kWh");
                    if (hasP) sb.Append("   ").Append(watts.ToString("0", inv)).Append(" W");
                }
            }

            JObject nodes = f.Nodes();
            if (nodes != null && nodes.Count > 0)
            {
                sb.Append("\nNodes:");
                foreach (JProperty p in nodes.Properties())
                    sb.Append(' ').Append(p.Name).Append(' ').Append(TwinJson.GetString(p.Value, "status") ?? "?");
            }

            if (h.Forecast.Count > 0)
            {
                double last = h.Forecast[h.Forecast.Count - 1].unixSeconds;
                sb.Append("\nForecast: ").Append(h.Forecast.Count).Append(" points to ")
                  .Append(TwinJson.FromUnixSeconds(last).ToLocalTime().ToString("HH:mm", inv));
            }
            return sb.ToString();
        }

        /// <summary>Device states as shown on the model (node report wins); "(cmd x)" where the controller asked for something else.</summary>
        void AppendDevices(JObject commanded, JObject effective)
        {
            if (effective == null || effective.Count == 0) return;
            sb.Append("\nDevices:");
            foreach (JProperty p in effective.Properties())
            {
                sb.Append("  ").Append(p.Name).Append(' ').Append(TwinJson.DeviceText(p.Value));
                JToken c = commanded != null ? commanded[p.Name] : null;
                if (c != null && !JToken.DeepEquals(c, p.Value)) sb.Append(" (cmd ").Append(TwinJson.DeviceText(c)).Append(')');
            }
        }

        void AppendMap(string title, JObject map)
        {
            if (map == null || map.Count == 0) return;
            sb.Append('\n').Append(title).Append(':');
            foreach (JProperty p in map.Properties())
                sb.Append("  ").Append(p.Name).Append(' ').Append(TwinJson.DeviceText(p.Value));
        }
    }
}
