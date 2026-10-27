using System.Collections.Generic;
using UnityEngine;

namespace SolarGrow
{
    /// <summary>Geometry of the glasshouse. House axes: x along the ridge, y up, z towards the north wall.</summary>
    public partial class GreenhouseBuilder
    {
        static readonly Color AluColor = new Color(0.78f, 0.80f, 0.82f, 1f);
        static readonly Color GlassTint = new Color(0.70f, 0.88f, 1f, 1f);
        static readonly Color SlabColor = new Color(0.62f, 0.62f, 0.60f, 1f);
        static readonly Color GroundColor = new Color(0.40f, 0.50f, 0.32f, 1f);
        static readonly Color PotColor = new Color(0.16f, 0.16f, 0.16f, 1f);
        static readonly Color PlantColor = new Color(0.25f, 0.55f, 0.20f, 1f);
        static readonly Color StemColor = new Color(0.33f, 0.52f, 0.22f, 1f);
        static readonly Color CaneColor = new Color(0.72f, 0.60f, 0.38f, 1f);
        static readonly Color RipeFruitColor = new Color(0.85f, 0.16f, 0.10f, 1f);
        static readonly Color UnripeFruitColor = new Color(0.50f, 0.72f, 0.28f, 1f);
        static readonly Color HeaterColor = new Color(0.35f, 0.35f, 0.38f, 1f);
        static readonly Color LedColor = new Color(0.75f, 0.75f, 0.80f, 1f);
        static readonly Color BoxColor = new Color(0.92f, 0.92f, 0.90f, 1f);
        static readonly Color MeterColor = new Color(0.25f, 0.30f, 0.45f, 1f);
        static readonly Color HedgeColor = new Color(0.14f, 0.34f, 0.14f, 1f);
        static readonly Color TankColor = new Color(0.20f, 0.35f, 0.60f, 1f);
        static readonly Color PumpColor = new Color(0.20f, 0.30f, 0.45f, 1f);
        static readonly Color ShieldColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        static readonly Color ActuatorAccent = new Color(0.95f, 0.55f, 0.10f, 1f);
        static readonly Color CompassColor = new Color(0.80f, 0.15f, 0.15f, 1f);
        static readonly Color HandleColor = new Color(0.20f, 0.20f, 0.22f, 1f);

        struct Dims
        {
            public float L, W, E, R, hl, hw, bar, glass, rise, S, sinT, cosT;

            /// <summary>Height of the roof line above the slab at cross position z.</summary>
            public float RoofY(float z)
            {
                float a = Mathf.Min(Mathf.Abs(z), hw);
                return R - a / hw * rise;
            }

            /// <summary>Point on a roof slope: side -1 south / +1 north, u along the ridge, v down the slope from the ridge.</summary>
            public Vector3 RoofPoint(int side, float u, float v)
            {
                return new Vector3(u, R - v * sinT, side * v * cosT);
            }
        }

        struct DoorSpan
        {
            public float sign, x, width, centre, left, right, height;
        }

        Dims MakeDims()
        {
            Dims g;
            g.L = config.length;
            g.W = config.width;
            g.E = config.eavesHeight;
            g.R = Mathf.Max(config.ridgeHeight, g.E + 0.02f);
            g.hl = g.L * 0.5f;
            g.hw = g.W * 0.5f;
            g.bar = config.frameBarWidth;
            g.glass = config.glassThickness;
            g.rise = g.R - g.E;
            g.S = Mathf.Sqrt(g.hw * g.hw + g.rise * g.rise);
            g.sinT = g.rise / g.S;
            g.cosT = g.hw / g.S;
            return g;
        }

        DoorSpan GetDoorSpan(Dims g)
        {
            DoorSpan d;
            d.sign = config.doorGable == GableEnd.West ? -1f : 1f;
            d.x = d.sign * g.hl;
            d.width = Mathf.Clamp(config.doorWidth, 0.2f, Mathf.Max(0.2f, g.W - 4f * g.bar));
            float limit = Mathf.Max(0f, g.hw - d.width * 0.5f - g.bar);
            d.centre = Mathf.Clamp(config.doorOffset, -limit, limit);
            d.left = d.centre - d.width * 0.5f;
            d.right = d.centre + d.width * 0.5f;
            float maxH = Mathf.Min(g.RoofY(d.left), g.RoofY(d.right)) - 1.5f * g.bar;
            d.height = Mathf.Clamp(config.doorHeight, 0.3f, Mathf.Max(0.3f, maxH));
            return d;
        }

        bool IsDoorGable(float sx)
        {
            return (config.doorGable == GableEnd.West) == (sx < 0f);
        }

        int VentSide(int i)
        {
            switch (config.ventSlope)
            {
                case RoofSlope.South: return -1;
                case RoofSlope.North: return 1;
                case RoofSlope.AlternateNorthFirst: return i % 2 == 0 ? 1 : -1;
                default: return i % 2 == 0 ? -1 : 1;
            }
        }

