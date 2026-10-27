using System.Collections.Generic;
using UnityEngine;

namespace SolarGrow
{
    /// <summary>
    /// Draws mean temperature and RH history (solid) and the latest forecast (dashed) into a small Texture2D
    /// that the HUD shows with GUI.DrawTexture. Time axis: now - history .. now + forecast. Left axis T, right axis RH.
    /// Redraws only when new data arrived or once per second.
    /// </summary>
    public sealed class ForecastGraph
    {
        static readonly Color32 Background = new Color32(18, 22, 26, 235);
        static readonly Color32 GridColor = new Color32(52, 58, 64, 255);
        static readonly Color32 NowColor = new Color32(150, 150, 150, 255);
        static readonly Color32 TempHistory = new Color32(255, 120, 60, 255);
        static readonly Color32 TempForecast = new Color32(255, 205, 150, 255);
        static readonly Color32 RhHistory = new Color32(80, 170, 255, 255);
        static readonly Color32 RhForecast = new Color32(165, 215, 255, 255);
        const double MaxGapSeconds = 900.0;

        readonly int width;
        readonly int height;
        Texture2D texture;
        Color32[] pixels;
        int drawnVersion = int.MinValue;
        float drawnAt = -100f;
        double windowStart, windowEnd;
        int dash;

        public ForecastGraph(int width, int height)
        {
            this.width = Mathf.Max(32, width);
            this.height = Mathf.Max(16, height);
        }

        /// <summary>Axis ranges of the last drawing (for the HUD labels).</summary>
        public float TempMin { get; private set; }
        public float TempMax { get; private set; }
        public float RhMin { get; private set; }
        public float RhMax { get; private set; }
        public bool HasData { get; private set; }
        /// <summary>Horizontal position of "now" as a fraction of the width.</summary>
        public float NowFraction { get; private set; }

