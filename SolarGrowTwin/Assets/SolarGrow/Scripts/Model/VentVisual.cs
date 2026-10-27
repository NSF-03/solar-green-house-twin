using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SolarGrow
{
    /// <summary>
    /// Roof vent hinged at the ridge. The value "open"/"closed"/"on"/"off" or a number 0-1 sets the open
    /// fraction; the hinge child turns to fraction x maxAngleDeg about hingeAxis.
    /// </summary>
    public class VentVisual : TwinComponent
    {
        [Tooltip("Child transform that turns; its pivot lies on the ridge.")]
        public Transform hinge;

        [Tooltip("Hinge axis in this object's local space; its sign makes positive angles lift the free edge.")]
        public Vector3 hingeAxis = Vector3.right;

        [Tooltip("Opening angle at fraction 1 (deg).")]
        public float maxAngleDeg = 40f;

        [Tooltip("Animation speed (deg/s).")]
        public float speedDegPerSecond = 40f;

        [Tooltip("True for the vent driven by the 12 V linear actuator.")]
        public bool hasActuator;

        [Tooltip("Latest open fraction from the data (0 = closed, 1 = fully open).")]
        [Range(0f, 1f)] public float fraction;

        /// <summary>Panel preview: when true every vent shows PreviewFraction instead of the data.</summary>
        public static bool PreviewOverride;
        public static float PreviewFraction;

        float angle;
        bool hasState;
        string stateText = "";
        string cachedLabel;
        bool cachedPreview;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            PreviewOverride = false;
            PreviewFraction = 0f;
        }

        public float CurrentFraction
        {
            get { return maxAngleDeg > 0f ? angle / maxAngleDeg : 0f; }
        }

        public override void Apply(JToken value)
        {
            float f;
            if (TwinJson.TryDeviceLevel(value, out f)) fraction = f;
            stateText = TwinJson.DeviceText(value);
            hasState = true;
            cachedLabel = null;
        }

        void Update()
        {
            if (hinge == null) return;
            float target = Mathf.Clamp01(PreviewOverride ? PreviewFraction : fraction) * maxAngleDeg;
            angle = Mathf.MoveTowards(angle, target, speedDegPerSecond * Time.deltaTime);
            hinge.localRotation = Quaternion.AngleAxis(angle, hingeAxis);
        }

        public override string GetLabelText()
        {
            if (!TwinUtil.ShowDeviceLabels) return null;
            if (!hasActuator && !PreviewOverride) return null;
            if (cachedLabel == null || cachedPreview != PreviewOverride)
            {
                cachedPreview = PreviewOverride;
                string state = PreviewOverride ? "preview " + Mathf.RoundToInt(PreviewFraction * 100f) + " %"
                                               : (hasState ? stateText : "no data");
                cachedLabel = id + (hasActuator ? " (actuator): " : ": ") + state;
            }
            return cachedLabel;
        }

        public override Vector3 LabelAnchor
        {
            get { return transform.position + Vector3.up * 0.25f; }
        }
    }
}