        /// <summary>Number of bays between roof rafters (and wall glazing bars) along the ridge.</summary>
        int RafterBays(Dims g)
        {
            return Mathf.Max(1, Mathf.RoundToInt(g.L / config.glazingBarSpacing));
        }

        /// <summary>Vent centre along the ridge, always mid-bay between two rafters: from ventStartBay on, counted from the door gable (DoorEnd), or spread over the bays.</summary>
        float VentCenterX(Dims g, int i, int n)
        {
            int bays = RafterBays(g);
            float bw = g.L / bays;
            if (config.ventPlacement == VentPlacement.DoorEnd)
            {
                float sign = config.doorGable == GableEnd.West ? -1f : 1f;
                return sign * (g.hl - (Mathf.Clamp(config.ventStartBay - 1 + i, 0, bays - 1) + 0.5f) * bw);
            }
            int bay = Mathf.Min(bays - 1, Mathf.FloorToInt((i + 0.5f) * bays / Mathf.Max(1, n)));
            return -g.hl + (bay + 0.5f) * bw;
        }

        static Vector3[] RoofRect(Dims g, int side, float u0, float u1, float v0, float v1)
        {
            return new Vector3[] { g.RoofPoint(side, u0, v0), g.RoofPoint(side, u1, v0), g.RoofPoint(side, u1, v1), g.RoofPoint(side, u0, v1) };
        }

        void BuildAll()
        {
            Dims g = MakeDims();
            Material alu = TwinUtil.GetMaterial(AluColor);
            Material glass = TwinUtil.GetGlassMaterial(new Color(GlassTint.r, GlassTint.g, GlassTint.b, config.glassOpacity));
            BuildSlab(g);
            BuildFrame(g, alu);
            BuildWallsAndGables(g, glass);
            BuildRoof(g, glass, alu);
            Transform door = BuildDoor(g, glass, alu);
            if (config.louvreEnabled) BuildLouvre(g, alu, door);
            BuildPots();
            BuildHeater();
            BuildLeds(g, alu);
            Vector3 meter = BuildControlBox(g);
            if (config.waterTankEnabled) BuildWaterTank();
            BuildSensors(meter);
            BuildHouseLabel(g);
            if (includeSurroundings)
            {
                BuildGround();
                if (config.hedgeEnabled) BuildHedge(g);
                BuildCompass(g);
                ApplySun();
            }
        }

        /// <summary>Points the scene's directional light along the sun ray for the configured site, date and time.</summary>
        void ApplySun()
        {
            Light sun = RenderSettings.sun;
            if (sun == null || sun.type != LightType.Directional)
            {
                sun = null;
                foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { sun = l; break; }
            }
            if (sun == null) return;
            int day = Mathf.Clamp(config.sunDay, 1, System.DateTime.DaysInMonth(2025, config.sunMonth));
            int dayOfYear = new System.DateTime(2025, config.sunMonth, day).DayOfYear;
            Vector3 toSun = SolarPosition.Direction(config.siteLatitudeDeg, config.siteLongitudeDeg, dayOfYear, config.sunHourUtc);
            // Below the horizon there are no shadows to show; keep a low grazing light instead of lighting from underneath.
            if (toSun.y < 0.02f) toSun = new Vector3(toSun.x, 0.02f, toSun.z).normalized;
            sun.transform.rotation = Quaternion.LookRotation(-toSun, Vector3.up);
        }

        void BuildSlab(Dims g)
        {
            Transform grp = Group("slab", builtRoot);
            float m = config.slabMargin, t = config.slabThickness;
            Part(PrimitiveType.Cube, "slab", TwinKind.Slab, grp, new Vector3(0f, -t * 0.5f, 0f),
                 new Vector3(g.L + 2f * m, t, g.W + 2f * m), TwinUtil.GetMaterial(SlabColor));
        }

