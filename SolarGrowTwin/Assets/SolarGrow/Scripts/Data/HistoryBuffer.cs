namespace SolarGrow
{
    /// <summary>Fixed-size ring buffer of (time, mean temperature, mean RH); no allocation after construction.</summary>
    public sealed class HistoryBuffer
    {
        readonly double[] times;
        readonly float[] temps;
        readonly float[] rhs;
        int start;
        int count;

        public HistoryBuffer(int capacity)
        {
            if (capacity < 2) capacity = 2;
            times = new double[capacity];
            temps = new float[capacity];
            rhs = new float[capacity];
        }

        public int Count { get { return count; } }
        public int Capacity { get { return times.Length; } }

        /// <summary>Unix seconds of the newest sample, or -infinity when empty.</summary>
        public double LastTime
        {
            get { return count > 0 ? TimeAt(count - 1) : double.NegativeInfinity; }
        }

        public void Add(double unixSeconds, float temperatureC, float relativeHumidity)
        {
            int cap = times.Length;
            int slot;
            if (count < cap)
            {
                slot = (start + count) % cap;
                count++;
            }
            else
            {
                slot = start;
                start = (start + 1) % cap;
            }
            times[slot] = unixSeconds;
            temps[slot] = temperatureC;
            rhs[slot] = relativeHumidity;
        }

        /// <summary>i = 0 is the oldest sample.</summary>
        public double TimeAt(int i) { return times[(start + i) % times.Length]; }
        public float TemperatureAt(int i) { return temps[(start + i) % temps.Length]; }
        public float HumidityAt(int i) { return rhs[(start + i) % rhs.Length]; }

        public void Clear()
        {
            start = 0;
            count = 0;
        }
    }
}
