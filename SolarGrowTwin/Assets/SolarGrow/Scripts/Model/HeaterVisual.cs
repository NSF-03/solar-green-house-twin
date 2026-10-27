using UnityEngine;

namespace SolarGrow
{
    /// <summary>2 kW heater: glows red when on.</summary>
    public class HeaterVisual : SwitchVisual
    {
        protected override Color DefaultOffColor { get { return new Color(0.35f, 0.35f, 0.38f, 1f); } }
        protected override Color DefaultOnColor { get { return new Color(0.95f, 0.18f, 0.08f, 1f); } }
    }
}
