using UnityEngine;

namespace SolarGrow
{
    /// <summary>100 W LED grow bar: glows white-purple when on.</summary>
    public class LedBarVisual : SwitchVisual
    {
        protected override Color DefaultOffColor { get { return new Color(0.75f, 0.75f, 0.8f, 1f); } }
        protected override Color DefaultOnColor { get { return new Color(1f, 0.6f, 1f, 1f); } }
    }
}
