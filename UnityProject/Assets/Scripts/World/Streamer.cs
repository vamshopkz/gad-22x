using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Gad
{
    /// <summary>
    /// Streams the detail of the city around the player: street lamps, trees, benches, signs,
    /// parked cars, gardens (built a block at a time, dropped when far away), plus the moving
    /// traffic and pedestrians. Building shells and roads are permanent (WorldBuilder).
    /// </summary>
    public class Streamer : MonoBehaviour
    {
        public static Streamer I;
        readonly Dictionary<(int, int), GameObject> loaded = new Dictionary<(int, int), GameObject>();
        Transform root;
        public static bool Outdoor = true;
        public static int LoadedCount => I == null ? 0 : I.loaded.Count;

        /// <summary>A car parked at the kerb. Shown merged with its neighbours; becomes a real car when taken.</summary>
        public class ParkedCar
        {
            public Vector3 pos; public float yaw; public CarDef def; public int color; public bool taken; public (int, int) chunk;
            public Vector3 DoorStand => pos + Quaternion.Euler(0, yaw, 0) * new Vector3(-(def.W / 2 + 0.75f), 0.2f, CarMesh.Get(def, 1).doorPivot.z - 0.55f);
        }
        readonly Dictionary<(int, int), (GameObject go, List<ParkedCar> list)> parked = new Dictionary<(int, int), (GameObject, List<ParkedCar>)>();

        static List<ParkedCar> ParkedSpots(int bx, int bz)
        {
            var list = new List<ParkedCar>();
            var rng = new Rng(H.U(bx, bz, 4711));
            var d = City.Districts[bx, bz];
            float X0 = bx * City.P, Z0 = bz * City.P, X1 = X0 + City.P, Z1 = Z0 + City.P;
            for (int side = 0; side < 4; side++)
            {
                if (!rng.Chance(d == District.Downtown || d == District.Business ? 0.6f : 0.45f)) continue;
                for (float s = 18; s < City.P - 18; s += rng.F(7f, 13f))
                {
                    if (!rng.Chance(0.5f)) continue;
                    Vector3 p; float yaw;
                    switch (side)
                    {
                        case 0: p = new Vector3(X0 + s, 0, Z0 + 5.1f); yaw = 270; break;
                        case 1: p = new Vector3(X0 + s, 0, Z1 - 5.1f); yaw = 90; break;
                        case 2: p = new Vector3(X0 + 5.1f, 0, Z0 + s); yaw = 0; break;
                        default: p = new Vector3(X1 - 5.1f, 0, Z0 + s); yaw = 180; break;
                    }
                    int k = rng.I(100);
                    var def = CarDef.All[k < 12 ? 0 : k < 40 ? 1 : k < 48 ? 2 : k < 60 ? 3 : k < 68 ? 4 : k < 74 ? 5 : k < 88 ? 6 : k < 94 ? 8 : 7];
                    list.Add(new ParkedCar { pos = p, yaw = yaw, def = def, color = rng.I(10), chunk = (bx, bz) });
                }
            }
            // 0.9: car parks — two rows in front of the mall, a row behind the blocks of flats
            foreach (var b in City.ByChunk[bx, bz])
            {
                var fd = b.FrontDir; var side = Vector3.Cross(Vector3.up, fd);
                float span = b.front <= 1 ? b.rect.width : b.rect.height, depth = b.front <= 1 ? b.rect.height : b.rect.width;
                var door = new Vector3(b.door.x, City.Curb, b.door.z);
                void Row(Vector3 centre, float halfLen, float facing, float skip)
                {
                    for (float s = -halfLen; s <= halfLen; s += 2.7f)
                    {
                        if (Mathf.Abs(s) < skip || !rng.Chance(0.72f)) continue;
                        int k = rng.I(100);
                        var def = CarDef.All[k < 15 ? 0 : k < 45 ? 1 : k < 55 ? 2 : k < 70 ? 3 : k < 80 ? 4 : k < 90 ? 6 : 8];
                        if (def.L > 5.2f) continue;
                        var p = centre + side * s;
                        list.Add(new ParkedCar { pos = p, yaw = facing, def = def, color = rng.I(10), chunk = (bx, bz) });
                    }
                }
                float yawIn = Quaternion.LookRotation(-fd).eulerAngles.y, yawOut = Quaternion.LookRotation(fd).eulerAngles.y;
                if (b.type == BType.Mall)
                {
                    Row(door + fd * 3.2f, span / 2f - 2f, yawIn, 9f);
                    Row(door + fd * 11.6f, span / 2f - 2f, yawOut, 3f);
                }
                else if (b.type == BType.Apartment)
                    Row(door - fd * (depth + 3.0f), span / 2f - 1.5f, yawOut, 0f);
            }
            return list;
        }

        GameObject BuildParked(int bx, int bz, List<ParkedCar> list)
        {
            var go = new GameObject($"Parked {bx},{bz}") { layer = 12 };
            go.transform.SetParent(root, false);
            var m = new MeshB();
            foreach (var c in list)
            {
                if (c.taken) continue;
                var rot = Quaternion.Euler(0, c.yaw, 0);
                m.AddMulti(CarMesh.Static(c.def, 1), Matrix4x4.TRS(c.pos, rot, Vector3.one), CarMesh.Materials(Car.ColorOf(c.def, c.color), c.def.Spec));
                var bc = go.AddComponent<BoxCollider>();
                var sz = rot * new Vector3(c.def.W, c.def.Spec.H - 0.3f, c.def.L);
                bc.center = c.pos + Vector3.up * (0.3f + (c.def.Spec.H - 0.3f) / 2);
                bc.size = new Vector3(Mathf.Abs(sz.x), Mathf.Abs(sz.y), Mathf.Abs(sz.z));
            }
            m.Build("Cars", go.transform, 12, true);
            return go;
        }

        public static ParkedCar ParkedNear(Vector3 p, float maxd)
        {
            if (I == null) return null;
            ParkedCar best = null; float bd = maxd;
            foreach (var kv in I.parked.Values)
                foreach (var c in kv.list)
                {
                    if (c.taken) continue;
                    float d = Vector3.Distance(c.DoorStand, p);
                    if (d < bd) { bd = d; best = c; }
                }
            return best;
        }

        /// <summary>Turns a parked car into a real one the player can drive.</summary>
        public static Car TakeParked(ParkedCar c)
        {
            if (I == null || c.taken) return null;
            c.taken = true;
            if (I.parked.TryGetValue(c.chunk, out var e))
            {
                Destroy(e.go);
                I.parked[c.chunk] = (I.BuildParked(c.chunk.Item1, c.chunk.Item2, e.list), e.list);
            }
            return Car.Spawn(c.def, c.color, c.pos + Vector3.up * 0.02f, c.yaw, false, -1, 0);
        }

        void StepParked(int cx, int cz)
        {
            int R = Gfx.Level == 2 ? 2 : 1;
            var drop = new List<(int, int)>();
            foreach (var k in parked.Keys)
                if (Mathf.Abs(k.Item1 - cx) > R + 1 || Mathf.Abs(k.Item2 - cz) > R + 1) drop.Add(k);
            foreach (var k in drop) { Destroy(parked[k].go); parked.Remove(k); }
            for (int x = cx - R; x <= cx + R; x++)
                for (int z = cz - R; z <= cz + R; z++)
                {
                    if (x < 0 || z < 0 || x >= City.N || z >= City.N || parked.ContainsKey((x, z))) continue;
                    var list = ParkedSpots(x, z);
                    parked[(x, z)] = (BuildParked(x, z, list), list);
                    return;
                }
        }

        public static void Create()
        {
            var go = new GameObject("Streamer");
            I = go.AddComponent<Streamer>();
            I.root = go.transform;
        }

        public static void SetOutdoor(bool on)
        {
            Outdoor = on;
            if (WorldBuilder.Root != null) WorldBuilder.Root.gameObject.SetActive(on);
            if (I != null) I.root.gameObject.SetActive(on);
            foreach (var c in Car.Live.ToArray()) if (c != null) c.gameObject.SetActive(on || c == PlayerCtl.I?.Car);
            Traffic.SetVisible(on);
        }

        /// <summary>Load everything around the player at once (after teleports).</summary>
        public static void ForceNear()
        {
            if (I == null) return;
            for (int i = 0; i < 40; i++) I.Step(true);
        }

        bool outskirtsDoors;

        void Update()
        {
            if (!outskirtsDoors && WorldBuilder.Root != null)
            {
                outskirtsDoors = true;
                foreach (var b in City.Outskirts) { AutoDoor.Make(root, b); Sign(root, b); }
            }
            if (!Outdoor) return;
            Step(false);
        }

        void Step(bool all)
        {
            var pl = PlayerCtl.I;
            if (pl == null) return;
            var p = pl.transform.position;
            var (cx, cz) = City.ChunkOf(p);
            int R = Gfx.DetailRadius;
            // unload far ones
            var drop = new List<(int, int)>();
            foreach (var k in loaded.Keys)
                if (Mathf.Abs(k.Item1 - cx) > R + 1 || Mathf.Abs(k.Item2 - cz) > R + 1) drop.Add(k);
            foreach (var k in drop) { Destroy(loaded[k]); loaded.Remove(k); }
            // load the nearest missing one
            (int, int)? best = null; int bd = int.MaxValue;
            for (int x = cx - R; x <= cx + R; x++)
                for (int z = cz - R; z <= cz + R; z++)
                {
                    if (x < 0 || z < 0 || x >= City.N || z >= City.N || loaded.ContainsKey((x, z))) continue;
                    int d = Mathf.Abs(x - cx) + Mathf.Abs(z - cz);
                    if (d < bd) { bd = d; best = (x, z); }
                }
            if (best.HasValue) loaded[best.Value] = BuildChunk(best.Value.Item1, best.Value.Item2);
            StepParked(cx, cz);
        }

        // ------------------------------------------------------------------ chunk detail

        static Material trunk, leafA, leafB, leafC, benchMat, binMat, poleMat, hedge, pool, fence, redL, greenL, amberL;

        static void Mats0()
        {
            if (trunk != null) return;
            trunk = Photo.Mat("dark_wood", new Color(0.55f, 0.45f, 0.38f), 0.1f);
            leafA = Photo.Mat("aerial_grass_rock", new Color(0.38f, 0.62f, 0.3f), 0.08f, 1.6f);
            leafB = Photo.Mat("aerial_grass_rock", new Color(0.48f, 0.7f, 0.32f), 0.08f, 1.6f);
            leafC = Photo.Mat("aerial_grass_rock", new Color(0.3f, 0.5f, 0.3f), 0.08f, 1.6f);
            benchMat = WorldBuilder.Wood;
            binMat = Mats.Lit(new Color(0.2f, 0.35f, 0.25f));
            poleMat = Mats.Lit(new Color(0.22f, 0.23f, 0.25f), 0.5f, 0.6f);
            hedge = Mats.Lit(new Color(0.16f, 0.36f, 0.16f));
            pool = Mats.Glass(new Color(0.2f, 0.65f, 0.85f, 0.85f));
            fence = Mats.Lit(new Color(0.95f, 0.95f, 0.93f));
            redL = Mats.Glow(new Color(1f, 0.15f, 0.1f)); greenL = Mats.Glow(new Color(0.2f, 1f, 0.4f)); amberL = Mats.Glow(new Color(1f, 0.7f, 0.1f));
        }

        static void M(MeshB m, PrimitiveType t, Vector3 p, Vector3 s, Material mat, float yaw = 0) =>
            m.Add(Prim.MeshOf(t), Matrix4x4.TRS(p, Quaternion.Euler(0, yaw, 0), s), mat);

        GameObject BuildChunk(int bx, int bz)
        {
            Mats0();
            var go = new GameObject($"Detail {bx},{bz}") { layer = 12 };
            go.transform.SetParent(root, false);
            var m = new MeshB();
            var rng = new Rng(H.U(bx, bz, 777));
            var d = City.Districts[bx, bz];
            float X0 = bx * City.P, Z0 = bz * City.P, X1 = X0 + City.P, Z1 = Z0 + City.P, y = City.Curb;

            void Col(Vector3 c, Vector3 s) { var b = go.AddComponent<BoxCollider>(); b.center = c; b.size = s; }

            // street lamps along the four sides (on the sidewalk, 7.2 m from the road centre lines)
            for (float s = X0 + 14; s < X1 - 10; s += 26)
            {
                Lamp(m, new Vector3(s, y, Z0 + 7.2f), 180); Col(new Vector3(s, 3, Z0 + 7.2f), new Vector3(0.3f, 6, 0.3f));
                Lamp(m, new Vector3(s, y, Z1 - 7.2f), 0); Col(new Vector3(s, 3, Z1 - 7.2f), new Vector3(0.3f, 6, 0.3f));
            }
            for (float s = Z0 + 27; s < Z1 - 10; s += 26)
            {
                Lamp(m, new Vector3(X0 + 7.2f, y, s), 270); Col(new Vector3(X0 + 7.2f, 3, s), new Vector3(0.3f, 6, 0.3f));
                Lamp(m, new Vector3(X1 - 7.2f, y, s), 90); Col(new Vector3(X1 - 7.2f, 3, s), new Vector3(0.3f, 6, 0.3f));
            }
            // road signs and bollards at the corners
            var signRed = Mats.Lit(new Color(0.8f, 0.08f, 0.08f), 0.4f); var signBlue = Mats.Lit(new Color(0.1f, 0.3f, 0.75f), 0.4f); var signWhite = Mats.Lit(new Color(0.95f, 0.95f, 0.95f), 0.4f);
            foreach (var (cx, cz, yaw) in new[] { (X0 + 6.6f, Z1 - 6.6f, 0f), (X1 - 6.6f, Z0 + 6.6f, 180f), (X1 - 6.6f, Z1 - 6.6f, 90f) })
            {
                var sp = new Vector3(cx, y, cz);
                M(m, PrimitiveType.Cylinder, sp + Vector3.up * 1.3f, new Vector3(0.07f, 1.3f, 0.07f), poleMat);
                var rot = Quaternion.Euler(0, yaw, 0);
                int kind = H.R(3, bx, bz, (int)yaw);
                if (kind == 0)
                {   // speed limit: white disc with red rim
                    m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(sp + Vector3.up * 2.45f, rot * Quaternion.Euler(90, 0, 0), new Vector3(0.62f, 0.02f, 0.62f)), signRed);
                    m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(sp + Vector3.up * 2.45f + rot * new Vector3(0, 0, -0.022f), rot * Quaternion.Euler(90, 0, 0), new Vector3(0.48f, 0.01f, 0.48f)), signWhite);
                }
                else if (kind == 1)
                {   // pedestrian crossing: blue square with a white triangle
                    m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(sp + Vector3.up * 2.45f, rot, new Vector3(0.6f, 0.6f, 0.03f)), signBlue);
                    m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(sp + Vector3.up * 2.42f + rot * new Vector3(0, 0, -0.02f), rot * Quaternion.Euler(0, 0, 45), new Vector3(0.3f, 0.3f, 0.01f)), signWhite);
                }
                else
                {   // no-entry: red disc with a white bar
                    m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(sp + Vector3.up * 2.45f, rot * Quaternion.Euler(90, 0, 0), new Vector3(0.6f, 0.02f, 0.6f)), signRed);
                    m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(sp + Vector3.up * 2.45f + rot * new Vector3(0, 0, -0.022f), rot, new Vector3(0.42f, 0.1f, 0.01f)), signWhite);
                }
                // bollards along the corner
                for (int k = 0; k < 4; k++)
                {
                    var bp = sp + rot * new Vector3(0.9f + k * 0.9f, 0, 0.6f);
                    M(m, PrimitiveType.Cylinder, bp + Vector3.up * 0.45f, new Vector3(0.14f, 0.45f, 0.14f), poleMat);
                    M(m, PrimitiveType.Cylinder, bp + Vector3.up * 0.8f, new Vector3(0.15f, 0.03f, 0.15f), FutureCity.NeonMat(0));
                }
            }
            // traffic light at the south-west crossroads corner
            TrafficLight(m, new Vector3(X0 + 6.8f, y, Z0 + 6.8f), (bx + bz) % 2 == 0);
            Col(new Vector3(X0 + 6.8f, 2.5f, Z0 + 6.8f), new Vector3(0.3f, 5, 0.3f));

            bool green = d == District.Residential || d == District.Suburb || d == District.Park || d == District.Beach || d == District.Business;
            // sidewalk trees
            if (green)
                for (float s = X0 + 20; s < X1 - 12; s += 26)
                    foreach (float zz in new[] { Z0 + 8.4f, Z1 - 8.4f })
                        if (rng.Chance(0.75f)) { Tree(m, new Vector3(s, y, zz), rng, 0.8f); Col(new Vector3(s, 1.5f, zz), new Vector3(0.4f, 3, 0.4f)); }
            // benches and bins
            for (int i = 0; i < 3; i++)
            {
                var bp = new Vector3(X0 + 18 + i * 22, y, Z0 + 8.6f);
                if (rng.Chance(0.5f)) Bench(m, bp, 0);
                M(m, PrimitiveType.Cylinder, new Vector3(X1 - 16 - i * 22, y + 0.45f, Z1 - 8.6f), new Vector3(0.5f, 0.45f, 0.5f), binMat);
            }
            // bus stop on some blocks
            if (rng.Chance(0.35f))
            {
                var bs = new Vector3(X0 + City.P / 2, y, Z0 + 8.6f);
                M(m, PrimitiveType.Cube, bs + new Vector3(0, 2.4f, 0.4f), new Vector3(4, 0.1f, 1.6f), WorldBuilder.Metal);
                M(m, PrimitiveType.Cube, bs + new Vector3(0, 1.2f, 1.1f), new Vector3(4, 2.4f, 0.05f), Mats.Glass(new Color(0.6f, 0.8f, 0.9f, 0.4f)));
                M(m, PrimitiveType.Cube, bs + new Vector3(-1.9f, 1.2f, 0.4f), new Vector3(0.08f, 2.4f, 1.4f), WorldBuilder.Metal);
                Bench(m, bs + new Vector3(0, 0, 0.6f), 0);
                M(m, PrimitiveType.Cube, bs + new Vector3(1.6f, 1.6f, 1.05f), new Vector3(0.8f, 1.2f, 0.06f), Mats.Glow(new Color(0.9f, 0.95f, 1f)));
            }

            // inside the block
            if (d == District.Park) Park(m, rng, X0, Z0, go);
            foreach (var b in City.ByChunk[bx, bz])
            {
                if (b.type == BType.Mall || b.type == BType.Apartment) CarPark(m, b);
                if (HouseWorld.IsReal(b))
                {   // 0.7: a real house (HouseWorld): just the garden here
                    if (b.type == BType.House) Garden(m, b, rng, false);
                    else if (b.type == BType.Villa) Garden(m, b, rng, true);
                    continue;
                }
                Sign(go.transform, b);
                AutoDoor.Make(go.transform, b);
                BuildingDetail.Add(m, b, new Rng(H.U(b.id, 991)));
                if ((b.type == BType.Tower || b.type == BType.Office || b.type == BType.Agency) && b.height > 28 && H.R(2, b.id, 21) == 0)
                {
                    var side = Vector3.Cross(Vector3.up, b.FrontDir);
                    float span = (b.front <= 1 ? b.rect.width : b.rect.height);
                    float bw = Mathf.Min(14f, span * 0.6f);
                    Billboard.Make(go.transform, b.door + b.FrontDir * 0.5f + Vector3.up * Mathf.Min(24f, b.height * 0.45f) + side * (span * 0.2f * (H.R(2, b.id, 22) == 0 ? 1 : -1)), Quaternion.LookRotation(-b.FrontDir), bw, bw * 0.45f, b.id);
                }
                if (b.type == BType.House) Garden(m, b, rng, false);
                else if (b.type == BType.Villa) Garden(m, b, rng, true);
                else if (b.type == BType.GasStation)
                {
                    float fz = b.front == 0 ? b.rect.yMin - 13 : b.rect.yMax + 3;
                    for (int k = 0; k < 2; k++)
                    {
                        var pp = new Vector3(b.rect.center.x - 3 + k * 6, y, fz + 5);
                        M(m, PrimitiveType.Cube, pp + Vector3.up * 0.8f, new Vector3(0.8f, 1.6f, 0.5f), Mats.Lit(new Color(0.85f, 0.85f, 0.88f)));
                        M(m, PrimitiveType.Cube, pp + new Vector3(0, 1.3f, 0.26f), new Vector3(0.5f, 0.35f, 0.02f), Mats.Glow(new Color(0.3f, 1f, 0.5f)));
                        Col(pp + Vector3.up * 0.8f, new Vector3(0.8f, 1.6f, 0.5f));
                    }
                }
                else if (d == District.Industrial)
                {
                    for (int k = 0; k < 5; k++)
                    {
                        var cp = new Vector3(rng.F(b.rect.xMin, b.rect.xMax), y, b.front == 0 ? b.rect.yMax + 1.5f : b.rect.yMin - 1.5f);
                        M(m, PrimitiveType.Cube, cp + Vector3.up * 0.5f, Vector3.one, WorldBuilder.Wood, rng.F(0, 90));
                    }
                }
                else if (green && rng.Chance(0.7f))
                {
                    // a few trees and hedges around apartment blocks
                    for (int k = 0; k < 3; k++)
                    {
                        var tp = new Vector3(rng.F(X0 + 12, X1 - 12), y, rng.F(Z0 + 12, Z1 - 12));
                        if (!Inside(tp, 1.5f)) { Tree(m, tp, rng, 1f); Col(tp + Vector3.up * 1.5f, new Vector3(0.5f, 3, 0.5f)); }
                    }
                }
            }
            var mesh = m.Build("Props", go.transform, 12, true);
            return go;

            bool Inside(Vector3 p, float pad)
            {
                foreach (var b in City.ByChunk[bx, bz])
                {
                    var r = b.rect;
                    if (p.x > r.xMin - pad && p.x < r.xMax + pad && p.z > r.yMin - pad - (b.type == BType.GasStation ? 14 : 0) && p.z < r.yMax + pad + (b.type == BType.GasStation ? 14 : 0)) return true;
                    var dd = b.door - p; if (Mathf.Abs(dd.x) < 4 && Mathf.Abs(dd.z) < 4) return true;
                }
                return false;
            }
        }

        static void Lamp(MeshB m, Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            M(m, PrimitiveType.Cylinder, p + Vector3.up * 3f, new Vector3(0.16f, 3f, 0.16f), poleMat);
            var arm = rot * Vector3.forward;
            m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(p + Vector3.up * 6f + arm * 0.9f, rot, new Vector3(0.1f, 0.1f, 1.8f)), poleMat);
            m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(p + Vector3.up * 5.92f + arm * 1.7f, rot, new Vector3(0.36f, 0.12f, 0.7f)), poleMat);
            m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(p + Vector3.up * 5.85f + arm * 1.7f, rot, new Vector3(0.3f, 0.04f, 0.6f)), WorldBuilder.LampGlow);
            // a neon strip up the pole (city of the future)
            m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(p + Vector3.up * 2.2f - arm * 0.09f, rot, new Vector3(0.05f, 3.2f, 0.03f)), FutureCity.NeonMat(0));
        }

        static void TrafficLight(MeshB m, Vector3 p, bool red)
        {
            M(m, PrimitiveType.Cylinder, p + Vector3.up * 2.4f, new Vector3(0.14f, 2.4f, 0.14f), poleMat);
            foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
            {
                var rot = Quaternion.Euler(0, yaw, 0);
                var f = rot * Vector3.forward * 0.2f;
                m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(p + Vector3.up * 4.3f + f, rot, new Vector3(0.32f, 0.9f, 0.18f)), Mats.Lit(new Color(0.1f, 0.1f, 0.1f)));
                bool r = (yaw % 180 == 0) == red;
                m.Add(Prim.MeshOf(PrimitiveType.Sphere), Matrix4x4.TRS(p + Vector3.up * 4.6f + f * 1.5f, rot, Vector3.one * 0.18f), r ? redL : Mats.Lit(new Color(0.25f, 0.05f, 0.05f)));
                m.Add(Prim.MeshOf(PrimitiveType.Sphere), Matrix4x4.TRS(p + Vector3.up * 4.0f + f * 1.5f, rot, Vector3.one * 0.18f), !r ? greenL : Mats.Lit(new Color(0.05f, 0.2f, 0.08f)));
            }
        }

        static Mesh[] crowns;

        /// <summary>Lumpy leaf clusters: a sphere pushed in and out by noise (looks like foliage, not a ball).</summary>
        static Mesh Crown(int v)
        {
            if (crowns == null) crowns = new Mesh[4];
            if (crowns[v] != null) return crowns[v];
            var src = Prim.MeshOf(PrimitiveType.Sphere);
            var verts = src.vertices;
            var uv = new Vector2[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var d = verts[i].normalized;
                float n = Mathf.PerlinNoise(d.x * 2.3f + v * 7, d.z * 2.3f + d.y * 1.7f + v * 3) * 0.55f
                        + Mathf.PerlinNoise(d.x * 6f + v, d.y * 6f + d.z * 6f) * 0.25f;
                verts[i] = d * (0.36f + n * 0.32f);
                if (verts[i].y < -0.05f) verts[i].y *= 0.55f;          // flatter underside
                uv[i] = new Vector2(Mathf.Atan2(d.z, d.x) * 0.8f, d.y * 1.2f);
            }
            var m = new Mesh { name = "crown" + v, vertices = verts, triangles = src.triangles, uv = uv };
            m.RecalculateNormals(); m.RecalculateBounds();
            crowns[v] = m;
            return m;
        }

        static void Tree(MeshB m, Vector3 p, Rng r, float scale)
        {
            float s = scale * r.F(0.8f, 1.25f);
            M(m, PrimitiveType.Cylinder, p + Vector3.up * 1.6f * s, new Vector3(0.24f, 1.6f, 0.24f) * s, trunk);
            // two branches
            m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(p + new Vector3(0.35f, 3.1f, 0) * s, Quaternion.Euler(0, 0, -35), new Vector3(0.12f, 0.6f, 0.12f) * s), trunk);
            m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(p + new Vector3(-0.3f, 3.3f, 0.2f) * s, Quaternion.Euler(20, 0, 30), new Vector3(0.11f, 0.55f, 0.11f) * s), trunk);
            var leaf = r.I(3) switch { 0 => leafA, 1 => leafB, _ => leafC };
            int k = 4 + r.I(3);
            for (int i = 0; i < k; i++)
            {
                float a = i / (float)k * 360f + r.F(0, 40);
                var off = Quaternion.Euler(0, a, 0) * new Vector3(r.F(0.5f, 1.1f), r.F(-0.3f, 0.9f), 0);
                if (i == 0) off = new Vector3(0, 1.0f, 0);
                float cs = r.F(1.9f, 2.8f);
                m.Add(Crown(r.I(4)), Matrix4x4.TRS(p + (Vector3.up * 4.2f + off) * s, Quaternion.Euler(r.F(-15, 15), r.F(0, 360), 0), new Vector3(cs, cs * 0.85f, cs) * s), leaf);
            }
        }

        static void Bench(MeshB m, Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(p + Vector3.up * 0.45f, rot, new Vector3(1.8f, 0.08f, 0.5f)), benchMat);
            m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(p + Vector3.up * 0.75f + rot * new Vector3(0, 0, 0.22f), rot, new Vector3(1.8f, 0.4f, 0.06f)), benchMat);
            foreach (float sx in new[] { -0.8f, 0.8f })
                m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(p + Vector3.up * 0.22f + rot * new Vector3(sx, 0, 0), rot, new Vector3(0.08f, 0.44f, 0.45f)), poleMat);
        }

        /// <summary>0.9: asphalt and white bay lines for the car parks of the mall and behind the flats.</summary>
        static void CarPark(MeshB m, Bld b)
        {
            var fd = b.FrontDir; var side = Vector3.Cross(Vector3.up, fd);
            float span = b.front <= 1 ? b.rect.width : b.rect.height, depth = b.front <= 1 ? b.rect.height : b.rect.width;
            var door = new Vector3(b.door.x, City.Curb + 0.02f, b.door.z);
            var paint = WorldBuilder.Paint;
            void Area(Vector3 c, float along, float across)
            {
                var a = c - side * along / 2f - fd * across / 2f; var e = c + side * along / 2f + fd * across / 2f;
                m.Flat(WorldBuilder.Asphalt, Mathf.Min(a.x, e.x), Mathf.Min(a.z, e.z), Mathf.Max(a.x, e.x), Mathf.Max(a.z, e.z), c.y, 6f);
            }
            void Lines(Vector3 c, float halfLen, float len)
            {
                for (float s = -halfLen - 1.35f; s <= halfLen + 1.4f; s += 2.7f)
                {
                    var p0 = c + side * s - fd * len / 2f + Vector3.up * 0.01f; var p1 = c + side * s + fd * len / 2f + Vector3.up * 0.01f;
                    var lo = Vector3.Min(p0, p1) - new Vector3(0.05f, 0, 0.05f); var hi = Vector3.Max(p0, p1) + new Vector3(0.05f, 0.005f, 0.05f);
                    m.Box(paint, paint, lo, hi, new Vector2(1, 1));
                }
            }
            if (b.type == BType.Mall)
            {
                Area(door + fd * 7.4f, span, 15f);
                Lines(door + fd * 3.2f, span / 2f - 2f, 4.8f);
                Lines(door + fd * 11.6f, span / 2f - 2f, 4.8f);
            }
            else
            {
                Area(door - fd * (depth + 3.0f), span, 5.6f);
                Lines(door - fd * (depth + 3.0f), span / 2f - 1.5f, 4.8f);
            }
        }

        static void Garden(MeshB m, Bld b, Rng r, bool villa)
        {
            var lotMin = new Vector2(b.rect.xMin - 3, b.front == 0 ? b.rect.yMin - (villa ? 9 : 5) : b.rect.yMin - 4);
            float y = City.Curb;
            // driveway / path to the door
            var dd = b.door;
            float pz0 = b.front == 0 ? dd.z - (villa ? 12 : 6) : dd.z, pz1 = b.front == 0 ? dd.z : dd.z + (villa ? 12 : 6);
            m.Flat(WorldBuilder.Plaza, dd.x - 1.2f, pz0, dd.x + 1.2f, pz1, y + 0.01f, 2f);
            // hedge along the side (not where the house has its garage)
            bool garage = !villa && HouseDesign.ForCity(b).garage;
            if (!garage)
            {
                M(m, PrimitiveType.Cube, new Vector3(b.rect.xMin - 2.5f, y + 0.6f, b.rect.center.y), new Vector3(0.8f, 1.2f, b.rect.height + 4), hedge);
                M(m, PrimitiveType.Cube, new Vector3(b.rect.xMax + 2.5f, y + 0.6f, b.rect.center.y), new Vector3(0.8f, 1.2f, b.rect.height + 4), hedge);
            }
            if (villa)
            {
                // pool and loungers beside the villa
                float px = b.rect.center.x, pz = b.front == 0 ? b.rect.yMax + 5 : b.rect.yMin - 5;
                m.Box(WorldBuilder.Paint, WorldBuilder.Plaza, new Vector3(px - 6, y, pz - 3.5f), new Vector3(px + 6, y + 0.12f, pz + 3.5f), new Vector2(4, 4));
                m.Flat(pool, px - 4.5f, pz - 2.2f, px + 4.5f, pz + 2.2f, y + 0.14f, 4f);
                for (int k = 0; k < 3; k++)
                    M(m, PrimitiveType.Cube, new Vector3(px - 4 + k * 2, y + 0.3f, pz + 2.9f), new Vector3(0.7f, 0.15f, 1.8f), WorldBuilder.Paint);
                // palms
                for (int k = 0; k < 2; k++)
                {
                    var pp = new Vector3(b.rect.xMin - 1 + k * (b.rect.width + 2), y, b.door.z + (b.front == 0 ? -4 : 4));
                    M(m, PrimitiveType.Cylinder, pp + Vector3.up * 3.5f, new Vector3(0.4f, 3.5f, 0.4f), trunk);
                    for (int l = 0; l < 6; l++)
                        m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(pp + Vector3.up * 7f, Quaternion.Euler(-25, l * 60, 0), new Vector3(0.8f, 0.1f, 3.8f)) * Matrix4x4.Translate(new Vector3(0, 0, 0.45f)), leafB);
                }
            }
            else
            {
                // white picket fence along the front
                float fz = b.front == 0 ? b.rect.yMin - 4.5f : b.rect.yMax + 4.5f;
                for (float x = b.rect.xMin - 2.5f; x < b.rect.xMax + 2.5f; x += 0.5f)
                    if (Mathf.Abs(x - dd.x) > 1.4f) M(m, PrimitiveType.Cube, new Vector3(x, y + 0.45f, fz), new Vector3(0.08f, 0.9f, 0.06f), fence);
                M(m, PrimitiveType.Cube, new Vector3(b.rect.center.x, y + 0.7f, fz), new Vector3(b.rect.width + 5, 0.08f, 0.05f), fence);
                if (r.Chance(0.6f)) { var tp = new Vector3(b.rect.xMax + 1.2f, y, b.rect.yMax + 1); Tree(m, tp, r, 0.9f); }
            }
            _ = lotMin;
        }

        static void Park(MeshB m, Rng r, float X0, float Z0, GameObject go)
        {
            float y = City.Curb + 0.01f, c = City.P / 2;
            m.Flat(WorldBuilder.Plaza, X0 + City.Walk, Z0 + c - 1.5f, X0 + City.P - City.Walk, Z0 + c + 1.5f, y, 2f);
            m.Flat(WorldBuilder.Plaza, X0 + c - 1.5f, Z0 + City.Walk, X0 + c + 1.5f, Z0 + City.P - City.Walk, y, 2f);
            // fountain
            var f = new Vector3(X0 + c, City.Curb, Z0 + c);
            M(m, PrimitiveType.Cylinder, f + Vector3.up * 0.35f, new Vector3(9, 0.35f, 9), WorldBuilder.Concrete);
            M(m, PrimitiveType.Cylinder, f + Vector3.up * 0.71f, new Vector3(8.2f, 0.02f, 8.2f), WorldBuilder.Water);
            M(m, PrimitiveType.Cylinder, f + Vector3.up * 1.6f, new Vector3(1.2f, 1.2f, 1.2f), WorldBuilder.Concrete);
            M(m, PrimitiveType.Sphere, f + Vector3.up * 3.2f, new Vector3(1.6f, 1.2f, 1.6f), Mats.Glass(new Color(0.7f, 0.9f, 1f, 0.5f)));
            var col = go.AddComponent<BoxCollider>(); col.center = f + Vector3.up * 0.5f; col.size = new Vector3(8.5f, 1f, 8.5f);
            for (int i = 0; i < 26; i++)
            {
                var p = new Vector3(X0 + r.F(City.Walk + 2, City.P - City.Walk - 2), City.Curb, Z0 + r.F(City.Walk + 2, City.P - City.Walk - 2));
                if (Mathf.Abs(p.x - f.x) < 3 || Mathf.Abs(p.z - f.z) < 3 || (p - f).magnitude < 8) continue;
                Tree(m, p, r, 1.2f);
                var tc = go.AddComponent<BoxCollider>(); tc.center = p + Vector3.up * 1.5f; tc.size = new Vector3(0.6f, 3, 0.6f);
            }
            for (int i = 0; i < 6; i++) Bench(m, new Vector3(X0 + c - 10 + i * 4, City.Curb, Z0 + c + 2.3f), 180);
        }

        static void Sign(Transform parent, Bld b)
        {
            if (b.type == BType.House) return;
            var go = new GameObject("Sign " + b.name);
            go.transform.SetParent(parent, false);
            float hh = b.type == BType.Mall || b.type == BType.Airport || b.type == BType.Station ? 4.6f : 3.6f;
            bool commercial = b.Info.BizPrice > 0 && b.type != BType.Tower && b.type != BType.Office && b.type != BType.Factory && b.type != BType.Warehouse;
            if (commercial && b.height > 4.6f) hh = Mathf.Min(b.height, 4.4f) - 0.6f;
            go.transform.position = b.door + b.FrontDir * 0.2f + Vector3.up * hh;
            go.transform.rotation = Quaternion.LookRotation(-b.FrontDir);
            var t = go.AddComponent<TextMeshPro>();
            t.font = UIKit.Font;
            t.fontSharedMaterial = UIKit.TextOutline;
            t.text = b.name;
            t.fontSize = b.type == BType.Mall ? 9 : 6;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(b.front <= 1 ? b.rect.width : b.rect.height, 2);
            t.enableAutoSizing = true; t.fontSizeMin = 2; t.fontSizeMax = t.fontSize;
        }
    }
}
