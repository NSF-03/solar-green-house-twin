using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace SolarGrow
{
    /// <summary>One point of a forecast: time (Unix seconds), temperature and RH (NaN when missing).</summary>
    public struct ForecastPoint
    {
        public double unixSeconds;
        public float temperatureC;
        public float relativeHumidity;

        public ForecastPoint(double unixSeconds, float temperatureC, float relativeHumidity)
        {
            this.unixSeconds = unixSeconds;
            this.temperatureC = temperatureC;
            this.relativeHumidity = relativeHumidity;
        }
    }

    /// <summary>
    /// The twin/state frame of one greenhouse (the bridge sends it with "type":"state"):
    /// {greenhouse, updated_at_utc, nodes, sensors, mean, devices, contacts, twin?}.
    /// Missing fields give null; unknown fields are kept in Raw and ignored.
    /// </summary>
    public sealed class TwinStateFrame
    {
        public JObject Raw { get; private set; }

        public TwinStateFrame(JObject raw)
        {
            Raw = raw ?? new JObject();
        }

        public string Greenhouse
        {
            get { return TwinJson.GetString(Raw, "greenhouse") ?? ""; }
        }

        public DateTimeOffset? UpdatedAt
        {
            get
            {
                DateTimeOffset t;
                return TwinJson.TryGetTime(Raw["updated_at_utc"], out t) ? t : (DateTimeOffset?)null;
            }
        }

        public JObject Sensors() { return Raw["sensors"] as JObject; }
        public JObject Devices() { return Raw["devices"] as JObject; }
        public JObject Contacts() { return Raw["contacts"] as JObject; }
        public JObject Mean() { return Raw["mean"] as JObject; }
        public JObject Nodes() { return Raw["nodes"] as JObject; }
        public JObject Twin() { return Raw["twin"] as JObject; }

        /// <summary>
        /// Device states to show: 'devices' (the controller's commands) overlaid with every node's
        /// 'reported_actuators' (what the node says it holds).
        /// Returns a new object, or null when the frame has neither.
        /// </summary>
        public JObject EffectiveDevices()
        {
            JObject commanded = Devices();
            JObject result = commanded != null ? (JObject)commanded.DeepClone() : null;
            JObject nodes = Nodes();
            if (nodes == null) return result;
            foreach (JProperty node in nodes.Properties())
            {
                JObject n = node.Value as JObject;
                JObject reported = n != null ? n["reported_actuators"] as JObject : null;
                if (reported == null) continue;
                if (result == null) result = new JObject();
                foreach (JProperty p in reported.Properties()) result[p.Name] = p.Value.DeepClone();
            }
            return result;
        }

        /// <summary>Greenhouse mean (mean.temperature_c etc.); false when sensor_count is 0 or there is no temperature.</summary>
        public bool TryGetMean(out DateTimeOffset at, out float temperatureC, out float relativeHumidity)
        {
            at = DateTimeOffset.UtcNow;
            temperatureC = float.NaN;
            relativeHumidity = float.NaN;
            JObject mean = Mean();
            if (mean == null) return false;
            double count;
            if (TwinJson.TryGetNumber(mean["sensor_count"], out count) && count <= 0) return false;
            double t;
            if (!TwinJson.TryGetNumber(mean["temperature_c"], out t)) return false;
            temperatureC = (float)t;
            double rh;
            if (TwinJson.TryGetNumber(mean["relative_humidity_percent"], out rh)) relativeHumidity = (float)rh;
            DateTimeOffset when;
            if (TwinJson.TryGetTime(mean["observed_at_utc"], out when)) at = when;
            else if (TwinJson.TryGetTime(Raw["updated_at_utc"], out when)) at = when;
            return true;
        }

        /// <summary>
        /// Forecast points into 'into' (cleared first). Reads twin.forecast.points[] (twin_core.frame()),
        /// or a plain list twin.forecast[] / forecast[]; time from at_utc, time or t. Returns the count.
        /// </summary>
        public int Forecast(List<ForecastPoint> into)
        {
            into.Clear();
            JToken fc = null;
            JObject twin = Twin();
            if (twin != null) fc = twin["forecast"];
            if (fc == null || fc.Type == JTokenType.Null) fc = Raw["forecast"];
            JArray points = fc as JArray;
            if (points == null)
            {
                JObject fo = fc as JObject;
                if (fo != null) points = fo["points"] as JArray;
            }
            if (points == null) return 0;
            for (int i = 0; i < points.Count; i++)
            {
                JObject p = points[i] as JObject;
                if (p == null) continue;
                DateTimeOffset t;
                if (!TwinJson.TryGetTime(p["at_utc"], out t) && !TwinJson.TryGetTime(p["time"], out t) && !TwinJson.TryGetTime(p["t"], out t))
                    continue;
                double temp, rh;
                float tc = TwinJson.TryGetNumber(p["temperature_c"], out temp) ? (float)temp : float.NaN;
                float rc = TwinJson.TryGetNumber(p["relative_humidity_percent"], out rh) ? (float)rh : float.NaN;
                into.Add(new ForecastPoint(TwinJson.ToUnixSeconds(t), tc, rc));
            }
            return into.Count;
        }

        /// <summary>Allocating form of Forecast(List).</summary>
        public List<ForecastPoint> Forecast()
        {
            List<ForecastPoint> list = new List<ForecastPoint>();
            Forecast(list);
            return list;
        }
    }
}
