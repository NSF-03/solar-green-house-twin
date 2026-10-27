using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SolarGrow
{
    /// <summary>Kind of a built part.</summary>
    public enum TwinKind
    {
        Ground, Slab, Frame, Glass, Door, Vent, Louvre, Pot, Plant, Heater, LedBar,
        ControlBox, EnergyMeter, Sensor, SensorShield, Hedge, WaterTank, Pump, Compass, Other
    }

    /// <summary>
    /// Every part the builder makes carries one of these, so it can be found by id
    /// (GreenhouseBuilder.FindComponent). Subclasses show live values through Apply().
    /// </summary>
    [DisallowMultipleComponent]
    public class TwinComponent : MonoBehaviour
    {
        [Tooltip("Unique id inside one greenhouse, e.g. heater, led-1, vent-1, glass-roof-south.")]
        public string id;

        [Tooltip("Kind of part.")]
        public TwinKind kind = TwinKind.Other;

        [Tooltip("Greenhouse this part belongs to (A, B, ...).")]
        public string greenhouseId;

        [Tooltip("Optional fixed text shown next to the part in Play mode.")]
        public string label;

        /// <summary>Enabled parts in Play mode; the HUD draws their labels.</summary>
        public static readonly List<TwinComponent> Active = new List<TwinComponent>();

        Renderer[] renderers;
        MaterialPropertyBlock block;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active.Clear();
        }

        protected virtual void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

        protected virtual void OnDisable()
        {
            Active.Remove(this);
        }

        /// <summary>Show a value from the twin/state frame (device state, contact or sensor entry). Base: nothing.</summary>
        public virtual void Apply(JToken value) { }

        /// <summary>Text for the world label, or null for no label.</summary>
        public virtual string GetLabelText()
        {
            return string.IsNullOrEmpty(label) ? null : label;
        }

        /// <summary>World position of the label.</summary>
        public virtual Vector3 LabelAnchor
        {
            get { return transform.position + Vector3.up * 0.12f; }
        }

        /// <summary>Colour (and emission) of this part's renderers through a property block.</summary>
        protected void Tint(Color color, Color emission)
        {
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>(true);
            if (block == null) block = new MaterialPropertyBlock();
            TwinUtil.Tint(renderers, block, color, emission);
        }

        /// <summary>Forget cached renderers (after children were added).</summary>
        public void RefreshRenderers()
        {
            renderers = null;
        }
    }
}
