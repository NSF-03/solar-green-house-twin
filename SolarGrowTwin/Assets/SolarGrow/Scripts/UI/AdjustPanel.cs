using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace SolarGrow
{
    /// <summary>
    /// Collapsible left IMGUI panel: connection, geometry, vents, pots/LEDs, sensors, manual actuator commands,
    /// config export/import and view options. Geometry edits change the config and rebuild the houses live.
    /// Button actions are queued and run at the next Layout event, so the IMGUI control count never changes mid-event.
    /// </summary>
    [DisallowMultipleComponent]
    public class AdjustPanel : MonoBehaviour
    {
        [Tooltip("Controller the panel drives.")]
        public TwinController controller;

        [Tooltip("Panel open; when closed a small button reopens it.")]
        public bool expanded = true;

        [Tooltip("Panel width in GUI units.")]
        public float panelWidth = 340f;

        [Tooltip("File name for Export/Import JSON inside Application.persistentDataPath.")]
        public string configFileName = "solargrow_greenhouse_config.json";

        [Tooltip("Set the UI scale from the screen DPI when Play starts.")]
        public bool autoUiScale = true;

        static readonly string[] SourceNames = { "Simulated", "TCP JSON lines" };
        static readonly string[] GableNames = { "West", "East" };
        static readonly string[] SlopeNames = { "South", "North", "Alt S", "Alt N" };
        static readonly string[] VentPlacementNames = { "Spread", "Door end" };
        static readonly string[] SensorKindNames = { "Air T/RH/CO2", "T/RH", "PAR", "Energy", "Other" };
        static readonly string[] ActuatorKindNames = { "Heater", "LED", "Vent", "Pump", "Door", "Other" };

        readonly bool[] open = { true, true, false, false, false, true, false, false };
        readonly List<Action> pending = new List<Action>();
        readonly Dictionary<int, string> floatBuffers = new Dictionary<int, string>();
        Vector2 scroll;
        string portText;
        string fileStatus = "";
        string configPath;
        string statusText = "";
        int statusCount = -1;
        string statusLast;
        string[] houseNames = new string[0];
        int actuatorHouse;
        GUIStyle headerStyle, sectionStyle, smallStyle;

        void Start()
        {
            if (autoUiScale) TwinUtil.UiScale = TwinUtil.SuggestedUiScale();
        }

        void OnDisable()
        {
            TwinUtil.SetUiRect(0, new Rect(0, 0, 0, 0));
        }

        void Defer(Action action)
        {
            pending.Add(action);
        }

        void InitStyles()
        {
            if (headerStyle != null) return;
            headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            sectionStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true, richText = false };
        }

        void OnGUI()
        {
            if (controller == null) controller = GetComponent<TwinController>();
            if (controller == null) return;
            GUI.depth = 0;
            if (Event.current.type == EventType.Layout && pending.Count > 0)
            {
                Action[] run = pending.ToArray();
                pending.Clear();
                for (int i = 0; i < run.Length; i++)
                {
                    try { run[i](); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            }
            TwinUtil.ApplyGuiScale();
            InitStyles();

            if (!expanded)
            {
                Rect r = new Rect(10f, 10f, 120f, 26f);
                TwinUtil.SetUiRect(0, r);
                if (GUI.Button(r, "Adjust panel")) Defer(() => expanded = true);
                return;
            }

            Rect area = new Rect(10f, 10f, Mathf.Min(panelWidth, TwinUtil.GuiWidth * 0.45f), TwinUtil.GuiHeight - 20f);
            TwinUtil.SetUiRect(0, area);
            GreenhouseConfig cfg = controller.config;
            bool changed = false;

            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("SolarGrow twin", headerStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Hide", GUILayout.Width(50f))) Defer(() => expanded = false);
            GUILayout.EndHorizontal();
            scroll = GUILayout.BeginScrollView(scroll);
            if (Section(0, "Connection")) DrawConnection();
            if (cfg == null)
            {
                GUILayout.Label("No config yet.", smallStyle);
            }
            else
            {
                if (Section(1, "Geometry")) changed |= DrawGeometry(cfg);
                if (Section(2, "Roof vents")) changed |= DrawVents(cfg);
                if (Section(3, "Pots and LED bars")) changed |= DrawPots(cfg);
                if (Section(4, "Sensors")) changed |= DrawSensors(cfg);
                if (Section(5, "Actuators (manual commands)")) changed |= DrawActuators(cfg);
                if (Section(6, "Config file and view")) DrawFiles(cfg);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            if (changed && cfg != null) Changed(cfg);
        }

        bool Section(int i, string title)
        {
            if (GUILayout.Button((open[i] ? "[-] " : "[+] ") + title, sectionStyle))
            {
                int k = i;
                Defer(() => open[k] = !open[k]);
            }
            return open[i];
        }

        // ------------------------------------------------------------------ sections

        void DrawConnection()
        {
            int sel = GUILayout.Toolbar((int)controller.source, SourceNames);
            if (sel != (int)controller.source)
            {
                SourceKind kind = (SourceKind)sel;
                Defer(() => controller.SetSource(kind));
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label("Host", GUILayout.Width(34f));
            string host = GUILayout.TextField(controller.host ?? "", GUILayout.MinWidth(90f));
            if (host != controller.host) controller.host = host;
            GUILayout.Label("Port", GUILayout.Width(30f));
            if (portText == null) portText = controller.port.ToString(CultureInfo.InvariantCulture);
            string pt = GUILayout.TextField(portText, GUILayout.Width(56f));
            if (pt != portText)
            {
                portText = pt;
                int p;
                if (int.TryParse(pt, NumberStyles.Integer, CultureInfo.InvariantCulture, out p) && p > 0 && p < 65536) controller.port = p;
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Connect")) Defer(controller.ConnectSource);
            if (GUILayout.Button("Disconnect")) Defer(controller.DisconnectSource);
            GUILayout.EndHorizontal();
            GUILayout.Label(controller.SourceDetail(), smallStyle);
            GUILayout.Label(StatusText(), smallStyle);
        }

        string StatusText()
        {
            IList<string> lines = controller.StatusLines;
            int n = lines.Count;
            string last = n > 0 ? lines[n - 1] : null;
            if (n != statusCount || !ReferenceEquals(last, statusLast))
            {
                statusCount = n;
                statusLast = last;
                StringBuilder b = new StringBuilder();
                for (int i = Mathf.Max(0, n - 5); i < n; i++)
                {
                    if (b.Length > 0) b.Append('\n');
                    b.Append(lines[i]);
                }
                statusText = b.Length > 0 ? b.ToString() : "No status yet.";
            }
            return statusText;
        }

        bool DrawGeometry(GreenhouseConfig cfg)
        {
            bool ch = false;
            ch |= Slider("Width", ref cfg.width, 0.5f, 6f, "0.000", " m");
            ch |= Slider("Length", ref cfg.length, 0.5f, 10f, "0.000", " m");
            ch |= Slider("Eaves height", ref cfg.eavesHeight, 0.5f, 4f, "0.000", " m");
            ch |= Slider("Ridge height", ref cfg.ridgeHeight, cfg.eavesHeight + 0.02f, 5f, "0.000", " m");
            ch |= Slider("Heading", ref cfg.orientationDeg, 0f, 359f, "0", " deg");
            ch |= Slider("B offset x", ref cfg.houseBOffset.x, -15f, 15f, "0.00", " m");
            ch |= Slider("B offset z", ref cfg.houseBOffset.z, -15f, 15f, "0.00", " m");
            ch |= Slider("Glass opacity", ref cfg.glassOpacity, 0.02f, 1f, "0.00", "");
            ch |= Slider("Door width", ref cfg.doorWidth, 0.2f, 2f, "0.000", " m");
            ch |= Slider("Door height", ref cfg.doorHeight, 0.5f, 3f, "0.000", " m");
            ch |= Slider("Door offset", ref cfg.doorOffset, -1.5f, 1.5f, "0.00", " m");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Door gable", GUILayout.Width(96f));
            int g = GUILayout.Toolbar((int)cfg.doorGable, GableNames);
            GUILayout.EndHorizontal();
            if (g != (int)cfg.doorGable) { cfg.doorGable = (GableEnd)g; ch = true; }
            ch |= Toggle("Trees north of A", ref cfg.hedgeEnabled);
            ch |= Slider("Tree gap", ref cfg.hedgeDistance, 0f, 5f, "0.00", " m");
            ch |= Slider("Tree height", ref cfg.hedgeHeight, 0.5f, 10f, "0.0", " m");
            ch |= Slider("Tree length", ref cfg.hedgeLength, 1f, 40f, "0.0", " m");
            ch |= IntSlider("Sun month", ref cfg.sunMonth, 1, 12);
            ch |= Slider("Sun hour (UTC)", ref cfg.sunHourUtc, 0f, 24f, "0.0", " h");
            return ch;
        }

        bool DrawVents(GreenhouseConfig cfg)
        {
            bool ch = false;
            ch |= IntSlider("Vent count", ref cfg.ventCount, 0, 6);
            ch |= Slider("Vent width", ref cfg.ventWidth, 0.1f, 1.5f, "0.00", " m");
            ch |= Slider("Vent length", ref cfg.ventLength, 0.1f, 1.2f, "0.00", " m");
            ch |= Slider("Max angle", ref cfg.ventMaxAngleDeg, 0f, 90f, "0", " deg");
            ch |= IntSlider("Actuated vent", ref cfg.actuatedVent, 0, Mathf.Max(1, cfg.ventCount));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Slope", GUILayout.Width(96f));
            int s = GUILayout.Toolbar((int)cfg.ventSlope, SlopeNames);
            GUILayout.EndHorizontal();
            if (s != (int)cfg.ventSlope) { cfg.ventSlope = (RoofSlope)s; ch = true; }
            GUILayout.BeginHorizontal();
            GUILayout.Label("Placement", GUILayout.Width(96f));
            int vp = GUILayout.Toolbar((int)cfg.ventPlacement, VentPlacementNames);
            GUILayout.EndHorizontal();
            if (vp != (int)cfg.ventPlacement) { cfg.ventPlacement = (VentPlacement)vp; ch = true; }
            ch |= IntSlider("Start bay", ref cfg.ventStartBay, 1, 4);
            VentVisual.PreviewOverride = GUILayout.Toggle(VentVisual.PreviewOverride, "Preview opening (ignores the data)");
            float f = VentVisual.PreviewFraction;
            if (Slider("Open fraction", ref f, 0f, 1f, "0.00", "")) VentVisual.PreviewFraction = f;
            return ch;
        }

        bool DrawPots(GreenhouseConfig cfg)
        {
            bool ch = false;
            ch |= IntSlider("Pots", ref cfg.potCount, 0, 30);
            ch |= IntSlider("Rows", ref cfg.potRows, 1, 4);
            ch |= Slider("Pot spacing", ref cfg.potSpacing, 0.1f, 1f, "0.00", " m");
            ch |= Slider("Row spacing", ref cfg.potRowSpacing, 0.2f, 1.6f, "0.00", " m");
            ch |= Slider("Pot diameter", ref cfg.potDiameter, 0.1f, 0.6f, "0.00", " m");
            ch |= Slider("Plant height", ref cfg.plantHeight, 0f, 1.8f, "0.00", " m");
            ch |= IntSlider("LED bars", ref cfg.ledCount, 0, 4);
            ch |= Slider("LED height", ref cfg.ledHeight, 0.5f, 2.4f, "0.00", " m");
            ch |= Slider("LED length", ref cfg.ledLength, 0.2f, 2.5f, "0.00", " m");
            ch |= Toggle("Water tank + timer pump (pending decision)", ref cfg.waterTankEnabled);
            return ch;
        }

        bool DrawSensors(GreenhouseConfig cfg)
        {
            bool ch = false;
            List<SensorSpec> list = cfg.sensors;
            for (int i = 0; i < list.Count; i++)
            {
                SensorSpec s = list[i];
                if (s == null) continue;
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                GUILayout.Label("id", GUILayout.Width(18f));
                string id = GUILayout.TextField(s.id ?? "", GUILayout.MinWidth(100f));
                if (id != s.id) { s.id = id; ch = true; }
                if (GUILayout.Button("Remove", GUILayout.Width(64f)))
                {
                    int idx = i;
                    Defer(() => RemoveSensor(cfg, idx));
                }
                GUILayout.EndHorizontal();
                int kind = GUILayout.SelectionGrid((int)s.kind, SensorKindNames, 3);
                if (kind != (int)s.kind) { s.kind = (SensorKind)kind; ch = true; }
                GUILayout.BeginHorizontal();
                GUILayout.Label("label", GUILayout.Width(34f));
                string text = GUILayout.TextField(s.label ?? "");
                if (text != s.label) { s.label = text; ch = true; }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                ch |= FloatField(i * 4, "x", ref s.position.x);
                ch |= FloatField(i * 4 + 1, "y", ref s.position.y);
                ch |= FloatField(i * 4 + 2, "z", ref s.position.z);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            if (GUILayout.Button("Add sensor")) Defer(() => AddSensor(cfg));
            GUILayout.Label("Ids must match the sensor ids in the frames. Position in m from the floor centre: x along the ridge, " +
                            "y up, z towards the north wall. Energy meter: offset from the meter box.", smallStyle);
            return ch;
        }

        bool DrawActuators(GreenhouseConfig cfg)
        {
            bool ch = false;
            int n = controller.HouseCount;
            if (houseNames.Length != n)
            {
                houseNames = new string[n];
                for (int i = 0; i < n; i++)
                {
                    HouseRuntime h = controller.GetHouse(i);
                    houseNames[i] = h != null ? h.Id : "?";
                }
            }
            if (n == 0)
            {
                GUILayout.Label("No greenhouse yet.", smallStyle);
            }
            else
            {
                actuatorHouse = Mathf.Clamp(actuatorHouse, 0, n - 1);
                actuatorHouse = GUILayout.Toolbar(actuatorHouse, houseNames);
                string house = houseNames[actuatorHouse];
                for (int i = 0; i < cfg.actuators.Count; i++)
                {
                    ActuatorSpec a = cfg.actuators[i];
                    if (a == null || string.IsNullOrEmpty(a.deviceName)) continue;
                    bool openClose = a.kind == ActuatorKind.Vent || a.kind == ActuatorKind.Door;
                    string device = a.deviceName;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(device, GUILayout.Width(80f));
                    if (GUILayout.Button(openClose ? "Open" : "On"))
                    {
                        string v = openClose ? "open" : "on";
                        Defer(() => controller.ManualCommand(house, device, v));
                    }
                    if (GUILayout.Button(openClose ? "Close" : "Off"))
                    {
                        string v = openClose ? "closed" : "off";
                        Defer(() => controller.ManualCommand(house, device, v));
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.Label(string.IsNullOrEmpty(controller.LastCommandResult)
                    ? "Buttons send a cmd line (source unity-manual) to the bridge; the simulated source holds it for 120 s."
                    : controller.LastCommandResult, smallStyle);
            }
            if (Section(7, "Device mapping (device name -> model part id)")) ch |= DrawMapping(cfg);
            return ch;
        }

        bool DrawMapping(GreenhouseConfig cfg)
        {
            bool ch = false;
            for (int i = 0; i < cfg.actuators.Count; i++)
            {
                ActuatorSpec a = cfg.actuators[i];
                if (a == null) continue;
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                GUILayout.Label("device", GUILayout.Width(44f));
                string d = GUILayout.TextField(a.deviceName ?? "", GUILayout.MinWidth(60f));
                if (d != a.deviceName) { a.deviceName = d; ch = true; }
                GUILayout.Label("part", GUILayout.Width(30f));
                string c = GUILayout.TextField(a.componentId ?? "", GUILayout.MinWidth(60f));
                if (c != a.componentId) { a.componentId = c; ch = true; }
                if (GUILayout.Button("x", GUILayout.Width(22f)))
                {
                    int idx = i;
                    Defer(() => RemoveActuator(cfg, idx));
                }
                GUILayout.EndHorizontal();
                int k = GUILayout.SelectionGrid((int)a.kind, ActuatorKindNames, 3);
                if (k != (int)a.kind) { a.kind = (ActuatorKind)k; ch = true; }
                GUILayout.EndVertical();
            }
            if (GUILayout.Button("Add device")) Defer(() => AddActuator(cfg));
            GUILayout.Label("Part ids: heater, led (all LED bars), vent-1, vent-2, door, pump. A device without a mapping goes to the part with its own name.", smallStyle);
            return ch;
        }

        void DrawFiles(GreenhouseConfig cfg)
        {
            string path = ConfigPath();
            GUILayout.Label(path, smallStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Export JSON")) Defer(() => Export(cfg, path));
            if (GUILayout.Button("Import JSON")) Defer(() => Import(cfg, path));
            if (GUILayout.Button("Rebuild")) Defer(controller.RebuildAll);
            GUILayout.EndHorizontal();
            GUILayout.Label(string.IsNullOrEmpty(fileStatus) ? " " : fileStatus, smallStyle);
            float s = TwinUtil.UiScale;
            if (Slider("UI scale", ref s, 0.75f, 2.5f, "0.00", ""))
            {
                float ns = s;
                Defer(() => TwinUtil.UiScale = ns);
            }
            GUILayout.BeginHorizontal();
            TwinUtil.ShowSensorLabels = GUILayout.Toggle(TwinUtil.ShowSensorLabels, "Sensor labels");
            TwinUtil.ShowDeviceLabels = GUILayout.Toggle(TwinUtil.ShowDeviceLabels, "Device labels");
            GUILayout.EndHorizontal();
            GUILayout.Label("Mouse: left-drag orbit, right-drag pan, wheel zoom. Keys: 1 / 2 focus greenhouse A / B, F resets the view.", smallStyle);
        }

        // ------------------------------------------------------------------ actions (run at Layout)

        string ConfigPath()
        {
            if (configPath == null) configPath = Path.Combine(Application.persistentDataPath, configFileName);
            return configPath;
        }

        void Export(GreenhouseConfig cfg, string path)
        {
            try
            {
                File.WriteAllText(path, cfg.ToJson(), new UTF8Encoding(false));
                fileStatus = "Exported at " + DateTime.Now.ToString("HH:mm:ss");
            }
            catch (Exception e)
            {
                fileStatus = "Export failed: " + e.Message;
            }
        }

        void Import(GreenhouseConfig cfg, string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    fileStatus = "No file yet: press Export first, or copy a JSON file to this path.";
                    return;
                }
                cfg.FromJson(File.ReadAllText(path, Encoding.UTF8));
                floatBuffers.Clear();
                MarkDirty(cfg);
                fileStatus = "Imported at " + DateTime.Now.ToString("HH:mm:ss");
            }
            catch (Exception e)
            {
                fileStatus = "Import failed: " + e.Message;
            }
        }

        void AddSensor(GreenhouseConfig cfg)
        {
            cfg.sensors.Add(new SensorSpec("sensor-" + (cfg.sensors.Count + 1), SensorKind.Other, new Vector3(0f, 1f, 0f), "New sensor"));
            floatBuffers.Clear();
            Changed(cfg);
        }

        void RemoveSensor(GreenhouseConfig cfg, int index)
        {
            if (index < 0 || index >= cfg.sensors.Count) return;
            cfg.sensors.RemoveAt(index);
            floatBuffers.Clear();
            Changed(cfg);
        }

        void AddActuator(GreenhouseConfig cfg)
        {
            cfg.actuators.Add(new ActuatorSpec("device-" + (cfg.actuators.Count + 1), ActuatorKind.Other, ""));
            Changed(cfg);
        }

        void RemoveActuator(GreenhouseConfig cfg, int index)
        {
            if (index < 0 || index >= cfg.actuators.Count) return;
            cfg.actuators.RemoveAt(index);
            Changed(cfg);
        }

        static void Changed(GreenhouseConfig cfg)
        {
            cfg.NotifyChanged();
            MarkDirty(cfg);
        }

        static void MarkDirty(UnityEngine.Object target)
        {
#if UNITY_EDITOR
            if (target != null) UnityEditor.EditorUtility.SetDirty(target);
#endif
        }

        // ------------------------------------------------------------------ controls

        /// <summary>Slider row; only a mouse or key event can change the value (drawing alone never clamps it).</summary>
        bool Slider(string label, ref float value, float min, float max, string format, string unit)
        {
            EventType before = Event.current.type;
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(96f));
            float v = GUILayout.HorizontalSlider(value, min, max, GUILayout.MinWidth(80f));
            GUILayout.Label(value.ToString(format, CultureInfo.InvariantCulture) + unit, GUILayout.Width(72f));
            GUILayout.EndHorizontal();
            bool user = before == EventType.MouseDown || before == EventType.MouseDrag || before == EventType.MouseUp || before == EventType.KeyDown;
            if (user && Mathf.Abs(v - value) > 1e-6f)
            {
                value = v;
                return true;
            }
            return false;
        }

        bool IntSlider(string label, ref int value, int min, int max)
        {
            float f = value;
            if (!Slider(label, ref f, min, max, "0", "")) return false;
            int v = Mathf.RoundToInt(f);
            if (v == value) return false;
            value = v;
            return true;
        }

        static bool Toggle(string label, ref bool value)
        {
            bool v = GUILayout.Toggle(value, label);
            if (v == value) return false;
            value = v;
            return true;
        }

        /// <summary>Number field that keeps the typed text (e.g. "1.") until it parses.</summary>
        bool FloatField(int key, string label, ref float value)
        {
            GUILayout.Label(label, GUILayout.Width(12f));
            string shown;
            if (!floatBuffers.TryGetValue(key, out shown)) shown = value.ToString("0.###", CultureInfo.InvariantCulture);
            string typed = GUILayout.TextField(shown, GUILayout.MinWidth(46f));
            if (typed == shown) return false;
            floatBuffers[key] = typed;
            float v;
            if (float.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && Mathf.Abs(v - value) > 1e-6f)
            {
                value = v;
                return true;
            }
            return false;
        }
    }
}
