using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SolarGrow
{
    /// <summary>
    /// Owns the data source and one GreenhouseBuilder per greenhouse id. Routes every frame to the model:
    /// sensors by id (unknown ids get an auto marker), devices through the config's actuator list,
    /// contacts (door) by name. Keeps mean T/RH history and the latest forecast per house.
    /// </summary>
    [DisallowMultipleComponent]
    public class TwinController : MonoBehaviour
    {
        [Header("Data source")]
        [Tooltip("Simulated (no hardware) or TCP JSON lines from a data bridge (protocol in README.md).")]
        public SourceKind source = SourceKind.Simulated;

        [Tooltip("Bridge host for the TCP source.")]
        public string host = "127.0.0.1";

        [Tooltip("Bridge port for the TCP source.")]
        public int port = 5055;

        [Tooltip("Connect the selected source when Play starts.")]
        public bool connectOnStart = true;

        [Header("Model")]
        [Tooltip("Shared configuration; the default asset or built-in defaults are used if empty.")]
        public GreenhouseConfig config;

        [Tooltip("Greenhouse ids expected in the frames; one house is built per id, in this order.")]
        public List<string> greenhouseIds = new List<string> { "A", "B" };

        [Tooltip("Builders of the houses; missing ones are found in the scene or created at Start.")]
        public List<GreenhouseBuilder> houses = new List<GreenhouseBuilder>();

        [Tooltip("Frames for ids not listed above add extra houses, up to this total.")]
        [Range(1, 12)] public int maxHouses = 6;

        [Tooltip("Mean temperature/RH samples kept per house for the graph.")]
        [Range(64, 100000)] public int historyCapacity = 4096;

        const int MaxStatusLines = 8;

        ITelemetrySource active;
        readonly Dictionary<string, HouseRuntime> byId = new Dictionary<string, HouseRuntime>();
        readonly List<HouseRuntime> order = new List<HouseRuntime>();
        readonly List<string> statusLines = new List<string>();
        readonly List<TwinComponent> tmpComponents = new List<TwinComponent>();
        readonly List<ForecastPoint> tmpForecast = new List<ForecastPoint>();
        bool started;
        bool warnedTooMany;

        /// <summary>Result of the last manual command.</summary>
        public string LastCommandResult { get; private set; }

        public IList<string> StatusLines { get { return statusLines; } }
        public bool IsConnected { get { return active != null && active.Connected; } }
        public ITelemetrySource ActiveSource { get { return active; } }
        public int HouseCount { get { return order.Count; } }

        public HouseRuntime GetHouse(int i)
        {
            return i >= 0 && i < order.Count ? order[i] : null;
        }

        public HouseRuntime GetHouse(string id)
        {
            HouseRuntime h;
            return id != null && byId.TryGetValue(id, out h) ? h : null;
        }

        // ------------------------------------------------------------------ lifecycle

        void Start()
        {
            started = true;
            EnsureConfig();
            EnsureHouses();
            if (connectOnStart) ConnectSource();
            else AddStatus("Source " + SourceLabel(source) + " selected; press Connect");
        }

        void OnEnable()
        {
            if (started && connectOnStart && active == null) ConnectSource();
        }

        void OnDisable()
        {
            DetachSource(false);
        }

        void OnDestroy()
        {
            for (int i = 0; i < order.Count; i++)
                if (order[i].Builder != null) order[i].Builder.Rebuilt -= OnBuilderRebuilt;
        }

        void Update()
        {
            if (active == null) return;
            try
            {
                active.Tick();
            }
            catch (Exception e)
            {
                AddStatus("Source error: " + e.Message);
                Debug.LogException(e);
            }
        }

        void EnsureConfig()
        {
            if (config != null) return;
            config = TwinSceneFactory.LoadDefaultConfig();
            AddStatus("No config assigned: using " + config.name);
        }

        void EnsureHouses()
        {
            for (int i = 0; i < greenhouseIds.Count; i++)
            {
                string id = greenhouseIds[i];
                if (string.IsNullOrEmpty(id) || byId.ContainsKey(id)) continue;
                AddHouse(id, FindBuilder(id));
            }
        }

        GreenhouseBuilder FindBuilder(string id)
        {
            for (int i = 0; i < houses.Count; i++)
                if (houses[i] != null && houses[i].greenhouseId == id) return houses[i];
            GreenhouseBuilder[] all = FindObjectsByType<GreenhouseBuilder>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i].greenhouseId == id) return all[i];
            return null;
        }

        HouseRuntime AddHouse(string id, GreenhouseBuilder builder)
        {
            int index = order.Count;
            if (builder == null) builder = TwinSceneFactory.CreateHouse(id, index, config);
            else if (builder.config == null) builder.config = config;
            if (!houses.Contains(builder)) houses.Add(builder);
            HouseRuntime h = new HouseRuntime(id, builder, historyCapacity);
            byId[id] = h;
            order.Add(h);
            builder.Rebuilt -= OnBuilderRebuilt;
            builder.Rebuilt += OnBuilderRebuilt;
            if (!builder.IsBuilt) builder.Rebuild();
            return h;
        }

        HouseRuntime GetOrCreateHouse(string id)
        {
            HouseRuntime h;
            if (byId.TryGetValue(id, out h)) return h;
            if (order.Count >= maxHouses)
            {
                if (!warnedTooMany) AddStatus("Ignoring greenhouse '" + id + "': more than " + maxHouses + " houses");
                warnedTooMany = true;
                return null;
            }
            AddStatus("Greenhouse '" + id + "' is new in the data: added a house");
            return AddHouse(id, FindBuilder(id));
        }

        void OnBuilderRebuilt(GreenhouseBuilder builder)
        {
            for (int i = 0; i < order.Count; i++)
            {
                HouseRuntime h = order[i];
                if (h.Builder == builder && h.LastFrame != null) ApplyToModel(h, h.LastFrame);
            }
        }

        // ------------------------------------------------------------------ frames

        void HandleState(TwinStateFrame frame)
        {
            try
            {
                string id = frame.Greenhouse;
                if (string.IsNullOrEmpty(id)) id = greenhouseIds.Count > 0 ? greenhouseIds[0] : "A";
                HouseRuntime h = GetOrCreateHouse(id);
                if (h == null) return;
                h.LastFrame = frame;
                h.FrameCount++;
                h.LastFrameRealtime = Time.realtimeSinceStartup;
                h.Version++;
                ApplyToModel(h, frame);

                DateTimeOffset at;
                float t, rh;
                if (frame.TryGetMean(out at, out t, out rh))
                {
                    double ts = TwinJson.ToUnixSeconds(at);
                    if (ts > h.History.LastTime) h.History.Add(ts, t, rh);
                }
                if (frame.Forecast(tmpForecast) > 0)
                {
                    h.Forecast.Clear();
                    h.Forecast.AddRange(tmpForecast);
                }
            }
            catch (Exception e)
            {
                AddStatus("Frame error: " + e.Message);
                Debug.LogException(e);
            }
        }

        void ApplyToModel(HouseRuntime h, TwinStateFrame frame)
        {
            GreenhouseBuilder b = h.Builder;
            if (b == null || !b.IsBuilt) return;

            JObject sensors = frame.Sensors();
            if (sensors != null)
            {
                foreach (JProperty p in sensors.Properties())
                {
                    SensorMarker m = b.FindSensor(p.Name);
                    if (m == null) m = b.EnsureSensorMarker(p.Name);
                    if (m != null) m.Apply(p.Value);
                }
            }

            JObject devices = frame.EffectiveDevices();
            if (devices != null)
            {
                foreach (JProperty p in devices.Properties())
                {
                    ActuatorSpec spec = config != null ? config.FindActuator(p.Name) : null;
                    string target = spec != null && !string.IsNullOrEmpty(spec.componentId) ? spec.componentId : p.Name;
                    ApplyToParts(b, target, p.Value);
                }
            }

            JObject contacts = frame.Contacts();
            if (contacts != null)
            {
                foreach (JProperty p in contacts.Properties()) ApplyToParts(b, p.Name, p.Value);
            }
        }

        void ApplyToParts(GreenhouseBuilder b, string idOrPrefix, JToken value)
        {
            tmpComponents.Clear();
            b.FindComponents(idOrPrefix, tmpComponents);
            for (int i = 0; i < tmpComponents.Count; i++) tmpComponents[i].Apply(value);
            tmpComponents.Clear();
        }

        // ------------------------------------------------------------------ source

        ITelemetrySource CreateSource(SourceKind kind)
        {
            switch (kind)
            {
                case SourceKind.TcpJsonLines: return new TcpJsonLinesSource(host, port);
                default: return new SimulatedSource(config, greenhouseIds);
            }
        }

        /// <summary>(Re)create the selected source with the current host/port and connect it.</summary>
        public void ConnectSource()
        {
            EnsureConfig();
            DetachSource(false);
            ITelemetrySource s = CreateSource(source);
            s.OnState += HandleState;
            s.OnStatus += AddStatus;
            active = s;
            try
            {
                s.Connect();
            }
            catch (Exception e)
            {
                AddStatus(e.GetType().Name + ": " + e.Message);
            }
        }

        public void DisconnectSource()
        {
            if (active == null)
            {
                AddStatus("Nothing to disconnect");
                return;
            }
            DetachSource(true);
        }

        void DetachSource(bool report)
        {
            if (active == null) return;
            ITelemetrySource s = active;
            active = null;
            try
            {
                s.Disconnect();
                if (report) s.Tick();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            s.OnState -= HandleState;
            s.OnStatus -= AddStatus;
            if (report) AddStatus("Disconnected (" + SourceLabel(source) + ")");
        }

        /// <summary>Select another source; the old one is disconnected. Press Connect to start the new one.</summary>
        public void SetSource(SourceKind kind)
        {
            if (kind == source) return;
            DetachSource(active != null);
            source = kind;
            AddStatus("Source set to " + SourceLabel(kind) + "; press Connect");
        }

        public static string SourceLabel(SourceKind kind)
        {
            switch (kind)
            {
                case SourceKind.TcpJsonLines: return "TCP JSON lines";
                default: return "Simulated";
            }
        }

        /// <summary>One line about the source for the panels.</summary>
        public string SourceDetail()
        {
            if (active == null) return SourceLabel(source) + ": not connected";
            TcpJsonLinesSource tcp = active as TcpJsonLinesSource;
            if (tcp != null)
            {
                string s = "TCP " + tcp.Host + ":" + tcp.Port + (tcp.Connected ? " connected" : " not connected");
                if (tcp.LastRttMs >= 0) s += ", ping " + tcp.LastRttMs.ToString("0") + " ms";
                if (!string.IsNullOrEmpty(tcp.BridgeInfo)) s += "\n" + tcp.BridgeInfo;
                return s;
            }
            return SourceLabel(source) + (active.Connected ? ": running" : ": not connected");
        }

        // ------------------------------------------------------------------ commands, model, status

        /// <summary>Manual override from the panel, passed to the source if it takes commands.</summary>
        public bool ManualCommand(string greenhouse, string device, string value)
        {
            ICommandSink sink = active as ICommandSink;
            if (sink == null)
            {
                LastCommandResult = "Not sent: no source connected";
                return false;
            }
            bool ok;
            try
            {
                ok = sink.SendCommand(greenhouse, device, value);
            }
            catch (Exception e)
            {
                LastCommandResult = "Not sent: " + e.Message;
                return false;
            }
            LastCommandResult = ok
                ? "Sent " + device + " = " + value + " to " + greenhouse + " at " + DateTime.Now.ToString("HH:mm:ss")
                : "Not sent: source not connected";
            AddStatus(LastCommandResult);
            return ok;
        }

        /// <summary>Rebuild every house at the next Update.</summary>
        public void RebuildAll()
        {
            for (int i = 0; i < order.Count; i++)
                if (order[i].Builder != null) order[i].Builder.RequestRebuild();
        }

        public bool TryGetHouseCenter(int index, out Vector3 center)
        {
            center = Vector3.zero;
            HouseRuntime h = GetHouse(index);
            if (h == null || h.Builder == null) return false;
            center = h.Builder.CenterWorld;
            return true;
        }

        public void AddStatus(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            statusLines.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + text);
            while (statusLines.Count > MaxStatusLines) statusLines.RemoveAt(0);
            Debug.Log("SolarGrow: " + text);
        }
    }
}
