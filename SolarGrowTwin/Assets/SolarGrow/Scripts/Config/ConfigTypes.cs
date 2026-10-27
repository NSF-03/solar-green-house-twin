using System;
using UnityEngine;

namespace SolarGrow
{
    /// <summary>Gable end of the house. Names assume heading 0, where local -X points west.</summary>
    public enum GableEnd { West, East }

    /// <summary>Roof slope. South = local -Z side, North = local +Z side (heading 0). Alternate starts on the south slope.</summary>
    public enum RoofSlope { South, North, Alternate, AlternateNorthFirst }

    /// <summary>Where the roof vents go along the ridge: spread over the bays, or bay by bay from the door gable.</summary>
    public enum VentPlacement { Spread, DoorEnd }

    /// <summary>Wall of the house (heading 0 names). Door = the sliding door panel itself.</summary>
    public enum WallSide { WestGable, EastGable, SouthWall, NorthWall, Door }

    /// <summary>What a sensor measures; decides its marker shape and colour.</summary>
    public enum SensorKind { AirTRHCO2, TRH, PAR, EnergyMeter, Other }

    /// <summary>What an actuator does; decides which manual buttons the panel shows.</summary>
    public enum ActuatorKind { Heater, Led, Vent, Pump, Door, Other }

    /// <summary>One sensor of the twin. The id must match the sensor id in the twin/state frames.</summary>
    [Serializable]
    public class SensorSpec
    {
        [Tooltip("Sensor id exactly as it appears under 'sensors' in the twin/state frame.")]
        public string id = "sensor";

        [Tooltip("What the sensor measures (marker shape and colour).")]
        public SensorKind kind = SensorKind.Other;

        [Tooltip("Position in metres from the floor centre (x along ridge, y up, z towards north wall); EnergyMeter: offset from the meter box.")]
        public Vector3 position = new Vector3(0f, 1f, 0f);

        [Tooltip("Text shown next to the marker.")]
        public string label = "";

        public SensorSpec() { }

        public SensorSpec(string id, SensorKind kind, Vector3 position, string label)
        {
            this.id = id;
            this.kind = kind;
            this.position = position;
            this.label = label;
        }
    }

    /// <summary>Maps a device name in the frame's 'devices' to the model part(s) that show it.</summary>
    [Serializable]
    public class ActuatorSpec
    {
        [Tooltip("Device name exactly as it appears under 'devices' in the twin/state frame, e.g. heater, led, vent, pump.")]
        public string deviceName = "device";

        [Tooltip("What the device does (decides the manual buttons: On/Off or Open/Close).")]
        public ActuatorKind kind = ActuatorKind.Other;

        [Tooltip("Model part id that shows the state; also matches every part whose id starts with this id plus '-' (led matches led-1, led-2).")]
        public string componentId = "";

        public ActuatorSpec() { }

        public ActuatorSpec(string deviceName, ActuatorKind kind, string componentId)
        {
            this.deviceName = deviceName;
            this.kind = kind;
            this.componentId = componentId;
        }
    }
}
