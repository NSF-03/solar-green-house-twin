using System.Collections.Generic;

namespace SolarGrow
{
    /// <summary>Live state of one greenhouse in the controller: its builder, latest frame, mean history and forecast.</summary>
    public sealed class HouseRuntime
    {
        public HouseRuntime(string id, GreenhouseBuilder builder, int historyCapacity)
        {
            Id = id;
            Builder = builder;
            History = new HistoryBuffer(historyCapacity);
            Forecast = new List<ForecastPoint>();
            LastFrameRealtime = -1f;
        }

        /// <summary>Greenhouse id (A, B, ...).</summary>
        public string Id { get; private set; }

        /// <summary>Builder of the house model.</summary>
        public GreenhouseBuilder Builder { get; set; }

        /// <summary>Mean temperature/RH samples (ring buffer).</summary>
        public HistoryBuffer History { get; private set; }

        /// <summary>Points of the latest forecast received.</summary>
        public List<ForecastPoint> Forecast { get; private set; }

        /// <summary>Latest frame for this house, or null.</summary>
        public TwinStateFrame LastFrame { get; set; }

        /// <summary>Frames received.</summary>
        public int FrameCount { get; set; }

        /// <summary>Time.realtimeSinceStartup of the latest frame, -1 if none.</summary>
        public float LastFrameRealtime { get; set; }

        /// <summary>Increments on every frame; the graph redraws when it changes.</summary>
        public int Version { get; set; }
    }
}
