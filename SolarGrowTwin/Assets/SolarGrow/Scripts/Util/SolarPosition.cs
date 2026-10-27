using UnityEngine;

namespace SolarGrow
{
    /// <summary>
    /// Sun position from NOAA's "General Solar Position Calculations" (fractional year, equation of time,
    /// declination, hour angle). Good to a fraction of a degree, plenty for shadows on a twin.
    /// </summary>
    public static class SolarPosition
    {
        /// <summary>Unit vector towards the sun in world axes: x east, y up, z north.</summary>
        public static Vector3 Direction(float latitudeDeg, float longitudeDeg, int dayOfYear, float hourUtc)
        {
            double g = 2.0 * System.Math.PI / 365.0 * (dayOfYear - 1 + (hourUtc - 12.0) / 24.0);
            double eqtime = 229.18 * (0.000075 + 0.001868 * System.Math.Cos(g) - 0.032077 * System.Math.Sin(g)
                                      - 0.014615 * System.Math.Cos(2 * g) - 0.040849 * System.Math.Sin(2 * g));
            double decl = 0.006918 - 0.399912 * System.Math.Cos(g) + 0.070257 * System.Math.Sin(g)
                          - 0.006758 * System.Math.Cos(2 * g) + 0.000907 * System.Math.Sin(2 * g)
                          - 0.002697 * System.Math.Cos(3 * g) + 0.00148 * System.Math.Sin(3 * g);
            double tst = hourUtc * 60.0 + eqtime + 4.0 * longitudeDeg;
            double ha = (tst / 4.0 - 180.0) * System.Math.PI / 180.0;
            double lat = latitudeDeg * System.Math.PI / 180.0;

            // Hour angle positive in the afternoon, when the sun is in the west.
            double east = -System.Math.Cos(decl) * System.Math.Sin(ha);
            double north = System.Math.Sin(decl) * System.Math.Cos(lat) - System.Math.Cos(decl) * System.Math.Sin(lat) * System.Math.Cos(ha);
            double up = System.Math.Sin(lat) * System.Math.Sin(decl) + System.Math.Cos(lat) * System.Math.Cos(decl) * System.Math.Cos(ha);
            return new Vector3((float)east, (float)up, (float)north).normalized;
        }
    }
}
