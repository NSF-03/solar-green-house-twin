using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SolarGrow
{
    /// <summary>Sliding door driven by contacts.door ("open"/"closed"). No reed switch is planned, so it only moves if a frame reports it.</summary>
    public class DoorVisual : TwinComponent
    {
        [Tooltip("Local position of the door panel when closed.")]
        public Vector3 closedLocalPosition;

        [Tooltip("Local position of the door panel when open.")]
        public Vector3 openLocalPosition;

        [Tooltip("Sliding speed (m/s).")]
        public float speed = 0.6f;

        [Tooltip("Latest state from the data.")]
        public bool isOpen;

        bool hasState;
        string cachedLabel;

        public override void Apply(JToken value)
        {
            float f;
            if (TwinJson.TryDeviceLevel(value, out f)) isOpen = f > 0.5f;
            hasState = true;
            cachedLabel = "door: " + TwinJson.DeviceText(value);
        }

        void Update()
        {
            Vector3 target = isOpen ? openLocalPosition : closedLocalPosition;
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, speed * Time.deltaTime);
        }

        public override string GetLabelText()
        {
            return TwinUtil.ShowDeviceLabels && hasState ? cachedLabel : null;
        }

        public override Vector3 LabelAnchor
        {
            get { return transform.position + Vector3.up * 1.95f; }
        }
    }
}
