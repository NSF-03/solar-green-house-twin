using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SolarGrow
{
    /// <summary>
    /// Base for parts that glow when their device is on (heater, LED bars). Value "on"/"off", "open"/"closed",
    /// true/false or a number 0-1 (duty cycle) sets the glow.
    /// </summary>
    public abstract class SwitchVisual : TwinComponent
    {
        [Tooltip("Body colour when off (alpha 0 = use the default of this part type).")]
        public Color offColor = Color.clear;

        [Tooltip("Body and glow colour when on (alpha 0 = use the default of this part type).")]
        public Color onColor = Color.clear;

        [Tooltip("Glow strength at full power.")]
        public float emissionStrength = 2f;

        [Tooltip("Latest level from the data, 0-1.")]
        [Range(0f, 1f)] public float level;

        bool hasState;
        string cachedLabel;

        protected abstract Color DefaultOffColor { get; }
        protected abstract Color DefaultOnColor { get; }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (offColor.a <= 0f) offColor = DefaultOffColor;
            if (onColor.a <= 0f) onColor = DefaultOnColor;
            Refresh();
        }

        public override void Apply(JToken value)
        {
            float f;
            if (TwinJson.TryDeviceLevel(value, out f)) level = f;
            hasState = true;
            cachedLabel = id + ": " + TwinJson.DeviceText(value);
            Refresh();
        }

        void Refresh()
        {
            Color body = Color.Lerp(offColor, onColor, level);
            Tint(body, onColor * (level * emissionStrength));
        }

        public override string GetLabelText()
        {
            return TwinUtil.ShowDeviceLabels && hasState ? cachedLabel : null;
        }
    }
}
