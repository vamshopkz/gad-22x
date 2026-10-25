using System.Collections.Generic;
using UnityEngine;

namespace Gad
{
    /// <summary>
    /// 0.7: the real houses of the city. Private houses, villas and the building plots are not part of
    /// the merged building shells: their exteriors (walls with see-through windows, roofs, yards) are
    /// merged here per block, and the close-up part (furniture, colliders, the opening front door,
    /// action points) is built when the player comes near — you walk straight in, no loading into a
    /// separate interior.
    /// </summary>
    public class HouseWorld : MonoBehaviour
    {
        public static HouseWorld I;
        public const float PlotPrice = 30000f;
        const float Near = 48f, Far = 70f;

        readonly Dictionary<(int, int), GameObject> blocks = new Dictionary<(int, int), GameObject>();
        readonly Dictionary<int, GameObject> insides = new Dictionary<int, GameObject>();
        readonly List<Bld> houses = new List<Bld>();
        float scanT;

        public static bool IsReal(Bld b) => b.type == BType.House || b.type == BType.Villa || b.type == BType.Plot;

        public static void Create()
        {
            I = new GameObject("HouseWorld").AddComponent<HouseWorld>();
            foreach (var b in City.All) if (IsReal(b)) I.houses.Add(b);
            var done = new HashSet<(int, int)>();
            foreach (var b in I.houses) if (done.Add((b.cx, b.cz))) I.BuildBlock(b.cx, b.cz);
            Debug.Log($"[HouseWorld] {I.houses.Count} real houses and plots in {done.Count} blocks");
        }

        /// <summary>Where the house of a building stands (front centre at the street side) and how it is turned.</summary>
        public static void Place(Bld b, HouseDesign d, out Vector3 origin, out Quaternion rot)
        {
            var r = b.rect;
            float y = City.Curb;
            float setback = b.type == BType.Plot ? 4.5f : 0f;
            float shift = b.type == BType.Plot && d != null && d.garage ? -1.9f : 0f;
            switch (b.front)
            {
                case 0: origin = new Vector3(r.center.x + shift, y, r.yMin + setback); rot = Quaternion.identity; break;
                case 1: origin = new Vector3(r.center.x - shift, y, r.yMax - setback); rot = Quaternion.Euler(0, 180, 0); break;
                case 2: origin = new Vector3(r.xMin + setback, y, r.center.y - shift); rot = Quaternion.Euler(0, 90, 0); break;
                default: origin = new Vector3(r.xMax - setback, y, r.center.y + shift); rot = Quaternion.Euler(0, -90, 0); break;
            }
        }

        public static PlotHouse PlotOf(Bld b) => Save.D.plots.Find(p => p.building == b.id);

        /// <summary>The design standing on a building: city houses have their own, plots the one the player built (or none).</summary>
        public static HouseDesign DesignOf(Bld b)
        {
            if (b.type == BType.Plot) { var p = PlotOf(b); return p == null ? null : HouseDesign.Get(p.design); }
            return HouseDesign.ForCity(b);
        }

        void BuildBlock(int bx, int bz)
        {
            if (blocks.TryGetValue((bx, bz), out var old) && old != null) Destroy(old);
            var m = new MeshB();
            var lawn = WorldBuilder.Grass;
            foreach (var b in City.ByChunk[bx, bz])
            {
                if (!IsReal(b)) continue;
                var d = DesignOf(b);
                if (b.type == BType.Plot) PlotGround(m, b, d != null);
                if (d == null) continue;
                Place(b, d, out var o, out var rot);
                HouseBuilder.Exterior(m, Matrix4x4.TRS(o, rot, Vector3.one), d);
            }
            var go = m.Build($"Houses {bx},{bz}", transform, 10, true);
            blocks[(bx, bz)] = go;
        }

