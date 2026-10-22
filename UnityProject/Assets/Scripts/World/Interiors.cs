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
        public static int Unit;                 // flat number (0 = lobby / whole building)
        public static readonly List<ActionPoint> Points = new List<ActionPoint>();
        static MeshB mb;
        static Rng rng;
        static float W, D, Hh;

        public static int Code => Current == null ? -1 : Current.id * 1000 + Unit;

        public static ActionPoint NearestPoint(Vector3 p, float max)
        {
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
            if (pl.Inside < 0) { Save.D.px = pl.transform.position.x; Save.D.py = pl.transform.position.y; Save.D.pz = pl.transform.position.z; }
            Build(b, unit);
            pl.Inside = b.id;
            pl.Teleport(O + new Vector3(0, 0.1f, 3f), 0);
            AudioFx.Play("door");
            Hud.Toast(unit > 0 ? b.name + " · кв. " + unit : b.name);
            Streamer.SetOutdoor(false);
        }

        public static void Exit()
        {
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
            Root = null; Current = null; Unit = 0;
            Points.Clear();
        }

        static void Point(Vector3 local, string label, Action act) => Points.Add(new ActionPoint { Pos = O + local, Label = label, Act = act });

        // ------------------------------------------------------------------ building blocks

        static Material floorMat, wallMat;

        static void Room(float w, float d, float h, Material floor, Color wall, bool windows = true)
        {
            W = w; D = d; Hh = h;
            floorMat = floor;
            wallMat = Mats.Lit(wall, 0.1f);
            var t = Root;
            // floor + ceiling
            mb.Box(Mats.Lit(wall * 0.8f), floor, new Vector3(-w / 2, -0.3f, 0), new Vector3(w / 2, 0, d), new Vector2(4, 4));
            mb.Box(wallMat, wallMat, new Vector3(-w / 2, h, 0), new Vector3(w / 2, h + 0.2f, d), new Vector2(4, 4), default, true, Mats.Lit(new Color(0.92f, 0.92f, 0.9f)));
            // walls (door gap in the south wall)
            mb.Box(wallMat, wallMat, new Vector3(-w / 2 - 0.3f, 0, -0.3f), new Vector3(-1.4f, h, 0), new Vector2(4, 4));
            mb.Box(wallMat, wallMat, new Vector3(1.4f, 0, -0.3f), new Vector3(w / 2 + 0.3f, h, 0), new Vector2(4, 4));
            mb.Box(wallMat, wallMat, new Vector3(-1.4f, 2.7f, -0.3f), new Vector3(1.4f, h, 0), new Vector2(4, 4));
            mb.Box(wallMat, wallMat, new Vector3(-w / 2 - 0.3f, 0, d), new Vector3(w / 2 + 0.3f, h, d + 0.3f), new Vector2(4, 4));
            mb.Box(wallMat, wallMat, new Vector3(-w / 2 - 0.3f, 0, 0), new Vector3(-w / 2, h, d), new Vector2(4, 4));
            mb.Box(wallMat, wallMat, new Vector3(w / 2, 0, 0), new Vector3(w / 2 + 0.3f, h, d), new Vector2(4, 4));
            // skirting
            var sk = Mats.Lit(wall * 0.6f);
            mb.Box(sk, sk, new Vector3(-w / 2, 0, d - 0.05f), new Vector3(w / 2, 0.12f, d), new Vector2(4, 4));
            // exit door: light rectangle with the street behind
            mb.Box(Mats.Glow(new Color(0.75f, 0.85f, 0.95f)), null, new Vector3(-1.4f, 0, -0.32f), new Vector3(1.4f, 2.7f, -0.3f), new Vector2(4, 4));
            mb.Box(Mats.Lit(new Color(0.2f, 0.2f, 0.22f)), null, new Vector3(-1.5f, 2.7f, -0.05f), new Vector3(1.5f, 2.85f, 0.05f), new Vector2(4, 4));
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
            Col(new Vector3(0, h / 2, -0.4f), new Vector3(w, h, 0.3f), 10);
            Col(new Vector3(0, h + 0.1f, d / 2), new Vector3(w, 0.2f, d), 10);
            // lights
            Light(new Vector3(0, h - 0.5f, d * 0.3f), Mathf.Max(w, d) * 0.9f, 1.3f);
            Light(new Vector3(0, h - 0.5f, d * 0.75f), Mathf.Max(w, d) * 0.9f, 1.3f);
            Point(new Vector3(0, 0, 0.6f), "ВЫЙТИ НА УЛИЦУ", Exit);
        }

        static void Light(Vector3 local, float range, float intensity)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(Root, false);
            go.transform.localPosition = local;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.range = range; l.intensity = intensity; l.color = new Color(1f, 0.96f, 0.9f);
            l.shadows = LightShadows.None;
        }

        static void Col(Vector3 c, Vector3 s, int layer)
        {
            var go = new GameObject("Col") { layer = layer };
            go.transform.SetParent(Root, false);
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

        static Human Npc(Vector3 local, float yaw, int seed, int pose = 0, int top = -1)
        {
            var r = new Rng((uint)seed);
            var look = new Look { gender = r.I(2), skin = r.I(5), hair = r.I(6), hairStyle = r.I(4), shirt = r.I(14), pants = r.I(14), shoes = r.I(14), top = top >= 0 ? top : r.I(3), hat = 0 };
            if (look.gender == 1 && look.hairStyle == 2) look.hairStyle = 1;
            var h = Human.Create(Root, look, "Staff");
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

        static void Table(Vector3 c, Color col, bool chairs = true, float size = 1f)
        {
            Box(c + new Vector3(-0.6f * size, 0.72f, -0.6f * size), c + new Vector3(0.6f * size, 0.78f, 0.6f * size), col, false, 0.4f);
            Mesh(PrimitiveType.Cylinder, c + new Vector3(0, 0.36f, 0), new Vector3(0.12f, 0.36f, 0.12f), new Color(0.2f, 0.2f, 0.2f));
            Col(c + new Vector3(0, 0.4f, 0), new Vector3(1.2f * size, 0.8f, 1.2f * size), 12);
            if (!chairs) return;
            foreach (var o in new[] { new Vector3(0, 0, 1f), new Vector3(0, 0, -1f), new Vector3(1f, 0, 0), new Vector3(-1f, 0, 0) })
                Chair(c + o * size, Quaternion.LookRotation(-o).eulerAngles.y, col * 0.7f);
        }

        static void Chair(Vector3 c, float yaw, Color col)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            var m = Mats.Lit(col);
            mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(c + Vector3.up * 0.45f, rot, new Vector3(0.45f, 0.06f, 0.45f)), m);
            mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(c + Vector3.up * 0.75f + rot * new Vector3(0, 0, -0.21f), rot, new Vector3(0.45f, 0.6f, 0.05f)), m);
            mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(c + Vector3.up * 0.22f, rot, new Vector3(0.4f, 0.44f, 0.4f)), Mats.Lit(new Color(0.2f, 0.2f, 0.2f)));
        }

        static void Plant(Vector3 c, float s = 1f)
        {
            Mesh(PrimitiveType.Cylinder, c + Vector3.up * 0.25f * s, new Vector3(0.45f, 0.25f, 0.45f) * s, new Color(0.55f, 0.35f, 0.25f));
            Mesh(PrimitiveType.Sphere, c + Vector3.up * 0.9f * s, new Vector3(0.8f, 1f, 0.8f) * s, new Color(0.2f, 0.5f, 0.22f));
        }

        static void Sofa(Vector3 c, float yaw, Color col)
        {
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

        static void Tv(Vector3 c, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(c + Vector3.up * 0.25f, rot, new Vector3(1.8f, 0.5f, 0.45f)), WorldBuilder.Wood);
            mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(c + Vector3.up * 1.15f, rot, new Vector3(1.6f, 0.92f, 0.06f)), Mats.Lit(new Color(0.05f, 0.05f, 0.06f), 0.9f));
            mb.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(c + Vector3.up * 1.15f + rot * new Vector3(0, 0, 0.035f), rot, new Vector3(1.5f, 0.82f, 0.01f)), Mats.Glow(new Color(0.25f, 0.45f, 0.75f)));
        }

        static void Sign(Vector3 local, string text, Color col, float size = 3f, float yaw = 0)
        {
            var go = new GameObject("Sign");
            go.transform.SetParent(Root, false);
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
            CarModel.Body(mb, xf, def.Shape, Palette.Get(Palette.Car, color), def.Id == "taxi");
            CarModel.Wheels(mb, xf, def.Shape);
            var d = CarModel.Of(def.Shape);
            Col(c + Vector3.up * 0.8f, Quaternion.Euler(0, yaw, 0) * new Vector3(d.W, 1.6f, d.L) is var v ? new Vector3(Mathf.Abs(v.x), 1.6f, Mathf.Abs(v.z)) : Vector3.one, 12);
        }

        // ------------------------------------------------------------------ layouts

        public static void Build(Bld b, int unit)
        {
            Clear();
            Current = b; Unit = unit;
            Root = new GameObject("Interior " + b.name).transform;
            Root.position = O;
            mb = new MeshB();
            rng = new Rng(H.U(b.id, unit, 31));
            var accent = b.Info.Sign;
            var tiles = Mats.Tex(Tex.Tiles("floortile", new Color(0.86f, 0.85f, 0.82f), new Color(0.7f, 0.7f, 0.68f), 4), Color.white, 0.5f);
            var dark = Mats.Tex(Tex.Tiles("darkfloor", new Color(0.3f, 0.3f, 0.32f), new Color(0.22f, 0.22f, 0.24f), 2), Color.white, 0.6f);
            var concrete = WorldBuilder.Concrete;
            var wood = WorldBuilder.Wood;

            bool homeView = unit > 0 || ((b.type == BType.House || b.type == BType.Villa) && Eco.OwnsHome(b.id, 0));
            if (homeView) { Home(b, unit); Finish(b); return; }

            switch (b.type)
            {
                case BType.Apartment:
                case BType.Tower:
                    Room(14, 12, 4f, b.type == BType.Tower ? dark : tiles, new Color(0.88f, 0.86f, 0.8f));
                    Counter(new Vector3(-4, 0, 6), 3, new Color(0.3f, 0.25f, 0.2f));
                    Npc(new Vector3(-4, 0, 7f), 180, b.id, 0, 3);
                    BoxM(new Vector3(3, 0, 11.6f), new Vector3(6, 3, 12f), WorldBuilder.Metal);
                    Box(new Vector3(4.45f, 0, 11.55f), new Vector3(4.55f, 3f, 11.6f), new Color(0.1f, 0.1f, 0.1f));
                    for (int i = 0; i < 4; i++) Box(new Vector3(6.6f, 1 + i * 0.3f, 2 + i * 0.01f), new Vector3(6.9f, 1.25f + i * 0.3f, 5), new Color(0.6f, 0.55f, 0.45f));
                    Sofa(new Vector3(-4, 0, 2.5f), 90, new Color(0.3f, 0.35f, 0.45f));
                    Plant(new Vector3(6, 0, 1));
                    Sign(new Vector3(4.5f, 3.4f, 11.5f), "ЛИФТ", Color.white, 3);
                    Point(new Vector3(4.5f, 0, 10.5f), "ЛИФТ: КВАРТИРЫ", () => Menus.Flats(b));
                    Point(new Vector3(-4, 0, 5f), "КОНСЬЕРЖ", () => Menus.Flats(b));
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
                    for (float x = -W / 2 + 3; x < W / 2 - 4; x += 3.6f)
                        for (float z = 3.5f; z < D - 5; z += 3.6f)
                            Table(new Vector3(x, 0, z), tcol);
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
                        ("ОДЕЖДА", new Color(0.9f, 0.3f, 0.6f), () => Menus.Wardrobe(true)),
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
                        Shelf(new Vector3(x, 0, D - 3), 6, true, b.id + i);
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
                    Plant(new Vector3(-7.5f, 0, 12)); Plant(new Vector3(7.5f, 0, 12));
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

                case BType.Clothes:
                    Room(16, 12, 4f, wood, new Color(0.95f, 0.9f, 0.92f));
                    for (int k = 0; k < 3; k++)
                    {
                        var p = new Vector3(-5 + k * 5, 0, 6);
                        BoxM(p + new Vector3(-1.5f, 1.6f, -0.03f), p + new Vector3(1.5f, 1.65f, 0.03f), WorldBuilder.Metal);
                        for (int c = 0; c < 8; c++) Box(p + new Vector3(-1.4f + c * 0.36f, 0.7f, -0.25f), p + new Vector3(-1.2f + c * 0.36f, 1.6f, 0.25f), Palette.Get(Palette.Cloth, c + k * 3));
                    }
                    Box(new Vector3(5, 0, 10.5f), new Vector3(7, 2.4f, 10.6f), new Color(0.8f, 0.9f, 0.95f));
                    Npc(new Vector3(-6, 0, 10), 150, b.id, 0);
                    Point(new Vector3(0, 0, 3.5f), "ПРИМЕРОЧНАЯ: КУПИТЬ ОДЕЖДУ", () => Menus.Wardrobe(true));
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
            var go = mb.Build("Interior mesh", Root, 12, true);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // markers on the floor under every action point
            foreach (var p in Points)
            {
                var m = Prim.Make(PrimitiveType.Cylinder, Root, p.Pos - O + Vector3.up * 0.02f, new Vector3(0.9f, 0.01f, 0.9f), Mats.Glow(new Color(0.2f, 0.85f, 1f)), "Spot");
                m.AddComponent<Pulse>();
            }
        }

        // ------------------------------------------------------------------ homes

        static void Home(Bld b, int unit)
        {
            bool owned = Eco.OwnsHome(b.id, unit);
            bool lux = b.type == BType.Villa || b.type == BType.Tower;
            float w = lux ? 22 : b.type == BType.House ? 16 : 14, d = lux ? 18 : 12;
            Room(w, d, lux ? 4f : 3.2f, WorldBuilder.Wood, lux ? new Color(0.95f, 0.95f, 0.93f) : Palette.Get(new[] { new Color(0.93f, 0.9f, 0.82f), new Color(0.85f, 0.9f, 0.92f), new Color(0.92f, 0.86f, 0.86f) }, b.id + unit));
            // partition wall to the bedroom
            Box(new Vector3(-w / 2, 0, d * 0.55f), new Vector3(-1.2f, Hh, d * 0.55f + 0.15f), new Color(0.88f, 0.86f, 0.82f), true);
            // kitchen corner
            Box(new Vector3(w / 2 - 0.7f, 0, 1.5f), new Vector3(w / 2, 0.95f, 6f), new Color(0.95f, 0.95f, 0.95f), true, 0.6f);
            Box(new Vector3(w / 2 - 0.8f, 0, 6.2f), new Vector3(w / 2, 2f, 7.2f), new Color(0.85f, 0.87f, 0.9f), true, 0.7f);
            var home = Eco.Home(b.id, unit);
            string Style(string slot) => home == null ? null : home.decor.Find(s => s.StartsWith(slot + ":"));
            Color Col(string slot, Color def)
            {
                var s = Style(slot);
                if (s == null) return def;
                int.TryParse(s.Substring(s.IndexOf(':') + 1), out int i);
                return Palette.Get(Palette.Cloth, i);
            }
            bool Has(string slot) => Style(slot) != null || !owned; // show-flat is furnished
            Bed(new Vector3(-w / 2 + 2.5f, 0, d - 2f), 180, Col("bed", new Color(0.8f, 0.8f, 0.85f)));
            if (Has("sofa")) Sofa(new Vector3(-w / 4, 0, 3f), 90, Col("sofa", new Color(0.35f, 0.4f, 0.5f)));
            if (Has("tv")) Tv(new Vector3(-w / 2 + 0.6f, 0, 3f), 90);
            if (Has("table")) Table(new Vector3(w / 4, 0, 4), Col("table", new Color(0.55f, 0.4f, 0.28f)));
            if (Has("rug")) Box(new Vector3(-w / 4 - 1.5f, 0.005f, 1.5f), new Vector3(-w / 4 + 1.5f, 0.02f, 4.5f), Col("rug", new Color(0.6f, 0.2f, 0.2f)));
            if (Has("plant")) { Plant(new Vector3(w / 2 - 1, 0, d - 1)); Plant(new Vector3(-1.8f, 0, d * 0.55f - 0.7f), 0.8f); }
            if (Has("lamp")) { Mesh(PrimitiveType.Cylinder, new Vector3(-w / 4 + 1.8f, 0.8f, 1.2f), new Vector3(0.05f, 0.8f, 0.05f), Color.black); Mesh(PrimitiveType.Cylinder, new Vector3(-w / 4 + 1.8f, 1.7f, 1.2f), new Vector3(0.45f, 0.18f, 0.45f), Col("lamp", new Color(0.95f, 0.9f, 0.7f))); }
            if (Has("art")) Box(new Vector3(-1, 1.3f, d * 0.55f - 0.03f), new Vector3(1, 2.3f, d * 0.55f), Col("art", new Color(0.3f, 0.5f, 0.8f)));
            if (Has("shelf")) Shelf(new Vector3(w / 2 - 0.5f, 0, d - 4), 2.5f, false, b.id);
            if (Has("aquarium") && owned) { Box(new Vector3(1, 0, d - 0.9f), new Vector3(3, 0.8f, d - 0.3f), new Color(0.2f, 0.2f, 0.2f), true); BoxM(new Vector3(1, 0.8f, d - 0.9f), new Vector3(3, 1.6f, d - 0.3f), Mats.Glass(new Color(0.3f, 0.6f, 0.9f, 0.5f))); }
            if (Has("piano") && owned) Box(new Vector3(3.5f, 0, d - 1.2f), new Vector3(5.2f, 1.2f, d - 0.3f), new Color(0.05f, 0.05f, 0.05f), true, 0.9f);
            if (Has("pc") && owned) { Box(new Vector3(-w / 2 + 4.5f, 0.72f, d - 0.9f), new Vector3(-w / 2 + 6.3f, 0.77f, d - 0.2f), new Color(0.1f, 0.1f, 0.1f), true); BoxM(new Vector3(-w / 2 + 4.9f, 0.77f, d - 0.5f), new Vector3(-w / 2 + 5.9f, 1.4f, d - 0.45f), Mats.Glow(new Color(0.4f, 0.2f, 0.9f))); }

            if (owned)
            {
                Point(new Vector3(-w / 2 + 2.5f, 0, d - 3.8f), "СПАТЬ (ВОССТАНОВИТЬ ЭНЕРГИЮ)", () => Menus.Sleep());
                Point(new Vector3(w / 2 - 1.8f, 0, 6.7f), "ХОЛОДИЛЬНИК: ПОЕСТЬ ($10)", () => Eco.Eat(10, 35, "Перекусили дома"));
                Point(new Vector3(-2, 0, d * 0.55f - 1.2f), "ГАРДЕРОБ: ПЕРЕОДЕТЬСЯ", () => Menus.Wardrobe(false));
                Point(new Vector3(0, 0, 2.2f), "ОБУСТРОИТЬ ДОМ", () => Menus.Decor(home));
                Sign(new Vector3(0, Hh - 0.6f, d - 0.3f), "МОЙ ДОМ", new Color(0.3f, 0.8f, 0.4f), 3);
            }
            else
            {
                int price = b.UnitPrice(unit);
                Sign(new Vector3(0, Hh - 0.6f, d - 0.3f), "ПРОДАЁТСЯ · " + Hud.Money(price), new Color(0.95f, 0.75f, 0.2f), 3);
                Point(new Vector3(0, 0, 2.2f), "КУПИТЬ — " + Hud.Money(price), () => Menus.BuyHome(b, unit));
            }
            if (unit > 0)
            {
                // the exit of a flat leads back to the lobby
                Points.RemoveAll(p => p.Label == "ВЫЙТИ НА УЛИЦУ");
                Point(new Vector3(0, 0, 0.6f), "В ЛОББИ", () => Enter(b, 0));
                Point(new Vector3(2.4f, 0, 0.6f), "ВЫЙТИ НА УЛИЦУ", Exit);
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