        void BuildFrame(Dims g, Material alu)
        {
            Transform f = Group("frame", builtRoot);
            float b = g.bar, hl = g.hl, hw = g.hw, E = g.E, R = g.R, y0 = b * 0.5f;

            Bar("frame-sill-south", f, new Vector3(-hl, y0, -hw), new Vector3(hl, y0, -hw), b, alu);
            Bar("frame-sill-north", f, new Vector3(-hl, y0, hw), new Vector3(hl, y0, hw), b, alu);
            Bar("frame-sill-west", f, new Vector3(-hl, y0, -hw), new Vector3(-hl, y0, hw), b, alu);
            Bar("frame-sill-east", f, new Vector3(hl, y0, -hw), new Vector3(hl, y0, hw), b, alu);
            for (int ix = -1; ix <= 1; ix += 2)
                for (int iz = -1; iz <= 1; iz += 2)
                    Bar("frame-corner", f, new Vector3(ix * hl, 0f, iz * hw), new Vector3(ix * hl, E, iz * hw), b, alu);
            Bar("frame-eaves-south", f, new Vector3(-hl, E, -hw), new Vector3(hl, E, -hw), b, alu);
            Bar("frame-eaves-north", f, new Vector3(-hl, E, hw), new Vector3(hl, E, hw), b, alu);
            Bar("frame-ridge", f, new Vector3(-hl, R, 0f), new Vector3(hl, R, 0f), b, alu);
            for (int ix = -1; ix <= 1; ix += 2)
            {
                Bar("frame-gable-rafter", f, new Vector3(ix * hl, E, -hw), new Vector3(ix * hl, R, 0f), b, alu);
                Bar("frame-gable-rafter", f, new Vector3(ix * hl, E, hw), new Vector3(ix * hl, R, 0f), b, alu);
            }

            // Glazing bars on the long walls and the matching roof rafters.
            int n = RafterBays(g);
            for (int k = 1; k < n; k++)
            {
                float x = -hl + k * g.L / n;
                for (int iz = -1; iz <= 1; iz += 2)
                {
                    Bar("frame-wall-bar", f, new Vector3(x, 0f, iz * hw), new Vector3(x, E, iz * hw), b, alu);
                    Bar("frame-rafter", f, new Vector3(x, E, iz * hw), new Vector3(x, R, 0f), b, alu);
                }
            }

            // Gable posts, eaves-height ties and the door frame.
            DoorSpan d = GetDoorSpan(g);
            for (int ix = -1; ix <= 1; ix += 2)
            {
                float x = ix * hl;
                bool doorHere = IsDoorGable(ix);
                float[] zs = { -hw * 0.5f, hw * 0.5f };
                for (int k = 0; k < zs.Length; k++)
                {
                    float z = zs[k];
                    if (doorHere && z > d.left - b && z < d.right + b) continue;
                    Bar("frame-gable-post", f, new Vector3(x, 0f, z), new Vector3(x, g.RoofY(z), z), b, alu);
                }
                if (doorHere && d.height > E - b)
                {
                    Bar("frame-gable-tie", f, new Vector3(x, E, -hw), new Vector3(x, E, d.left), b, alu);
                    Bar("frame-gable-tie", f, new Vector3(x, E, d.right), new Vector3(x, E, hw), b, alu);
                }
                else
                {
                    Bar("frame-gable-tie", f, new Vector3(x, E, -hw), new Vector3(x, E, hw), b, alu);
                }
                if (doorHere)
                {
                    Bar("frame-door-post", f, new Vector3(x, 0f, d.left), new Vector3(x, d.height, d.left), b, alu);
                    Bar("frame-door-post", f, new Vector3(x, 0f, d.right), new Vector3(x, d.height, d.right), b, alu);
                    Bar("frame-door-header", f, new Vector3(x, d.height, d.left), new Vector3(x, d.height, d.right), b, alu);
                }
            }
        }

        void BuildWallsAndGables(Dims g, Material glass)
        {
            Transform grp = Group("glass", builtRoot);
            List<Vector3[]> polys = new List<Vector3[]>();
            polys.Add(new Vector3[] { new Vector3(-g.hl, 0f, -g.hw), new Vector3(g.hl, 0f, -g.hw), new Vector3(g.hl, g.E, -g.hw), new Vector3(-g.hl, g.E, -g.hw) });
            Glass("glass-wall-south", grp, polys, Vector3.back, g.glass, glass);

            polys = new List<Vector3[]>();
            polys.Add(new Vector3[] { new Vector3(-g.hl, 0f, g.hw), new Vector3(g.hl, 0f, g.hw), new Vector3(g.hl, g.E, g.hw), new Vector3(-g.hl, g.E, g.hw) });
            Glass("glass-wall-north", grp, polys, Vector3.forward, g.glass, glass);

            DoorSpan d = GetDoorSpan(g);
            for (int ix = -1; ix <= 1; ix += 2)
            {
                float x = ix * g.hl;
                polys = new List<Vector3[]>();
                if (IsDoorGable(ix))
                {
                    AddGableStrip(polys, g, x, -g.hw, d.left, 0f);
                    AddGableStrip(polys, g, x, d.left, d.right, d.height);
                    AddGableStrip(polys, g, x, d.right, g.hw, 0f);
                }
                else
                {
                    AddGableStrip(polys, g, x, -g.hw, g.hw, 0f);
                }
                Glass(ix < 0 ? "glass-gable-west" : "glass-gable-east", grp, polys, new Vector3(ix, 0f, 0f), g.glass, glass);
            }
        }

        /// <summary>Gable glass between z = a and z = b, from height yb up to the roof line (convex).</summary>
        static void AddGableStrip(List<Vector3[]> polys, Dims g, float x, float a, float b, float yb)
        {
            if (b - a < 1e-3f) return;
            if (yb >= g.RoofY(a) - 1e-3f || yb >= g.RoofY(b) - 1e-3f) return;
            List<Vector3> pts = new List<Vector3>(5);
            pts.Add(new Vector3(x, yb, a));
            pts.Add(new Vector3(x, yb, b));
            pts.Add(new Vector3(x, g.RoofY(b), b));
            if (a < 0f && b > 0f) pts.Add(new Vector3(x, g.R, 0f));
            pts.Add(new Vector3(x, g.RoofY(a), a));
            polys.Add(pts.ToArray());
        }

