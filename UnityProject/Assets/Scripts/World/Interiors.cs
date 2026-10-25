using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gad
{
    /// <summary>Something to do inside a building (a counter, a bed, the exit door…).</summary>
    public class ActionPoint
    {
        public Vector3 Pos;
        public string Label;
        public Action Act;
    }

    /// <summary>
    /// Building interiors. One interior exists at a time, far below the city; it is generated from
    /// the building id (and flat number), so other players in the same building see the same room
    /// at the same coordinates. Every building type has its own layout and actions.
    /// </summary>
    public static class Interiors
    {
        public static readonly Vector3 O = new Vector3(0, -1000, 0);
        public static Transform Root;
        public static Bld Current;
        public static int Unit;                 // floor (0 = ground floor / lobby)
        public static int Floor => Unit;
        static Transform Level, Ctr;
        static int pointsFlushed;
        public static float EX, EZ;             // lift position (same on every floor)
        public static readonly List<ActionPoint> Points = new List<ActionPoint>();
        static MeshB mb;
        static Rng rng;
        static float W, D, Hh;

        public static int Code => Current == null ? -1 : Current.id * 1000 + Unit;
        public static bool HasLift(Bld b) => b.type == BType.Apartment || b.type == BType.Tower || b.type == BType.Office || b.type == BType.Hotel || b.type == BType.Agency || b.type == BType.Corp;
        public static int TopFloor(Bld b) => b.type == BType.Agency ? 19 : Mathf.Max(1, b.floors - 1);

        public static ActionPoint NearestPoint(Vector3 p, float max)
        {
            var lift = Elevator.I;
            if (lift != null && Root != null && !lift.Riding && lift.PlayerInside)
                return new ActionPoint { Pos = p, Label = "ЛИФТ: ВЫБРАТЬ ЭТАЖ", Act = () => Menus.Lift(Current, lift) };
            ActionPoint best = null; float bd = max * max;
            foreach (var a in Points)
            {
                var d = a.Pos - p; d.y *= 0.3f;
                if (d.sqrMagnitude < bd) { bd = d.sqrMagnitude; best = a; }
            }
            return best;
        }

        public static void Enter(Bld b, int unit = 0)
        {
            var pl = PlayerCtl.I;
            if (unit == 0 && InPlace.I != null && InPlace.Eligible(b))
            {   // 0.8: shops are inside their own building: go there
                if (Root != null && !inPlaceActive) { Clear(); pl.Inside = -1; Streamer.SetOutdoor(true); }
                InPlace.EnterNow(b);
                return;
            }
            if (pl.Inside < 0) { Save.D.px = pl.transform.position.x; Save.D.py = pl.transform.position.y; Save.D.pz = pl.transform.position.z; }
            Build(b, unit);
            pl.Inside = b.id;
            if (unit > 0 && HasLift(b)) pl.Teleport(O + new Vector3(EX, unit * Elevator.FH + 0.1f, EZ - 2.6f), 180);
            else pl.Teleport(O + new Vector3(0, 0.1f, 2.2f), 0);
            AudioFx.Play("door");
            Hud.Toast(b.name + (unit > 0 ? " · этаж " + (unit + 1) : ""));
            Streamer.SetOutdoor(false);
        }

        public static void Exit()
        {
            if (inPlaceActive) { InPlace.ExitNow(); return; }
            var b = Current;
            Clear();
            var pl = PlayerCtl.I;
            pl.Inside = -1;
            Streamer.SetOutdoor(true);
            if (b != null) pl.Teleport(b.ExitPoint, b.front switch { 0 => 180, 1 => 0, 2 => 270, _ => 90 } );
            else pl.Teleport(City.Spawn, 0);
            Work.Cancel("Вы ушли со смены");
            AudioFx.Play("door");
        }

        public static void Clear()
        {
            if (Root != null) UnityEngine.Object.Destroy(Root.gameObject);
            Root = null; Current = null; Unit = 0; Level = null; Ctr = null; Elevator.I = null;
            Points.Clear(); pointsFlushed = 0;
        }

        // ------------------------------------------------------------------ 0.8: shops built inside their own building

        public static bool InPlaceMode;
        static bool inPlaceActive;

        /// <summary>
        /// Builds the ground floor of a shop at its real place (pos/rot = the front door, facing in),
        /// shrunk by scale to fit the building. The current interior state is left as it was.
        /// </summary>
        public static GameObject BuildInPlace(Bld b, Vector3 pos, Quaternion rot, float scale, out List<ActionPoint> points)
        {
            var sCur = Current; var sRoot = Root; var sLevel = Level; var sCtr = Ctr; int sUnit = Unit;
            var sPts = new List<ActionPoint>(Points); int sFl = pointsFlushed; var sMb = mb; var sRng = rng;
            float sW = W, sD = D, sH = Hh, sEX = EX, sEZ = EZ; var sEl = Elevator.I;
            Points.Clear(); pointsFlushed = 0;
            Current = b;
            Root = new GameObject("Shop " + b.name).transform;
            Root.SetPositionAndRotation(pos, rot);
            Root.localScale = Vector3.one * Mathf.Min(1f, scale);
            Level = null; Unit = 0;
            InPlaceMode = true;
            try { BuildLevel(0); }
            finally { InPlaceMode = false; }
            points = new List<ActionPoint>(Points);
            var go = Root.gameObject;
            // the building above keeps the sun out: real lamps under the ceiling light the shop
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 2; j++)
                {
                    var lg = new GameObject("ShopLight").AddComponent<Light>();
                    lg.transform.SetParent(Root, false);
                    lg.transform.localPosition = new Vector3((i - 0.5f) * W * 0.5f, Mathf.Max(2.8f, Hh - 0.5f), D * (0.3f + j * 0.4f));
                    lg.type = LightType.Point; lg.range = Mathf.Max(W, D) * 0.9f; lg.intensity = 1.6f;
                    lg.color = new Color(1f, 0.95f, 0.86f); lg.shadows = LightShadows.None;
                }
            Current = sCur; Root = sRoot; Level = sLevel; Ctr = sCtr; Unit = sUnit;
            Points.Clear(); Points.AddRange(sPts); pointsFlushed = sFl; mb = sMb; rng = sRng;
            W = sW; D = sD; Hh = sH; EX = sEX; EZ = sEZ; Elevator.I = sEl;
            return go;
        }

        /// <summary>The player walked into a shop built in place: its counters and actions become the current ones.</summary>
        public static void UseInPlace(Bld b, List<ActionPoint> pts)
        {
            Current = b; inPlaceActive = true;
            Points.Clear(); Points.AddRange(pts);
        }

        public static void LeaveInPlace()
        {
            if (!inPlaceActive) return;
            inPlaceActive = false; Current = null;
            Points.Clear();
            Work.Cancel("Вы ушли со смены");
        }

        static void Point(Vector3 local, string label, Action act) => Points.Add(new ActionPoint { Pos = Ctr.TransformPoint(local), Label = label, Act = act });

        // ------------------------------------------------------------------ building blocks

        static Material floorMat, wallMat;

        static void Room(float w, float d, float h, Material floor, Color wall, bool windows = true, float dh = 1.4f, bool street = true, int lights = 2)
        {
            W = w; D = d; Hh = h;
            floorMat = floor;
            wallMat = Photo.Mat("plastered_wall", Color.Lerp(wall, Color.white, 0.25f) * 1.18f, 0.1f, 0.5f);
            var t = Root;
            // floor + ceiling
            mb.Box(Mats.Lit(wall * 0.8f), floor, new Vector3(-w / 2, -0.3f, 0), new Vector3(w / 2, 0, d), new Vector2(4, 4));
            mb.Box(wallMat, wallMat, new Vector3(-w / 2, h, 0), new Vector3(w / 2, h + 0.2f, d), new Vector2(4, 4), default, true, Mats.Lit(new Color(0.92f, 0.92f, 0.9f)));
            // walls (door gap in the south wall); built in place the front is the building's glass shop front
            if (InPlaceMode) { windows = false; street = false; }
            else
            {
                mb.Box(wallMat, wallMat, new Vector3(-w / 2 - 0.3f, 0, -0.3f), new Vector3(-dh, h, 0), new Vector2(4, 4));
                mb.Box(wallMat, wallMat, new Vector3(dh, 0, -0.3f), new Vector3(w / 2 + 0.3f, h, 0), new Vector2(4, 4));
                mb.Box(wallMat, wallMat, new Vector3(-dh, 2.7f, -0.3f), new Vector3(dh, h, 0), new Vector2(4, 4));
            }
            mb.Box(wallMat, wallMat, new Vector3(-w / 2 - 0.3f, 0, d), new Vector3(w / 2 + 0.3f, h, d + 0.3f), new Vector2(4, 4));
            mb.Box(wallMat, wallMat, new Vector3(-w / 2 - 0.3f, 0, 0), new Vector3(-w / 2, h, d), new Vector2(4, 4));
            mb.Box(wallMat, wallMat, new Vector3(w / 2, 0, 0), new Vector3(w / 2 + 0.3f, h, d), new Vector2(4, 4));
            // skirting
            var sk = Mats.Lit(wall * 0.6f);
            mb.Box(sk, sk, new Vector3(-w / 2, 0, d - 0.05f), new Vector3(w / 2, 0.12f, d), new Vector2(4, 4));
            // exit door: light rectangle with the street behind
            if (street) mb.Box(Mats.Glow(DayNight.IsNight ? new Color(0.12f, 0.14f, 0.25f) : new Color(0.75f, 0.85f, 0.95f)), null, new Vector3(-1.4f, 0, -0.62f), new Vector3(1.4f, 2.7f, -0.6f), new Vector2(4, 4));
            if (!InPlaceMode) mb.Box(Mats.Lit(new Color(0.2f, 0.2f, 0.22f)), null, new Vector3(-dh - 0.1f, 2.7f, -0.05f), new Vector3(dh + 0.1f, 2.85f, 0.05f), new Vector2(4, 4));
            if (windows)
            {
                var glow = Mats.Glow(DayNight.IsNight ? new Color(0.08f, 0.1f, 0.2f) : new Color(0.65f, 0.82f, 0.98f));
                for (float z = 3; z < d - 2; z += 5)
                {
                    mb.Box(glow, null, new Vector3(-w / 2 + 0.01f, 1f, z), new Vector3(-w / 2 + 0.02f, Mathf.Min(h - 0.6f, 2.6f), z + 2.6f), new Vector2(4, 4));
                    mb.Box(glow, null, new Vector3(w / 2 - 0.02f, 1f, z), new Vector3(w / 2 - 0.01f, Mathf.Min(h - 0.6f, 2.6f), z + 2.6f), new Vector2(4, 4));
                }
            }
            // ceiling lamps
            var lamp = Mats.Glow(new Color(1f, 0.97f, 0.88f));
            for (float x = -w / 2 + 3; x < w / 2 - 1; x += 6)
                for (float z = 3; z < d - 1; z += 6)
                    mb.Box(lamp, lamp, new Vector3(x - 0.6f, h - 0.06f, z - 0.6f), new Vector3(x + 0.6f, h, z + 0.6f), new Vector2(1, 1));
            // colliders
            Col(new Vector3(0, -0.15f, d / 2), new Vector3(w + 1, 0.3f, d + 1), 13);
            Col(new Vector3(-w / 2 - 0.15f, h / 2, d / 2), new Vector3(0.3f, h, d), 10);
            Col(new Vector3(w / 2 + 0.15f, h / 2, d / 2), new Vector3(0.3f, h, d), 10);
            Col(new Vector3(0, h / 2, d + 0.15f), new Vector3(w, h, 0.3f), 10);
            Col(new Vector3(-(w / 2 + dh) / 2, h / 2, -0.15f), new Vector3(w / 2 - dh, h, 0.3f), 10);
            Col(new Vector3((w / 2 + dh) / 2, h / 2, -0.15f), new Vector3(w / 2 - dh, h, 0.3f), 10);
            if (street) Col(new Vector3(0, h / 2, -0.7f), new Vector3(w, h, 0.3f), 10);
            Col(new Vector3(0, h + 0.1f, d / 2), new Vector3(w, 0.2f, d), 10);
            // lights
            if (lights >= 2)
            {
                Light(new Vector3(0, h - 0.5f, d * 0.3f), Mathf.Max(w, d) * 0.9f, 1.3f);
                Light(new Vector3(0, h - 0.5f, d * 0.75f), Mathf.Max(w, d) * 0.9f, 1.3f);
            }
            else if (lights == 1) Light(new Vector3(0, h - 0.5f, d * 0.5f), Mathf.Max(w, d) * 1.1f, 1.4f);
            if (street) InteriorExit.Make(Ctr);
        }

        static void Light(Vector3 local, float range, float intensity)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(Ctr, false);
            go.transform.localPosition = local;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.range = range; l.intensity = intensity; l.color = new Color(1f, 0.96f, 0.9f);
            l.shadows = LightShadows.None;
        }

        static void Col(Vector3 c, Vector3 s, int layer)
        {
            var go = new GameObject("Col") { layer = layer };
            go.transform.SetParent(Ctr, false);
            go.transform.localPosition = c;
            go.AddComponent<BoxCollider>().size = s;
        }

        static void Box(Vector3 min, Vector3 max, Color c, bool solid = false, float smooth = 0.2f)
        {
            var m = Mats.Lit(c, smooth);
            mb.Box(m, m, min, max, new Vector2(2, 2));
            if (solid) Col((min + max) / 2, max - min, 12);
        }

        static void BoxM(Vector3 min, Vector3 max, Material m, bool solid = false)
        {
            mb.Box(m, m, min, max, new Vector2(2, 2));
            if (solid) Col((min + max) / 2, max - min, 12);
        }

        static void Mesh(PrimitiveType t, Vector3 pos, Vector3 scale, Color c, float yaw = 0)
            => mb.Add(Prim.MeshOf(t), Matrix4x4.TRS(pos, Quaternion.Euler(0, yaw, 0), scale), Mats.Lit(c));

        static Person Npc(Vector3 local, float yaw, int seed, int pose = 0, int top = -1)
        {
            var r = new Rng((uint)seed * 2654435761u + 7);
            var look = Person.Random(r);
            if (top == 3) { look.hTop = r.Chance(0.5f) ? "ht_suit_navy" : "ht_suit_grey"; look.hBottom = "hb_pants_black"; look.hShoes = "hf_shoes_black"; }
            if (top == 1) { look.hTop = "ht_flannel_blue"; if (Wardrobe.Find(look.hTop) == null) look.hTop = "ht_tee_grey"; look.hBottom = "hb_cargo_olive"; look.hShoes = "hf_boots_black"; }
            var h = Person.Create(Ctr, look, "Staff");
            if (pose == 2 && top == 3) pose = 5;
            h.transform.localPosition = local;
            h.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            h.Pose = pose;
            return h;
        }

        static void Counter(Vector3 c, float len, Color col, bool alongX = true)
        {
            var s = alongX ? new Vector3(len, 1.05f, 0.7f) : new Vector3(0.7f, 1.05f, len);
            Box(c - new Vector3(s.x / 2, 0, s.z / 2), c + new Vector3(s.x / 2, s.y, s.z / 2), col, true, 0.5f);
            var top = alongX ? new Vector3(len + 0.1f, 0.05f, 0.8f) : new Vector3(0.8f, 0.05f, len + 0.1f);
            Box(c + new Vector3(-top.x / 2, 1.05f, -top.z / 2), c + new Vector3(top.x / 2, 1.1f, top.z / 2), new Color(0.9f, 0.9f, 0.88f), false, 0.7f);
        }

        static void Shelf(Vector3 c, float len, bool alongX, int seed)
        {
            var r = new Rng((uint)seed);
            var s = alongX ? new Vector3(len, 2f, 0.6f) : new Vector3(0.6f, 2f, len);
            BoxM(c - new Vector3(s.x / 2, 0, s.z / 2), c + new Vector3(s.x / 2, 0.08f, s.z / 2), WorldBuilder.Metal, true);
            Col(c + new Vector3(0, 1, 0), s, 12);
            for (int lvl = 0; lvl < 4; lvl++)
            {
                float y = 0.1f + lvl * 0.5f;
                BoxM(c + new Vector3(-s.x / 2, y, -s.z / 2), c + new Vector3(s.x / 2, y + 0.04f, s.z / 2), WorldBuilder.Metal);
                for (float u = -len / 2 + 0.2f; u < len / 2 - 0.2f; u += 0.3f)
                {
                    var col = Palette.Get(Palette.Cloth, r.I(14));
                    float hh = r.F(0.18f, 0.38f);
                    var p = alongX ? c + new Vector3(u, y + 0.04f, 0) : c + new Vector3(0, y + 0.04f, u);
                    Box(p + new Vector3(-0.12f, 0, -0.18f), p + new Vector3(0.12f, hh, 0.18f), col);
                }
            }
            BoxM(c + new Vector3(-s.x / 2, 0, -s.z / 2), c + new Vector3(-s.x / 2 + 0.04f, 2.1f, s.z / 2), WorldBuilder.Metal);
            BoxM(c + new Vector3(s.x / 2 - 0.04f, 0, -s.z / 2), c + new Vector3(s.x / 2, 2.1f, s.z / 2), WorldBuilder.Metal);
        }

        static bool chairModels;

        static void Table(Vector3 c, Color col, bool chairs = true, float size = 1f)
        {
            if (chairs && cafeTables && Model("outdoor_table_chair_set_01", c, H.R(4, (int)c.x, (int)c.z) * 90, 1.9f * size)) return;
            if (Model("wooden_table_02", c, 0, 1.2f * size, null, true))
            {
                if (!chairs) return;
                chairModels = true;
                foreach (var o in new[] { new Vector3(0, 0, 0.95f), new Vector3(0, 0, -0.95f) })
                    Chair(c + o * size, Quaternion.LookRotation(-o).eulerAngles.y, col * 0.7f);
                chairModels = false;
                return;
            }
            Box(c + new Vector3(-0.6f * size, 0.72f, -0.6f * size), c + new Vector3(0.6f * size, 0.78f, 0.6f * size), col, false, 0.4f);
            Mesh(PrimitiveType.Cylinder, c + new Vector3(0, 0.36f, 0), new Vector3(0.12f, 0.36f, 0.12f), new Color(0.2f, 0.2f, 0.2f));
            Col(c + new Vector3(0, 0.4f, 0), new Vector3(1.2f * size, 0.8f, 1.2f * size), 12);
            if (!chairs) return;
            foreach (var o in new[] { new Vector3(0, 0, 1f), new Vector3(0, 0, -1f), new Vector3(1f, 0, 0), new Vector3(-1f, 0, 0) })
                Chair(c + o * size, Quaternion.LookRotation(-o).eulerAngles.y, col * 0.7f);
        }

        static void Chair(Vector3 c, float yaw, Color col)
        {
            if (chairModels && Model("ArmChair_01", c, yaw, 0.6f, Color.Lerp(col, Color.white, 0.4f), false)) return;
            var rot = Quaternion.Euler(0, yaw, 0);
            var m = Mats.Lit(col);
            mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(c + Vector3.up * 0.45f, rot, new Vector3(0.45f, 0.06f, 0.45f)), m);
            mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(c + Vector3.up * 0.75f + rot * new Vector3(0, 0, -0.21f), rot, new Vector3(0.45f, 0.6f, 0.05f)), m);
            mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(c + Vector3.up * 0.22f, rot, new Vector3(0.4f, 0.44f, 0.4f)), Mats.Lit(new Color(0.2f, 0.2f, 0.2f)));
        }

        /// <summary>Places a real (Poly Haven) model in the current container; false if it is missing.</summary>
        static bool Model(string name, Vector3 pos, float yaw, float fit, Color? tint = null, bool collide = true) =>
            ModelLib.Place(Ctr, name, pos, yaw, fit, collide, tint) != null;

        static void Plant(Vector3 c, float s = 1f)
        {
            if (Model("potted_plant_04", c, H.R(360, (int)(c.x * 10), (int)(c.z * 10)), 0.75f * s, null, true)) return;
            Mesh(PrimitiveType.Cylinder, c + Vector3.up * 0.25f * s, new Vector3(0.45f, 0.25f, 0.45f) * s, new Color(0.55f, 0.35f, 0.25f));
            Mesh(PrimitiveType.Sphere, c + Vector3.up * 0.9f * s, new Vector3(0.8f, 1f, 0.8f) * s, new Color(0.2f, 0.5f, 0.22f));
        }

        static void Sofa(Vector3 c, float yaw, Color col)
        {
            if (Model(H.R(2, (int)(c.x * 7), (int)(c.z * 7)) == 0 ? "sofa_02" : "sofa_03", c, yaw, 2.2f, Color.Lerp(col, Color.white, 0.55f)))
            {
                // a coffee table and an armchair go with it
                Model("modern_coffee_table_01", c + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, 1.25f), yaw, 1.1f, null, true);
                return;
            }
            var rot = Quaternion.Euler(0, yaw, 0);
            var m = Mats.Lit(col, 0.1f);
            mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(c + Vector3.up * 0.25f, rot, new Vector3(2.2f, 0.5f, 0.9f)), m);
            mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(c + Vector3.up * 0.65f + rot * new Vector3(0, 0, -0.38f), rot, new Vector3(2.2f, 0.8f, 0.2f)), m);
            foreach (float sx in new[] { -1.05f, 1.05f })
                mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(c + Vector3.up * 0.4f + rot * new Vector3(sx, 0, 0), rot, new Vector3(0.2f, 0.8f, 0.9f)), m);
            Col(c + Vector3.up * 0.4f, rot * new Vector3(2.2f, 0.8f, 0.9f) is var v ? new Vector3(Mathf.Abs(v.x), 0.8f, Mathf.Abs(v.z)) : Vector3.one, 12);
        }

        static void Bed(Vector3 c, float yaw, Color sheet)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(c + Vector3.up * 0.25f, rot, new Vector3(1.8f, 0.5f, 2.2f)), WorldBuilder.Wood);
            mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(c + Vector3.up * 0.55f, rot, new Vector3(1.7f, 0.2f, 2.1f)), Mats.Lit(sheet, 0.05f));
            mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(c + Vector3.up * 0.7f + rot * new Vector3(0, 0, -0.8f), rot, new Vector3(1.4f, 0.15f, 0.4f)), Mats.Lit(Color.white, 0.05f));
            mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(c + Vector3.up * 0.8f + rot * new Vector3(0, 0, -1.08f), rot, new Vector3(1.8f, 1.1f, 0.1f)), WorldBuilder.Wood);
        }

        static bool cafeTables;

        static void Tv(Vector3 c, float yaw)
        {
            Model("side_table_01", c + Quaternion.Euler(0, yaw, 0) * new Vector3(1.2f, 0, 0.1f), yaw, 0.5f, null, true);
            var rot = Quaternion.Euler(0, yaw, 0);
            mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(c + Vector3.up * 0.25f, rot, new Vector3(1.8f, 0.5f, 0.45f)), WorldBuilder.Wood);
            mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(c + Vector3.up * 1.15f, rot, new Vector3(1.6f, 0.92f, 0.06f)), Mats.Lit(new Color(0.05f, 0.05f, 0.06f), 0.9f));
            mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(c + Vector3.up * 1.15f + rot * new Vector3(0, 0, 0.035f), rot, new Vector3(1.5f, 0.82f, 0.01f)), Mats.Glow(new Color(0.25f, 0.45f, 0.75f)));
        }

        static void Sign(Vector3 local, string text, Color col, float size = 3f, float yaw = 0)
        {
            var go = new GameObject("Sign");
            go.transform.SetParent(Ctr, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var t = go.AddComponent<TMPro.TextMeshPro>();
            UIKit.Init();
            t.font = UIKit.Font;
            t.text = text;
            t.fontSize = size;
            t.color = col;
            t.alignment = TMPro.TextAlignmentOptions.Center;
            t.rectTransform.sizeDelta = new Vector2(12, 2);
        }

        static void DisplayCar(Vector3 c, float yaw, CarDef def, int color)
        {
            var xf = Matrix4x4.TRS(c, Quaternion.Euler(0, yaw, 0), Vector3.one);
            mb.AddMulti(CarMesh.Static(def, 0), xf, CarMesh.Materials(Car.ColorOf(def, color), def.Spec));
            var r = Quaternion.Euler(0, yaw, 0) * new Vector3(def.W, 1.5f, def.L);
            Col(c + Vector3.up * 0.8f, new Vector3(Mathf.Abs(r.x), 1.5f, Mathf.Abs(r.z)), 12);
        }

        // ------------------------------------------------------------------ clothing shop

        static Material[] fabrics;
        static Material Fabric(int i)
        {
            if (fabrics == null)
            {
                var photo = new[] { "denim_fabric", "velour_velvet", "brown_leather" };
                fabrics = new Material[48];
                for (int k = 0; k < fabrics.Length; k++)
                    fabrics[k] = k % 4 == 1 ? Photo.Mat(photo[(k / 4) % photo.Length], Color.white, 0.12f, 1.2f)
                        : Mats.Lit(Palette.Get(Palette.Cloth, k * 5 + 3), 0.12f);
            }
            return fabrics[Mathf.Abs(i) % fabrics.Length];
        }

        /// <summary>A garment on a hanger: a shirt with sleeves, a dress, or trousers.</summary>
        static void Garment(Vector3 hook, Vector3 along, int kind, Material mat)
        {
            var rot = Quaternion.LookRotation(along) * Quaternion.Euler(0, 90, 0);   // faces across the rail
            var chrome = WorldBuilder.Metal;
            mb.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(hook + Vector3.down * 0.05f, Quaternion.identity, new Vector3(0.01f, 0.05f, 0.01f)), chrome);
            mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(hook + Vector3.down * 0.12f, rot, new Vector3(0.42f, 0.015f, 0.015f)), chrome);
            if (kind == 0)
            {   // shirt / tee: body + two sleeves + collar
                mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(hook + Vector3.down * 0.46f, rot, new Vector3(0.46f, 0.66f, 0.045f)), mat);
                foreach (float s in new[] { -1f, 1f })
                    mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(hook + rot * new Vector3(s * 0.29f, -0.27f, 0), rot * Quaternion.Euler(0, 0, s * 35), new Vector3(0.14f, 0.3f, 0.04f)), mat);
                mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(hook + Vector3.down * 0.15f, rot, new Vector3(0.16f, 0.05f, 0.05f)), mat);
            }
            else if (kind == 1)
            {   // dress: a narrow top flaring into a skirt
                mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(hook + Vector3.down * 0.36f, rot, new Vector3(0.34f, 0.42f, 0.05f)), mat);
                mb.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(hook + Vector3.down * 0.86f, rot * Quaternion.Euler(0, 0, 0), new Vector3(0.5f, 0.3f, 0.07f)), mat);
            }
            else
            {   // trousers folded over the bar
                foreach (float s in new[] { -1f, 1f })
                    mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(hook + rot * new Vector3(s * 0.1f, -0.52f, 0), rot, new Vector3(0.18f, 0.78f, 0.05f)), mat);
                mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(hook + Vector3.down * 0.16f, rot, new Vector3(0.4f, 0.08f, 0.055f)), mat);
            }
        }

        /// <summary>A chrome clothes rail on two legs with a row of hanging garments.</summary>
        static void Rack(Vector3 c, float len, bool alongX, int seed)
        {
            var r = new Rng((uint)seed * 7919u + 1);
            var along = alongX ? Vector3.right : Vector3.forward;
            var chrome = WorldBuilder.Metal;
            foreach (float s in new[] { -0.5f, 0.5f })
            {
                var leg = c + along * s * len;
                mb.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(leg + Vector3.up * 0.8f, Quaternion.identity, new Vector3(0.04f, 0.8f, 0.04f)), chrome);
                mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(leg + Vector3.up * 0.02f, Quaternion.LookRotation(along), new Vector3(0.5f, 0.04f, 0.06f)), chrome);
            }
            mb.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(c + Vector3.up * 1.6f, Quaternion.FromToRotation(Vector3.up, along), new Vector3(0.035f, len / 2, 0.035f)), chrome);
            int kind = r.I(3);
            int n = Mathf.FloorToInt(len / 0.17f);
            for (int i = 0; i < n; i++)
            {
                float u = -len / 2 + 0.12f + i * (len - 0.24f) / Mathf.Max(1, n - 1);
                Garment(c + along * u + Vector3.up * 1.6f, along, kind, Fabric(seed * 3 + (i / 3)));
            }
            Col(c + Vector3.up * 0.8f, alongX ? new Vector3(len, 1.6f, 0.6f) : new Vector3(0.6f, 1.6f, len), 12);
        }

        /// <summary>Wall shelving with folded clothes, shoes or accessories.</summary>
        static void Stock(Vector3 c, float len, bool alongX, int kind, int seed)
        {
            var r = new Rng((uint)seed * 31u + 5);
            var along = alongX ? Vector3.right : Vector3.forward;
            var depth = alongX ? Vector3.forward : Vector3.right;
            var wood = WorldBuilder.Wood;
            BoxM(c - along * len / 2 - depth * 0.25f, c + along * len / 2 + depth * 0.25f + Vector3.up * 0.04f, wood, true);
            for (int lvl = 0; lvl < 4; lvl++)
            {
                float y = 0.35f + lvl * 0.5f;
                BoxM(c - along * len / 2 - depth * 0.25f + Vector3.up * y, c + along * len / 2 + depth * 0.25f + Vector3.up * (y + 0.03f), wood);
                for (float u = -len / 2 + 0.25f; u < len / 2 - 0.2f; u += kind == 1 ? 0.32f : 0.45f)
                {
                    var p = c + along * u + Vector3.up * (y + 0.03f);
                    if (kind == 0)
                    {   // a stack of folded clothes
                        int h = 2 + r.I(4);
                        for (int k = 0; k < h; k++)
                            mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(p + Vector3.up * (0.035f + k * 0.07f), Quaternion.LookRotation(depth) * Quaternion.Euler(0, r.F(-4, 4), 0), new Vector3(0.34f, 0.065f, 0.28f)), Fabric(seed + (int)(u * 10) + lvl));
                    }
                    else if (kind == 1)
                    {   // a pair of shoes
                        var sc = Mats.Lit(Palette.Get(Palette.Cloth, r.I(14)), 0.4f);
                        foreach (float s in new[] { -0.06f, 0.06f })
                        {
                            var q = p + along * s;
                            mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(q + Vector3.up * 0.015f, Quaternion.LookRotation(depth), new Vector3(0.1f, 0.03f, 0.27f)), Mats.Lit(new Color(0.92f, 0.92f, 0.9f), 0.3f));
                            mb.Add(Prim.MeshOf(PrimitiveType.Sphere), Matrix4x4.TRS(q + Vector3.up * 0.07f + depth * 0.02f, Quaternion.LookRotation(depth), new Vector3(0.1f, 0.1f, 0.25f)), sc);
                        }
                    }
                    else
                    {   // accessories: caps, sunglasses, bags
                        int a = r.I(3);
                        if (a == 0) { var cm = Mats.Lit(Palette.Get(Palette.Cloth, r.I(14)), 0.2f); mb.Add(Prim.MeshOf(PrimitiveType.Sphere), Matrix4x4.TRS(p + Vector3.up * 0.06f, Quaternion.identity, new Vector3(0.2f, 0.14f, 0.22f)), cm); mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(p + Vector3.up * 0.015f - depth * 0.13f, Quaternion.LookRotation(depth), new Vector3(0.18f, 0.01f, 0.1f)), cm); }
                        else if (a == 1) { var g = Mats.Lit(new Color(0.05f, 0.05f, 0.06f), 0.9f); foreach (float s in new[] { -0.045f, 0.045f }) mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(p + along * s + Vector3.up * 0.04f, Quaternion.LookRotation(depth), new Vector3(0.075f, 0.045f, 0.01f)), g); mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(p + Vector3.up * 0.05f, Quaternion.LookRotation(depth), new Vector3(0.17f, 0.008f, 0.01f)), g); }
                        else { var bm = Photo.Mat(r.Chance(0.5f) ? "brown_leather" : "leather_white", Color.white, 0.4f); mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(p + Vector3.up * 0.13f, Quaternion.LookRotation(depth), new Vector3(0.3f, 0.24f, 0.12f)), bm); mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(p + Vector3.up * 0.3f, Quaternion.LookRotation(depth), new Vector3(0.2f, 0.1f, 0.02f)), bm); }
                    }
                }
            }
            Col(c + Vector3.up * 1.1f, alongX ? new Vector3(len, 2.2f, 0.5f) : new Vector3(0.5f, 2.2f, len), 12);
        }

        /// <summary>A white shop mannequin wearing a ready-made look.</summary>
        static void Mannequin(Vector3 local, float yaw, int seed)
        {
            var preset = Menus.SkinPresets[Mathf.Abs(seed) % Menus.SkinPresets.Length].look.Clone();
            preset.hSkin = "hs_mannequin"; preset.hHair = "hh_none"; preset.hEyes = "he_none"; preset.hMouth = "hm_none";
            var p = Person.Create(Ctr, preset, "Mannequin");
            p.transform.localPosition = local + Vector3.up * 0.12f;
            p.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            p.enabled = false;
            var an = p.GetComponentInChildren<HumanAnimator>();
            if (an != null) an.enabled = false;
            Mesh(PrimitiveType.Cylinder, local + Vector3.up * 0.06f, new Vector3(0.5f, 0.06f, 0.5f), new Color(0.15f, 0.15f, 0.17f));
        }

        /// <summary>A full clothing shop in a w x d area (centre of the near edge at c, facing +z).</summary>
        static void ClothingShop(Vector3 c, float w, float d, int seed)
        {
            // racks in rows
            int racks = Mathf.Max(1, Mathf.FloorToInt((w - 2) / 3.2f));
            for (int i = 0; i < racks; i++)
                for (int j = 0; j < Mathf.Max(1, Mathf.FloorToInt((d - 4) / 2.6f)); j++)
                    Rack(c + new Vector3(-w / 2 + 1.8f + i * 3.2f, 0, 2.6f + j * 2.6f), 2.3f, true, seed + i * 5 + j);
            // shelving on the back wall: folded clothes, shoes, accessories
            float seg = w / 3f;
            for (int k = 0; k < 3; k++) Stock(c + new Vector3(-w / 2 + seg * (k + 0.5f), 0, d - 0.35f), seg - 0.3f, true, k, seed + k);
            // mannequins near the entrance
            for (int k = 0; k < Mathf.Min(4, racks + 1); k++) Mannequin(c + new Vector3(-w / 2 + 1.2f + k * (w - 2.4f) / Mathf.Max(1, Mathf.Min(4, racks + 1) - 1), 0, 1.1f), 180 + (k - 1.5f) * 15, seed + k);
        }

        // ------------------------------------------------------------------ layouts

        public static void Build(Bld b, int unit)
        {
            Clear();
            Current = b;
            Root = new GameObject("Interior " + b.name).transform;
            Root.position = O;
            var ls = LobbySize(b);
            EX = ls.x / 2 - 2.2f; EZ = ls.y - 1.25f;
            if (HasLift(b)) Elevator.Make(Root, b, unit, new Vector3(EX, 0, EZ));
            BuildLevel(unit);
        }

        static Vector2 LobbySize(Bld b) => b.type switch
        {
            BType.Apartment or BType.Tower => new Vector2(14, 12),
            BType.Hotel => new Vector2(18, 14),
            BType.Agency => new Vector2(30, 22),
            _ => new Vector2(22, 16),
        };

        /// <summary>Builds one floor of the current building (0 = ground floor), replacing the previous one.</summary>
        public static void BuildLevel(int floor)
        {
            var b = Current;
            if (Level != null) UnityEngine.Object.Destroy(Level.gameObject);
            Points.Clear(); pointsFlushed = 0;
            Unit = floor;
            Level = new GameObject("Level " + floor).transform;
            Level.SetParent(Root, false);
            Level.localPosition = Vector3.up * floor * Elevator.FH;
            Ctr = Level;
            mb = new MeshB();
            rng = new Rng(H.U(b.id, floor, 31));
            int unit = 0;
            if (floor > 0 && HasLift(b)) { UpperFloor(b, floor); return; }
            var accent = b.Info.Sign;
            var tiles = Photo.Mat("interior_tiles", Color.white, 0.55f);
            var dark = Photo.Mat("terrazzo_tiles", new Color(0.6f, 0.6f, 0.62f), 0.65f);
            var concrete = WorldBuilder.Concrete;
            var wood = Photo.Mat("laminate_floor_02", Color.white, 0.45f);

            bool homeView = (b.type == BType.House || b.type == BType.Villa) && Eco.OwnsHome(b.id, 0);
            if (homeView) { Home(b, unit); Finish(b); return; }

            switch (b.type)
            {
                case BType.Apartment:
                case BType.Tower:
                    Room(14, 12, 4f, b.type == BType.Tower ? dark : tiles, new Color(0.88f, 0.86f, 0.8f));
                    Counter(new Vector3(-4, 0, 6), 3, new Color(0.3f, 0.25f, 0.2f));
                    Npc(new Vector3(-4, 0, 7f), 180, b.id, 0, 3);
                    for (int i = 0; i < 4; i++) Box(new Vector3(6.6f, 1 + i * 0.3f, 2 + i * 0.01f), new Vector3(6.9f, 1.25f + i * 0.3f, 5), new Color(0.6f, 0.55f, 0.45f));
                    Sofa(new Vector3(-4, 0, 2.5f), 90, new Color(0.3f, 0.35f, 0.45f));
                    Plant(new Vector3(6, 0, 1));
                    Sign(new Vector3(EX, 3.4f, 11.85f), "ЛИФТ", Color.white, 3);
                    Point(new Vector3(-4, 0, 5f), "КОНСЬЕРЖ: КВАРТИРЫ", () => Menus.Flats(b));
                    if (b.type == BType.Tower) Point(new Vector3(0, 0, 10.5f), "ОФИСЫ: РАБОТА И БИЗНЕС", () => Menus.Building(b));
                    break;

                case BType.House:
                case BType.Villa:
                    Room(12, 10, 3.4f, wood, new Color(0.93f, 0.9f, 0.82f));
                    Sofa(new Vector3(0, 0, 7), 180, new Color(0.55f, 0.5f, 0.45f));
                    Plant(new Vector3(-5, 0, 9));
                    Sign(new Vector3(0, 2.6f, 9.6f), "ПРОДАЁТСЯ", new Color(0.95f, 0.75f, 0.2f), 4);
                    Point(new Vector3(0, 0, 5), "КУПИТЬ ДОМ — " + Hud.Money(b.UnitPrice(0)), () => Menus.BuyHome(b, 0));
                    break;

                case BType.Shop:
                case BType.Supermarket:
                case BType.GasStation:
                {
                    bool big = b.type == BType.Supermarket;
                    Room(big ? 26 : 14, big ? 22 : 12, 4.2f, tiles, new Color(0.95f, 0.95f, 0.93f));
                    for (int i = 0; i < (big ? 5 : 2); i++) Shelf(new Vector3(-W / 2 + 4 + i * 4.2f, 0, D / 2 + 1), D - 7, false, b.id * 10 + i);
                    Counter(new Vector3(W / 2 - 3, 0, 3.5f), 3.2f, accent, false);
                    Npc(new Vector3(W / 2 - 1.8f, 0, 3.5f), 270, b.id, 0);
                    if (!Model("CashRegister_01", new Vector3(W / 2 - 3f, 1.1f, 3.3f), 90, 0.5f, null, false))
                        Box(new Vector3(W / 2 - 3.3f, 1.1f, 3f), new Vector3(W / 2 - 2.8f, 1.5f, 3.5f), new Color(0.15f, 0.15f, 0.15f));
                    Point(new Vector3(W / 2 - 4f, 0, 3.5f), "КАССА: КУПИТЬ ЕДУ", () => Menus.Food(b, FoodDef.Shop));
                    Sign(new Vector3(0, 3.4f, D - 0.4f), b.name, accent, 4);
                    break;
                }

                case BType.Cafe:
                case BType.Restaurant:
                {
                    bool r = b.type == BType.Restaurant;
                    Room(r ? 20 : 14, r ? 16 : 12, 3.8f, r ? dark : wood, r ? new Color(0.45f, 0.18f, 0.16f) : new Color(0.9f, 0.82f, 0.7f));
                    var tcol = r ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.55f, 0.38f, 0.22f);
                    cafeTables = !r;
                    for (float x = -W / 2 + 3; x < W / 2 - 4; x += 3.6f)
                        for (float z = 3.5f; z < D - 5; z += 3.6f)
                            Table(new Vector3(x, 0, z), tcol);
                    cafeTables = false;
                    Counter(new Vector3(0, 0, D - 2.2f), W * 0.5f, accent);
                    Npc(new Vector3(-1.5f, 0, D - 1.2f), 180, b.id, 1);
                    Npc(new Vector3(2.5f, 0, D - 1.2f), 180, b.id + 1, 0);
                    // kitchen hint behind the counter
                    Box(new Vector3(-W / 4, 0, D - 0.9f), new Vector3(W / 4, 0.9f, D - 0.3f), new Color(0.75f, 0.75f, 0.78f), true, 0.8f);
                    Point(new Vector3(0, 0, D - 3.2f), r ? "ЗАКАЗАТЬ ЕДУ" : "МЕНЮ КАФЕ", () => Menus.Food(b, r ? FoodDef.Restaurant : FoodDef.Cafe));
                    Sign(new Vector3(0, 3.1f, D - 0.4f), b.name, accent, 4);
                    break;
                }

                case BType.Mall:
                {
                    Room(40, 32, 7f, tiles, new Color(0.92f, 0.92f, 0.95f));
                    // shop fronts along the walls
                    (string title, Color col, Action act)[] stores =
                    {
                        ("ОДЕЖДА", new Color(0.9f, 0.3f, 0.6f), () => Menus.Dress(true)),
                        ("МЕБЕЛЬ", new Color(0.6f, 0.45f, 0.3f), () => Menus.Decor()),
                        ("ФУДКОРТ", new Color(0.95f, 0.5f, 0.1f), () => Menus.Food(b, FoodDef.Cafe)),
                        ("ПРОДУКТЫ", new Color(0.2f, 0.6f, 0.3f), () => Menus.Food(b, FoodDef.Shop)),
                    };
                    for (int i = 0; i < stores.Length; i++)
                    {
                        float x = -15 + i * 10;
                        Box(new Vector3(x - 4, 0, D - 6), new Vector3(x + 4, 4, D - 5.8f), stores[i].col * 0.9f);
                        BoxM(new Vector3(x - 3.2f, 0.2f, D - 6.05f), new Vector3(x + 3.2f, 3.4f, D - 5.95f), Mats.Glass(new Color(0.6f, 0.8f, 0.9f, 0.4f)));
                        Sign(new Vector3(x, 4.6f, D - 6.1f), stores[i].title, stores[i].col, 6);
                        var act = stores[i].act;
                        Point(new Vector3(x, 0, D - 7.2f), stores[i].title, act);
                        if (i == 0) { Rack(new Vector3(x - 1.7f, 0, D - 3.2f), 2.6f, true, b.id); Rack(new Vector3(x + 1.7f, 0, D - 3.2f), 2.6f, true, b.id + 1); Stock(new Vector3(x, 0, D - 0.4f), 7f, true, i % 3, b.id); Mannequin(new Vector3(x + 3.2f, 0, D - 7.6f), 200, b.id); }
                        else Shelf(new Vector3(x, 0, D - 3), 6, true, b.id + i);
                        Npc(new Vector3(x + 2, 0, D - 7), 180, b.id * 7 + i);
                    }
                    // fountain and benches
                    Mesh(PrimitiveType.Cylinder, new Vector3(0, 0.3f, 12), new Vector3(6, 0.3f, 6), new Color(0.8f, 0.8f, 0.82f));
                    Mesh(PrimitiveType.Cylinder, new Vector3(0, 0.62f, 12), new Vector3(5.4f, 0.02f, 5.4f), new Color(0.3f, 0.6f, 0.85f));
                    Mesh(PrimitiveType.Cylinder, new Vector3(0, 1.2f, 12), new Vector3(0.6f, 1f, 0.6f), new Color(0.85f, 0.85f, 0.88f));
                    Col(new Vector3(0, 0.6f, 12), new Vector3(5.4f, 1.2f, 5.4f), 12);
                    for (int i = 0; i < 6; i++) Plant(new Vector3(-16 + i * 6.4f, 0, 4), 1.4f);
                    for (int i = 0; i < 4; i++) Npc(new Vector3(-10 + i * 6, 0, 8 + (i % 2) * 6), i * 70, b.id + 100 + i);
                    Sign(new Vector3(0, 6f, D - 0.4f), b.name, accent, 8);
                    break;
                }

                case BType.CarDealer:
                {
                    Room(30, 24, 6f, dark, new Color(0.9f, 0.92f, 0.95f));
                    int i = 0;
                    foreach (var def in CarDef.All)
                    {
                        if (i >= 6) break;
                        float x = -10 + (i % 3) * 10, z = 7 + (i / 3) * 8;
                        Mesh(PrimitiveType.Cylinder, new Vector3(x, 0.05f, z), new Vector3(6, 0.05f, 6), new Color(0.8f, 0.8f, 0.85f));
                        DisplayCar(new Vector3(x, 0.1f, z), 30 + i * 25, def, (b.id + i) % Palette.Car.Length);
                        i++;
                    }
                    Counter(new Vector3(10, 0, D - 3), 4, accent);
                    Npc(new Vector3(10, 0, D - 2), 180, b.id, 0, 3);
                    Point(new Vector3(10, 0, D - 4.2f), "КУПИТЬ АВТОМОБИЛЬ", () => Menus.CarDealer(b));
                    Point(new Vector3(-10, 0, 4), "КАТАЛОГ МАШИН", () => Menus.CarDealer(b));
                    Sign(new Vector3(0, 5f, D - 0.4f), b.name, accent, 6);
                    break;
                }

                case BType.Garage:
                {
                    Room(20, 16, 5.5f, concrete, new Color(0.7f, 0.72f, 0.75f));
                    // lift with a car on it
                    Box(new Vector3(-6, 0, 6), new Vector3(-5.6f, 2.2f, 12), new Color(0.9f, 0.6f, 0.1f), true);
                    Box(new Vector3(-0.4f, 0, 6), new Vector3(0f, 2.2f, 12), new Color(0.9f, 0.6f, 0.1f), true);
                    DisplayCar(new Vector3(-3, 1.7f, 9), 0, CarDef.All[rng.I(CarDef.All.Count)], rng.I(10));
                    for (int k = 0; k < 4; k++) Box(new Vector3(W / 2 - 1, 0, 3 + k * 2.5f), new Vector3(W / 2 - 0.3f, 1.8f, 5 + k * 2.5f), new Color(0.75f, 0.1f, 0.1f), true, 0.6f);
                    for (int k = 0; k < 6; k++) Mesh(PrimitiveType.Cylinder, new Vector3(4 + k * 0.8f, 0.35f, D - 1), new Vector3(0.7f, 0.15f, 0.7f), new Color(0.08f, 0.08f, 0.08f));
                    Npc(new Vector3(-3, 0, 5), 0, b.id, 1);
                    Counter(new Vector3(5, 0, 5), 3, accent);
                    Point(new Vector3(5, 0, 3.8f), "РЕМОНТ И ТЮНИНГ", () => Menus.Garage(b));
                    Sign(new Vector3(0, 4.4f, D - 0.4f), b.name, accent, 5);
                    break;
                }

                case BType.Hospital:
                    Room(24, 18, 4f, tiles, new Color(0.9f, 0.95f, 0.95f));
                    Counter(new Vector3(0, 0, 6), 6, new Color(0.85f, 0.85f, 0.88f));
                    Npc(new Vector3(0, 0, 7), 180, b.id, 0);
                    for (int k = 0; k < 4; k++) { Bed(new Vector3(-9 + k * 6, 0, 14), 0, new Color(0.7f, 0.85f, 0.95f)); }
                    for (int k = 0; k < 6; k++) Chair(new Vector3(-9 + k * 1, 0, 2.5f), 0, new Color(0.3f, 0.55f, 0.75f));
                    Sign(new Vector3(0, 3.2f, D - 0.4f), "+ " + b.name, new Color(0.9f, 0.1f, 0.1f), 5);
                    Point(new Vector3(0, 0, 4.8f), "РЕГИСТРАТУРА: ЛЕЧЕНИЕ", () => Menus.Hospital(b));
                    break;

                case BType.FireStation:
                    Room(22, 18, 6f, concrete, new Color(0.85f, 0.85f, 0.82f));
                    // fire truck
                    Box(new Vector3(-6, 0.6f, 4), new Vector3(-3, 3.4f, 13), new Color(0.85f, 0.1f, 0.1f), true, 0.6f);
                    Box(new Vector3(-6, 3.4f, 5), new Vector3(-3, 3.6f, 12), new Color(0.75f, 0.75f, 0.78f));
                    for (int k = 0; k < 3; k++) Mesh(PrimitiveType.Cylinder, new Vector3(-6.05f, 0.55f, 5 + k * 3.5f), new Vector3(1.1f, 0.2f, 1.1f), Color.black);
                    for (int k = 0; k < 6; k++) Box(new Vector3(W / 2 - 0.8f, 0, 3 + k * 1.2f), new Vector3(W / 2 - 0.2f, 2, 4 + k * 1.2f), new Color(0.6f, 0.62f, 0.65f), true, 0.6f);
                    Npc(new Vector3(4, 0, 8), 200, b.id, 0, 1);
                    Point(new Vector3(4, 0, 6.5f), "ДЕЖУРНЫЙ: РАБОТА", () => Menus.Building(b));
                    break;

                case BType.School:
                case BType.University:
                {
                    Room(20, 16, 3.8f, wood, new Color(0.85f, 0.9f, 0.85f));
                    Box(new Vector3(-5, 1, D - 0.35f), new Vector3(5, 2.6f, D - 0.25f), new Color(0.1f, 0.3f, 0.2f));
                    for (int r = 0; r < 3; r++)
                        for (int c = 0; c < 4; c++)
                        {
                            var p = new Vector3(-6 + c * 4, 0, 4 + r * 3);
                            Box(p + new Vector3(-0.7f, 0.7f, -0.35f), p + new Vector3(0.7f, 0.75f, 0.35f), new Color(0.6f, 0.45f, 0.3f), true);
                            Chair(p + new Vector3(0, 0, -0.8f), 0, new Color(0.2f, 0.4f, 0.7f));
                            if ((r + c) % 2 == 0) Npc(p + new Vector3(0, 0, -0.8f), 0, b.id + r * 4 + c, 2);
                        }
                    Npc(new Vector3(3, 0, D - 1.5f), 180, b.id + 50, 3, 3);
                    Point(new Vector3(0, 0, 2.5f), b.type == BType.School ? "УЧИТЬСЯ В ШКОЛЕ" : "УЧИТЬСЯ В УНИВЕРСИТЕТЕ", () => Menus.Study(b));
                    Point(new Vector3(7, 0, 2.5f), "РАБОТА", () => Menus.Building(b));
                    break;
                }

                case BType.Library:
                    Room(20, 16, 4.5f, wood, new Color(0.75f, 0.65f, 0.5f));
                    for (int k = 0; k < 4; k++) Shelf(new Vector3(-7 + k * 4.5f, 0, 11), 3.2f, true, b.id + k);
                    for (int k = 0; k < 3; k++) Table(new Vector3(-5 + k * 5, 0, 5), new Color(0.45f, 0.3f, 0.2f));
                    Counter(new Vector3(7, 0, 3), 2.5f, new Color(0.4f, 0.28f, 0.18f), false);
                    Npc(new Vector3(8.2f, 0, 3), 270, b.id, 0);
                    Point(new Vector3(0, 0, 3), "ЧИТАТЬ КНИГИ (+ОБРАЗОВАНИЕ)", () => Menus.Study(b));
                    Point(new Vector3(5.8f, 0, 3), "БИБЛИОТЕКАРЬ: РАБОТА", () => Menus.Building(b));
                    break;

                case BType.Factory:
                case BType.Warehouse:
                {
                    bool f = b.type == BType.Factory;
                    Room(30, 24, 8f, concrete, new Color(0.6f, 0.62f, 0.6f), false);
                    if (f)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            Box(new Vector3(-10 + k * 9, 0, 6), new Vector3(-6 + k * 9, 3f, 10), new Color(0.3f, 0.5f, 0.75f), true, 0.5f);
                            Box(new Vector3(-12, 0.8f, 12 + k * 0.01f), new Vector3(12, 1f, 13.5f), new Color(0.15f, 0.15f, 0.15f), true);
                            Npc(new Vector3(-8 + k * 9, 0, 11), 180, b.id + k, 1, 1);
                        }
                    }
                    else
                    {
                        for (int r = 0; r < 3; r++)
                            for (int k = 0; k < 4; k++)
                            {
                                var p = new Vector3(-10 + k * 6.5f, 0, 7 + r * 5);
                                BoxM(p + new Vector3(-2.5f, 0, -0.6f), p + new Vector3(2.5f, 0.15f, 0.6f), WorldBuilder.Wood);
                                Box(p + new Vector3(-2.2f, 0.15f, -0.5f), p + new Vector3(2.2f, 1.4f + rng.F() * 1.5f, 0.5f), new Color(0.65f, 0.5f, 0.3f), true);
                            }
                        Npc(new Vector3(8, 0, 4), 200, b.id, 1, 1);
                    }
                    Point(new Vector3(-6, 0, 3), "НАЧАЛЬНИК СМЕНЫ: РАБОТА", () => Menus.Building(b));
                    Sign(new Vector3(0, 6f, D - 0.4f), b.name, Color.white, 6);
                    break;
                }

                case BType.Bank:
                    Room(18, 14, 5f, Mats.Tex(Tex.Tiles("marble", new Color(0.9f, 0.88f, 0.85f), new Color(0.75f, 0.72f, 0.7f), 2), Color.white, 0.8f), new Color(0.85f, 0.82f, 0.75f));
                    for (int k = 0; k < 3; k++)
                    {
                        Counter(new Vector3(-5 + k * 5, 0, 9), 3.5f, new Color(0.3f, 0.22f, 0.15f));
                        BoxM(new Vector3(-6.7f + k * 5, 1.1f, 8.8f), new Vector3(-3.3f + k * 5, 2.4f, 8.85f), Mats.Glass(new Color(0.7f, 0.85f, 0.9f, 0.35f)));
                        Npc(new Vector3(-5 + k * 5, 0, 10), 180, b.id + k, 0, 3);
                    }
                    Point(new Vector3(0, 0, 7.6f), "БАНК: ВКЛАД", () => Menus.Bank(b));
                    Sign(new Vector3(0, 4f, D - 0.4f), b.name, new Color(0.85f, 0.7f, 0.3f), 5);
                    break;

                case BType.JobCenter:
                    Room(16, 12, 3.6f, tiles, new Color(0.85f, 0.92f, 0.98f));
                    for (int k = 0; k < 3; k++)
                    {
                        Table(new Vector3(-4 + k * 4, 0, 7), new Color(0.9f, 0.9f, 0.9f), false);
                        Npc(new Vector3(-4 + k * 4, 0, 8.2f), 180, b.id + k, 0, 3);
                    }
                    Point(new Vector3(0, 0, 5.5f), "ВСЕ ВАКАНСИИ ГОРОДА", () => Menus.AllJobs());
                    Sign(new Vector3(0, 3f, D - 0.4f), "ЦЕНТР ЗАНЯТОСТИ", accent, 5);
                    break;

                case BType.Hotel:
                    Room(18, 14, 4.5f, dark, new Color(0.75f, 0.65f, 0.5f));
                    Counter(new Vector3(0, 0, 9), 6, new Color(0.35f, 0.25f, 0.18f));
                    Npc(new Vector3(-1, 0, 10), 180, b.id, 0, 3);
                    Sofa(new Vector3(-6, 0, 4), 90, new Color(0.55f, 0.15f, 0.2f));
                    Sofa(new Vector3(6, 0, 4), 270, new Color(0.55f, 0.15f, 0.2f));
                    Plant(new Vector3(-7.5f, 0, 12)); Plant(new Vector3(-7.5f, 0, 2));
                    Point(new Vector3(0, 0, 7.8f), "РЕСЕПШН: НОМЕР", () => Menus.Hotel(b));
                    Sign(new Vector3(0, 3.8f, D - 0.4f), b.name, new Color(0.9f, 0.75f, 0.3f), 5);
                    break;

                case BType.Gym:
                    Room(20, 16, 4.5f, Mats.Tex(Tex.Tiles("rubber", new Color(0.18f, 0.18f, 0.2f), new Color(0.12f, 0.12f, 0.14f), 2), Color.white, 0.2f), new Color(0.85f, 0.85f, 0.88f));
                    for (int k = 0; k < 4; k++)
                    {
                        Box(new Vector3(-8 + k * 4, 0, 10), new Vector3(-6.6f + k * 4, 1.2f, 13), new Color(0.15f, 0.15f, 0.17f), true);
                        Npc(new Vector3(-7.3f + k * 4, 1.2f, 11.5f), 0, b.id + k, k % 2 == 0 ? 0 : 1, 0);
                    }
                    for (int k = 0; k < 5; k++) Mesh(PrimitiveType.Cylinder, new Vector3(5 + k * 0.9f, 0.3f, 5), new Vector3(0.6f, 0.05f, 0.6f), new Color(0.1f, 0.1f, 0.1f));
                    Point(new Vector3(0, 0, 6), "ТРЕНИРОВКА (−20 энергии)", () => Menus.Gym(b));
                    break;

                case BType.GunShop:
                {
                    Room(16, 14, 3.8f, tiles, new Color(0.3f, 0.3f, 0.33f));
                    // glass counters with pistols on show
                    for (int k = 0; k < 3; k++)
                    {
                        var cpos = new Vector3(-4.5f + k * 4.5f, 0, 4.2f);
                        Box(cpos + new Vector3(-1.8f, 0, -0.45f), cpos + new Vector3(1.8f, 0.9f, 0.45f), new Color(0.12f, 0.12f, 0.14f), true);
                        BoxM(cpos + new Vector3(-1.8f, 0.9f, -0.45f), cpos + new Vector3(1.8f, 0.92f, 0.45f), Mats.Glass(new Color(0.7f, 0.85f, 0.95f, 0.3f)));
                        for (int g = 0; g < 2; g++)
                        {
                            var gd = GunDef.All[(k * 2 + g) % GunDef.All.Count];
                            var gun = Guns.Model(gd, Ctr, out _);
                            gun.transform.localPosition = cpos + new Vector3(-0.7f + g * 1.4f, 0.93f, 0);
                            gun.transform.localRotation = Quaternion.Euler(0, 90, 90);
                            gun.transform.localScale = Vector3.one * 1.6f;
                        }
                    }
                    // wall boards with pistols, a sign, the cashier
                    for (int k = 0; k < 6; k++)
                    {
                        var gd = GunDef.All[k % GunDef.All.Count];
                        var gun = Guns.Model(gd, Ctr, out _);
                        gun.transform.localPosition = new Vector3(-W / 2 + 0.12f, 1.5f + (k % 2) * 0.6f, 3f + (k / 2) * 1.4f);
                        gun.transform.localRotation = Quaternion.Euler(0, 0, 90);
                        gun.transform.localScale = Vector3.one * 1.8f;
                    }
                    Box(new Vector3(-W / 2, 1.1f, 2.2f), new Vector3(-W / 2 + 0.08f, 2.4f, 7.2f), new Color(0.35f, 0.25f, 0.15f), true);
                    Npc(new Vector3(0, 0, 5.4f), 180, b.id, 0);
                    Point(new Vector3(0, 0, 3.0f), "КАССА: КУПИТЬ ПИСТОЛЕТ", GunMenus.Shop);
                    // the shooting range behind a glass wall: lanes with round targets
                    Box(new Vector3(-W / 2, 0, 8.6f), new Vector3(W / 2, 1.1f, 8.8f), new Color(0.2f, 0.2f, 0.22f), true);
                    for (int k = 0; k < 4; k++)
                    {
                        float tx = -5.25f + k * 3.5f;
                        var tg = Prim.Make(PrimitiveType.Cylinder, Ctr, new Vector3(tx, 1.5f, D - 0.5f), new Vector3(0.8f, 0.02f, 0.8f), Mats.Tex(Tex.Target(), Color.white, 0.2f), "Target");
                        tg.transform.localRotation = Quaternion.Euler(90, 0, 0);
                        tg.layer = 10;
                        tg.AddComponent<RangeTarget>();
                        Box(new Vector3(tx - 0.03f, 0, D - 0.55f), new Vector3(tx + 0.03f, 1.1f, D - 0.45f), new Color(0.3f, 0.3f, 0.3f));
                    }
                    Point(new Vector3(0, 0, 7.6f), "ТИР: ВЗЯТЬ ПИСТОЛЕТ И СТРЕЛЯТЬ", () =>
                    {
                        if (Save.D.guns.Count == 0) { GunMenus.Shop(); return; }
                        if (!Guns.Armed) Guns.Toggle();
                        PlayerCtl.I.Yaw = 0; PlayerCtl.I.Pitch = 4; PlayerCtl.I.Dist = 2.4f;
                        Hud.Toast("Цельтесь в мишени и жмите «ОГОНЬ»", UIKit.Cyan);
                    });
                    Sign(new Vector3(0, 3.3f, 2.2f), b.name, accent, 4);
                    break;
                }

                case BType.Clothes:
                    Room(18, 15, 4f, wood, new Color(0.97f, 0.95f, 0.93f));
                    ClothingShop(new Vector3(0, 0, 0.6f), 16f, 13f, b.id);
                    // fitting rooms with curtains on the right wall
                    for (int k = 0; k < 3; k++)
                    {
                        float z = 3.5f + k * 1.6f;
                        Box(new Vector3(7.6f, 0, z - 0.75f), new Vector3(9f, 2.4f, z - 0.7f), new Color(0.9f, 0.9f, 0.88f), true);
                        BoxM(new Vector3(7.55f, 0.1f, z - 0.7f), new Vector3(7.6f, 2.3f, z + 0.7f), Fabric(k + 3));
                    }
                    Box(new Vector3(7.6f, 0, 8.65f), new Vector3(9f, 2.4f, 8.7f), new Color(0.9f, 0.9f, 0.88f), true);
                    Counter(new Vector3(-6.5f, 0, 3f), 2.4f, new Color(0.15f, 0.15f, 0.17f), false);
                    Npc(new Vector3(-7.6f, 0, 3f), 90, b.id, 0);
                    Point(new Vector3(6.6f, 0, 5.1f), "ПРИМЕРОЧНАЯ: ПРИМЕРИТЬ И КУПИТЬ", () => Menus.Dress(true));
                    Point(new Vector3(-5.2f, 0, 3f), "КАССА: ГОТОВЫЕ ОБРАЗЫ (СКИНЫ)", () => Menus.Skins());
                    Sign(new Vector3(0, 3.4f, 14.6f), b.name, b.Info.Sign, 5);
                    break;

                case BType.Furniture:
                    Room(18, 14, 4f, wood, new Color(0.92f, 0.9f, 0.85f));
                    Sofa(new Vector3(-5, 0, 5), 0, new Color(0.3f, 0.4f, 0.55f));
                    Bed(new Vector3(4, 0, 9), 180, new Color(0.8f, 0.7f, 0.6f));
                    Table(new Vector3(-4, 0, 10), new Color(0.6f, 0.45f, 0.3f));
                    Tv(new Vector3(5, 0, 3), 0);
                    Plant(new Vector3(7.5f, 0, 1.5f));
                    Npc(new Vector3(0, 0, 7), 180, b.id, 0);
                    Point(new Vector3(0, 0, 5.5f), "КАТАЛОГ МЕБЕЛИ", () => Menus.Decor());
                    break;

                case BType.Parking:
                {
                    Room(36, 30, 3.2f, Mats.Tex(Tex.Asphalt(), new Color(0.8f, 0.8f, 0.8f)), new Color(0.6f, 0.6f, 0.6f), false);
                    for (float x = -12; x <= 12; x += 8)
                        for (float z = 6; z < 28; z += 8)
                        {
                            Box(new Vector3(x - 0.3f, 0, z - 0.3f), new Vector3(x + 0.3f, 3.2f, z + 0.3f), new Color(0.65f, 0.65f, 0.62f), true);
                            Box(new Vector3(x - 0.31f, 0, z - 0.31f), new Vector3(x + 0.31f, 0.8f, z + 0.31f), new Color(0.95f, 0.8f, 0.1f));
                        }
                    for (int k = 0; k < 9; k++)
                    {
                        float x = -14 + k * 3.5f;
                        BoxM(new Vector3(x, 0.01f, 22), new Vector3(x + 0.1f, 0.02f, 27), WorldBuilder.Paint);
                        if (rng.Chance(0.6f)) DisplayCar(new Vector3(x + 1.75f, 0.02f, 24.5f), 180, CarDef.All[rng.I(6)], rng.I(10));
                    }
                    Point(new Vector3(0, 0, 3), "ЗАБРАТЬ МОЮ МАШИНУ", () => Menus.Garage(b, true));
                    Point(new Vector3(6, 0, 3), "ПАРКИНГ КАК БИЗНЕС", () => Menus.Building(b));
                    break;
                }

                case BType.Airport:
                case BType.Station:
                case BType.Port:
                {
                    Room(34, 22, b.type == BType.Airport ? 9f : 7f, tiles, b.type == BType.Port ? new Color(0.7f, 0.75f, 0.8f) : new Color(0.92f, 0.94f, 0.96f));
                    for (int r = 0; r < 3; r++)
                        for (int k = 0; k < 8; k++) Chair(new Vector3(-12 + k * 1.1f, 0, 6 + r * 3), 0, new Color(0.2f, 0.4f, 0.7f));
                    Counter(new Vector3(8, 0, 12), 8, accent);
                    Npc(new Vector3(6, 0, 13), 180, b.id, 0, 3); Npc(new Vector3(10, 0, 13), 180, b.id + 1, 0, 3);
                    BoxM(new Vector3(-6, 4, D - 0.4f), new Vector3(6, 6.4f, D - 0.3f), Mats.Glow(new Color(0.05f, 0.07f, 0.12f)));
                    Sign(new Vector3(0, 5.5f, D - 0.45f), b.type == BType.Airport ? "ВЫЛЕТЫ · ПО ВРЕМЕНИ" : b.type == BType.Station ? "ПОЕЗДА · ПО РАСПИСАНИЮ" : "РЕЙСЫ · ПО РАСПИСАНИЮ", new Color(1f, 0.75f, 0.2f), 5);
                    for (int k = 0; k < 6; k++) Npc(new Vector3(-12 + k * 2.2f, 0, 6 + (k % 3) * 3), 0, b.id + 10 + k, 2);
                    Point(new Vector3(8, 0, 10.8f), "КАССА: БИЛЕТЫ (БЫСТРАЯ ПОЕЗДКА)", () => Menus.Travel(b));
                    Point(new Vector3(-6, 0, 3), "РАБОТА", () => Menus.Building(b));
                    break;
                }

                case BType.Agency:
                {
                    var marble = Mats.Tex(Tex.Tiles("marble", new Color(0.9f, 0.88f, 0.85f), new Color(0.75f, 0.72f, 0.7f), 2), Color.white, 0.85f);
                    Room(30, 22, 7f, marble, new Color(0.1f, 0.12f, 0.16f));
                    // glowing property ads along the walls
                    for (int k = 0; k < 4; k++)
                    {
                        float z = 4 + k * 4.2f;
                        BoxM(new Vector3(-14.9f, 1.4f, z - 1.6f), new Vector3(-14.85f, 3.6f, z + 1.6f), Mats.Glow(Color.Lerp(new Color(0.15f, 0.5f, 0.9f), new Color(0.9f, 0.3f, 0.7f), k / 3f)));
                        Sign(new Vector3(-14.8f, 2.5f, z), k switch { 0 => "ПЕНТХАУСЫ\nот $260 000", 1 => "ВИЛЛЫ У МОРЯ\nот $600 000", 2 => "КВАРТИРЫ\nот $35 000", _ => "ДОМА\nот $90 000" }, Color.white, 2.2f, 90);
                    }
                    // a model of the whole city in the middle of the hall
                    var tableTop = new Vector3(2, 0.9f, 10);
                    Box(tableTop + new Vector3(-3.3f, -0.9f, -3.3f), tableTop + new Vector3(3.3f, 0, 3.3f), new Color(0.08f, 0.09f, 0.12f), true, 0.8f);
                    BoxM(tableTop + new Vector3(-3.1f, 0, -3.1f), tableTop + new Vector3(3.1f, 0.01f, 3.1f), Mats.Glow(new Color(0.05f, 0.15f, 0.25f)));
                    float k2 = 6.0f / City.Size;
                    foreach (var cb in City.All)
                    {
                        if (cb.cx < 0) continue;
                        var mn = new Vector3((cb.rect.xMin - City.Size / 2) * k2, 0.01f, (cb.rect.yMin - City.Size / 2) * k2);
                        var mx = new Vector3((cb.rect.xMax - City.Size / 2) * k2, 0.01f + cb.height * k2 * 2.5f, (cb.rect.yMax - City.Size / 2) * k2);
                        BoxM(tableTop + mn, tableTop + mx, cb.type == BType.Agency ? Mats.Glow(new Color(0.2f, 0.95f, 1f)) : Mats.Lit(MapTex.Cat(cb.type), 0.6f));
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        Counter(new Vector3(-8 + k * 8, 0, 17.5f), 4, new Color(0.12f, 0.14f, 0.2f));
                        Npc(new Vector3(-8 + k * 8, 0, 18.6f), 180, b.id + k, 0, 3);
                        BoxM(new Vector3(-9.2f + k * 8, 1.1f, 17.6f), new Vector3(-8.4f + k * 8, 1.6f, 17.65f), Mats.Glow(new Color(0.3f, 0.6f, 1f)));
                    }
                    Sofa(new Vector3(9, 0, 4), 270, new Color(0.9f, 0.9f, 0.92f)); Sofa(new Vector3(-6, 0, 4), 90, new Color(0.9f, 0.9f, 0.92f));
                    for (int k = 0; k < 4; k++) Npc(new Vector3(-4 + k * 3, 0, 6 + (k % 2) * 2), k * 90, b.id + 40 + k, 0);
                    Plant(new Vector3(-13.5f, 0, 20.5f), 1.4f); Plant(new Vector3(13.5f, 0, 1.5f), 1.4f);
                    Sign(new Vector3(0, 5.6f, 21.8f), "HERBAL CITY · НЕДВИЖИМОСТЬ", new Color(0.2f, 0.9f, 1f), 7);
                    Sign(new Vector3(EX, 3.4f, 21.85f), "ЛИФТ · 20 ЭТАЖЕЙ", Color.white, 2.5f);
                    for (int k = 0; k < 3; k++) Point(new Vector3(-8 + k * 8, 0, 16.2f), "АГЕНТ: КУПИТЬ ЖИЛЬЁ", () => Menus.RealEstate());
                    Point(new Vector3(2, 0, 6), "МАКЕТ ГОРОДА: ВСЁ ЖИЛЬЁ", () => Menus.RealEstate());
                    break;
                }

                case BType.Office:
                default:
                    Room(22, 16, 3.6f, Mats.Tex(Tex.Tiles("carpet", new Color(0.35f, 0.4f, 0.5f), new Color(0.32f, 0.37f, 0.47f), 2), Color.white, 0.05f), new Color(0.92f, 0.93f, 0.95f));
                    for (int r = 0; r < 3; r++)
                        for (int c = 0; c < 4; c++)
                        {
                            var p = new Vector3(-7.5f + c * 5, 0, 5 + r * 3.6f);
                            Box(p + new Vector3(-0.9f, 0.72f, -0.45f), p + new Vector3(0.9f, 0.77f, 0.45f), new Color(0.9f, 0.9f, 0.88f), true);
                            Box(p + new Vector3(-0.35f, 0.77f, 0.1f), p + new Vector3(0.35f, 1.2f, 0.14f), new Color(0.08f, 0.08f, 0.1f));
                            Chair(p + new Vector3(0, 0, -0.75f), 0, new Color(0.15f, 0.15f, 0.17f));
                            if ((r * 4 + c) % 3 != 0) Npc(p + new Vector3(0, 0, -0.75f), 0, b.id + r * 4 + c, 2, 3);
                        }
                    Plant(new Vector3(-10, 0, 14)); Plant(new Vector3(10, 0, 14));
                    Point(new Vector3(0, 0, 2.5f), "РЕСЕПШН: РАБОТА И БИЗНЕС", () => Menus.Building(b));
                    Sign(new Vector3(0, 3f, D - 0.4f), b.name, accent, 4);
                    break;
            }
            Finish(b);
        }

        static void Finish(Bld b)
        {
            // every building: jobs/business desk if not placed explicitly by the layout
            bool hasBuilding = Points.Exists(p => p.Label.Contains("РАБОТА") || p.Label.Contains("БИЗНЕС"));
            bool canWork = JobDef.All.Exists(j => j.Where == b.type) || b.BizPrice > 0;
            if (!hasBuilding && canWork && Unit == 0)
                Point(new Vector3(-W / 2 + 1.5f, 0, 1.5f), "ВАКАНСИИ И ВЛАДЕЛЕЦ", () => Menus.Building(b));
            // shift work point for my job here
            var job = JobDef.Get(Save.D.job);
            if (job != null && job.Kind == 0 && job.Where == b.type && Unit == 0)
                Point(new Vector3(W / 2 - 1.5f, 0, 1.5f), "НАЧАТЬ СМЕНУ: " + job.Title.ToUpper(), () => Work.StartShift(job, b));
            Flush();
        }

        /// <summary>Builds the merged mesh of the current container and the floor markers of its new action points.</summary>
        static void Flush()
        {
            var go = mb.Build("Interior mesh", Ctr, 12, true);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = pointsFlushed; i < Points.Count; i++)
            {
                var m = Prim.Make(PrimitiveType.Cylinder, Level, Level.InverseTransformPoint(Points[i].Pos) + Vector3.up * 0.02f, new Vector3(0.9f, 0.01f, 0.9f), Mats.Glow(new Color(0.2f, 0.85f, 1f)), "Spot");
                m.AddComponent<Pulse>();
            }
            pointsFlushed = Points.Count;
            mb = new MeshB();
        }

        // ------------------------------------------------------------------ homes

        static readonly string[] Owners =
        {
            "Айгерим Н.", "Ерлан К.", "Мария С.", "Даурен А.", "Олег П.", "Асель Т.", "Тимур Б.", "Наталья В.", "Арман Ж.", "Камила Р.",
            "Сергей Л.", "Дина М.", "Бауыржан О.", "Алия Е.", "Максим Г.", "Жанна И.", "Нурлан С.", "Екатерина Д.", "Руслан Х.", "Мадина Ш.",
        };

        /// <summary>0 someone else's (locked), 1 for sale (open for viewing), 2 the player's.</summary>
        public static int FlatStatus(Bld b, int unit) => Eco.OwnsHome(b.id, unit) ? 2 : H.R(100, b.id, unit, 5) < 35 ? 1 : 0;
        public static string OwnerName(Bld b, int unit) => H.Pick(Owners, b.id, unit, 9);

        static void UpperFloor(Bld b, int floor)
        {
            if (b.type == BType.Agency && floor >= 19) { OpenFloor(b, floor, true); return; }
            bool office = b.type == BType.Office || b.type == BType.Agency || b.type == BType.Corp || (b.type == BType.Tower && floor <= b.floors / 2);
            if (office) { OpenFloor(b, floor, false); return; }
            Corridor(b, floor, b.type == BType.Tower, b.type == BType.Hotel);
        }

        /// <summary>A corridor with five doors: every flat (hotel room) belongs to someone — maybe to you.</summary>
        static void Corridor(Bld b, int floor, bool lux, bool hotel)
        {
            float cz0 = EZ - 4.6f, czN = EZ + 1.15f, x0 = EX - 17.7f, x1 = EX + 17.7f, h = 3.0f;
            var carpet = lux ? Mats.Tex(Tex.Tiles("marble2", new Color(0.88f, 0.86f, 0.82f), new Color(0.74f, 0.72f, 0.7f), 2), Color.white, 0.8f)
                             : Mats.Tex(Tex.Tiles("carpet2", new Color(0.45f, 0.2f, 0.22f), new Color(0.4f, 0.17f, 0.19f), 2), Color.white, 0.05f);
            var wallM = Mats.Lit(lux ? new Color(0.92f, 0.92f, 0.9f) : new Color(0.9f, 0.86f, 0.78f), 0.15f);
            var tile = new Vector2(4, 4);
            mb.Box(wallM, carpet, new Vector3(x0, -0.3f, cz0), new Vector3(x1, 0, czN), tile);
            mb.Box(wallM, wallM, new Vector3(x0, h, cz0), new Vector3(x1, h + 0.2f, czN), tile, default, true, Mats.Lit(new Color(0.95f, 0.95f, 0.93f)));
            mb.Box(wallM, wallM, new Vector3(x0 - 0.3f, 0, czN), new Vector3(x1 + 0.3f, h, czN + 0.3f), tile);
            mb.Box(wallM, wallM, new Vector3(x0 - 0.3f, 0, cz0 - 0.3f), new Vector3(x0, h, czN), tile);
            mb.Box(wallM, wallM, new Vector3(x1, 0, cz0 - 0.3f), new Vector3(x1 + 0.3f, h, czN), tile);
            var glow = Mats.Glow(DayNight.IsNight ? new Color(0.1f, 0.12f, 0.25f) : new Color(0.65f, 0.82f, 0.98f));
            mb.Box(glow, null, new Vector3(x0 + 0.01f, 0.9f, cz0 + 0.8f), new Vector3(x0 + 0.02f, 2.5f, czN - 0.8f), tile);
            mb.Box(glow, null, new Vector3(x1 - 0.02f, 0.9f, cz0 + 0.8f), new Vector3(x1 - 0.01f, 2.5f, czN - 0.8f), tile);
            var lamp = Mats.Glow(new Color(1f, 0.96f, 0.85f));
            for (float x = x0 + 2; x < x1 - 1; x += 4) mb.Box(lamp, lamp, new Vector3(x - 0.3f, h - 0.04f, cz0 + 1.2f), new Vector3(x + 0.3f, h, cz0 + 1.8f), new Vector2(1, 1));
            Col(new Vector3(EX, -0.15f, (cz0 + czN) / 2), new Vector3(x1 - x0 + 1, 0.3f, czN - cz0 + 1), 13);
            Col(new Vector3(EX, h / 2, czN + 0.15f), new Vector3(x1 - x0, h, 0.3f), 10);
            Col(new Vector3(x0 - 0.15f, h / 2, (cz0 + czN) / 2), new Vector3(0.3f, h, czN - cz0), 10);
            Col(new Vector3(x1 + 0.15f, h / 2, (cz0 + czN) / 2), new Vector3(0.3f, h, czN - cz0), 10);
            Col(new Vector3(EX, h + 0.1f, (cz0 + czN) / 2), new Vector3(x1 - x0, 0.2f, czN - cz0), 10);
            for (int k = -1; k <= 1; k++) Light(new Vector3(EX + k * 11, h - 0.4f, cz0 + 1.6f), 12f, 1.1f);
            Plant(new Vector3(x0 + 1, 0, czN - 0.8f)); Plant(new Vector3(x1 - 1, 0, czN - 0.8f));
            Sign(new Vector3(EX + 2.6f, 2.2f, czN - 0.05f), "ЭТАЖ " + (floor + 1), lux ? new Color(0.85f, 0.7f, 0.3f) : Color.white, 3, 180);
            Flush();

            for (int k = 0; k < 5; k++)
            {
                float xk = EX + (k - 2) * 7f;
                int unit = floor * 10 + k + 1;
                int st = FlatStatus(b, unit);
                if (hotel) st = Eco.OwnsHome(b.id, unit) ? 2 : H.R(100, b.id, unit, 6) < 45 ? 1 : 0;
                string who = st == 2 ? (hotel ? "ВАШ НОМЕР" : "ВЫ") : st == 1 ? (hotel ? "СВОБОДЕН" : "ПРОДАЁТСЯ") : (hotel ? "ЗАНЯТ" : OwnerName(b, unit));
                var col = st == 2 ? new Color(0.3f, 1f, 0.5f) : st == 1 ? new Color(1f, 0.8f, 0.25f) : new Color(0.9f, 0.9f, 0.9f);
                string num = (hotel ? "№ " : "Кв. ") + ((floor + 1) * 100 + k + 1);
                Sign(new Vector3(xk + 1.25f, 1.8f, cz0 + 0.32f), num + "\n" + who, col, 1.5f, 180);
                if (st == 0)
                {
                    string name = OwnerName(b, unit);
                    Point(new Vector3(xk, 0, cz0 + 1.1f), "ПОСТУЧАТЬ: " + (hotel ? "НОМЕР ЗАНЯТ" : name), () =>
                    {
                        AudioFx.Play("door");
                        Hud.Toast(hotel ? "Гости отдыхают — не беспокоить" : H.R(3, (int)Time.time) == 0 ? name + ": «Кто там? Я занят(а), заходите позже!»" : "Никого нет дома. Владелец: " + name);
                    });
                }
                var c = new GameObject("Flat " + unit).transform;
                c.SetParent(Level, false);
                c.localPosition = new Vector3(xk, 0, cz0);
                c.localRotation = Quaternion.Euler(0, 180, 0);
                Ctr = c;
                rng = new Rng(H.U(b.id, unit, 77));
                if (hotel) HotelRoom(b, unit, st);
                else Home(b, unit, true, st, lux);
                FlatDoor.Make(c, new Vector3(0, 0, -0.15f), st > 0, lux ? new Color(0.25f, 0.18f, 0.12f) : new Color(0.55f, 0.38f, 0.24f));
                Flush();
                Ctr = Level;
            }
        }

        static void HotelRoom(Bld b, int unit, int st)
        {
            Room(6.8f, 9f, 3.0f, Mats.Tex(Tex.Tiles("carpet3", new Color(0.35f, 0.3f, 0.45f), new Color(0.32f, 0.27f, 0.41f), 2), Color.white, 0.05f), new Color(0.88f, 0.84f, 0.76f), true, 0.7f, false, 1);
            Bed(new Vector3(-1.2f, 0, 6.5f), 180, new Color(0.95f, 0.95f, 0.95f));
            Tv(new Vector3(-1.2f, 0, 2.2f), 0);
            Sofa(new Vector3(2.2f, 0, 4.5f), 270, new Color(0.45f, 0.25f, 0.3f));
            Plant(new Vector3(2.8f, 0, 8.2f));
            if (st == 1) Point(new Vector3(0, 0, 4f), "СНЯТЬ НОМЕР И ВЫСПАТЬСЯ ($250)", () => { if (Eco.Spend(250, "Номер в отеле")) Menus.Sleep(); });
            else if (st == 0) Npc(new Vector3(1.5f, 0, 5f), 200, b.id * 31 + unit, 0);
        }

        /// <summary>Open-plan office floor (or the sky lounge on the top floor of the agency).</summary>
        static void OpenFloor(Bld b, int floor, bool lounge)
        {
            var c = new GameObject("Floor").transform;
            c.SetParent(Level, false);
            c.localPosition = new Vector3(EX, 0, EZ + 1.15f);
            c.localRotation = Quaternion.Euler(0, 180, 0);
            Ctr = c;
            float w = 30, d = 18;
            if (lounge)
            {
                Room(w, d, 4.2f, Mats.Tex(Tex.Tiles("darkfloor", new Color(0.3f, 0.3f, 0.32f), new Color(0.22f, 0.22f, 0.24f), 2), Color.white, 0.6f), new Color(0.12f, 0.13f, 0.16f), false, 1.2f, false, 2);
                // panoramic windows on three sides
                var sky = Mats.Glow(DayNight.IsNight ? new Color(0.05f, 0.08f, 0.2f) : new Color(0.55f, 0.75f, 0.98f));
                mb.Box(sky, null, new Vector3(-w / 2 + 0.01f, 0.4f, 1), new Vector3(-w / 2 + 0.02f, 3.9f, d - 1), new Vector2(4, 4));
                mb.Box(sky, null, new Vector3(w / 2 - 0.02f, 0.4f, 1), new Vector3(w / 2 - 0.01f, 3.9f, d - 1), new Vector2(4, 4));
                mb.Box(sky, null, new Vector3(-w / 2 + 1, 0.4f, d - 0.02f), new Vector3(w / 2 - 1, 3.9f, d - 0.01f), new Vector2(4, 4));
                Counter(new Vector3(0, 0, d - 3), 8, new Color(0.15f, 0.12f, 0.1f));
                for (int k = 0; k < 6; k++) Mesh(PrimitiveType.Cylinder, new Vector3(-3 + k * 1.2f, 1.35f, d - 2.8f), new Vector3(0.08f, 0.2f, 0.08f), Palette.Get(Palette.Cloth, k + 3));
                Npc(new Vector3(0, 0, d - 1.8f), 180, b.id + 900, 1, 3);
                Sofa(new Vector3(-10, 0, 9), 90, new Color(0.15f, 0.15f, 0.2f)); Sofa(new Vector3(10, 0, 9), 270, new Color(0.15f, 0.15f, 0.2f));
                Sofa(new Vector3(-6, 0, 14), 0, new Color(0.5f, 0.1f, 0.15f)); Sofa(new Vector3(6, 0, 14), 0, new Color(0.5f, 0.1f, 0.15f));
                for (int k = 0; k < 6; k++) Npc(new Vector3(-9 + k * 3.5f, 0, 6 + (k % 2) * 4), k * 60, b.id + 950 + k, k % 3 == 0 ? 3 : 0);
                Sign(new Vector3(0, 3.6f, d - 0.1f), "SKY LOUNGE · " + (floor + 1) + " ЭТАЖ", new Color(0.3f, 0.9f, 1f), 6);
                Point(new Vector3(0, 0, d - 4.3f), "БАР НА КРЫШЕ: НАПИТКИ И ЕДА", () => Menus.Food(b, FoodDef.Restaurant));
                Light(new Vector3(0, 3.6f, d - 3), 10, 1.4f);
            }
            else
            {
                Room(w, d, 3.4f, Mats.Tex(Tex.Tiles("carpet", new Color(0.35f, 0.4f, 0.5f), new Color(0.32f, 0.37f, 0.47f), 2), Color.white, 0.05f), new Color(0.92f, 0.93f, 0.95f), true, 1.2f, false, 2);
                for (float x = -12; x <= 12; x += 4.8f)
                    for (float z = 4.5f; z < d - 1.5f; z += 3.6f)
                    {
                        if (Mathf.Abs(x) < 2.5f && z < 6) continue;
                        var p = new Vector3(x, 0, z);
                        if (Model("metal_office_desk", p, 0, 1.8f)) Model("classic_laptop", p + new Vector3(0, 0.79f, 0.05f), 180, 0.38f, null, false);
                        else Box(p + new Vector3(-0.9f, 0.72f, -0.45f), p + new Vector3(0.9f, 0.77f, 0.45f), new Color(0.92f, 0.92f, 0.9f), true);
                        Box(p + new Vector3(-0.35f, 0.79f, 0.2f), p + new Vector3(0.35f, 1.22f, 0.24f), new Color(0.08f, 0.08f, 0.1f));
                        BoxM(p + new Vector3(-0.32f, 0.82f, 0.195f), p + new Vector3(0.32f, 1.19f, 0.2f), Mats.Glow(new Color(0.3f, 0.55f, 0.9f)));
                        Chair(p + new Vector3(0, 0, -0.75f), 0, new Color(0.15f, 0.15f, 0.17f));
                        if (rng.Chance(0.55f)) Npc(p + new Vector3(0, 0, -0.75f), 0, b.id * 13 + (int)(x * 7 + z * 3) + floor * 101, 2, 3);
                    }
                Plant(new Vector3(-w / 2 + 1, 0, d - 1)); Plant(new Vector3(w / 2 - 1, 0, d - 1));
                string title = b.type == BType.Agency ? "АГЕНТСТВО «HERBAL CITY» · ЭТАЖ " + (floor + 1) : b.name + " · ЭТАЖ " + (floor + 1);
                Sign(new Vector3(0, 2.8f, d - 0.1f), title, b.Info.Sign, 4);
                if (b.type == BType.Corp)
                {
                    // 0.8: offices for sale floor by floor; in your own one a big desk with your computer
                    int code = b.id * 1000 + floor;
                    long price = 80000 + floor * 4000;
                    if (Save.D.offices.Contains(code))
                    {
                        var dp = new Vector3(0, 0, 3.2f);
                        Box(dp + new Vector3(-1.4f, 0.72f, -0.6f), dp + new Vector3(1.4f, 0.78f, 0.6f), new Color(0.25f, 0.17f, 0.1f), true);
                        Box(dp + new Vector3(-0.55f, 0.78f, 0.25f), dp + new Vector3(0.55f, 1.42f, 0.3f), new Color(0.05f, 0.05f, 0.06f));
                        BoxM(dp + new Vector3(-0.52f, 0.82f, 0.245f), dp + new Vector3(0.52f, 1.39f, 0.25f), Mats.Glow(new Color(0.25f, 0.6f, 0.95f)));
                        Chair(dp + new Vector3(0, 0, -0.95f), 0, new Color(0.1f, 0.1f, 0.12f));
                        Sign(new Vector3(0, 2.8f, d - 0.1f), "ВАШ ОФИС · " + b.name + " · ЭТАЖ " + (floor + 1), new Color(0.3f, 1f, 0.5f), 4);
                        Point(dp + new Vector3(0, 0, -1.2f), "МОЙ КОМПЬЮТЕР", () => PhoneUI.ShowComputer());
                    }
                    else Point(new Vector3(3.5f, 0, 3.5f), "КУПИТЬ ЭТОТ ЭТАЖ ПОД ОФИС — " + Hud.Money(price), () =>
                    {
                        if (!Eco.Spend(price, "Офис " + b.name)) return;
                        Save.D.offices.Add(code); Save.Write();
                        Hud.Toast("Офис ваш! Садитесь за компьютер", new Color(0.5f, 1f, 0.6f));
                        var pos = PlayerCtl.I.transform.position; float yaw = PlayerCtl.I.transform.eulerAngles.y;
                        BuildLevel(Floor);
                        PlayerCtl.I.Teleport(pos, yaw);
                    });
                }
                else Point(new Vector3(3.5f, 0, 3.5f), b.type == BType.Agency ? "АГЕНТ: КУПИТЬ ЖИЛЬЁ" : "РЕСЕПШН: РАБОТА И БИЗНЕС", () => { if (b.type == BType.Agency) Menus.RealEstate(); else Menus.Building(b); });
            }
            Flush();
            Ctr = Level;
        }

        // ------------------------------------------------------------------ homes

        /// <summary>A home: a house/villa (whole interior) or one flat of a floor (flat = true, built in its own container).</summary>
        static void Home(Bld b, int unit, bool flat = false, int status = -1, bool lux = false)
        {
            bool owned = Eco.OwnsHome(b.id, unit);
            if (status < 0) status = owned ? 2 : 1;
            if (!flat) lux = b.type == BType.Villa;
            float w = flat ? 6.8f : lux ? 22 : 16, d = flat ? 9f : lux ? 18 : 12;
            var wallCol = lux ? new Color(0.95f, 0.95f, 0.93f) : Palette.Get(new[] { new Color(0.93f, 0.9f, 0.82f), new Color(0.85f, 0.9f, 0.92f), new Color(0.92f, 0.86f, 0.86f), new Color(0.88f, 0.92f, 0.86f) }, b.id + unit);
            if (flat) Room(w, d, 3.0f, WorldBuilder.Wood, wallCol, true, 0.7f, false, 1);
            else Room(w, d, lux ? 4f : 3.2f, WorldBuilder.Wood, wallCol);
            // partition wall to the bedroom
            Box(new Vector3(-w / 2, 0, d * 0.55f), new Vector3(-1.0f, Hh, d * 0.55f + 0.15f), new Color(0.88f, 0.86f, 0.82f), true);
            // kitchen corner
            Box(new Vector3(w / 2 - 0.7f, 0, 1.5f), new Vector3(w / 2, 0.95f, flat ? 4f : 6f), new Color(0.95f, 0.95f, 0.95f), true, 0.6f);
            Box(new Vector3(w / 2 - 0.8f, 0, flat ? 4.2f : 6.2f), new Vector3(w / 2, 2f, flat ? 5.2f : 7.2f), new Color(0.85f, 0.87f, 0.9f), true, 0.7f);
            var home = Eco.Home(b.id, unit);
            string Style(string slot) => home == null ? null : home.decor.Find(s => s.StartsWith(slot + ":"));
            Color Col(string slot, Color def)
            {
                var s = Style(slot);
                if (s == null) return def;
                int.TryParse(s.Substring(s.IndexOf(':') + 1), out int i);
                return Palette.Get(Palette.Cloth, i);
            }
            bool Has(string slot) => Style(slot) != null || !owned; // show-flats and other people's homes are furnished
            Bed(new Vector3(-w / 2 + (flat ? 1.6f : 2.5f), 0, d - 2f), 180, Col("bed", new Color(0.8f, 0.8f, 0.85f)));
            if (Has("sofa")) Sofa(new Vector3(-w / 4, 0, 3f), 90, Col("sofa", new Color(0.35f, 0.4f, 0.5f)));
            if (Has("tv")) Tv(new Vector3(-w / 2 + 0.6f, 0, 3f), 90);
            if (Has("table")) Table(new Vector3(flat ? 1.1f : w / 4, 0, flat ? 2.6f : 4), Col("table", new Color(0.55f, 0.4f, 0.28f)), true, flat ? 0.75f : 1f);
            if (Has("rug")) Box(new Vector3(-w / 4 - 1.2f, 0.005f, 1.5f), new Vector3(-w / 4 + 1.2f, 0.02f, 4.5f), Col("rug", new Color(0.6f, 0.2f, 0.2f)));
            if (Has("plant")) { Plant(new Vector3(w / 2 - 0.8f, 0, d - 0.8f)); Plant(new Vector3(-1.5f, 0, d * 0.55f - 0.6f), 0.8f); }
            if (Has("lamp")) { Mesh(PrimitiveType.Cylinder, new Vector3(-w / 4 + 1.4f, 0.8f, 1.0f), new Vector3(0.05f, 0.8f, 0.05f), Color.black); Mesh(PrimitiveType.Cylinder, new Vector3(-w / 4 + 1.4f, 1.7f, 1.0f), new Vector3(0.45f, 0.18f, 0.45f), Col("lamp", new Color(0.95f, 0.9f, 0.7f))); }
            if (Has("art")) Box(new Vector3(-w / 2 + 0.6f, 1.3f, d * 0.55f - 0.03f), new Vector3(-w / 2 + 2.2f, 2.2f, d * 0.55f), Col("art", new Color(0.3f, 0.5f, 0.8f)));
            if (Has("shelf") && !Model("wooden_bookshelf_worn", new Vector3(w / 2 - 0.45f, 0, d - 2.6f), 270, 1.3f)) Shelf(new Vector3(w / 2 - 0.5f, 0, d - 2.6f), 2.0f, false, b.id + unit);
            Model("ClassicNightstand_01", new Vector3(-w / 2 + (flat ? 1.6f : 2.5f) + 1.3f, 0, d - 0.6f), 180, 0.5f);
            if (Has("lamp")) Model("modern_ceiling_lamp_01", new Vector3(-w / 4, Hh - 0.95f, 3f), 0, 0.45f, null, false);
            if (Has("sofa")) Model("mid_century_lounge_chair", new Vector3(-w / 4 + (flat ? 1.6f : 2.2f), 0, 4.6f), 225, 0.85f);
            if (Has("aquarium") && owned) { Box(new Vector3(0.6f, 0, d - 0.9f), new Vector3(2.2f, 0.8f, d - 0.3f), new Color(0.2f, 0.2f, 0.2f), true); BoxM(new Vector3(0.6f, 0.8f, d - 0.9f), new Vector3(2.2f, 1.6f, d - 0.3f), Mats.Glass(new Color(0.3f, 0.6f, 0.9f, 0.5f))); }
            if (Has("piano") && owned) Box(new Vector3(-0.8f, 0, d * 0.55f + 0.2f), new Vector3(0.9f, 1.2f, d * 0.55f + 1.0f), new Color(0.05f, 0.05f, 0.05f), true, 0.9f);
            if (Has("pc") && owned) { Box(new Vector3(-w / 2 + 0.3f, 0.72f, d * 0.55f + 0.4f), new Vector3(-w / 2 + 1.0f, 0.77f, d * 0.55f + 2.0f), new Color(0.1f, 0.1f, 0.1f), true); BoxM(new Vector3(-w / 2 + 0.6f, 0.77f, d * 0.55f + 0.8f), new Vector3(-w / 2 + 0.65f, 1.4f, d * 0.55f + 1.6f), Mats.Glow(new Color(0.4f, 0.2f, 0.9f))); }

            if (status == 2)
            {
                Point(new Vector3(-w / 2 + (flat ? 1.6f : 2.5f), 0, d - 3.8f), "СПАТЬ (ВОССТАНОВИТЬ ЭНЕРГИЮ)", () => Menus.Sleep());
                Point(new Vector3(w / 2 - 1.6f, 0, flat ? 4.7f : 6.7f), "ХОЛОДИЛЬНИК: ПОЕСТЬ ($10)", () => Eco.Eat(10, 35, "Перекусили дома"));
                Point(new Vector3(-1.6f, 0, d * 0.55f - 1.2f), "ГАРДЕРОБ: ПЕРЕОДЕТЬСЯ", () => Menus.Dress(false));
                Point(new Vector3(0.6f, 0, 1.6f), "ОБУСТРОИТЬ ДОМ", () => Menus.Decor(home));
                Sign(new Vector3(0, Hh - 0.6f, d - 0.3f), "МОЙ ДОМ", new Color(0.3f, 0.8f, 0.4f), 3);
            }
            else if (status == 1)
            {
                int price = b.UnitPrice(unit);
                Sign(new Vector3(0, Hh - 0.6f, d - 0.3f), "ПРОДАЁТСЯ · " + Hud.Money(price), new Color(0.95f, 0.75f, 0.2f), 3);
                Point(new Vector3(0, 0, 2.6f), "КУПИТЬ — " + Hud.Money(price), () => Menus.BuyHome(b, unit));
            }
            else
            {
                // someone lives here
                var r = new Rng(H.U(b.id, unit, 3));
                if (r.Chance(0.7f)) Npc(new Vector3(-w / 4, 0, 3f), 90, b.id * 7 + unit, 2);
            }
        }
    }

    public class Pulse : MonoBehaviour
    {
        Vector3 s0;
        void Start() { s0 = transform.localScale; }
        void Update()
        {
            float k = 1f + Mathf.Sin(Time.time * 3f) * 0.12f;
            transform.localScale = new Vector3(s0.x * k, s0.y, s0.z * k);
        }
    }
}
