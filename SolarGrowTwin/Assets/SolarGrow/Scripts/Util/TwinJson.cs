using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SolarGrow
{
    /// <summary>
    /// JSON and time helpers. Pure .NET (no UnityEngine calls), so the TCP thread may use them.
    /// </summary>
    public static class TwinJson
    {
        /// <summary>Parse one JSON line; date-like strings stay strings. Returns null if the line is not a JSON object.</summary>
        public static JObject ParseObject(string json)
        {
            using (StringReader sr = new StringReader(json))
            using (JsonTextReader reader = new JsonTextReader(sr))
            {
                reader.DateParseHandling = DateParseHandling.None;
                reader.FloatParseHandling = FloatParseHandling.Double;
                JToken token = JToken.ReadFrom(reader);
                return token as JObject;
            }
        }

        /// <summary>A finite JSON number (integer or float). Strings and booleans are not numbers.</summary>
        public static bool TryGetNumber(JToken token, out double value)
        {
            value = 0;
            if (token == null) return false;
            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                try
                {
                    value = Convert.ToDouble(((JValue)token).Value, CultureInfo.InvariantCulture);
                }
                catch (Exception)
                {
                    return false;
                }
                return !double.IsNaN(value) && !double.IsInfinity(value);
            }
            return false;
        }

        /// <summary>String value of obj[key]; numbers and booleans are formatted invariantly; null if missing.</summary>
        public static string GetString(JToken obj, string key)
        {
            JObject o = obj as JObject;
            if (o == null) return null;
            return AsString(o[key]);
        }

        public static string AsString(JToken token)
        {
            if (token == null) return null;
            switch (token.Type)
            {
                case JTokenType.String:
                    return (string)token;
                case JTokenType.Integer:
                case JTokenType.Float:
                case JTokenType.Boolean:
                case JTokenType.Date:
                    return ((JValue)token).ToString(CultureInfo.InvariantCulture);
                default:
                    return null;
            }
        }

        /// <summary>ISO 8601 with offset ("...+00:00", "...Z", up to 7 decimals). No offset = UTC.</summary>
        public static bool TryParseTime(string text, out DateTimeOffset time)
        {
            time = default(DateTimeOffset);
            if (string.IsNullOrEmpty(text)) return false;
            return DateTimeOffset.TryParse(text.Trim(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out time);
        }

        public static bool TryGetTime(JToken token, out DateTimeOffset time)
        {
            time = default(DateTimeOffset);
            if (token == null) return false;
            if (token.Type == JTokenType.String) return TryParseTime((string)token, out time);
            if (token.Type == JTokenType.Date)
            {
                object v = ((JValue)token).Value;
                if (v is DateTimeOffset) { time = (DateTimeOffset)v; return true; }
                if (v is DateTime)
                {
                    DateTime dt = (DateTime)v;
                    if (dt.Kind == DateTimeKind.Unspecified) dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                    time = new DateTimeOffset(dt.ToUniversalTime());
                    return true;
                }
            }
            return false;
        }

        public static double ToUnixSeconds(DateTimeOffset time)
        {
            return time.ToUnixTimeMilliseconds() / 1000.0;
        }

        public static double NowUnixSeconds()
        {
            return ToUnixSeconds(DateTimeOffset.UtcNow);
        }

        public static DateTimeOffset FromUnixSeconds(double seconds)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(seconds * 1000.0));
        }

        /// <summary>UTC time in Python isoformat style: 2026-10-27T12:00:00.123456+00:00.</summary>
        public static string ToIso(DateTimeOffset time)
        {
            return time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'+00:00'", CultureInfo.InvariantCulture);
        }

        public static string NowIso()
        {
            return ToIso(DateTimeOffset.UtcNow);
        }

        /// <summary>Device value as a level 0-1: on/open/true = 1, off/closed/false = 0, numbers clamped.</summary>
        public static bool TryDeviceLevel(JToken token, out float level)
        {
            level = 0f;
            if (token == null) return false;
            switch (token.Type)
            {
                case JTokenType.Integer:
                case JTokenType.Float:
                {
                    double d;
                    if (!TryGetNumber(token, out d)) return false;
                    level = (float)Math.Max(0.0, Math.Min(1.0, d));
                    return true;
                }
                case JTokenType.Boolean:
                    level = (bool)token ? 1f : 0f;
                    return true;
                case JTokenType.String:
                {
                    string s = ((string)token).Trim().ToLowerInvariant();
                    if (s == "on" || s == "open" || s == "true") { level = 1f; return true; }
                    if (s == "off" || s == "closed" || s == "close" || s == "false") { level = 0f; return true; }
                    double p;
                    if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out p))
                    {
                        level = (float)Math.Max(0.0, Math.Min(1.0, p));
                        return true;
                    }
                    return false;
                }
                default:
                    return false;
            }
        }

        /// <summary>Short display text of a device or contact value.</summary>
        public static string DeviceText(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return "?";
            if (token.Type == JTokenType.String) return (string)token;
            if (token.Type == JTokenType.Boolean) return (bool)token ? "on" : "off";
            double d;
            if (TryGetNumber(token, out d)) return d.ToString("0.##", CultureInfo.InvariantCulture);
            return token.ToString(Formatting.None);
        }
    }
}
