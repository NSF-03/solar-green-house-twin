using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SolarGrow
{
    /// <summary>Optional timer pump in the water tank: blinks blue while on.</summary>
    public class PumpVisual : TwinComponent
    {
        [Tooltip("Colour when off.")]
        public Color offColor = new Color(0.2f, 0.3f, 0.45f, 1f);

        [Tooltip("Blink colour when on.")]
        public Color onColor = new Color(0.2f, 0.55f, 1f, 1f);

        [Tooltip("Blinks per second while on.")]
        public float blinkHz = 2f;

        [Tooltip("Latest state from the data.")]
        public bool isOn;

        int phase = -1;
        bool hasState;
        string cachedLabel;

        protected override void OnEnable()
        {
            base.OnEnable();
            phase = -1;
        }

        public override void Apply(JToken value)
        {
            float f;
            if (TwinJson.TryDeviceLevel(value, out f)) isOn = f > 0.5f;
            hasState = true;
            cachedLabel = id + ": " + TwinJson.DeviceText(value);
        }

        void Update()
        {
            int p = isOn ? ((int)(Time.time * blinkHz * 2f) & 1) : 2;
            if (p == phase) return;
            phase = p;
            if (p == 0) Tint(onColor, onColor * 2f);
            else Tint(offColor, Color.black);
        }

        public override string GetLabelText()
        {
            return TwinUtil.ShowDeviceLabels && hasState ? cachedLabel : null;
        }
    }
}
