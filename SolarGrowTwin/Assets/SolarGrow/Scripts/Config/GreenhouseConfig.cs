using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarGrow
{
    /// <summary>
    /// Every adjustable number of the glasshouse model, in metres and degrees.
    /// Local axes of one house: origin at the floor centre on the slab top, +X along the ridge
    /// (towards the east gable at heading 0), +Y up, +Z towards the north long wall.
    /// Defaults describe a small aluminium-framed glasshouse, 1976 x 2632 mm.
    /// </summary>
    [CreateAssetMenu(fileName = "GreenhouseConfig", menuName = "SolarGrow/Greenhouse Config")]
    public class GreenhouseConfig : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/SolarGrow/Config/GreenhouseConfig.asset";

        [Header("Glasshouse body (1976 x 2632 mm)")]
        [Tooltip("Gable-end width across the ridge (m). Default 1.976.")]
        [Range(0.5f, 6f)] public float width = 1.976f;
        [Tooltip("Length along the ridge (m). Default 2.632.")]
        [Range(0.5f, 10f)] public float length = 2.632f;
        [Tooltip("Eaves height above the slab top (m). Default 1.691.")]
        [Range(0.5f, 4f)] public float eavesHeight = 1.691f;
        [Tooltip("Ridge height above the slab top (m). Default 2.249.")]
        [Range(0.6f, 5f)] public float ridgeHeight = 2.249f;
        [Tooltip("Glass pane thickness (m). 4 mm toughened glass.")]
        [Range(0.001f, 0.02f)] public float glassThickness = 0.004f;
        [Tooltip("Square size of the aluminium frame bars (m).")]
        [Range(0.01f, 0.1f)] public float frameBarWidth = 0.035f;
        [Tooltip("Target spacing of the glazing bars on the long walls and roof (m).")]
        [Range(0.2f, 1.5f)] public float glazingBarSpacing = 0.66f;
        [Tooltip("Glass opacity for display only (0 = invisible, 1 = opaque).")]
        [Range(0.02f, 1f)] public float glassOpacity = 0.18f;

        [Header("Concrete slab")]
        [Tooltip("Slab thickness (m).")]
        [Range(0.01f, 0.4f)] public float slabThickness = 0.10f;
        [Tooltip("How far the slab reaches beyond the walls on every side (m).")]
        [Range(0f, 1f)] public float slabMargin = 0.10f;

        [Header("Sliding door (610 x 1836 mm)")]
        [Tooltip("Door width (m). Default 0.610.")]
        [Range(0.2f, 2f)] public float doorWidth = 0.610f;
        [Tooltip("Door height (m). Default 1.836; clipped below the gable roof line.")]
        [Range(0.5f, 3f)] public float doorHeight = 1.836f;
        [Tooltip("Gable end that carries the door. Default east, facing greenhouse B.")]
        public GableEnd doorGable = GableEnd.East;
        [Tooltip("Door centre offset along the gable from its centre line (m, + towards the north wall).")]
        [Range(-2f, 2f)] public float doorOffset = 0f;

        [Header("Roof vents (one with a 12 V linear actuator)")]
        [Tooltip("Number of roof vents.")]
        [Range(0, 8)] public int ventCount = 2;
        [Tooltip("Vent width along the ridge (m).")]
        [Range(0.1f, 2f)] public float ventWidth = 0.60f;
        [Tooltip("Vent length down the slope from the ridge (m).")]
        [Range(0.1f, 2f)] public float ventLength = 0.55f;
        [Tooltip("Roof slope that carries the vents; Alternate (south first) and AlternateNorthFirst put them on both slopes in turn.")]
        public RoofSlope ventSlope = RoofSlope.AlternateNorthFirst;
        [Tooltip("Vents sit in the middle of a bay between two rafters. DoorEnd = vent 1 in the bay next to the door, vent 2 in the next bay, and so on.")]
        public VentPlacement ventPlacement = VentPlacement.DoorEnd;
        [Tooltip("DoorEnd only: bay of vent 1 counted from the door gable (1 = next to the door); vent 2 takes the next bay. Default 2: the middle two of four bays.")]
        [Range(1, 8)] public int ventStartBay = 2;
        [Tooltip("Largest opening angle of a vent (deg), shown when the vent value is 'open' or 1.0.")]
        [Range(0f, 90f)] public float ventMaxAngleDeg = 40f;
        [Tooltip("1-based number of the vent that carries the actuator (0 = none).")]
        [Range(0, 8)] public int actuatedVent = 1;

        [Header("Louvre")]
        [Tooltip("Show the louvre.")]
        public bool louvreEnabled = true;
        [Tooltip("Wall that carries the louvre; Door = in the lower door panel. Default: the gable opposite the door.")]
        public WallSide louvreWall = WallSide.WestGable;
        [Tooltip("Louvre width (m).")]
        [Range(0.05f, 1.5f)] public float louvreWidth = 0.45f;
        [Tooltip("Louvre height (m).")]
        [Range(0.05f, 1.5f)] public float louvreHeight = 0.35f;
        [Tooltip("Height of the louvre centre above the slab (m).")]
        [Range(0f, 3f)] public float louvreCentreHeight = 0.45f;
        [Tooltip("Louvre centre offset along its wall from the wall centre (m); on the door, from the door centre.")]
        [Range(-3f, 3f)] public float louvreOffset = 0f;

        [Header("Placement")]
        [Tooltip("Rotation of each house about the vertical axis (deg, clockwise seen from above). 0 = ridge east-west, long walls face south and north.")]
        [Range(0f, 360f)] public float orientationDeg = 0f;
        [Tooltip("Offset of greenhouse B from A in A's axes (m): +X along the ridge, +Z towards the north wall. Default: a gap of 1.5 house lengths (3.948 m) to the east.")]
        public Vector3 houseBOffset = new Vector3(6.58f, 0f, 0f);

        [Header("Sun (sets the Directional Light on every rebuild)")]
        [Tooltip("Site latitude (deg, + north).")]
        [Range(-90f, 90f)] public float siteLatitudeDeg = 52.0f;
        [Tooltip("Site longitude (deg, + east).")]
        [Range(-180f, 180f)] public float siteLongitudeDeg = 0f;
        [Tooltip("Month of the sun position. Default: winter solstice, the lowest sun of the year.")]
        [Range(1, 12)] public int sunMonth = 12;
        [Tooltip("Day of the month of the sun position.")]
        [Range(1, 31)] public int sunDay = 21;
        [Tooltip("Time of the sun position (h, UTC; UK winter time is UTC). 12 is about solar noon near the Greenwich meridian.")]
        [Range(0f, 24f)] public float sunHourUtc = 12f;

        [Header("Pots and plants (two rows)")]
        [Tooltip("Number of pots (start with 3; up to 10 planned).")]
        [Range(0, 60)] public int potCount = 10;
        [Tooltip("Number of pot rows along the ridge.")]
        [Range(1, 6)] public int potRows = 2;
        [Tooltip("Pot top diameter (m); 15 L pot about 0.30.")]
        [Range(0.05f, 1f)] public float potDiameter = 0.30f;
        [Tooltip("Pot height (m).")]
        [Range(0.05f, 1f)] public float potHeight = 0.28f;
        [Tooltip("Pot spacing along a row (m).")]
        [Range(0.05f, 2f)] public float potSpacing = 0.40f;
        [Tooltip("Distance between row centre lines (m).")]
        [Range(0.05f, 3f)] public float potRowSpacing = 0.90f;
        [Tooltip("Plant height above the pot (m); 0 hides the plants.")]
        [Range(0f, 2.5f)] public float plantHeight = 0.80f;

        [Header("Heater (2 kW fan heater)")]
        [Tooltip("Heater centre in house coordinates (m). Default: aisle at the end away from the door, about 0.2 m off the glass; estimate.")]
        public Vector3 heaterPosition = new Vector3(-1.00f, 0.22f, 0f);
        [Tooltip("Heater size x, y, z (m), approximate.")]
        public Vector3 heaterSize = new Vector3(0.22f, 0.44f, 0.30f);

        [Header("LED bars (2 x 100 W)")]
        [Tooltip("Number of LED bars; bar i hangs over pot row i.")]
        [Range(0, 8)] public int ledCount = 2;
        [Tooltip("Length of one LED bar along the ridge (m).")]
        [Range(0.1f, 3f)] public float ledLength = 1.20f;
        [Tooltip("Height of the LED bars above the slab (m).")]
        [Range(0.1f, 4f)] public float ledHeight = 1.90f;

        [Header("Control box and energy meter (outside, on the ground)")]
        [Tooltip("Gable end the control box stands by, outside. Default: the door end.")]
        public GableEnd controlBoxGable = GableEnd.East;
        [Tooltip("Gap between the gable and the back of the control box (m). Default 1.5, on the ground; estimate.")]
        [Range(0f, 3f)] public float controlBoxGap = 1.5f;
        [Tooltip("Control box size: x = along the gable, y = height, z = depth out from the wall (m).")]
        public Vector3 controlBoxSize = new Vector3(0.40f, 0.50f, 0.20f);
        [Tooltip("Height of the control box centre above the slab (m); half the box height puts it on the ground.")]
        [Range(0.1f, 3f)] public float controlBoxHeight = 0.25f;
        [Tooltip("Control box centre offset along the gable (m, + towards the north wall).")]
        [Range(-3f, 3f)] public float controlBoxOffset = 0.6f;
        [Tooltip("Energy meter box position relative to the control box centre (m, house axes); default puts it on the ground beside the box.")]
        public Vector3 energyMeterOffset = new Vector3(0f, -0.17f, -0.33f);

        [Header("Water tank and timer pump (pending decision)")]
        [Tooltip("Show a water tank with a pump inside the house.")]
        public bool waterTankEnabled = false;
        [Tooltip("Water tank centre in house coordinates (m); north corner at the end away from the door, clear of the heater.")]
        public Vector3 waterTankPosition = new Vector3(-1.12f, 0.20f, 0.60f);
        [Tooltip("Water tank size x, y, z (m); 25 L about 0.30 x 0.40 x 0.35.")]
        public Vector3 waterTankSize = new Vector3(0.30f, 0.40f, 0.35f);

        [Header("Trees / hedge north of greenhouse A")]
        [Tooltip("Show the row of trees along the north long wall of greenhouse A.")]
        public bool hedgeEnabled = true;
        [Tooltip("Gap between the north long wall and the trees (m).")]
        [Range(0f, 10f)] public float hedgeDistance = 0.8f;
        [Tooltip("Tree height (m); default about twice the greenhouse.")]
        [Range(0.1f, 15f)] public float hedgeHeight = 4.5f;
        [Tooltip("Length of the tree row along the ridge (m), centred on greenhouse A, long enough to run behind both houses.")]
        [Range(0.1f, 40f)] public float hedgeLength = 20.0f;
        [Tooltip("Depth of the tree row (m).")]
        [Range(0.1f, 5f)] public float hedgeDepth = 1.0f;

        [Header("Sensors (ids must match the twin/state frame)")]
        [Tooltip("Sensors shown on the model; add one here and it appears in the model and the panel.")]
        public List<SensorSpec> sensors = DefaultSensors();

        [Header("Actuators (device names must match the twin/state frame)")]
        [Tooltip("Devices shown on the model and offered as manual buttons.")]
        public List<ActuatorSpec> actuators = DefaultActuators();

        [Header("Display")]
        [Tooltip("A sensor marker turns grey when its latest reading is older than this (s); the backend drops sensors after 300 s.")]
        [Range(5f, 3600f)] public float staleAfterSeconds = 300f;

        /// <summary>Raised after any change (inspector, panel, import). Builders rebuild on it.</summary>
        public event Action Changed;

        public static List<SensorSpec> DefaultSensors()
        {
            return new List<SensorSpec>
            {
                new SensorSpec("th-07", SensorKind.AirTRHCO2, new Vector3(0f, 1.20f, 0f), "th-07 air T/RH/CO2, 1.2 m, shield"),
                new SensorSpec("th-08", SensorKind.TRH, new Vector3(0f, 2.05f, 0f), "th-08 T/RH below the ridge"),
                new SensorSpec("par-01", SensorKind.PAR, new Vector3(0f, 1.55f, -0.45f), "par-01 PAR above the canopy"),
                new SensorSpec("meter-01", SensorKind.EnergyMeter, new Vector3(0f, 0.12f, 0f), "meter-01 energy meter")
            };
        }

        public static List<ActuatorSpec> DefaultActuators()
        {
            return new List<ActuatorSpec>
            {
                new ActuatorSpec("heater", ActuatorKind.Heater, "heater"),
                new ActuatorSpec("led", ActuatorKind.Led, "led"),
                new ActuatorSpec("vent", ActuatorKind.Vent, "vent-1"),
                new ActuatorSpec("pump", ActuatorKind.Pump, "pump")
            };
        }

        public SensorSpec FindSensor(string sensorId)
        {
            if (sensors == null || sensorId == null) return null;
            for (int i = 0; i < sensors.Count; i++)
                if (sensors[i] != null && sensors[i].id == sensorId) return sensors[i];
            return null;
        }

        public ActuatorSpec FindActuator(string deviceName)
        {
            if (actuators == null || deviceName == null) return null;
            for (int i = 0; i < actuators.Count; i++)
                if (actuators[i] != null && actuators[i].deviceName == deviceName) return actuators[i];
            return null;
        }

        /// <summary>Clamp every value into a buildable range and repair the lists.</summary>
        public void Sanitize()
        {
            width = Mathf.Max(0.5f, width);
            length = Mathf.Max(0.5f, length);
            eavesHeight = Mathf.Max(0.5f, eavesHeight);
            ridgeHeight = Mathf.Max(eavesHeight + 0.02f, ridgeHeight);
            glassThickness = Mathf.Clamp(glassThickness, 0.0005f, 0.05f);
            frameBarWidth = Mathf.Clamp(frameBarWidth, 0.005f, 0.2f);
            glazingBarSpacing = Mathf.Max(0.2f, glazingBarSpacing);
            glassOpacity = Mathf.Clamp(glassOpacity, 0.02f, 1f);
            slabThickness = Mathf.Max(0.01f, slabThickness);
            slabMargin = Mathf.Max(0f, slabMargin);
            doorWidth = Mathf.Clamp(doorWidth, 0.2f, Mathf.Max(0.2f, width - 0.1f));
            doorHeight = Mathf.Max(0.5f, doorHeight);
            ventCount = Mathf.Clamp(ventCount, 0, 8);
            ventWidth = Mathf.Max(0.1f, ventWidth);
            ventLength = Mathf.Max(0.1f, ventLength);
            ventMaxAngleDeg = Mathf.Clamp(ventMaxAngleDeg, 0f, 90f);
            actuatedVent = Mathf.Clamp(actuatedVent, 0, ventCount);
            ventStartBay = Mathf.Clamp(ventStartBay, 1, 8);
            louvreWidth = Mathf.Max(0.05f, louvreWidth);
            louvreHeight = Mathf.Max(0.05f, louvreHeight);
            louvreCentreHeight = Mathf.Max(0f, louvreCentreHeight);
            orientationDeg = Mathf.Repeat(orientationDeg, 360f);
            potCount = Mathf.Clamp(potCount, 0, 60);
            potRows = Mathf.Clamp(potRows, 1, 6);
            potDiameter = Mathf.Max(0.05f, potDiameter);
            potHeight = Mathf.Max(0.05f, potHeight);
            potSpacing = Mathf.Max(0.05f, potSpacing);
            potRowSpacing = Mathf.Max(0.05f, potRowSpacing);
            plantHeight = Mathf.Max(0f, plantHeight);
            heaterSize = AtLeast(heaterSize, 0.02f);
            ledCount = Mathf.Clamp(ledCount, 0, 8);
            ledLength = Mathf.Max(0.1f, ledLength);
            ledHeight = Mathf.Max(0.1f, ledHeight);
            controlBoxSize = AtLeast(controlBoxSize, 0.02f);
            controlBoxGap = Mathf.Max(0f, controlBoxGap);
            siteLatitudeDeg = Mathf.Clamp(siteLatitudeDeg, -90f, 90f);
            siteLongitudeDeg = Mathf.Clamp(siteLongitudeDeg, -180f, 180f);
            sunMonth = Mathf.Clamp(sunMonth, 1, 12);
            sunDay = Mathf.Clamp(sunDay, 1, System.DateTime.DaysInMonth(2025, sunMonth));
            sunHourUtc = Mathf.Clamp(sunHourUtc, 0f, 24f);
            waterTankSize = AtLeast(waterTankSize, 0.02f);
            hedgeDistance = Mathf.Max(0f, hedgeDistance);
            hedgeHeight = Mathf.Max(0.1f, hedgeHeight);
            hedgeLength = Mathf.Max(0.1f, hedgeLength);
            hedgeDepth = Mathf.Max(0.1f, hedgeDepth);
            staleAfterSeconds = Mathf.Max(5f, staleAfterSeconds);

            if (sensors == null) sensors = new List<SensorSpec>();
            for (int i = sensors.Count - 1; i >= 0; i--)
                if (sensors[i] == null) sensors.RemoveAt(i);
            for (int i = 0; i < sensors.Count; i++)
            {
                // An empty id is allowed while typing; the builder skips sensors without an id.
                if (sensors[i].id == null) sensors[i].id = "";
                if (sensors[i].label == null) sensors[i].label = "";
            }

            if (actuators == null) actuators = new List<ActuatorSpec>();
            for (int i = actuators.Count - 1; i >= 0; i--)
                if (actuators[i] == null) actuators.RemoveAt(i);
            for (int i = 0; i < actuators.Count; i++)
            {
                if (actuators[i].deviceName == null) actuators[i].deviceName = "";
                if (actuators[i].componentId == null) actuators[i].componentId = "";
            }
        }

        static Vector3 AtLeast(Vector3 v, float min)
        {
            return new Vector3(Mathf.Max(min, v.x), Mathf.Max(min, v.y), Mathf.Max(min, v.z));
        }

        /// <summary>Clamp, then tell the builders to rebuild.</summary>
        public void NotifyChanged()
        {
            Sanitize();
            Action handler = Changed;
            if (handler != null) handler();
        }

        void OnValidate()
        {
            NotifyChanged();
        }

        /// <summary>Readable JSON of every public field (Newtonsoft; enums as names, vectors as {x,y,z}).</summary>
        public string ToJson()
        {
            return ConfigJson.Serialize(this);
        }

        /// <summary>Overwrite the fields present in the JSON; missing fields keep their values, unknown fields are ignored.</summary>
        public void FromJson(string json)
        {
            ConfigJson.Populate(json, this);
            NotifyChanged();
        }
    }
}