        void BuildRoof(Dims g, Material glass, Material alu)
        {
            Transform grp = Group("roof", builtRoot);
            int n = config.ventCount;
            // A vent fills the bay between two rafters, clear of both.
            float vw = n > 0 ? Mathf.Clamp(config.ventWidth, 0.1f, Mathf.Max(0.1f, g.L / RafterBays(g) - 2f * g.bar)) : 0f;
            float vl = Mathf.Clamp(config.ventLength, 0.1f, Mathf.Max(0.1f, g.S - 2f * g.bar));
            for (int si = 0; si < 2; si++)
            {
                int side = si == 0 ? -1 : 1;
                List<Vector3[]> polys = new List<Vector3[]>();
                List<float> centres = new List<float>();
                for (int i = 0; i < n; i++)
                    if (VentSide(i) == side) centres.Add(VentCenterX(g, i, n));
                centres.Sort();
                float cursor = -g.hl;
                foreach (float xc in centres)
                {
                    float x0 = xc - vw * 0.5f, x1 = xc + vw * 0.5f;
                    if (x0 > cursor + 1e-3f) polys.Add(RoofRect(g, side, cursor, x0, 0f, g.S));
                    if (vl < g.S - 1e-3f) polys.Add(RoofRect(g, side, x0, x1, vl, g.S));
                    cursor = x1;
                }
                if (cursor < g.hl - 1e-3f) polys.Add(RoofRect(g, side, cursor, g.hl, 0f, g.S));
                Glass(side < 0 ? "glass-roof-south" : "glass-roof-north", grp, polys,
                      new Vector3(0f, g.cosT, side * g.sinT), g.glass, glass);
            }
            for (int i = 0; i < n; i++) BuildVent(g, i, n, vw, vl, grp, glass, alu);
        }

        /// <summary>Vent root on the ridge (VentVisual) with a child "hinge" that turns; pane and frame hang from the hinge.</summary>
        void BuildVent(Dims g, int i, int n, float vw, float vl, Transform parent, Material glass, Material alu)
        {
            int side = VentSide(i);
            string vid = "vent-" + (i + 1);
            GameObject root = new GameObject(vid);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(VentCenterX(g, i, n), g.R, 0f);
            VentVisual vv = Register<VentVisual>(root, vid, TwinKind.Vent);
            GameObject hingeGo = new GameObject("hinge");
            hingeGo.transform.SetParent(root.transform, false);
            vv.hinge = hingeGo.transform;
            vv.hingeAxis = side < 0 ? Vector3.right : Vector3.left;
            vv.maxAngleDeg = config.ventMaxAngleDeg;
            vv.hasActuator = (i + 1) == config.actuatedVent;

            Vector3 nrm = new Vector3(0f, g.cosT, side * g.sinT);
            float lift = g.bar * 0.8f;
            float h = vw * 0.5f;
            float top = 0.02f;
            Vector3 p00 = VentPoint(g, side, nrm, lift, -h, top);
            Vector3 p10 = VentPoint(g, side, nrm, lift, h, top);
            Vector3 p11 = VentPoint(g, side, nrm, lift, h, vl);
            Vector3 p01 = VentPoint(g, side, nrm, lift, -h, vl);
            List<Vector3[]> polys = new List<Vector3[]>();
            polys.Add(new Vector3[] { p00, p10, p11, p01 });
            Glass(vid + "-pane", hingeGo.transform, polys, nrm, g.glass, glass);

            Material frameMat = vv.hasActuator ? TwinUtil.GetMaterial(ActuatorAccent) : alu;
            float fb = g.bar * 0.7f;
            Bar(vid + "-frame", hingeGo.transform, p00, p10, fb, frameMat);
            Bar(vid + "-frame", hingeGo.transform, p01, p11, fb, frameMat);
            Bar(vid + "-frame", hingeGo.transform, p00, p01, fb, frameMat);
            Bar(vid + "-frame", hingeGo.transform, p10, p11, fb, frameMat);
        }

        static Vector3 VentPoint(Dims g, int side, Vector3 nrm, float lift, float u, float v)
        {
            return new Vector3(u, -v * g.sinT, side * v * g.cosT) + nrm * lift;
        }