        public Texture2D Render(HouseRuntime house, double nowUnix, float historyMinutes, float forecastMinutes)
        {
            float t = Time.realtimeSinceStartup;
            if (texture != null && house.Version == drawnVersion && t - drawnAt < 1f) return texture;
            drawnVersion = house.Version;
            drawnAt = t;
            if (texture == null)
            {
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.hideFlags = HideFlags.HideAndDontSave;
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                pixels = new Color32[width * height];
            }
            windowStart = nowUnix - Mathf.Max(1f, historyMinutes) * 60.0;
            windowEnd = nowUnix + Mathf.Max(1f, forecastMinutes) * 60.0;
            NowFraction = (float)((nowUnix - windowStart) / (windowEnd - windowStart));

            for (int i = 0; i < pixels.Length; i++) pixels[i] = Background;
            for (int k = 1; k < 4; k++) HLine(Mathf.RoundToInt(k * (height - 1) / 4f), GridColor);
            VLine(Mathf.RoundToInt(NowFraction * (width - 1)), NowColor);

            ComputeRanges(house.History, house.Forecast);
            if (HasData)
            {
                DrawHistory(house.History, false, RhHistory);
                DrawForecast(house.Forecast, false, RhForecast);
                DrawHistory(house.History, true, TempHistory);
                DrawForecast(house.Forecast, true, TempForecast);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        public void Dispose()
        {
            if (texture != null) UnityEngine.Object.Destroy(texture);
            texture = null;
        }

        void ComputeRanges(HistoryBuffer history, List<ForecastPoint> forecast)
        {
            float tmin = float.MaxValue, tmax = float.MinValue, rmin = float.MaxValue, rmax = float.MinValue;
            bool anyT = false, anyRh = false;
            for (int i = 0; i < history.Count; i++)
            {
                double ts = history.TimeAt(i);
                if (ts < windowStart || ts > windowEnd) continue;
                float v = history.TemperatureAt(i);
                if (!float.IsNaN(v)) { anyT = true; tmin = Mathf.Min(tmin, v); tmax = Mathf.Max(tmax, v); }
                float r = history.HumidityAt(i);
                if (!float.IsNaN(r)) { anyRh = true; rmin = Mathf.Min(rmin, r); rmax = Mathf.Max(rmax, r); }
            }
            for (int i = 0; i < forecast.Count; i++)
            {
                ForecastPoint p = forecast[i];
                if (p.unixSeconds < windowStart || p.unixSeconds > windowEnd) continue;
                if (!float.IsNaN(p.temperatureC)) { anyT = true; tmin = Mathf.Min(tmin, p.temperatureC); tmax = Mathf.Max(tmax, p.temperatureC); }
                if (!float.IsNaN(p.relativeHumidity)) { anyRh = true; rmin = Mathf.Min(rmin, p.relativeHumidity); rmax = Mathf.Max(rmax, p.relativeHumidity); }
            }
            HasData = anyT || anyRh;
            if (!anyT) { tmin = 15f; tmax = 25f; }
            if (tmax - tmin < 4f) { float mid = (tmin + tmax) * 0.5f; tmin = mid - 2f; tmax = mid + 2f; }
            float padT = (tmax - tmin) * 0.08f;
            TempMin = tmin - padT;
            TempMax = tmax + padT;
            if (!anyRh) { rmin = 40f; rmax = 90f; }
            if (rmax - rmin < 20f) { float mid = (rmin + rmax) * 0.5f; rmin = mid - 10f; rmax = mid + 10f; }
            float padR = (rmax - rmin) * 0.08f;
            RhMin = Mathf.Max(0f, rmin - padR);
            RhMax = Mathf.Min(100f, rmax + padR);
            if (RhMax - RhMin < 1f) RhMax = RhMin + 1f;
        }

        float X(double ts)
        {
            return (float)((ts - windowStart) / (windowEnd - windowStart) * (width - 1));
        }

        float Y(float v, float min, float max)
        {
            return (v - min) / (max - min) * (height - 1);
        }

        void DrawHistory(HistoryBuffer history, bool temperature, Color32 color)
        {
            bool havePrev = false;
            float px = 0f, py = 0f;
            double prevTs = 0.0;
            for (int i = 0; i < history.Count; i++)
            {
                double ts = history.TimeAt(i);
                float v = temperature ? history.TemperatureAt(i) : history.HumidityAt(i);
                if (float.IsNaN(v)) { havePrev = false; continue; }
                float x = X(ts);
                float y = temperature ? Y(v, TempMin, TempMax) : Y(v, RhMin, RhMax);
                if (havePrev && ts - prevTs <= MaxGapSeconds) Line(px, py, x, y, color, false);
                else Plot(Mathf.RoundToInt(x), Mathf.RoundToInt(y), color);
                px = x; py = y; prevTs = ts; havePrev = true;
            }
        }

        void DrawForecast(List<ForecastPoint> forecast, bool temperature, Color32 color)
        {
            bool havePrev = false;
            float px = 0f, py = 0f;
            dash = 0;
            for (int i = 0; i < forecast.Count; i++)
            {
                ForecastPoint p = forecast[i];
                float v = temperature ? p.temperatureC : p.relativeHumidity;
                if (float.IsNaN(v)) { havePrev = false; continue; }
                float x = X(p.unixSeconds);
                float y = temperature ? Y(v, TempMin, TempMax) : Y(v, RhMin, RhMax);
                if (havePrev) Line(px, py, x, y, color, true);
                px = x; py = y; havePrev = true;
            }
        }

        void Line(float x0, float y0, float x1, float y1, Color32 c, bool dashed)
        {
            float maxX = width - 1;
            if (x1 < x0)
            {
                float tx = x0; x0 = x1; x1 = tx;
                float ty = y0; y0 = y1; y1 = ty;
            }
            if (x1 < 0f || x0 > maxX) return;
            float dx = x1 - x0;
            if (dx > 1e-6f && x0 < 0f)
            {
                y0 += (y1 - y0) * (0f - x0) / dx;
                x0 = 0f;
            }
            dx = x1 - x0;
            if (dx > 1e-6f && x1 > maxX)
            {
                y1 = y0 + (y1 - y0) * (maxX - x0) / dx;
                x1 = maxX;
            }
            y0 = Mathf.Clamp(y0, -height, 2f * height);
            y1 = Mathf.Clamp(y1, -height, 2f * height);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0))));
            for (int s = 0; s <= steps; s++)
            {
                if (dashed)
                {
                    dash++;
                    if (((dash / 5) & 1) == 1) continue;
                }
                float f = s / (float)steps;
                int ix = Mathf.RoundToInt(x0 + (x1 - x0) * f);
                int iy = Mathf.RoundToInt(y0 + (y1 - y0) * f);
                Plot(ix, iy, c);
                Plot(ix, iy + 1, c);
            }
        }

        void Plot(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            pixels[y * width + x] = c;
        }

        void HLine(int y, Color32 c)
        {
            for (int x = 0; x < width; x++) Plot(x, y, c);
        }

        void VLine(int x, Color32 c)
        {
            for (int y = 0; y < height; y++) Plot(x, y, c);
        }
    }
}
