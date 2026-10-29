using System.Collections.Generic;
using UnityEngine;

namespace Gad
{
    /// <summary>
    /// 0.9: roads out of the grid that bend and lead somewhere: the west highway to the lake (water,
    /// a pier, a café, trees) and the coast road along the sea to the lighthouse. Smooth curves
    /// (Catmull-Rom), lane markings, lamps, guard rails by the sea, a few cars driving along them, and
    /// routes the taxi can follow.
    /// </summary>
    public static class OutRoads
    {
        public class Road { public string name; public List<Vector3> pts; public Vector3 end; }
        public static readonly List<Road> All = new List<Road>();
        public static Vector3 LakeCentre => new Vector3(-820f, 0f, 470f);
        public static Vector3 Lighthouse => new Vector3(City.Size + 660f, 0f, -128f);
        const float Width = 11f;

        static List<Vector3> Spline(Vector3[] c, float step)
        {
            var o = new List<Vector3>();
            for (int i = 0; i < c.Length - 1; i++)
            {
                var p0 = c[Mathf.Max(0, i - 1)]; var p1 = c[i]; var p2 = c[i + 1]; var p3 = c[Mathf.Min(c.Length - 1, i + 2)];
                int n = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(p1, p2) / step));
                for (int k = 0; k < n; k++)
                {
                    float t = k / (float)n, t2 = t * t, t3 = t2 * t;
                    o.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            o.Add(c[c.Length - 1]);
            return o;
        }

        public static void Build(MeshB m)
        {
            All.Clear();
            float S = City.Size, P = City.P;
            // the west highway: from the end of the street on grid line z = 3P, past the railway, to the lake
            var west = Spline(new[] { new Vector3(-City.Road, 0, 3 * P), new Vector3(-90, 0, 3 * P + 4), new Vector3(-220, 0, 3 * P + 70), new Vector3(-360, 0, 3 * P + 40),
                new Vector3(-500, 0, 3 * P + 120), new Vector3(-600, 0, 3 * P + 210), new Vector3(-660, 0, 3 * P + 230) }, 3f);
            // the coast road: from the south-east corner along the shore to the lighthouse
            var coast = Spline(new[] { new Vector3(S + City.Road, 0, 0), new Vector3(S + 90, 0, -6), new Vector3(S + 200, 0, -40), new Vector3(S + 330, 0, -20),
                new Vector3(S + 450, 0, -70), new Vector3(S + 560, 0, -96), new Vector3(S + 630, 0, -110) }, 3f);
            All.Add(new Road { name = "Озеро", pts = west, end = west[west.Count - 1] });
            All.Add(new Road { name = "Маяк", pts = coast, end = coast[coast.Count - 1] });
            Strip(m, west, false);
            Strip(m, coast, true);
            Lake(m);
            LighthouseSite(m);
        }