        Transform BuildDoor(Dims g, Material glass, Material alu)
        {
            DoorSpan d = GetDoorSpan(g);
            float standOff = g.bar + 0.015f;
            Vector3 closed = new Vector3(d.x + d.sign * standOff, 0f, d.centre);
            float slide = d.centre > 0.001f ? -1f : 1f;
            Vector3 opened = closed + new Vector3(0f, 0f, slide * d.width * 0.92f);

            GameObject door = new GameObject("door");
            door.transform.SetParent(builtRoot, false);
            door.transform.localPosition = closed;
            DoorVisual dv = Register<DoorVisual>(door, "door", TwinKind.Door);
            dv.closedLocalPosition = closed;
            dv.openLocalPosition = opened;

            float w2 = d.width * 0.5f, h = d.height, fb = g.bar * 0.8f;
            List<Vector3[]> polys = new List<Vector3[]>();
            polys.Add(new Vector3[] { new Vector3(0f, 0.03f, -w2), new Vector3(0f, 0.03f, w2), new Vector3(0f, h, w2), new Vector3(0f, h, -w2) });
            Glass("door-pane", door.transform, polys, new Vector3(d.sign, 0f, 0f), g.glass, glass);
            Bar("door-frame", door.transform, new Vector3(0f, fb * 0.5f, -w2), new Vector3(0f, fb * 0.5f, w2), fb, alu);
            Bar("door-frame", door.transform, new Vector3(0f, h, -w2), new Vector3(0f, h, w2), fb, alu);
            Bar("door-frame", door.transform, new Vector3(0f, 0f, -w2), new Vector3(0f, h, -w2), fb, alu);
            Bar("door-frame", door.transform, new Vector3(0f, 0f, w2), new Vector3(0f, h, w2), fb, alu);
            Part(PrimitiveType.Cube, "door-handle", TwinKind.Door, door.transform,
                 new Vector3(d.sign * 0.02f, Mathf.Min(1.0f, h * 0.55f), -slide * (w2 - 0.06f)), new Vector3(0.02f, 0.12f, 0.02f),
                 TwinUtil.GetMaterial(HandleColor));
            return door.transform;
        }

        void BuildLouvre(Dims g, Material alu, Transform door)
        {
            float lh = config.louvreHeight;
            float standOff = g.bar + 0.02f;
            bool onDoor = config.louvreWall == WallSide.Door && door != null;
            bool onGable = config.louvreWall == WallSide.WestGable || config.louvreWall == WallSide.EastGable;
            DoorSpan d = GetDoorSpan(g);
            float wallLen = onDoor ? d.width : onGable ? g.W : g.L;
            float lw = Mathf.Min(config.louvreWidth, Mathf.Max(0.05f, wallLen - 4f * g.bar));
            float along = Mathf.Clamp(config.louvreOffset, -wallLen * 0.5f + lw * 0.5f, Mathf.Max(-wallLen * 0.5f + lw * 0.5f, wallLen * 0.5f - lw * 0.5f));
            float top = onDoor ? d.height : onGable ? g.RoofY(Mathf.Abs(along) + lw * 0.5f) : g.E;
            float y = Mathf.Clamp(config.louvreCentreHeight, lh * 0.5f, Mathf.Max(lh * 0.5f, top - lh * 0.5f - g.bar));
            Vector3 centre;
            Vector3 outward;
            Transform parent = builtRoot;
            if (onDoor)
            {
                // Door space: x outward from the gable, z along the door from its centre; the louvre slides with the door.
                centre = new Vector3(d.sign * 0.012f, y, along);
                outward = new Vector3(d.sign, 0f, 0f);
                parent = door;
            }
            else switch (config.louvreWall)
            {
                case WallSide.WestGable: centre = new Vector3(-g.hl - standOff, y, along); outward = Vector3.left; break;
                case WallSide.EastGable: centre = new Vector3(g.hl + standOff, y, along); outward = Vector3.right; break;
                case WallSide.SouthWall: centre = new Vector3(along, y, -g.hw - standOff); outward = Vector3.back; break;
                default: centre = new Vector3(along, y, g.hw + standOff); outward = Vector3.forward; break;
            }
            GameObject lv = new GameObject("louvre");
            lv.transform.SetParent(parent, false);
            lv.transform.localPosition = centre;
            lv.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);
            Register<TwinComponent>(lv, "louvre", TwinKind.Louvre);

            // In louvre space: x along the wall, y up, z outward.
            float fb = g.bar * 0.7f, w2 = lw * 0.5f, h2 = lh * 0.5f;
            Bar("louvre-frame", lv.transform, new Vector3(-w2, -h2, 0f), new Vector3(w2, -h2, 0f), fb, alu);
            Bar("louvre-frame", lv.transform, new Vector3(-w2, h2, 0f), new Vector3(w2, h2, 0f), fb, alu);
            Bar("louvre-frame", lv.transform, new Vector3(-w2, -h2, 0f), new Vector3(-w2, h2, 0f), fb, alu);
            Bar("louvre-frame", lv.transform, new Vector3(w2, -h2, 0f), new Vector3(w2, h2, 0f), fb, alu);
            int slats = Mathf.Max(2, Mathf.RoundToInt(lh / 0.06f));
            float pitch = lh / slats;
            for (int s = 0; s < slats; s++)
            {
                GameObject slat = Prim(PrimitiveType.Cube, "louvre-slat", lv.transform,
                                       new Vector3(0f, -h2 + (s + 0.5f) * pitch, 0f), Quaternion.Euler(35f, 0f, 0f),
                                       new Vector3(Mathf.Max(0.01f, lw - fb), 0.005f, Mathf.Min(0.06f, pitch * 1.2f)), alu);
                Register<TwinComponent>(slat, "louvre-slat", TwinKind.Louvre);
            }
        }