        /// <summary>The plot itself: lawn, a low fence with a gate, a sign; while empty, a "for sale" board.</summary>
        static void PlotGround(MeshB m, Bld b, bool built)
        {
            var r = b.rect;
            float y = City.Curb;
            m.Flat(WorldBuilder.Grass, r.xMin, r.yMin, r.xMax, r.yMax, y + 0.02f, 3f);
            var post = Photo.Mat("dark_wood", Color.white, 0.3f);
            var rail = Photo.Mat("white_planks_clean", Color.white, 0.3f);
            // fence round the lot, with a gap for the gate on the street side
            Vector3 c0 = new Vector3(r.xMin, y, r.yMin), c1 = new Vector3(r.xMax, y, r.yMin), c2 = new Vector3(r.xMax, y, r.yMax), c3 = new Vector3(r.xMin, y, r.yMax);
            var sides = new[] { (c0, c1, 0), (c1, c2, 3), (c2, c3, 1), (c3, c0, 2) };
            foreach (var (a, e, side) in sides)
            {
                float len = Vector3.Distance(a, e);
                var dir = (e - a) / len;
                bool street = side == b.front;
                for (float u = 0; u <= len + 0.01f; u += 2f)
                {
                    if (street && Mathf.Abs(u - len / 2f) < 1.6f) continue;
                    var p = a + dir * u;
                    m.Box(post, post, p + new Vector3(-0.06f, 0, -0.06f), p + new Vector3(0.06f, 1.0f, 0.06f), new Vector2(1, 1));
                }
                foreach (float hgt in new[] { 0.45f, 0.85f })
                {
                    if (street)
                    {
                        Rail(m, rail, a, a + dir * (len / 2f - 1.6f), hgt);
                        Rail(m, rail, a + dir * (len / 2f + 1.6f), e, hgt);
                    }
                    else Rail(m, rail, a, e, hgt);
                }
            }
            if (!built)
            {
                // a sign board facing the street
                var fd = b.FrontDir;
                var s = b.door - fd * 1.2f + Vector3.Cross(Vector3.up, fd) * 2.6f;
                m.Box(post, post, s + new Vector3(-0.06f, 0, -0.06f), s + new Vector3(0.06f, 2.2f, 0.06f), new Vector2(1, 1));
                var board = Mats.Lit(new Color(0.15f, 0.55f, 0.25f), 0.4f);
                var side = Vector3.Cross(Vector3.up, fd);
                var bc = s + Vector3.up * 1.8f;
                var a0 = bc - side * 1.0f - Vector3.up * 0.45f; var a1 = bc + side * 1.0f + Vector3.up * 0.45f;
                m.Box(board, board, Vector3.Min(a0, a1) - new Vector3(0.03f, 0, 0.03f), Vector3.Max(a0, a1) + new Vector3(0.03f, 0, 0.03f), new Vector2(1, 1));
            }
        }

        static void Rail(MeshB m, Material mat, Vector3 a, Vector3 e, float h)
        {
            var mn = Vector3.Min(a, e) + new Vector3(-0.03f, h, -0.03f); var mx = Vector3.Max(a, e) + new Vector3(0.03f, h + 0.1f, 0.03f);
            m.Box(mat, mat, mn, mx, new Vector2(2, 2));
        }

        void Update()
        {
            scanT -= Time.deltaTime;
            if (scanT > 0f) return;
            scanT = 0.4f;
            var pl = PlayerCtl.I;
            if (pl == null) return;
            var p = pl.transform.position;
            foreach (var b in houses)
            {
                var c = new Vector3(b.rect.center.x, 0, b.rect.center.y);
                float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(c.x, c.z));
                bool have = insides.TryGetValue(b.id, out var go) && go != null;
                if (!have && d < Near && p.y > -50f) insides[b.id] = BuildInside(b);
                else if (have && (d > Far || p.y < -50f)) { Destroy(go); insides.Remove(b.id); }
            }
        }

        GameObject BuildInside(Bld b)
        {
            var d = DesignOf(b);
            if (b.type == BType.Plot && d == null)
            {
                // an empty plot: just the action point at the gate
                var go = new GameObject("Plot " + b.id);
                go.transform.SetParent(transform, false);
                var ow = go.AddComponent<WorldActions.Owner>();
                ow.points.Add(new WorldActions.Point { Pos = b.door - b.FrontDir * 1.0f, Label = "УЧАСТОК: ПОСТРОИТЬ ДОМ", Act = () => HouseMenus.Plot(b) });
                WorldActions.Register(ow);
                return go;
            }
            Place(b, d, out var o, out var rot);
            bool owned = b.type == BType.Plot || Eco.Home(b.id, 0) != null;
            string label = null; System.Action act = null;
            if (b.type == BType.Plot) { label = "МОЙ ДОМ: ПЕРЕСТРОИТЬ"; act = () => HouseMenus.Plot(b); }
            else if (!owned) { label = "КУПИТЬ ДОМ — " + Hud.Money(b.UnitPrice(0)); act = () => Menus.BuyHome(b, 0); }
            return HouseBuilder.Inside(transform, o, rot, d, owned, Menus.Sleep, () => Menus.Dress(false), label, act);
        }

        /// <summary>Redraw one house or plot after it changed hands or was (re)built.</summary>
        public static void Refresh(Bld b)
        {
            if (I == null) return;
            if (I.insides.TryGetValue(b.id, out var go) && go != null) Destroy(go);
            I.insides.Remove(b.id);
            I.BuildBlock(b.cx, b.cz);
            I.scanT = 0f;
        }

        /// <summary>For tests: the close-up object of a house if it is built.</summary>
        public static GameObject InsideOf(Bld b) => I != null && I.insides.TryGetValue(b.id, out var go) ? go : null;
    }
}