        /// <summary>The road surface, lines, lamps and (by the sea) a guard rail along a smooth path.</summary>
        static void Strip(MeshB m, List<Vector3> pts, bool seaRail)
        {
            var asphalt = WorldBuilder.Asphalt; var paint = WorldBuilder.Paint; var metal = WorldBuilder.Metal;
            float hw = Width / 2f, run = 0f, y = 0.015f;
            Vector3 Right(int i)
            {
                var a = pts[Mathf.Max(0, i - 1)]; var b = pts[Mathf.Min(pts.Count - 1, i + 1)];
                var d = (b - a); d.y = 0; d.Normalize();
                return new Vector3(d.z, 0, -d.x);
            }
            for (int i = 0; i < pts.Count - 1; i++)
            {
                var r0 = Right(i); var r1 = Right(i + 1);
                var p0 = pts[i] + Vector3.up * y; var p1 = pts[i + 1] + Vector3.up * y;
                float len = Vector3.Distance(p0, p1);
                // asphalt, uv along the road in metres
                m.Quad(asphalt, p0 - r0 * hw, p1 - r1 * hw, p1 + r1 * hw, p0 + r0 * hw,
                    new Vector2(0, run / 8f), new Vector2(0, (run + len) / 8f), new Vector2(Width / 8f, (run + len) / 8f), new Vector2(Width / 8f, run / 8f));
                // edge lines and the dashed middle line
                foreach (float off in new[] { -hw + 0.5f, hw - 0.5f })
                    m.Quad(paint, p0 + r0 * (off - 0.08f) + Vector3.up * 0.01f, p1 + r1 * (off - 0.08f) + Vector3.up * 0.01f, p1 + r1 * (off + 0.08f) + Vector3.up * 0.01f, p0 + r0 * (off + 0.08f) + Vector3.up * 0.01f, Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                if (Mathf.Repeat(run, 6f) < 3f)
                    m.Quad(paint, p0 - r0 * 0.08f + Vector3.up * 0.01f, p1 - r1 * 0.08f + Vector3.up * 0.01f, p1 + r1 * 0.08f + Vector3.up * 0.01f, p0 + r0 * 0.08f + Vector3.up * 0.01f, Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                // lamps every ~45 m on alternate sides
                if (Mathf.Repeat(run, 45f) + len >= 45f)
                {
                    float sd = Mathf.Repeat(run, 90f) < 45f ? 1f : -1f;
                    var lp = pts[i] + r0 * sd * (hw + 1.2f);
                    m.Box(metal, metal, lp + new Vector3(-0.08f, 0, -0.08f), lp + new Vector3(0.08f, 6f, 0.08f), new Vector2(1, 1));
                    var head = lp - r0 * sd * 1.2f + Vector3.up * 5.9f;
                    m.Box(metal, metal, Vector3.Min(lp + Vector3.up * 5.9f, head) - new Vector3(0.05f, 0, 0.05f), Vector3.Max(lp + Vector3.up * 6.0f, head + Vector3.up * 0.1f) + new Vector3(0.05f, 0, 0.05f), new Vector2(1, 1));
                    m.Box(WorldBuilder.LampGlow, WorldBuilder.LampGlow, head + new Vector3(-0.25f, -0.12f, -0.25f), head + new Vector3(0.25f, 0f, 0.25f), new Vector2(1, 1));
                }
                // a guard rail on the sea side (the right-hand side going out of town is the sea: south)
                if (seaRail && Mathf.Repeat(run, 4f) + len >= 4f)
                {
                    var side = Vector3.Dot(r0, Vector3.back) > 0 ? r0 : -r0;
                    var gp = pts[i] + side * (hw + 0.8f);
                    m.Box(metal, metal, gp + new Vector3(-0.06f, 0, -0.06f), gp + new Vector3(0.06f, 0.75f, 0.06f), new Vector2(1, 1));
                }
                if (seaRail)
                {
                    var s0 = Vector3.Dot(r0, Vector3.back) > 0 ? r0 : -r0; var s1 = Vector3.Dot(r1, Vector3.back) > 0 ? r1 : -r1;
                    var a0 = pts[i] + s0 * (hw + 0.8f) + Vector3.up * 0.55f; var a1 = pts[i + 1] + s1 * (hw + 0.8f) + Vector3.up * 0.55f;
                    m.Quad(metal, a0, a0 + Vector3.up * 0.25f, a1 + Vector3.up * 0.25f, a1, Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                    m.Quad(metal, a1, a1 + Vector3.up * 0.25f, a0 + Vector3.up * 0.25f, a0, Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                }
                run += len;
            }
        }

        /// <summary>A lake with a sandy shore, a pier, benches and trees round it.</summary>
        static void Lake(MeshB m)
        {
            var c = LakeCentre;
            var water = Mats.Lit(new Color(0.12f, 0.4f, 0.55f), 0.95f);
            var sand = WorldBuilder.Sand;
            const int N = 48;
            for (int i = 0; i < N; i++)
            {
                float a0 = i / (float)N * Mathf.PI * 2f, a1 = (i + 1) / (float)N * Mathf.PI * 2f;
                float r0 = 170f + Mathf.Sin(a0 * 3f) * 22f + Mathf.Sin(a0 * 7f) * 9f, r1 = 170f + Mathf.Sin(a1 * 3f) * 22f + Mathf.Sin(a1 * 7f) * 9f;
                var e0 = c + new Vector3(Mathf.Cos(a0) * r0, 0, Mathf.Sin(a0) * r0); var e1 = c + new Vector3(Mathf.Cos(a1) * r1, 0, Mathf.Sin(a1) * r1);
                var s0 = c + new Vector3(Mathf.Cos(a0) * (r0 + 14f), 0, Mathf.Sin(a0) * (r0 + 14f)); var s1 = c + new Vector3(Mathf.Cos(a1) * (r1 + 14f), 0, Mathf.Sin(a1) * (r1 + 14f));
                m.Tri(water, c + Vector3.up * 0.06f, e1 + Vector3.up * 0.06f, e0 + Vector3.up * 0.06f, new Vector2(0.5f, 0.5f), Vector2.one, Vector2.zero);
                m.Quad(sand, e0 + Vector3.up * 0.01f, e1 + Vector3.up * 0.01f, s1 + Vector3.up * 0.0f, s0 + Vector3.up * 0.0f, Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
            }
            // the pier towards the middle, from the end of the road
            var road = All[0].end;
            var dir = (c - road); dir.y = 0; dir.Normalize();
            var side = new Vector3(dir.z, 0, -dir.x);
            var deck = Photo.Mat("wood_floor_deck", Color.white, 0.2f);
            var start = c - dir * 175f;
            for (int k = 0; k < 18; k++)
            {
                var p = start + dir * (k * 3f);
                var lo = p - side * 1.6f; var hi = p + side * 1.6f + dir * 3f;
                m.Box(deck, deck, Vector3.Min(lo, hi) + Vector3.up * 0.35f, Vector3.Max(lo, hi) + Vector3.up * 0.5f, new Vector2(2, 2));
                foreach (float s in new[] { -1.5f, 1.5f })
                {
                    var post = p + side * s;
                    m.Box(WorldBuilder.Wood, WorldBuilder.Wood, post + new Vector3(-0.1f, -0.5f, -0.1f), post + new Vector3(0.1f, 1.4f, 0.1f), new Vector2(1, 1));
                }
            }
            // a car park at the end of the road and benches on the shore
            m.Flat(WorldBuilder.Asphalt, road.x - 14, road.z - 12, road.x + 14, road.z + 12, 0.012f, 8f);
            var rng = new Rng(4242);
            for (int i = 0; i < 40; i++)
            {
                float a = rng.F(0f, Mathf.PI * 2f), r = rng.F(200f, 260f);
                var tp = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                if (Vector3.Distance(tp, road) < 30f) continue;
                var trunk = Mats.Lit(new Color(0.35f, 0.24f, 0.16f));
                var leaf = Mats.Lit(new Color(0.18f + rng.F(0f, 0.1f), 0.42f + rng.F(0f, 0.12f), 0.18f));
                float hgt = rng.F(5f, 9f);
                m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(tp + Vector3.up * hgt * 0.35f, Quaternion.identity, new Vector3(0.35f, hgt * 0.35f, 0.35f)), trunk);
                m.Add(Prim.MeshOf(PrimitiveType.Sphere), Matrix4x4.TRS(tp + Vector3.up * hgt * 0.8f, Quaternion.identity, new Vector3(hgt * 0.55f, hgt * 0.6f, hgt * 0.55f)), leaf);
            }
        }

        /// <summary>A red and white lighthouse on a rock at the end of the coast road, with a keeper's house.</summary>
        static void LighthouseSite(MeshB m)
        {
            var c = Lighthouse;
            var rock = WorldBuilder.Rock ?? WorldBuilder.Concrete;
            m.Add(Prim.MeshOf(PrimitiveType.Sphere), Matrix4x4.TRS(c + new Vector3(18, -3f, -14), Quaternion.identity, new Vector3(46, 10, 38)), rock);
            var white = Mats.Lit(new Color(0.95f, 0.95f, 0.93f), 0.4f); var red = Mats.Lit(new Color(0.8f, 0.12f, 0.12f), 0.4f);
            for (int k = 0; k < 8; k++)
                m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(c + Vector3.up * (1.5f + k * 3f), Quaternion.identity, new Vector3(5.2f - k * 0.25f, 1.5f, 5.2f - k * 0.25f)), k % 2 == 0 ? white : red);
            m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(c + Vector3.up * 25.2f, Quaternion.identity, new Vector3(4.6f, 0.3f, 4.6f)), Mats.Lit(new Color(0.15f, 0.15f, 0.16f)));
            m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(c + Vector3.up * 26.8f, Quaternion.identity, new Vector3(3f, 1.3f, 3f)), Mats.Glow(new Color(1f, 0.92f, 0.6f)));
            m.Add(Prim.MeshOf(PrimitiveType.Sphere), Matrix4x4.TRS(c + Vector3.up * 28.6f, Quaternion.identity, new Vector3(3.4f, 2f, 3.4f)), red);
            var road = All[1].end;
            m.Flat(WorldBuilder.Asphalt, road.x - 12, road.z - 10, road.x + 12, road.z + 10, 0.012f, 8f);
        }

        /// <summary>The nearest point on any road (city grid or the roads out of town) to a point on the map.</summary>
        public static Vector3 NearestRoad(Vector3 p)
        {
            var best = City.NearestRoad(new Vector3(Mathf.Clamp(p.x, 0, City.Size), 0, Mathf.Clamp(p.z, 0, City.Size)), out _);
            float bd = Vector2.Distance(new Vector2(best.x, best.z), new Vector2(p.x, p.z));
            foreach (var r in All)
                foreach (var q in r.pts)
                {
                    float d = Vector2.Distance(new Vector2(q.x, q.z), new Vector2(p.x, p.z));
                    if (d < bd) { bd = d; best = q; }
                }
            // inside the city a point between the streets goes to the street beside it
            if (p.x > 0 && p.x < City.Size && p.z > 0 && p.z < City.Size) best = City.NearestRoad(p, out _);
            return new Vector3(best.x, 0.05f, best.z);
        }

        /// <summary>For the taxi: if the destination is out along one of these roads, the way there from the city edge.</summary>
        public static List<Vector3> TailTo(Vector3 target)
        {
            foreach (var r in All)
                if (Vector3.Distance(new Vector3(target.x, 0, target.z), r.end) < 160f) return r.pts;
            return null;
        }

        // ------------------------------------------------------------------ cars driving out and back

        public class Mover : MonoBehaviour
        {
            public List<Vector3> path; public float pos, speed = 13f; public int dir = 1; Car car;
            void Start() { car = GetComponent<Car>(); }
            void Update()
            {
                if (path == null || car == null) return;
                pos += dir * speed * Time.deltaTime;
                float len = 0;
                for (int i = 0; i < path.Count - 1; i++)
                {
                    float seg = Vector3.Distance(path[i], path[i + 1]);
                    if (pos <= len + seg || i == path.Count - 2)
                    {
                        float t = Mathf.Clamp01((pos - len) / seg);
                        var d = (path[i + 1] - path[i]).normalized * dir;
                        var right = new Vector3(d.z, 0, -d.x);
                        var p = Vector3.Lerp(path[i], path[i + 1], t) + right * 2.8f;
                        p.y = Car.GroundY(p, transform.position.y);
                        transform.SetPositionAndRotation(p, Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 4f));
                        car.Speed = speed;
                        car.SpinWheels(speed * Time.deltaTime);
                        break;
                    }
                    len += seg;
                }
                if (pos > len) { pos = len; dir = -1; }
                if (pos < 0) { pos = 0; dir = 1; }
            }
        }

        static float total(List<Vector3> p) { float s = 0; for (int i = 1; i < p.Count; i++) s += Vector3.Distance(p[i - 1], p[i]); return s; }

        /// <summary>A few cars going out to the lake and the lighthouse and back.</summary>
        public static void Traffic()
        {
            var rng = new Rng(9001);
            foreach (var r in All)
            {
                float L = total(r.pts);
                for (int k = 0; k < 3; k++)
                {
                    var def = CarDef.All[rng.I(CarDef.All.Count)];
                    var car = Car.Spawn(def, rng.I(10), r.pts[0], 0, true, -1, 1);
                    car.name = "Road car";
                    var mv = car.gameObject.AddComponent<Mover>();
                    mv.path = r.pts; mv.pos = L * (k + 0.5f) / 3f; mv.dir = k % 2 == 0 ? 1 : -1; mv.speed = rng.F(11f, 16f);
                }
            }
        }
    }
}