        void BuildPots()
        {
            int count = config.potCount, rows = config.potRows;
            if (count <= 0) return;
            Transform grp = Group("pots", builtRoot);
            Material potMat = TwinUtil.GetMaterial(PotColor);
            int perRow = Mathf.CeilToInt(count / (float)rows);
            int made = 0;
            for (int r = 0; r < rows && made < count; r++)
            {
                int inRow = Mathf.Min(perRow, count - made);
                float z = (r - (rows - 1) * 0.5f) * config.potRowSpacing;
                for (int k = 0; k < inRow; k++)
                {
                    float x = (k - (inRow - 1) * 0.5f) * config.potSpacing;
                    made++;
                    float d = config.potDiameter, h = config.potHeight;
                    Part(PrimitiveType.Cylinder, "pot-" + made, TwinKind.Pot, grp, new Vector3(x, h * 0.5f, z), new Vector3(d, h * 0.5f, d), potMat);
                    if (config.plantHeight > 0.01f) BuildTomato(grp, made, new Vector3(x, h, z), config.plantHeight);
                }
            }
        }

        /// <summary>Staked tomato plant on a pot top: stem, cane, compound leaves in a spiral, and two trusses once it is tall enough.</summary>
        void BuildTomato(Transform parent, int n, Vector3 potTop, float ph)
        {
            GameObject plant = new GameObject("plant-" + n);
            plant.transform.SetParent(parent, false);
            plant.transform.localPosition = potTop;
            plant.transform.localRotation = Quaternion.Euler(0f, n * 47f, 0f);
            Register<TwinComponent>(plant, "plant-" + n, TwinKind.Plant);
            Transform t = plant.transform;
            Material leaf = TwinUtil.GetMaterial(PlantColor);
            Material stem = TwinUtil.GetMaterial(StemColor);

            Prim(PrimitiveType.Cylinder, "cane", t, new Vector3(0.03f, ph * 0.55f, 0f), Quaternion.identity,
                 new Vector3(0.012f, ph * 0.55f, 0.012f), TwinUtil.GetMaterial(CaneColor));
            Prim(PrimitiveType.Cylinder, "stem", t, new Vector3(0f, ph * 0.5f, 0f), Quaternion.identity,
                 new Vector3(0.014f, ph * 0.5f, 0.014f), stem);

            // Leaves: longest at the base, pointing up near the tip; 137.5 deg apart like a real phyllotaxis.
            int leaves = Mathf.Clamp(Mathf.RoundToInt(ph / 0.13f), 3, 10);
            float k = Mathf.Clamp(ph / 0.8f, 0.5f, 1.5f);
            for (int i = 0; i < leaves; i++)
            {
                float f = (i + 0.5f) / leaves;
                float len = Mathf.Lerp(0.30f, 0.14f, f) * k;
                Transform lf = new GameObject("leaf").transform;
                lf.SetParent(t, false);
                lf.localPosition = new Vector3(0f, ph * Mathf.Lerp(0.12f, 0.95f, f), 0f);
                lf.localRotation = Quaternion.Euler(Mathf.Lerp(15f, -35f, f), i * 137.5f, 0f);
                Prim(PrimitiveType.Cube, "rachis", lf, new Vector3(0f, 0f, len * 0.5f), Quaternion.identity, new Vector3(0.006f, 0.006f, len), stem);
                Vector3 leaflet = new Vector3(len * 0.32f, 0.006f, len * 0.42f);
                Prim(PrimitiveType.Sphere, "leaflet", lf, new Vector3(0f, 0f, len * 0.85f), Quaternion.identity, leaflet, leaf);
                for (int s = -1; s <= 1; s += 2)
                    Prim(PrimitiveType.Sphere, "leaflet", lf, new Vector3(s * len * 0.16f, 0f, len * 0.5f), Quaternion.Euler(0f, s * 40f, 0f), leaflet * 0.8f, leaf);
            }

            if (ph < 0.5f) return;
            Material ripe = TwinUtil.GetMaterial(RipeFruitColor);
            Material unripe = TwinUtil.GetMaterial(UnripeFruitColor);
            Vector3[] berries = { new Vector3(0f, 0f, 0f), new Vector3(0.035f, -0.03f, 0.01f), new Vector3(-0.03f, -0.035f, 0.02f), new Vector3(0.01f, -0.065f, -0.015f) };
            for (int tr = 0; tr < 2; tr++)
            {
                Transform truss = new GameObject("truss").transform;
                truss.SetParent(t, false);
                truss.localRotation = Quaternion.Euler(0f, 60f + tr * 180f, 0f);
                truss.localPosition = truss.localRotation * new Vector3(0f, 0f, 0.07f) + new Vector3(0f, ph * (tr == 0 ? 0.32f : 0.58f), 0f);
                for (int b = 0; b < berries.Length; b++)
                {
                    float dia = 0.045f - b * 0.004f;
                    Prim(PrimitiveType.Sphere, "fruit", truss, berries[b], Quaternion.identity, new Vector3(dia, dia, dia), tr == 0 ? ripe : unripe);
                }
            }
        }

