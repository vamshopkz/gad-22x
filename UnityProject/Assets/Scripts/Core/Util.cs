using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gad
{
    /// <summary>Deterministic hashing: the same city, interiors and names on every phone.</summary>
    public static class H
    {
        public static uint U(int a, int b = 0, int c = 0)
        {
            uint h = 2166136261u;
            unchecked
            {
                h = (h ^ (uint)a) * 16777619u; h ^= h >> 13;
                h = (h ^ (uint)b) * 16777619u; h ^= h >> 15;
                h = (h ^ (uint)c) * 16777619u; h ^= h >> 13;
                h *= 0x5bd1e995; h ^= h >> 15;
            }
            return h;
        }
        public static float F(int a, int b = 0, int c = 0) => (U(a, b, c) & 0xFFFFFF) / 16777216f;
        public static int R(int n, int a, int b = 0, int c = 0) => (int)(U(a, b, c) % (uint)Mathf.Max(1, n));
        public static T Pick<T>(IList<T> list, int a, int b = 0, int c = 0) => list[R(list.Count, a, b, c)];
    }

    /// <summary>Simple seeded RNG for building geometry.</summary>
    public class Rng
    {
        uint s;
        public Rng(uint seed) { s = seed == 0 ? 1u : seed; }
        public uint Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return s; }
        public float F() => (Next() & 0xFFFFFF) / 16777216f;
        public float F(float a, float b) => a + (b - a) * F();
        public int I(int n) => (int)(Next() % (uint)Mathf.Max(1, n));
        public bool Chance(float p) => F() < p;
    }

    /// <summary>Shared materials: copies of the URP materials made by ProjectSetup (Resources/Materials).</summary>
    public static partial class Mats
    {
        static Material lit, litT, litE, unlit, unlitT, partA;
        static readonly Dictionary<int, Material> colors = new Dictionary<int, Material>();
        static readonly Dictionary<int, Material> glows = new Dictionary<int, Material>();

        static Material Base(ref Material m, string name)
        {
            if (m == null) m = Resources.Load<Material>("Materials/" + name);
            return m;
        }

        public static Material Lit(Color c, float smooth = 0.2f, float metal = 0f)
        {
            int key = ((int)(c.r * 63) << 18) | ((int)(c.g * 63) << 12) | ((int)(c.b * 63) << 6) | ((int)(smooth * 7) << 3) | (int)(metal * 7);
            if (colors.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Base(ref lit, "Lit")) { color = c, enableInstancing = true };
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            colors[key] = m;
            return m;
        }

        public static Material Tex(Texture2D t, Color tint, float smooth = 0.15f)
        {
            var m = new Material(Base(ref lit, "Lit")) { color = tint, mainTexture = t, enableInstancing = true };
            m.SetFloat("_Smoothness", smooth);
            return m;
        }

        /// <summary>Lit material with an emission map (windows at night): emission strength set via SetColor at runtime.</summary>
        public static Material Emissive(Texture2D main, Texture2D emit, Color tint, float smooth = 0.3f)
        {
            var m = new Material(Base(ref litE, "LitEmissive")) { color = tint, mainTexture = main };
            m.SetTexture("_EmissionMap", emit);
            m.SetColor("_EmissionColor", Color.black);
            m.SetFloat("_Smoothness", smooth);
            m.EnableKeyword("_EMISSION");
            return m;
        }

        public static Material Glow(Color c)
        {
            int key = ((int)(c.r * 31) << 10) | ((int)(c.g * 31) << 5) | (int)(c.b * 31);
            if (glows.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Base(ref unlit, "Unlit")) { color = c };
            glows[key] = m;
            return m;
        }

        public static Material Glass(Color c)
        {
            var m = new Material(Base(ref litT, "LitTransparent")) { color = c };
            m.SetFloat("_Smoothness", 0.9f);
            return m;
        }

        public static Material UnlitTransparent(Color c, Texture2D t = null)
        {
            var m = new Material(Base(ref unlitT, "UnlitTransparent")) { color = c };
            if (t != null) m.mainTexture = t;
            return m;
        }

        public static Material Particles(Texture2D t = null)
        {
            var m = new Material(Base(ref partA, "ParticleAlpha"));
            if (t != null) m.mainTexture = t;
            return m;
        }
    }

    /// <summary>Primitive helpers without colliders (the world uses explicit box colliders).</summary>
    public static class Prim
    {
        static readonly Dictionary<PrimitiveType, Mesh> meshes = new Dictionary<PrimitiveType, Mesh>();
        // CreatePrimitive adds these colliders: keep them in stripped (WebGL) builds.
        static readonly System.Type[] keep = { typeof(SphereCollider), typeof(CapsuleCollider), typeof(BoxCollider), typeof(MeshCollider) };

        public static Mesh MeshOf(PrimitiveType t)
        {
            if (meshes.TryGetValue(t, out var m) && m != null) return m;
            var go = GameObject.CreatePrimitive(t);
            m = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            meshes[t] = m;
            return m;
        }

        public static GameObject Make(PrimitiveType t, Transform parent, Vector3 localPos, Vector3 scale, Material mat, string name = null)
        {
            var go = new GameObject(name ?? t.ToString());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = MeshOf(t);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            return go;
        }

        public static GameObject Box(Transform parent, Vector3 pos, Vector3 size, Material mat, string name = null)
            => Make(PrimitiveType.Cube, parent, pos, size, mat, name);

        public static GameObject Box(Transform parent, Vector3 pos, Vector3 size, Color c, string name = null)
            => Make(PrimitiveType.Cube, parent, pos, size, Mats.Lit(c), name);

        public static GameObject Cyl(Transform parent, Vector3 pos, Vector3 size, Color c, string name = null)
            => Make(PrimitiveType.Cylinder, parent, pos, size, Mats.Lit(c), name);

        public static GameObject Ball(Transform parent, Vector3 pos, Vector3 size, Color c, string name = null)
            => Make(PrimitiveType.Sphere, parent, pos, size, Mats.Lit(c), name);

        public static GameObject Cap(Transform parent, Vector3 pos, Vector3 size, Color c, string name = null)
            => Make(PrimitiveType.Capsule, parent, pos, size, Mats.Lit(c), name);

        public static BoxCollider Solid(GameObject go)
        {
            var bc = go.AddComponent<BoxCollider>();
            return bc;
        }
    }

    /// <summary>Builds one combined mesh with a submesh per material (a whole city block in a few draw calls).</summary>
    public class MeshB
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<Color32> col = new List<Color32>();
        readonly List<Material> mats = new List<Material>();
        readonly List<List<int>> tris = new List<List<int>>();

        public int VertexCount => v.Count;
        /// <summary>Added to the V coordinate (metres) of box sides: facades continue across stacked boxes.</summary>
        public float uvYStart;

        List<int> T(Material m)
        {
            int i = mats.IndexOf(m);
            if (i < 0) { mats.Add(m); tris.Add(new List<int>()); i = mats.Count - 1; }
            return tris[i];
        }

        public void Quad(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uva, Vector2 uvb, Vector2 uvc, Vector2 uvd)
        {
            var t = T(m);
            int s = v.Count;
            var nn = Vector3.Cross(b - a, d - a).normalized;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            n.Add(nn); n.Add(nn); n.Add(nn); n.Add(nn);
            uv.Add(uva); uv.Add(uvb); uv.Add(uvc); uv.Add(uvd);
            var w = new Color32(255, 255, 255, 255);
            col.Add(w); col.Add(w); col.Add(w); col.Add(w);
            t.Add(s); t.Add(s + 1); t.Add(s + 3);
            t.Add(s + 1); t.Add(s + 2); t.Add(s + 3);
        }

        /// <summary>Axis-aligned box from min to max. UVs in metres / tile (so textures keep their real size).
        /// Bottom face skipped unless asked. uvOffset shifts facade textures so neighbours don't look identical.</summary>
        public void Box(Material side, Material top, Vector3 min, Vector3 max, Vector2 tile, Vector2 uvOffset = default, bool bottom = false, Material bottomMat = null)
        {
            float x0 = min.x, y0 = min.y, z0 = min.z, x1 = max.x, y1 = max.y, z1 = max.z;
            float sx = x1 - x0, sy = y1 - y0, sz = z1 - z0;
            Vector2 U(float u, float w) => new Vector2(u / tile.x + uvOffset.x, (w + uvYStart) / tile.y + uvOffset.y);
            // south (-z)
            Quad(side, new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0), U(0, 0), U(0, sy), U(sx, sy), U(sx, 0));
            // north (+z)
            Quad(side, new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), U(0, 0), U(0, sy), U(sx, sy), U(sx, 0));
            // west (-x)
            Quad(side, new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), new Vector3(x0, y0, z0), U(0, 0), U(0, sy), U(sz, sy), U(sz, 0));
            // east (+x)
            Quad(side, new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), U(0, 0), U(0, sy), U(sz, sy), U(sz, 0));
            if (top != null)
                Quad(top, new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0),
                    new Vector2(x0, z0) / 4f, new Vector2(x0, z1) / 4f, new Vector2(x1, z1) / 4f, new Vector2(x1, z0) / 4f);
            if (bottom)
                Quad(bottomMat ?? top ?? side, new Vector3(x0, y0, z1), new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1),
                    new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1));
        }

        public void Tri(Material m, Vector3 a, Vector3 b, Vector3 c, Vector2 uva, Vector2 uvb, Vector2 uvc)
        {
            var t = T(m);
            int s = v.Count;
            var nn = Vector3.Cross(b - a, c - a).normalized;
            v.Add(a); v.Add(b); v.Add(c);
            n.Add(nn); n.Add(nn); n.Add(nn);
            uv.Add(uva); uv.Add(uvb); uv.Add(uvc);
            var w = new Color32(255, 255, 255, 255);
            col.Add(w); col.Add(w); col.Add(w);
            t.Add(s); t.Add(s + 1); t.Add(s + 2);
        }

        /// <summary>Flat horizontal rectangle at height y with UVs in world metres / tile.</summary>
        public void Flat(Material m, float x0, float z0, float x1, float z1, float y, float tile)
        {
            Quad(m, new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0),
                new Vector2(x0, z0) / tile, new Vector2(x0, z1) / tile, new Vector2(x1, z1) / tile, new Vector2(x1, z0) / tile);
        }

        /// <summary>Adds an arbitrary mesh transformed by matrix m (used for small props merged into a chunk).</summary>
        public void Add(Mesh mesh, Matrix4x4 m, Material mat)
        {
            var t = T(mat);
            int s = v.Count;
            var mv = mesh.vertices; var mn = mesh.normals; var mu = mesh.uv;
            var nm = m.inverse.transpose;
            for (int i = 0; i < mv.Length; i++)
            {
                v.Add(m.MultiplyPoint3x4(mv[i]));
                n.Add(nm.MultiplyVector(mn[i]).normalized);
                uv.Add(mu.Length > i ? mu[i] : Vector2.zero);
                col.Add(new Color32(255, 255, 255, 255));
            }
            foreach (var idx in mesh.triangles) t.Add(s + idx);
        }

        /// <summary>Adds a multi-material mesh (one material per submesh), transformed by m.</summary>
        public void AddMulti(Mesh mesh, Matrix4x4 m, Material[] mats)
        {
            int s0 = v.Count;
            var mv = mesh.vertices; var mn = mesh.normals; var mu = mesh.uv;
            var nm = m.inverse.transpose;
            for (int i = 0; i < mv.Length; i++)
            {
                v.Add(m.MultiplyPoint3x4(mv[i]));
                n.Add(i < mn.Length ? nm.MultiplyVector(mn[i]).normalized : Vector3.up);
                uv.Add(i < mu.Length ? mu[i] : Vector2.zero);
                col.Add(new Color32(255, 255, 255, 255));
            }
            for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
            {
                var tri = mesh.GetTriangles(s);
                if (tri.Length == 0 || mats[s] == null) continue;
                var t = T(mats[s]);
                foreach (var idx in tri) t.Add(s0 + idx);
            }
        }

        public GameObject Build(string name, Transform parent, int layer = 0, bool shadows = true)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            var mesh = new Mesh { indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16, name = name };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = mats.Count;
            for (int i = 0; i < tris.Count; i++) mesh.SetTriangles(tris[i], i, false);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats.ToArray();
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }
    }

    /// <summary>Procedural textures (no downloaded assets): facades, asphalt, grass, tiles…</summary>
    public static class Tex
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        static Texture2D New(int w, int h, string name)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 2 };
            return t;
        }

        static Texture2D Done(Texture2D t, string key) { t.Apply(true, true); cache[key] = t; return t; }

        static float Noise(float x, float y) => Mathf.PerlinNoise(x, y);

        public static Texture2D Noisy(string key, Color a, Color b, float scale, float speck = 0f, int size = 256)
        {
            if (cache.TryGetValue(key, out var c)) return c;
            var t = New(size, size, key);
            var px = new Color32[size * size];
            var rng = new Rng(H.U(key.GetHashCode()));
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // tileable noise: blend 4 corners
                    float u = (float)x / size, w = (float)y / size;
                    float n1 = Noise(x * scale, y * scale), n2 = Noise((x - size) * scale, y * scale), n3 = Noise(x * scale, (y - size) * scale), n4 = Noise((x - size) * scale, (y - size) * scale);
                    float nv = Mathf.Lerp(Mathf.Lerp(n1, n2, u), Mathf.Lerp(n3, n4, u), w);
                    var col = Color.Lerp(a, b, nv);
                    if (speck > 0 && rng.Chance(speck)) col = Color.Lerp(col, rng.Chance(0.5f) ? Color.white : Color.black, 0.25f);
                    px[y * size + x] = col;
                }
            t.SetPixels32(px);
            return Done(t, key);
        }

        public static Texture2D Asphalt() => Noisy("asphalt", new Color(0.17f, 0.17f, 0.18f), new Color(0.25f, 0.25f, 0.26f), 0.08f, 0.08f);
        public static Texture2D Grass() => Noisy("grass", new Color(0.22f, 0.42f, 0.16f), new Color(0.36f, 0.56f, 0.22f), 0.05f, 0.05f);
        public static Texture2D Sand() => Noisy("sand", new Color(0.83f, 0.75f, 0.55f), new Color(0.93f, 0.86f, 0.66f), 0.07f, 0.06f);
        public static Texture2D Rock() => Noisy("rock", new Color(0.36f, 0.34f, 0.32f), new Color(0.55f, 0.53f, 0.5f), 0.04f, 0.04f);
        public static Texture2D Gravel() => Noisy("gravel", new Color(0.38f, 0.38f, 0.38f), new Color(0.5f, 0.5f, 0.5f), 0.3f, 0.2f, 128);

        public static Texture2D Tiles(string key, Color a, Color grout, int n, int size = 256)
        {
            if (cache.TryGetValue(key, out var c)) return c;
            var t = New(size, size, key);
            var px = new Color32[size * size];
            int cell = size / n;
            var rng = new Rng(H.U(key.GetHashCode()));
            var tones = new float[n * n];
            for (int i = 0; i < tones.Length; i++) tones[i] = rng.F(-0.05f, 0.05f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool g = x % cell < 2 || y % cell < 2;
                    float tone = tones[(y / cell) * n + (x / cell)] + (Noise(x * 0.1f, y * 0.1f) - 0.5f) * 0.05f;
                    var col = g ? grout : new Color(a.r + tone, a.g + tone, a.b + tone);
                    px[y * size + x] = col;
                }
            t.SetPixels32(px);
            return Done(t, key);
        }

        public static Texture2D Wood()
        {
            const string key = "wood";
            if (cache.TryGetValue(key, out var c)) return c;
            int size = 256;
            var t = New(size, size, key);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int plank = y / 32;
                    float grain = Noise(x * 0.02f + plank * 7, y * 0.4f) * 0.5f + Noise(x * 0.1f, y * 0.05f) * 0.2f;
                    bool seam = y % 32 < 1 || ((x + plank * 97) % 256) < 1;
                    var col = Color.Lerp(new Color(0.45f, 0.29f, 0.16f), new Color(0.66f, 0.47f, 0.28f), grain);
                    if (seam) col *= 0.6f;
                    px[y * size + x] = col;
                }
            t.SetPixels32(px);
            return Done(t, key);
        }

        /// <summary>
        /// Facade of 4×4 window bays. Style: 0 glass tower, 1 concrete flats, 2 brick, 3 white modern,
        /// 4 shopfront (one tall storefront per bay), 5 industrial, 6 house plaster, 7 dark glass offices.
        /// Returns colour + emission (lit windows at night, random per bay).
        /// </summary>
        public static (Texture2D main, Texture2D emit) Facade(int style)
        {
            string key = "facade" + style;
            if (cache.TryGetValue(key, out var have)) return (have, cache[key + "e"]);
            const int S = 512, B = 128;
            var t = New(S, S, key);
            var e = New(S, S, key + "e");
            var px = new Color32[S * S];
            var ex = new Color32[S * S];
            var rng = new Rng(H.U(style * 7919 + 13));
            Color wall, win, frame;
            switch (style)
            {
                case 0: wall = new Color(0.55f, 0.62f, 0.68f); win = new Color(0.32f, 0.47f, 0.6f); frame = new Color(0.75f, 0.78f, 0.8f); break;
                case 1: wall = new Color(0.82f, 0.76f, 0.64f); win = new Color(0.25f, 0.3f, 0.36f); frame = new Color(0.95f, 0.95f, 0.93f); break;
                case 2: wall = new Color(0.58f, 0.3f, 0.22f); win = new Color(0.22f, 0.26f, 0.3f); frame = new Color(0.9f, 0.88f, 0.82f); break;
                case 3: wall = new Color(0.92f, 0.92f, 0.9f); win = new Color(0.18f, 0.22f, 0.27f); frame = new Color(0.4f, 0.42f, 0.45f); break;
                case 4: wall = new Color(0.85f, 0.83f, 0.8f); win = new Color(0.4f, 0.55f, 0.62f); frame = new Color(0.25f, 0.25f, 0.27f); break;
                case 5: wall = new Color(0.6f, 0.62f, 0.6f); win = new Color(0.35f, 0.42f, 0.45f); frame = new Color(0.45f, 0.47f, 0.45f); break;
                case 6: wall = new Color(0.93f, 0.86f, 0.72f); win = new Color(0.3f, 0.36f, 0.42f); frame = new Color(1f, 1f, 1f); break;
                default: wall = new Color(0.2f, 0.24f, 0.28f); win = new Color(0.12f, 0.2f, 0.3f); frame = new Color(0.35f, 0.38f, 0.42f); break;
            }
            for (int by = 0; by < 4; by++)
                for (int bx = 0; bx < 4; bx++)
                {
                    bool lit = rng.Chance(style == 5 ? 0.25f : 0.45f);
                    var warm = rng.Chance(0.7f) ? new Color(1f, 0.82f, 0.5f) : new Color(0.75f, 0.85f, 1f);
                    float shade = rng.F(-0.05f, 0.08f);
                    bool blinds = rng.Chance(0.3f);
                    for (int y = 0; y < B; y++)
                        for (int x = 0; x < B; x++)
                        {
                            int gx = bx * B + x, gy = by * B + y;
                            float n = (Noise(gx * 0.05f, gy * 0.05f) - 0.5f) * 0.06f;
                            Color c = wall + new Color(n, n, n);
                            Color em = Color.black;
                            bool inWin, inFrame;
                            switch (style)
                            {
                                case 0: case 7:
                                    inFrame = x < 4 || y < 6;
                                    inWin = !inFrame;
                                    break;
                                case 4:
                                    inWin = x > 8 && x < B - 8 && y > 6 && y < B - 20;
                                    inFrame = !inWin && x > 4 && x < B - 4 && y > 2 && y < B - 16;
                                    break;
                                case 5:
                                    inWin = y > 80 && y < 112 && x > 14 && x < B - 14;
                                    inFrame = !inWin && y > 76 && y < 116 && x > 10 && x < B - 10;
                                    if (!inWin && !inFrame && x % 10 < 2) c *= 0.85f;
                                    break;
                                case 6:
                                    inWin = x > 34 && x < 94 && y > 34 && y < 104;
                                    inFrame = !inWin && x > 28 && x < 100 && y > 28 && y < 110;
                                    break;
                                case 3:
                                    inWin = x > 10 && x < B - 10 && y > 30 && y < 104;
                                    inFrame = !inWin && x > 6 && x < B - 6 && y > 26 && y < 108;
                                    break;
                                default:
                                    inWin = x > 28 && x < 100 && y > 34 && y < 106;
                                    inFrame = !inWin && x > 22 && x < 106 && y > 28 && y < 112;
                                    break;
                            }
                            if (style == 2 && !inWin && !inFrame)
                            {
                                int row = gy / 8;
                                bool mortar = gy % 8 == 0 || ((gx + (row % 2) * 12) % 24) == 0;
                                if (mortar) c = new Color(0.75f, 0.72f, 0.66f);
                            }
                            if (style == 1 && !inWin && !inFrame && y < 26 && y > 18) c = frame * 0.9f; // balcony ledge line
                            if (inFrame) c = frame;
                            if (inWin)
                            {
                                float refl = (float)y / B * 0.35f + n * 2;
                                c = win + new Color(shade + refl, shade + refl, shade + refl * 1.1f);
                                if (blinds && (y / 6) % 2 == 0) c = Color.Lerp(c, frame, 0.3f);
                                if (style == 0 || style == 7) { if (x % 64 < 3) c = frame; }
                                if (lit) em = warm * (0.75f + 0.25f * Noise(gx * 0.08f, gy * 0.08f));
                            }
                            px[gy * S + gx] = c;
                            ex[gy * S + gx] = em;
                        }
                }
            t.SetPixels32(px);
            e.SetPixels32(ex);
            Done(e, key + "e");
            return (Done(t, key), e);
        }

        /// <summary>Soft round dot (rain drops, light glows, markers).</summary>
        public static Texture2D Dot()
        {
            const string key = "dot";
            if (cache.TryGetValue(key, out var c)) return c;
            int s = 64;
            var t = New(s, s, key);
            t.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(s / 2f - 0.5f, s / 2f - 0.5f)) / (s / 2f);
                    float a = Mathf.Clamp01(1 - d);
                    t.SetPixel(x, y, new Color(1, 1, 1, a * a));
                }
            return Done(t, key);
        }

        public static Texture2D Streak()
        {
            const string key = "streak";
            if (cache.TryGetValue(key, out var c)) return c;
            int w = 8, h = 64;
            var t = New(w, h, key);
            t.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float a = (1 - Mathf.Abs(x - 3.5f) / 4f) * Mathf.Sin(y / (float)h * Mathf.PI);
                    t.SetPixel(x, y, new Color(0.8f, 0.85f, 1f, a * 0.6f));
                }
            return Done(t, key);
        }

        public static Texture2D Water()
        {
            const string key = "water";
            if (cache.TryGetValue(key, out var c)) return c;
            return Noisy(key, new Color(0.08f, 0.32f, 0.45f), new Color(0.18f, 0.5f, 0.62f), 0.06f, 0f);
        }
    }
}
