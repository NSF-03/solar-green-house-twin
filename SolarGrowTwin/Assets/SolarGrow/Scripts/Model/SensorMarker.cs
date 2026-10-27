using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SolarGrow
{
    /// <summary>
    /// A sensor on the model. Keeps every numeric field of the latest sensor entry
    /// (temperature_c, relative_humidity_percent, co2_ppm, par_umol_m2_s, energy_kwh, power_w, anything else)
    /// and turns grey when the reading is older than staleAfterSeconds.
    /// </summary>
    public class SensorMarker : TwinComponent
    {
        [Tooltip("What the sensor measures.")]
        public SensorKind sensorKind = SensorKind.Other;

        [Tooltip("Seconds after which the reading counts as stale (grey).")]
        public float staleAfterSeconds = 300f;

        [Tooltip("True if the sensor id was not in the config and the marker was created on the fly.")]
        public bool autoCreated;

        [Tooltip("Marker colour while the reading is fresh.")]
        public Color freshColor = new Color(1f, 0.6f, 0.1f);

        [Tooltip("Marker colour while there is no fresh reading.")]
        public Color staleColor = new Color(0.45f, 0.45f, 0.45f);

        readonly Dictionary<string, double> values = new Dictionary<string, double>();
        readonly List<string> keys = new List<string>();
        readonly StringBuilder sb = new StringBuilder(128);
        bool hasObserved;
        DateTimeOffset observedAt;
        string quality = "";
        float lastApplyRealtime = -1f;
        int tintState = -1;
        string cachedText;
        float cachedAt = -10f;
        bool dirty = true;

        public bool HasData { get { return lastApplyRealtime >= 0f; } }
        public string Quality { get { return quality; } }
        public IList<string> Keys { get { return keys; } }

        public bool TryGetValue(string key, out double value)
        {
            return values.TryGetValue(key, out value);
        }

        public DateTimeOffset? ObservedAt
        {
            get { return hasObserved ? observedAt : (DateTimeOffset?)null; }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            tintState = -1;
        }

        /// <summary>value = one entry of frame.sensors: {observed_at_utc, temperature_c, ..., quality: [...]} or a bare number.</summary>
        public override void Apply(JToken value)
        {
            values.Clear();
            keys.Clear();
            JObject o = value as JObject;
            if (o == null)
            {
                double d;
                if (TwinJson.TryGetNumber(value, out d)) Set("value", d);
                hasObserved = false;
                quality = "";
            }
            else
            {
                foreach (JProperty p in o.Properties())
                {
                    if (p.Name == "seq") continue;
                    double d;
                    if (TwinJson.TryGetNumber(p.Value, out d)) Set(p.Name, d);
                }
                DateTimeOffset t;
                hasObserved = TwinJson.TryGetTime(o["observed_at_utc"], out t);
                if (hasObserved) observedAt = t;
                quality = JoinQuality(o["quality"]);
            }
            lastApplyRealtime = Time.realtimeSinceStartup;
            dirty = true;
            UpdateTint(true);
        }

        void Set(string key, double v)
        {
            if (!values.ContainsKey(key)) keys.Add(key);
            values[key] = v;
        }

        static string JoinQuality(JToken token)
        {
            JArray a = token as JArray;
            if (a == null) return TwinJson.AsString(token) ?? "";
            string s = "";
            for (int i = 0; i < a.Count; i++)
            {
                string part = TwinJson.AsString(a[i]);
                if (string.IsNullOrEmpty(part)) continue;
                s = s.Length == 0 ? part : s + "," + part;
            }
            return s;
        }

        public bool IsStale()
        {
            if (lastApplyRealtime < 0f) return true;
            if (Time.realtimeSinceStartup - lastApplyRealtime > staleAfterSeconds) return true;
            if (hasObserved && (DateTimeOffset.UtcNow - observedAt).TotalSeconds > staleAfterSeconds) return true;
            if (quality.Contains("stale")) return true;
            return false;
        }

        void Update()
        {
            UpdateTint(false);
        }

        void UpdateTint(bool force)
        {
            int state = IsStale() ? 0 : 1;
            if (!force && state == tintState) return;
            tintState = state;
            Color c = state == 1 ? freshColor : staleColor;
            Tint(c, state == 1 ? c * 0.35f : Color.black);
        }

        public override string GetLabelText()
        {
            if (!TwinUtil.ShowSensorLabels) return null;
            float now = Time.realtimeSinceStartup;
            if (dirty || cachedText == null || now - cachedAt > 1f)
            {
                cachedText = Compose();
                cachedAt = now;
                dirty = false;
            }
            return cachedText;
        }

        public override Vector3 LabelAnchor
        {
            get { return transform.position + Vector3.up * 0.1f; }
        }

        string Compose()
        {
            sb.Length = 0;
            sb.Append(string.IsNullOrEmpty(label) ? id : label);
            if (autoCreated) sb.Append(" [").Append(id).Append(", not in config]");
            if (lastApplyRealtime < 0f)
            {
                sb.Append("\nno data yet");
                return sb.ToString();
            }
            int onLine = 0;
            for (int i = 0; i < keys.Count; i++)
            {
                sb.Append(onLine == 0 ? "\n" : "   ");
                sb.Append(FormatValue(keys[i], values[keys[i]]));
                onLine = (onLine + 1) % 3;
            }
            sb.Append('\n');
            if (hasObserved) sb.Append(TwinUtil.FormatAge((DateTimeOffset.UtcNow - observedAt).TotalSeconds));
            else sb.Append("received ").Append(TwinUtil.FormatAge(Time.realtimeSinceStartup - lastApplyRealtime));
            if (IsStale()) sb.Append(" (stale)");
            if (quality.Length > 0 && quality != "valid") sb.Append("\nquality: ").Append(quality);
            return sb.ToString();
        }

        /// <summary>Known fields get a unit; any other numeric field is shown as "name value".</summary>
        public static string FormatValue(string key, double v)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            switch (key)
            {
                case "temperature_c": return v.ToString("0.0", inv) + " °C";
                case "relative_humidity_percent": return v.ToString("0", inv) + " % RH";
                case "air_vpd_kpa": return v.ToString("0.00", inv) + " kPa VPD";
                case "co2_ppm": return v.ToString("0", inv) + " ppm CO2";
                case "par_umol_m2_s": return v.ToString("0", inv) + " µmol/m²/s PAR";
                case "energy_kwh": return v.ToString("0.000", inv) + " kWh";
                case "power_w": return v.ToString("0", inv) + " W";
                default: return key + " " + v.ToString("0.###", inv);
            }
        }
    }
}