        void BuildHeater()
        {
            GameObject go = Prim(PrimitiveType.Cube, "heater", builtRoot, config.heaterPosition, Quaternion.identity,
                                 config.heaterSize, TwinUtil.GetMaterial(HeaterColor, true));
            Register<HeaterVisual>(go, "heater", TwinKind.Heater);
        }

        void BuildLeds(Dims g, Material alu)
        {
            int n = config.ledCount;
            if (n <= 0) return;
            Transform grp = Group("leds", builtRoot);
            Material m = TwinUtil.GetMaterial(LedColor, true);
            float len = Mathf.Min(config.ledLength, Mathf.Max(0.1f, g.L - 0.1f));
            for (int i = 0; i < n; i++)
            {
                float z = (i - (n - 1) * 0.5f) * config.potRowSpacing;
                float roof = g.RoofY(z);
                float y = Mathf.Min(config.ledHeight, roof - 0.05f);
                string lid = "led-" + (i + 1);
                GameObject go = Prim(PrimitiveType.Cube, lid, grp, new Vector3(0f, y, z), Quaternion.identity, new Vector3(len, 0.03f, 0.07f), m, false);
                Register<LedBarVisual>(go, lid, TwinKind.LedBar);
                if (roof - 0.02f > y + 0.05f)
                {
                    for (int sx = -1; sx <= 1; sx += 2)
                        Bar(lid + "-wire", grp, new Vector3(sx * len * 0.4f, y, z), new Vector3(sx * len * 0.4f, roof - 0.02f, z), 0.006f, alu);
                }
            }
        }

        /// <summary>Control box outside a gable, on the ground; returns the energy meter box centre.</summary>
        Vector3 BuildControlBox(Dims g)
        {
            float sx = config.controlBoxGable == GableEnd.West ? -1f : 1f;
            Vector3 size = config.controlBoxSize;
            float back = g.hl + g.bar + 0.03f + config.controlBoxGap;
            Vector3 centre = new Vector3(sx * (back + size.z * 0.5f), config.controlBoxHeight, config.controlBoxOffset);
            Part(PrimitiveType.Cube, "control-box", TwinKind.ControlBox, builtRoot, centre, new Vector3(size.z, size.y, size.x), TwinUtil.GetMaterial(BoxColor));
            Vector3 meter = centre + config.energyMeterOffset;
            Part(PrimitiveType.Cube, "energy-meter-box", TwinKind.EnergyMeter, builtRoot, meter, new Vector3(0.07f, 0.16f, 0.10f), TwinUtil.GetMaterial(MeterColor));
            return meter;
        }

        void BuildWaterTank()
        {
            Part(PrimitiveType.Cube, "water-tank", TwinKind.WaterTank, builtRoot, config.waterTankPosition, config.waterTankSize, TwinUtil.GetMaterial(TankColor));
            Vector3 p = config.waterTankPosition + new Vector3(0f, config.waterTankSize.y * 0.5f + 0.05f, 0f);
            GameObject go = Prim(PrimitiveType.Cylinder, "pump", builtRoot, p, Quaternion.identity, new Vector3(0.08f, 0.05f, 0.08f), TwinUtil.GetMaterial(PumpColor, true));
            Register<PumpVisual>(go, "pump", TwinKind.Pump);
        }

        void BuildSensors(Vector3 meterCentre)
        {
            Transform grp = Group("sensors", builtRoot);
            for (int i = 0; i < config.sensors.Count; i++)
            {
                SensorSpec s = config.sensors[i];
                if (s == null || string.IsNullOrEmpty(s.id)) continue;
                Vector3 pos = s.kind == SensorKind.EnergyMeter ? meterCentre + s.position : s.position;
                CreateSensorMarker(s.id, s.kind, pos, s.label, grp);
            }
        }

        static Color SensorColor(SensorKind kind)
        {
            switch (kind)
            {
                case SensorKind.AirTRHCO2: return new Color(1f, 0.6f, 0.1f, 1f);
                case SensorKind.TRH: return new Color(1f, 0.85f, 0.2f, 1f);
                case SensorKind.PAR: return new Color(0.2f, 0.85f, 1f, 1f);
                case SensorKind.EnergyMeter: return new Color(0.9f, 0.3f, 0.9f, 1f);
                default: return new Color(0.6f, 1f, 0.6f, 1f);
            }
        }

