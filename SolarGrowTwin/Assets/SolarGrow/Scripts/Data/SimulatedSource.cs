using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SolarGrow
{
    /// <summary>
    /// Synthetic frames with the same schema as twin/state, so the model runs without hardware:
    /// sine-wave climate per house, every sensor of the config (unknown kinds get a 'value'), devices that
    /// toggle by simple rules, a door that opens briefly, and a fake 2 h forecast. Manual commands from the
    /// panel override a device for 120 s. Runs on the main thread (Tick).
    /// </summary>
    public sealed class SimulatedSource : ITelemetrySource, ICommandSink
    {
        public event Action<TwinStateFrame> OnState;
        public event Action<string> OnStatus;

        /// <summary>Seconds between frames.</summary>
        public float IntervalSeconds { get; set; }
        /// <summary>Period of the simulated temperature wave (s).</summary>
        public double PeriodSeconds { get; set; }
        /// <summary>How long a manual command overrides the simulated rule (s).</summary>
        public double OverrideSeconds { get; set; }

        sealed class HouseSim
        {
            public bool heaterOn;
            public double energyKwh = 12.0;
            public double lastUnix;
            public int seq;
            public readonly Dictionary<string, string> manual = new Dictionary<string, string>();
            public readonly Dictionary<string, double> manualUntil = new Dictionary<string, double>();
        }

        readonly GreenhouseConfig config;
        readonly List<string> houses = new List<string>();
        readonly Dictionary<string, HouseSim> sims = new Dictionary<string, HouseSim>();
        bool connected;
        float nextAt;

        public SimulatedSource(GreenhouseConfig config, IList<string> houseIds)
        {
            this.config = config;
            if (houseIds != null)
                for (int i = 0; i < houseIds.Count; i++)
                    if (!string.IsNullOrEmpty(houseIds[i])) houses.Add(houseIds[i]);
            if (houses.Count == 0) { houses.Add("A"); houses.Add("B"); }
            IntervalSeconds = 2f;
            PeriodSeconds = 900.0;
            OverrideSeconds = 120.0;
        }

        public bool Connected { get { return connected; } }

        public void Connect()
        {
            connected = true;
            nextAt = 0f;
            RaiseStatus("Simulated source running: houses " + string.Join(", ", houses.ToArray()) + ", a frame every " + IntervalSeconds + " s");
        }

        public void Disconnect()
        {
            if (connected) RaiseStatus("Simulated source stopped");
            connected = false;
        }

        public void Tick()
        {
            if (!connected) return;
            float now = Time.realtimeSinceStartup;
            if (now < nextAt) return;
            nextAt = now + Mathf.Max(0.1f, IntervalSeconds);
            Action<TwinStateFrame> handler = OnState;
            for (int i = 0; i < houses.Count; i++)
            {
                JObject frame = BuildFrame(houses[i], i);
                if (handler != null) handler(new TwinStateFrame(frame));
            }
        }

        public bool SendCommand(string greenhouse, string device, string value)
        {
            if (!connected || string.IsNullOrEmpty(device)) return false;
            HouseSim sim = Sim(string.IsNullOrEmpty(greenhouse) ? houses[0] : greenhouse);
            sim.manual[device] = value;
            sim.manualUntil[device] = TwinJson.NowUnixSeconds() + OverrideSeconds;
            nextAt = 0f;
            RaiseStatus("Simulated: " + device + " = " + value + " on " + greenhouse + " for " + OverrideSeconds + " s");
            return true;
        }

        void RaiseStatus(string text)
        {
            Action<string> handler = OnStatus;
            if (handler != null) handler(text);
        }

        HouseSim Sim(string house)
        {
            HouseSim s;
            if (!sims.TryGetValue(house, out s))
            {
                s = new HouseSim();
                sims[house] = s;
            }
            return s;
        }

        double Temperature(double t, int k)
        {
            return 18.5 + 3.5 * Math.Sin(2.0 * Math.PI * t / PeriodSeconds + k * 0.9) + 0.4 * k;
        }

        double Humidity(double t, int k)
        {
            return 72.0 - 9.0 * Math.Sin(2.0 * Math.PI * t / PeriodSeconds + k * 0.9 + 0.4);
        }

        static double Vpd(double tempC, double rh)
        {
            double sat = 610.78 * Math.Exp(17.2694 * tempC / (tempC + 238.3));
            return (1.0 - rh / 100.0) * sat / 1000.0;
        }

        bool Manual(HouseSim sim, string device, double now, out string value)
        {
            value = null;
            double until;
            if (!sim.manualUntil.TryGetValue(device, out until)) return false;
            if (now > until)
            {
                sim.manualUntil.Remove(device);
                sim.manual.Remove(device);
                return false;
            }
            return sim.manual.TryGetValue(device, out value);
        }

        JObject BuildFrame(string house, int k)
        {
            HouseSim sim = Sim(house);
            DateTimeOffset nowTime = DateTimeOffset.UtcNow;
            double t = TwinJson.ToUnixSeconds(nowTime);
            double dt = sim.lastUnix > 0 ? Math.Max(0.0, t - sim.lastUnix) : 0.0;
            sim.lastUnix = t;
            sim.seq++;
            string iso = TwinJson.ToIso(nowTime);
            string node = "sim-node-" + house;

            double temp = Temperature(t, k);
            double rh = Humidity(t, k);
            double co2 = 620.0 - 180.0 * Math.Sin(2.0 * Math.PI * t / PeriodSeconds + k * 0.9 - 0.3);
            double par = Math.Max(0.0, 520.0 * Math.Sin(2.0 * Math.PI * t / (PeriodSeconds * 1.7) + k * 0.5));

            // Devices: simple rules, overridden by manual commands.
            string manual;
            if (temp < 17.0) sim.heaterOn = true;
            else if (temp > 18.5) sim.heaterOn = false;
            JToken heater = sim.heaterOn ? "on" : "off";
            if (Manual(sim, "heater", t, out manual)) heater = manual;
            double ventFraction = Math.Max(0.0, Math.Min(1.0, (temp - 20.5) / 1.5));
            JToken vent = ventFraction <= 0.0 ? (JToken)"closed" : (JToken)Math.Round(ventFraction, 2);
            if (Manual(sim, "vent", t, out manual)) vent = manual;
            JToken led = par < 80.0 ? "on" : "off";
            if (Manual(sim, "led", t, out manual)) led = manual;
            JToken pump = (t % 120.0) < 10.0 ? "on" : "off";
            if (Manual(sim, "pump", t, out manual)) pump = manual;
            string door = (k == 0 && (t % 90.0) < 8.0) ? "open" : "closed";

            float heaterLevel, ledLevel, pumpLevel;
            TwinJson.TryDeviceLevel(heater, out heaterLevel);
            TwinJson.TryDeviceLevel(led, out ledLevel);
            TwinJson.TryDeviceLevel(pump, out pumpLevel);
            double power = 6.0 + 2000.0 * heaterLevel + 200.0 * ledLevel + 35.0 * pumpLevel;
            sim.energyKwh += power * dt / 3.6e6;

            JObject devices = new JObject();
            devices["heater"] = heater;
            devices["vent"] = vent;
            devices["led"] = led;
            devices["pump"] = pump;
            JObject contacts = new JObject();
            contacts["door"] = door;

            // Sensors from the config, so new sensors show up in the simulation too.
            JObject sensors = new JObject();
            JArray sensorIds = new JArray();
            double sumT = 0, sumRh = 0, minT = double.MaxValue, maxT = double.MinValue;
            int climateCount = 0;
            List<SensorSpec> specs = config != null ? config.sensors : null;
            if (specs != null)
            {
                for (int i = 0; i < specs.Count; i++)
                {
                    SensorSpec s = specs[i];
                    if (s == null || string.IsNullOrEmpty(s.id)) continue;
                    JObject e = new JObject();
                    e["node_id"] = node;
                    e["observed_at_utc"] = iso;
                    e["seq"] = sim.seq;
                    e["quality"] = new JArray("valid");
                    double st = temp, srh = rh;
                    switch (s.kind)
                    {
                        case SensorKind.AirTRHCO2:
                            e["temperature_c"] = Math.Round(st, 2);
                            e["relative_humidity_percent"] = Math.Round(srh, 1);
                            e["air_vpd_kpa"] = Math.Round(Vpd(st, srh), 3);
                            e["co2_ppm"] = Math.Round(co2, 0);
                            break;
                        case SensorKind.TRH:
                            st = temp + 0.9 + 0.3 * i;
                            srh = rh - 3.0;
                            e["temperature_c"] = Math.Round(st, 2);
                            e["relative_humidity_percent"] = Math.Round(srh, 1);
                            e["air_vpd_kpa"] = Math.Round(Vpd(st, srh), 3);
                            break;
                        case SensorKind.PAR:
                            e["par_umol_m2_s"] = Math.Round(par, 0);
                            break;
                        case SensorKind.EnergyMeter:
                            e["energy_kwh"] = Math.Round(sim.energyKwh, 4);
                            e["power_w"] = Math.Round(power, 0);
                            break;
                        default:
                            e["value"] = Math.Round(Math.Sin(t / 60.0 + i), 3);
                            break;
                    }
                    if (s.kind == SensorKind.AirTRHCO2 || s.kind == SensorKind.TRH)
                    {
                        sumT += st; sumRh += srh; climateCount++;
                        minT = Math.Min(minT, st); maxT = Math.Max(maxT, st);
                    }
                    sensors[s.id] = e;
                    sensorIds.Add(s.id);
                }
            }

            JObject mean = new JObject();
            mean["sensor_count"] = climateCount;
            if (climateCount > 0)
            {
                mean["observed_at_utc"] = iso;
                mean["temperature_c"] = Math.Round(sumT / climateCount, 3);
                mean["relative_humidity_percent"] = Math.Round(sumRh / climateCount, 2);
                mean["temperature_spread_c"] = Math.Round(maxT - minT, 3);
            }

            JObject nodeEntry = new JObject();
            nodeEntry["status"] = "online";
            nodeEntry["seq"] = sim.seq;
            nodeEntry["observed_at_utc"] = iso;
            nodeEntry["reported_actuators"] = devices.DeepClone();
            nodeEntry["sensors"] = sensorIds;
            JObject nodes = new JObject();
            nodes[node] = nodeEntry;

            JObject frame = new JObject();
            frame["type"] = "state";
            frame["greenhouse"] = house;
            frame["updated_at_utc"] = iso;
            frame["nodes"] = nodes;
            frame["sensors"] = sensors;
            frame["mean"] = mean;
            frame["devices"] = devices;
            frame["contacts"] = contacts;
            frame["twin"] = BuildTwin(t, k, iso, devices);
            return frame;
        }

        /// <summary>Same shape as twin_core.frame(): estimated state plus forecast.points[{at_utc, temperature_c, relative_humidity_percent}].</summary>
        JObject BuildTwin(double t, int k, string iso, JObject devices)
        {
            JArray points = new JArray();
            for (int i = 1; i <= 24; i++)
            {
                double ti = t + i * 300.0;
                double bias = 0.04 * i * Math.Sin(i * 0.7 + k);
                double ft = Temperature(ti, k) + bias;
                double frh = Humidity(ti, k) - bias * 2.0;
                JObject p = new JObject();
                p["at_utc"] = TwinJson.ToIso(TwinJson.FromUnixSeconds(ti));
                p["temperature_c"] = Math.Round(ft, 3);
                p["relative_humidity_percent"] = Math.Round(frh, 2);
                p["air_vpd_kpa"] = Math.Round(Vpd(ft, frh), 3);
                points.Add(p);
            }
            JObject forecast = new JObject();
            forecast["issued_at_utc"] = iso;
            forecast["horizon_seconds"] = 7200;
            forecast["step_seconds"] = 300;
            forecast["plan"] = "hold devices";
            forecast["points"] = points;

            double et = Temperature(t, k) + 0.1;
            double erh = Humidity(t, k);
            JObject estimated = new JObject();
            estimated["temperature_c"] = Math.Round(et, 3);
            estimated["relative_humidity_percent"] = Math.Round(erh, 2);
            estimated["air_vpd_kpa"] = Math.Round(Vpd(et, erh), 3);
            estimated["mass_temperature_c"] = Math.Round(Temperature(t - 1800.0, k), 3);
            estimated["mass_source"] = "model";

            JObject twin = new JObject();
            twin["as_of_utc"] = iso;
            twin["estimated"] = estimated;
            twin["devices_in_effect"] = devices.DeepClone();
            twin["last_innovation"] = JValue.CreateNull();
            twin["forecast"] = forecast;
            twin["assumptions"] = new JArray("simulated in Unity: sine waves, not a model");
            return twin;
        }
    }
}
