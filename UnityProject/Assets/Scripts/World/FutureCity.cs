using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Gad
{
    /// <summary>
    /// The "city of the future" layer: neon outlines on towers (bright at night), holographic
    /// billboards, flying air-taxis in sky lanes above the streets, a maglev monorail looping round
    /// downtown, and the crown of the GAD City tower. Also clouds and wet roads in the rain.
    /// </summary>
    public class FutureCity : MonoBehaviour
    {
        public static FutureCity I;
        public static readonly List<(Material m, Color c)> Neon = new List<(Material, Color)>();
        static readonly Color[] NeonCols = { new Color(0.1f, 0.85f, 1f), new Color(1f, 0.15f, 0.65f), new Color(0.55f, 0.25f, 1f), new Color(0.2f, 1f, 0.55f), new Color(1f, 0.6f, 0.1f) };
        Transform root;

        public static Material NeonMat(int i)
        {
            while (Neon.Count < NeonCols.Length)
            {
                var c = NeonCols[Neon.Count];
                Neon.Add((new Material(Mats.Glow(c)) { name = "Neon" + Neon.Count }, c));
            }
            return Neon[Mathf.Abs(i) % Neon.Count].m;
        }

        /// <summary>Neon edges of a tower (corners and roof outline, a few horizontal bands).</summary>
        public static void Outline(MeshB m, Bld b, float x0, float z0, float x1, float z1, float y0, float y1)
        {
            var mat = NeonMat(H.R(5, b.id, 3));
            const float t = 0.22f, o = 0.06f;
            foreach (var (x, z) in new[] { (x0, z0), (x1, z0), (x0, z1), (x1, z1) })
                m.Box(mat, mat, new Vector3(x - t / 2 - (x == x0 ? o : -o), y0 + 0.3f, z - t / 2 - (z == z0 ? o : -o)), new Vector3(x + t / 2 - (x == x0 ? o : -o), y1 + 0.3f, z + t / 2 - (z == z0 ? o : -o)), new Vector2(1, 1));
            float y = y1 + 0.35f;
            m.Box(mat, mat, new Vector3(x0 - o, y, z0 - o - t), new Vector3(x1 + o, y + t, z0 - o), new Vector2(1, 1));
            m.Box(mat, mat, new Vector3(x0 - o, y, z1 + o), new Vector3(x1 + o, y + t, z1 + o + t), new Vector2(1, 1));
            m.Box(mat, mat, new Vector3(x0 - o - t, y, z0 - o), new Vector3(x0 - o, y + t, z1 + o), new Vector2(1, 1));
            m.Box(mat, mat, new Vector3(x1 + o, y, z0 - o), new Vector3(x1 + o + t, y + t, z1 + o), new Vector2(1, 1));
            if (H.R(3, b.id, 5) == 0)
            {
                var band = NeonMat(H.R(5, b.id, 6));
                for (float by = y0 + 13.2f; by < y1 - 4; by += 13.2f * (1 + H.R(2, b.id, 7)))
                {
                    m.Box(band, band, new Vector3(x0 - 0.08f, by, z0 - 0.08f), new Vector3(x1 + 0.08f, by + 0.15f, z0), new Vector2(1, 1));
                    m.Box(band, band, new Vector3(x0 - 0.08f, by, z1), new Vector3(x1 + 0.08f, by + 0.15f, z1 + 0.08f), new Vector2(1, 1));
                }
            }
        }

        public static void Create()
        {
            var go = new GameObject("FutureCity");
            I = go.AddComponent<FutureCity>();
            I.root = go.transform;
            I.BuildMonorail();
            I.BuildCrown();
            I.BuildClouds();
        }

        // ------------------------------------------------------------------ maglev monorail

        readonly List<Vector3> rail = new List<Vector3>();
        readonly List<float> railLen = new List<float>();
        float railTotal;
        Transform[] train;
        float trainS;
        const float RailY = 14f;

        void BuildMonorail()
        {
            float P = City.P, off = 8f;
            float xa = 3 * P + off, xb = 10 * P + off, za = 4 * P + off, zb = 10 * P + off;
            var corners = new[] { new Vector3(xa, RailY, za), new Vector3(xb, RailY, za), new Vector3(xb, RailY, zb), new Vector3(xa, RailY, zb) };
            // rounded corners
            for (int i = 0; i < 4; i++)
            {
                var a = corners[(i + 3) % 4]; var c = corners[i]; var n = corners[(i + 1) % 4];
                var din = (c - a).normalized; var dout = (n - c).normalized;
                for (int k = 0; k <= 6; k++)
                {
                    float u = k / 6f;
                    var p0 = c - din * 10; var p2 = c + dout * 10;
                    rail.Add((1 - u) * (1 - u) * p0 + 2 * (1 - u) * u * c + u * u * p2);
                }
            }
            railLen.Add(0);
            for (int i = 1; i <= rail.Count; i++) { railTotal += Vector3.Distance(rail[i - 1], rail[i % rail.Count]); railLen.Add(railTotal); }
            var m = new MeshB();
            var beam = Mats.Lit(new Color(0.85f, 0.87f, 0.9f), null, 0.7f, 0.4f);
            var strip = NeonMat(0);
            var colGo = new GameObject("Monorail colliders") { layer = 12 };
            colGo.transform.SetParent(root, false);
            for (int i = 0; i < rail.Count; i++)
            {
                var a = rail[i]; var b = rail[(i + 1) % rail.Count];
                var mid = (a + b) / 2; var dir = b - a; float len = dir.magnitude;
                var rot = Quaternion.LookRotation(dir.normalized);
                m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(mid, rot, new Vector3(1.6f, 1.0f, len + 0.3f)), beam);
                m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(mid + Vector3.down * 0.52f, rot, new Vector3(0.5f, 0.06f, len + 0.3f)), strip);
            }
            // pillars every ~45 m
            for (float s = 0; s < railTotal; s += 45f)
            {
                var p = RailAt(s, out _);
                m.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(new Vector3(p.x, RailY / 2, p.z), Quaternion.identity, new Vector3(0.9f, RailY / 2, 0.9f)), beam);
                m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(new Vector3(p.x, RailY - 0.9f, p.z), Quaternion.identity, new Vector3(2.4f, 0.8f, 2.4f)), beam);
                var bc = colGo.AddComponent<BoxCollider>();
                bc.center = new Vector3(p.x, RailY / 2, p.z); bc.size = new Vector3(0.9f, RailY, 0.9f);
            }
            m.Build("Monorail", root, 12, true);
            // the train
            var body = Mats.Lit(new Color(0.94f, 0.95f, 0.97f), null, 0.85f, 0.3f);
            var win = Mats.Glow(new Color(0.55f, 0.85f, 1f));
            train = new Transform[4];
            for (int i = 0; i < train.Length; i++)
            {
                var car = new GameObject("Maglev car").transform;
                car.SetParent(root, false);
                var tm = new MeshB();
                tm.Add(Prim.MeshOf(PrimitiveType.Capsule), Matrix4x4.TRS(new Vector3(0, 1.6f, 0), Quaternion.Euler(90, 0, 0), new Vector3(2.6f, 6.2f, 2.4f)), body);
                tm.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(new Vector3(0, 2.0f, 0), Quaternion.identity, new Vector3(2.62f, 0.55f, 9f)), win);
                tm.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(new Vector3(0, 0.55f, 0), Quaternion.identity, new Vector3(1.2f, 0.25f, 10.5f)), NeonMat(1));
                tm.Build("Body", car, 12, true);
                train[i] = car;
            }
        }

        Vector3 RailAt(float s, out Vector3 dir)
        {
            s = Mathf.Repeat(s, railTotal);
            int i = 1;
            while (i < railLen.Count - 1 && railLen[i] < s) i++;
            var a = rail[(i - 1) % rail.Count]; var b = rail[i % rail.Count];
            float u = (s - railLen[i - 1]) / Mathf.Max(0.01f, railLen[i] - railLen[i - 1]);
            dir = (b - a).normalized;
            return Vector3.Lerp(a, b, u);
        }

        // ------------------------------------------------------------------ the agency crown

        Transform ring;

        void BuildCrown()
        {
            var ag = City.OfType(BType.Agency);
            if (ag.Count == 0) return;
            var b = ag[0];
            float top = City.Curb + b.height;
            var c = new Vector3(b.rect.center.x, top, b.rect.center.y);
            ring = new GameObject("Crown ring").transform;
            ring.SetParent(root, false);
            ring.position = c + Vector3.up * 9f;
            var m = new MeshB();
            var neon = NeonMat(0);
            for (int k = 0; k < 48; k++)
            {
                float a = k / 48f * 360f;
                m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(Quaternion.Euler(0, a, 0) * new Vector3(0, 0, 9f), Quaternion.Euler(0, a, 0), new Vector3(1.25f, 0.35f, 0.35f)), neon);
                if (k % 4 == 0) m.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(Quaternion.Euler(0, a, 0) * new Vector3(0, -2.5f, 9f), Quaternion.Euler(0, a, 0), new Vector3(0.15f, 5f, 0.15f)), WorldBuilder.Metal);
            }
            m.Build("Ring", ring, 12, false);
            // spire with a beacon
            var sp = new MeshB();
            sp.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(c + Vector3.up * 16, Quaternion.identity, new Vector3(0.6f, 16, 0.6f)), WorldBuilder.Metal);
            sp.Add(Prim.MeshOf(PrimitiveType.Sphere), Matrix4x4.TRS(c + Vector3.up * 32.5f, Quaternion.identity, Vector3.one * 1.6f), NeonMat(1));
            sp.Build("Spire", root, 12, true);
            // big logo letters on two sides
            foreach (var (dir, yaw) in new[] { (Vector3.back, 0f), (Vector3.forward, 180f), (Vector3.left, 90f), (Vector3.right, 270f) })
            {
                var go = new GameObject("Logo");
                go.transform.SetParent(root, false);
                float half = (dir.z != 0 ? b.rect.height : b.rect.width) / 2;
                go.transform.position = c + Vector3.down * 8f + dir * (half + 0.6f);
                go.transform.rotation = Quaternion.Euler(0, yaw, 0);
                var t = go.AddComponent<TextMeshPro>();
                t.font = UIKit.Font; t.fontSharedMaterial = UIKit.TextOutline;
                t.text = "HERBAL CITY"; t.fontSize = 40; t.alignment = TextAlignmentOptions.Center;
                t.color = new Color(0.4f, 0.95f, 1f);
                t.rectTransform.sizeDelta = new Vector2(30, 8);
                t.textWrappingMode = TextWrappingModes.NoWrap;
            }
        }

        // ------------------------------------------------------------------ clouds

        Transform clouds;
        Material cloudMat;

        void BuildClouds()
        {
            const int S = 256;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "clouds" };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = 0, amp = 1, tot = 0;
                    for (int o = 0; o < 4; o++)
                    {
                        int cells = 4 << o;
                        n += ProcTex.PeriodicNoise(x, y, S, cells, 3 + o) * amp; tot += amp; amp *= 0.5f;
                    }
                    n /= tot;
                    float a = Mathf.Clamp01((n - 0.48f) * 3.2f);
                    px[y * S + x] = new Color(1, 1, 1, a);
                }
            tex.SetPixels32(px); tex.Apply(true);
            cloudMat = Mats.UnlitTransparent(new Color(1, 1, 1, 0.85f), tex);
            cloudMat.mainTextureScale = new Vector2(6, 6);
            var m = new MeshB();
            m.Flat(cloudMat, -3000, -3000, 3000, 3000, 0, 6000);
            var go = m.Build("Clouds", root, 0, false);
            go.transform.localRotation = Quaternion.Euler(180, 0, 0);   // seen from below
            clouds = go.transform;
        }

        // ------------------------------------------------------------------ air-taxis

        class Flyer { public Transform t; public Vector2Int a, b; public float u, y, speed; public Transform[] lights; }
        readonly List<Flyer> flyers = new List<Flyer>();

        Flyer MakeFlyer(Vector3 near)
        {
            var def = CarDef.Get("electro");
            var go = new GameObject("Air taxi");
            go.transform.SetParent(root, false);
            var mats = CarMesh.Materials(Palette.Get(Palette.Car, Random.Range(0, 10)), def.Spec);
            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = CarMesh.Static(def, 1, false);
            body.AddComponent<MeshRenderer>().sharedMaterials = mats;
            foreach (var (x, z) in new[] { (-0.9f, 1.6f), (0.9f, 1.6f), (-0.9f, -1.6f), (0.9f, -1.6f) })
            {
                Prim.Make(PrimitiveType.Cylinder, go.transform, new Vector3(x, 0.32f, z), new Vector3(0.75f, 0.08f, 0.75f), NeonMat(0), "Pad");
                Prim.Make(PrimitiveType.Cylinder, go.transform, new Vector3(x, 0.4f, z), new Vector3(0.95f, 0.05f, 0.95f), WorldBuilder.Metal, "Ring");
            }
            var f = new Flyer { t = go.transform, y = Random.Range(32f, 90f), speed = Random.Range(16f, 26f) };
            var c = new Vector2Int(Mathf.Clamp(Mathf.RoundToInt(near.x / City.P), 0, City.N), Mathf.Clamp(Mathf.RoundToInt(near.z / City.P), 0, City.N));
            f.a = c + new Vector2Int(Random.Range(-4, 5), Random.Range(-4, 5));
            f.a.x = Mathf.Clamp(f.a.x, 0, City.N); f.a.y = Mathf.Clamp(f.a.y, 0, City.N);
            f.b = Next(f.a, f.a + Vector2Int.right);
            f.u = Random.value;
            return f;
        }

        static Vector2Int Next(Vector2Int from, Vector2Int prev)
        {
            var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            for (int k = 0; k < 8; k++)
            {
                var n = from + dirs[Random.Range(0, 4)];
                if (n == prev || n.x < 0 || n.y < 0 || n.x > City.N || n.y > City.N) continue;
                return n;
            }
            return prev;
        }

        static Vector3 Node(Vector2Int n) => new Vector3(n.x * City.P, 0, n.y * City.P);

        void Update()
        {
            var pl = PlayerCtl.I;
            if (pl == null) return;
            float dt = Time.deltaTime;
            bool outside = pl.Inside < 0;
            root.gameObject.SetActive(outside);
            if (!outside) return;
            var me = pl.transform.position;

            // train
            if (train != null)
            {
                trainS += dt * 16f;
                for (int i = 0; i < train.Length; i++)
                {
                    var p = RailAt(trainS - i * 11.5f, out var d);
                    train[i].SetPositionAndRotation(p + Vector3.up * 0.65f, Quaternion.LookRotation(d));
                }
            }
            if (ring != null) ring.Rotate(0, dt * 12f, 0);
            if (clouds != null)
            {
                clouds.position = new Vector3(Mathf.Round(me.x / 500) * 500, 420, Mathf.Round(me.z / 500) * 500);
                cloudMat.mainTextureOffset = new Vector2(Time.time * 0.0015f, Time.time * 0.0008f);
                var c = cloudMat.color;
                c.a = Mathf.Lerp(0.35f, 0.95f, Weather.Cloud) * Mathf.Lerp(1f, 0.35f, DayNight.NightK);
                float g = Mathf.Lerp(1f, 0.55f, Weather.Rain);
                cloudMat.color = new Color(g, g, g * 1.02f, c.a);
            }

            // air-taxis
            int want = Gfx.Level == 0 ? 6 : Gfx.Level == 1 ? 14 : 22;
            for (int i = flyers.Count - 1; i >= 0; i--)
                if ((flyers[i].t.position - me).sqrMagnitude > 700f * 700f) { Destroy(flyers[i].t.gameObject); flyers.RemoveAt(i); }
            if (flyers.Count < want) flyers.Add(MakeFlyer(me));
            foreach (var f in flyers)
            {
                var A = Node(f.a); var B = Node(f.b);
                float len = Vector3.Distance(A, B);
                f.u += f.speed * dt / len;
                if (f.u >= 1) { var prev = f.a; f.a = f.b; f.b = Next(f.b, prev); f.u = 0; A = Node(f.a); B = Node(f.b); }
                var side = Vector3.Cross(Vector3.up, (B - A).normalized) * 4f;
                var pos = Vector3.Lerp(A, B, f.u) + side + Vector3.up * (f.y + Mathf.Sin(Time.time * 0.7f + f.y) * 1.5f);
                f.t.position = pos;
                var want2 = Quaternion.LookRotation((B - A).normalized) * Quaternion.Euler(4, 0, 0);
                f.t.rotation = Quaternion.Slerp(f.t.rotation, want2, dt * 3f);
            }

            // neon: soft by day, blazing at night (HDR → bloom)
            float k = Mathf.Lerp(1.0f, 3.2f, DayNight.NightK);
            float flicker = 1f;
            for (int i = 0; i < Neon.Count; i++)
            {
                if (i == 1) flicker = 0.92f + Mathf.Sin(Time.time * 2.7f) * 0.08f;
                Neon[i].m.color = Neon[i].c * k * flicker;
            }

            // wet streets in the rain
            if (WorldBuilder.Asphalt != null)
            {
                WorldBuilder.Asphalt.SetFloat("_Smoothness", Mathf.Lerp(0.25f, 0.82f, Weather.Rain));
                WorldBuilder.Asphalt.color = Color.Lerp(Color.white, new Color(0.62f, 0.64f, 0.68f), Weather.Rain);
                WorldBuilder.Sidewalk.SetFloat("_Smoothness", Mathf.Lerp(0.15f, 0.6f, Weather.Rain));
            }
        }
    }

    /// <summary>A holographic advertising screen on a tower: slogans change and the light pulses.</summary>
    public class Billboard : MonoBehaviour
    {
        static readonly string[] Ads =
        {
            "Herbal\nДОБРО ПОЖАЛОВАТЬ В БУДУЩЕЕ", "ЭЛЕКТРОКАР VOLT\n0–100 ЗА 2,9 С", "HERBAL CITY\nПЕНТХАУСЫ С ВИДОМ НА МОРЕ",
            "АЭРОТАКСИ\nВЫШЕ ПРОБОК", "НЕОН КОЛА\nВКУС НОЧНОГО ГОРОДА", "BMW 5 SERIES\nДИНАМИКА БИЗНЕСА", "ТРЦ «МЕГА»\nСКИДКИ ДО 70%", "РАБОТА МЕЧТЫ\nЦЕНТР ЗАНЯТОСТИ",
        };
        TextMeshPro text;
        Material panel;
        Color baseCol;
        int idx;
        float t;

        public static void Make(Transform parent, Vector3 pos, Quaternion rot, float w, float h, int seed)
        {
            var go = new GameObject("Billboard");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var bb = go.AddComponent<Billboard>();
            bb.baseCol = Color.Lerp(new Color(0.05f, 0.1f, 0.3f), new Color(0.3f, 0.05f, 0.25f), (seed % 5) / 4f);
            bb.panel = Mats.Glow(bb.baseCol);
            bb.panel = new Material(bb.panel);
            Prim.Make(PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(w, h, 0.2f), bb.panel, "Panel");
            Prim.Make(PrimitiveType.Cube, go.transform, new Vector3(0, h / 2 + 0.1f, 0), new Vector3(w + 0.3f, 0.2f, 0.3f), FutureCity.NeonMat(seed), "Frame");
            Prim.Make(PrimitiveType.Cube, go.transform, new Vector3(0, -h / 2 - 0.1f, 0), new Vector3(w + 0.3f, 0.2f, 0.3f), FutureCity.NeonMat(seed), "Frame");
            var tg = new GameObject("Text");
            tg.transform.SetParent(go.transform, false);
            tg.transform.localPosition = new Vector3(0, 0, -0.12f);
            bb.text = tg.AddComponent<TextMeshPro>();
            bb.text.font = UIKit.Font; bb.text.fontSharedMaterial = UIKit.TextOutline;
            bb.text.fontSize = h * 3.2f; bb.text.alignment = TextAlignmentOptions.Center;
            bb.text.rectTransform.sizeDelta = new Vector2(w * 0.94f, h * 0.9f);
            bb.text.enableAutoSizing = true; bb.text.fontSizeMin = 1; bb.text.fontSizeMax = h * 3.2f;
            bb.idx = seed % Ads.Length;
            bb.text.text = Ads[bb.idx];
            bb.t = (seed % 7) * 0.7f;
        }

        void Update()
        {
            t += Time.deltaTime;
            if (t > 5f) { t = 0; idx = (idx + 1) % Ads.Length; text.text = Ads[idx]; }
            float a = Mathf.Clamp01(t * 3f) * Mathf.Clamp01((5f - t) * 3f);
            text.alpha = a;
            float glow = Mathf.Lerp(1f, 2.4f, DayNight.NightK) * (0.85f + Mathf.Sin(Time.time * 3f) * 0.15f);
            panel.color = baseCol * glow;
            text.color = Color.Lerp(new Color(0.5f, 0.95f, 1f), new Color(1f, 0.6f, 0.95f), Mathf.PingPong(Time.time * 0.2f, 1f)) * Mathf.Lerp(1f, 1.6f, DayNight.NightK);
        }
    }
}