        SensorMarker CreateSensorMarker(string sensorId, SensorKind sensorKind, Vector3 pos, string text, Transform parent)
        {
            Color c = SensorColor(sensorKind);
            Material mat = TwinUtil.GetMaterial(c, true);
            GameObject go;
            switch (sensorKind)
            {
                case SensorKind.PAR:
                    go = Prim(PrimitiveType.Cylinder, "sensor-" + sensorId, parent, pos, Quaternion.identity, new Vector3(0.09f, 0.012f, 0.09f), mat);
                    break;
                case SensorKind.EnergyMeter:
                    go = Prim(PrimitiveType.Cube, "sensor-" + sensorId, parent, pos, Quaternion.identity, new Vector3(0.06f, 0.06f, 0.06f), mat);
                    break;
                default:
                    go = Prim(PrimitiveType.Sphere, "sensor-" + sensorId, parent, pos, Quaternion.identity, new Vector3(0.07f, 0.07f, 0.07f), mat);
                    break;
            }
            SensorMarker m = Register<SensorMarker>(go, sensorId, TwinKind.Sensor);
            m.sensorKind = sensorKind;
            m.label = text;
            m.freshColor = c;
            m.staleAfterSeconds = config.staleAfterSeconds;
            if (sensorKind == SensorKind.AirTRHCO2 || sensorKind == SensorKind.TRH)
            {
                // Radiation shield: a stack of white plates just above the sensor.
                Material shield = TwinUtil.GetMaterial(ShieldColor);
                for (int k = 0; k < 4; k++)
                {
                    float d = 0.15f - k * 0.015f;
                    Part(PrimitiveType.Cylinder, sensorId + "-shield", TwinKind.SensorShield, parent,
                         new Vector3(pos.x, pos.y + 0.06f + k * 0.025f, pos.z), new Vector3(d, 0.004f, d), shield);
                }
            }
            return m;
        }

        void BuildHouseLabel(Dims g)
        {
            GameObject go = new GameObject("house-label");
            go.transform.SetParent(builtRoot, false);
            go.transform.localPosition = new Vector3(0f, g.R + 0.25f, 0f);
            TwinComponent tc = Register<TwinComponent>(go, "house-label", TwinKind.Other);
            tc.label = "Greenhouse " + greenhouseId;
        }

        void BuildGround()
        {
            GameObject go = Prim(PrimitiveType.Plane, "ground", builtRoot, new Vector3(0f, -0.03f, 0f), Quaternion.identity,
                                 new Vector3(8f, 1f, 8f), TwinUtil.GetMaterial(GroundColor));
            Register<TwinComponent>(go, "ground", TwinKind.Ground);
        }

        void BuildHedge(Dims g)
        {
            Transform grp = Group("trees-north", builtRoot);
            Material m = TwinUtil.GetMaterial(HedgeColor);
            float len = config.hedgeLength;
            int n = Mathf.Max(1, Mathf.CeilToInt(len));
            float seg = len / n;
            float zc = g.hw + config.hedgeDistance + config.hedgeDepth * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float h = config.hedgeHeight * (0.85f + 0.3f * Mathf.PerlinNoise(i * 0.73f + 0.31f, 0.57f));
                float depth = config.hedgeDepth * (0.85f + 0.3f * Mathf.PerlinNoise(0.19f, i * 0.61f + 0.11f));
                Part(PrimitiveType.Cube, "tree-" + (i + 1), TwinKind.Hedge, grp,
                     new Vector3(-len * 0.5f + (i + 0.5f) * seg, h * 0.5f - 0.03f, zc), new Vector3(seg * 1.02f, h, depth), m);
            }
        }

        /// <summary>Red arrow on the ground pointing to world north (+Z), whatever the house heading.</summary>
        void BuildCompass(Dims g)
        {
            GameObject c = new GameObject("compass");
            c.transform.SetParent(builtRoot, false);
            c.transform.localPosition = new Vector3(-g.hl - 1.2f, 0f, -g.hw - 0.6f);
            c.transform.rotation = Quaternion.identity;
            TwinComponent tc = Register<TwinComponent>(c, "compass", TwinKind.Compass);
            tc.label = "N";
            Material red = TwinUtil.GetMaterial(CompassColor);
            Part(PrimitiveType.Cube, "compass-arrow", TwinKind.Compass, c.transform, new Vector3(0f, 0.01f, -0.35f), new Vector3(0.05f, 0.01f, 0.7f), red);
            GameObject head = Prim(PrimitiveType.Cube, "compass-head", c.transform, new Vector3(0f, 0.01f, -0.04f),
                                   Quaternion.Euler(0f, 45f, 0f), new Vector3(0.14f, 0.012f, 0.14f), red);
            Register<TwinComponent>(head, "compass-head", TwinKind.Compass);
        }
    }
}
