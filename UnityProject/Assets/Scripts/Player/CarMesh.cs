using System.Collections.Generic;
using UnityEngine;

namespace Gad
{
    /// <summary>Proportions of a car body. All in metres, car faces +Z, origin on the ground under its centre.</summary>
    public class CarSpec
    {
        public float L, W, H, WB, R, FrontOH;
        public float Sill = 0.22f, NoseY = 0.52f, NoseBottom = 0.2f, TailY = 0.62f, TailBottom = 0.3f;
        public float HoodY = 0.95f, DeckY = 1.0f;          // belt line at the windshield base / on the boot lid
        public float ZA, ZB, ZC, ZD;                        // windshield base, windshield top, roof end, rear window base
        public float Tumble = 0.8f;                         // roof half-width / belt half-width
        public float NoseNarrow = 0.8f, TailNarrow = 0.82f;
        public int Grille;                                  // 0 none (electric), 1 twin kidney, 2 wide, 3 big truck grille
        public int Head;                                    // 0 slim + angel eyes, 1 round, 2 light bar, 3 sharp
        public int Tail;                                    // 0 L-shaped, 1 full-width bar, 2 vertical
        public bool Bed, Spoiler, Wing, RoofRails, Taxi;
        public int Exhaust = 2;
        public bool Badge;                                  // round badge on bonnet and boot
        public Color Caliper = new Color(0.15f, 0.15f, 0.17f);
        public int Spokes = 5;

        public float FrontAxle => L / 2 - FrontOH;
        public float RearAxle => FrontAxle - WB;
    }

    /// <summary>
    /// Builds realistic-looking car meshes from a <see cref="CarSpec"/>: a smooth lofted lower body
    /// (rounded sections, wheel arches cut in), a glasshouse with painted roof and pillars, the driver's
    /// door as a separate hinged piece, lights, grille, mirrors, trims, a cabin (seats, dashboard,
    /// steering wheel, screen) visible through tinted glass, and detailed wheels.
    /// One mesh per model with fixed material slots (paint is swapped per colour).
    /// </summary>
    public static class CarMesh
    {
        public const int Paint = 0, GlassS = 1, Chrome = 2, Gloss = 3, Matte = 4, HeadL = 5, TailL = 6, Interior = 7, Screen = 8, Plate = 9, Rubber = 10, RimS = 11, Brake = 12, PlateT = 13, Slots = 14;

        public class Built
        {
            public Mesh body, door, wheel;
            public Vector3 doorPivot, seat, steering;
            public float doorLen;
            public CarSpec spec;
        }

        static readonly Dictionary<string, Built> cache = new Dictionary<string, Built>();
        static Material[] baseMats;
        static readonly Dictionary<int, Material> paints = new Dictionary<int, Material>();

        public static Material PaintMat(Color c)
        {
            int key = ((int)(c.r * 255) << 16) | ((int)(c.g * 255) << 8) | (int)(c.b * 255);
            if (paints.TryGetValue(key, out var m) && m != null) return m;
            m = Mats.Lit(c, null, 0.86f, 0.45f);
            m.name = "CarPaint";
            paints[key] = m;
            return m;
        }

        public static Material[] Materials(Color paint, CarSpec spec)
        {
            if (baseMats == null)
            {
                baseMats = new Material[Slots];
                baseMats[GlassS] = Mats.Glass(new Color(0.05f, 0.08f, 0.1f, 0.42f));
                baseMats[GlassS].SetFloat("_Smoothness", 0.97f);
                baseMats[Chrome] = Mats.Lit(new Color(0.86f, 0.87f, 0.9f), null, 0.92f, 1f);
                baseMats[Gloss] = Mats.Lit(new Color(0.03f, 0.03f, 0.035f), null, 0.85f, 0.2f);
                baseMats[Matte] = Mats.Lit(new Color(0.07f, 0.07f, 0.075f), null, 0.25f, 0f);
                baseMats[HeadL] = Mats.Glow(new Color(0.92f, 0.97f, 1f));
                baseMats[TailL] = Mats.Glow(new Color(0.85f, 0.02f, 0.03f));
                baseMats[Interior] = Mats.Lit(new Color(0.16f, 0.12f, 0.1f), null, 0.35f, 0f);
                baseMats[Screen] = Mats.Glow(new Color(0.2f, 0.45f, 0.85f));
                baseMats[Plate] = Mats.Lit(new Color(0.93f, 0.93f, 0.9f), null, 0.4f, 0f);
                baseMats[Rubber] = Mats.Lit(new Color(0.045f, 0.045f, 0.05f), null, 0.18f, 0f);
                baseMats[PlateT] = Mats.Lit(Color.white, Resources.Load<Texture2D>("Cars/plates"), 0.45f, 0f);
                baseMats[RimS] = Mats.Lit(new Color(0.62f, 0.64f, 0.67f), null, 0.8f, 0.95f);
            }
            var arr = (Material[])baseMats.Clone();
            arr[Paint] = PaintMat(paint);
            arr[Brake] = Mats.Lit(spec.Caliper, null, 0.6f, 0.3f);
            return arr;
        }

        // ------------------------------------------------------------------ slot mesh builder

        class SM
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int>[] t = new List<int>[Slots];
            public SM() { for (int i = 0; i < Slots; i++) t[i] = new List<int>(); }

            public void Quad(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                uv.Add(Vector2.zero); uv.Add(Vector2.up); uv.Add(Vector2.one); uv.Add(Vector2.right);
                t[s].Add(i); t[s].Add(i + 1); t[s].Add(i + 3);
                t[s].Add(i + 1); t[s].Add(i + 2); t[s].Add(i + 3);
            }

            public void Tri(int s, Vector3 a, Vector3 b, Vector3 c)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c);
                uv.Add(Vector2.zero); uv.Add(Vector2.up); uv.Add(Vector2.one);
                t[s].Add(i); t[s].Add(i + 1); t[s].Add(i + 2);
            }

            public void Add(Mesh m, Matrix4x4 xf, int s)
            {
                int i0 = v.Count;
                foreach (var p in m.vertices) v.Add(xf.MultiplyPoint3x4(p));
                var u = m.uv;
                for (int k = 0; k < m.vertexCount; k++) uv.Add(k < u.Length ? u[k] : Vector2.zero);
                bool flip = xf.determinant < 0;
                var tris = m.triangles;
                for (int k = 0; k < tris.Length; k += 3)
                {
                    if (flip) { t[s].Add(i0 + tris[k]); t[s].Add(i0 + tris[k + 2]); t[s].Add(i0 + tris[k + 1]); }
                    else { t[s].Add(i0 + tris[k]); t[s].Add(i0 + tris[k + 1]); t[s].Add(i0 + tris[k + 2]); }
                }
            }

            public void AddSub(Mesh m, int sub, Matrix4x4 xf, int s)
            {
                var verts = m.vertices; var u = m.uv; var nrm = m.normals;
                var tris = m.GetTriangles(sub);
                var map = new Dictionary<int, int>();
                bool flip = xf.determinant < 0;
                for (int k = 0; k < tris.Length; k += 3)
                {
                    int A = Map(tris[k]), B = Map(tris[k + 1]), C = Map(tris[k + 2]);
                    if (flip) { t[s].Add(A); t[s].Add(C); t[s].Add(B); } else { t[s].Add(A); t[s].Add(B); t[s].Add(C); }
                }
                int Map(int i)
                {
                    if (map.TryGetValue(i, out var j)) return j;
                    j = v.Count;
                    v.Add(xf.MultiplyPoint3x4(verts[i]));
                    uv.Add(i < u.Length ? u[i] : Vector2.zero);
                    nr.Add(i < nrm.Length ? xf.MultiplyVector(nrm[i]).normalized * (flip ? 1 : 1) : Vector3.up);
                    map[i] = j;
                    return j;
                }
            }
            public readonly List<Vector3> nr = new List<Vector3>();

            /// <summary>A flat quad facing `n` with the uv rectangle `r` (number plates).</summary>
            public void Decal(int s, Vector3 c, Vector3 n, float w, float h, Rect r)
            {
                var right = Vector3.Cross(Vector3.up, n).normalized * (w / 2);
                var up = Vector3.up * (h / 2);
                int i = v.Count;
                v.Add(c + right - up); v.Add(c + right + up); v.Add(c - right + up); v.Add(c - right - up);
                uv.Add(new Vector2(r.xMin, r.yMin)); uv.Add(new Vector2(r.xMin, r.yMax)); uv.Add(new Vector2(r.xMax, r.yMax)); uv.Add(new Vector2(r.xMax, r.yMin));
                t[s].Add(i); t[s].Add(i + 1); t[s].Add(i + 3);
                t[s].Add(i + 1); t[s].Add(i + 2); t[s].Add(i + 3);
            }

            public void Box(int s, Vector3 c, Vector3 size, Quaternion? rot = null) =>
                Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(c, rot ?? Quaternion.identity, size), s);

            public void Cyl(int s, Vector3 c, Vector3 size, Quaternion rot) =>
                Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(c, rot, size), s);

            public void Ball(int s, Vector3 c, Vector3 size) =>
                Add(Prim.MeshOf(PrimitiveType.Sphere), Matrix4x4.TRS(c, Quaternion.identity, size), s);

            public Mesh Build(string name, bool smooth)
            {
                var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                m.SetVertices(v);
                m.SetUVs(0, uv);
                m.subMeshCount = Slots;
                for (int i = 0; i < Slots; i++) m.SetTriangles(t[i], i, false);
                if (nr.Count == v.Count) m.SetNormals(nr);
                else m.RecalculateNormals(smooth ? 40f : 0f);
                m.RecalculateBounds();
                return m;
            }
        }

        /// <summary>Normals with a smoothing angle (welds equal positions, keeps hard edges).</summary>
        static void RecalculateNormals(this Mesh mesh, float angle)
        {
            if (angle <= 0f) { mesh.RecalculateNormals(); return; }
            var verts = mesh.vertices;
            var normals = new Vector3[verts.Length];
            var faceN = new Dictionary<int, List<Vector3>>();
            var byPos = new Dictionary<Vector3Int, List<int>>();
            Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x * 2000), Mathf.RoundToInt(p.y * 2000), Mathf.RoundToInt(p.z * 2000));
            var vn = new List<Vector3>[verts.Length];
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    var n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
                    foreach (var k in new[] { a, b, c }) { (vn[k] ??= new List<Vector3>()).Add(n); }
                }
            }
            for (int i = 0; i < verts.Length; i++)
            {
                var k = Key(verts[i]);
                if (!byPos.TryGetValue(k, out var l)) byPos[k] = l = new List<int>();
                l.Add(i);
            }
            float cos = Mathf.Cos(angle * Mathf.Deg2Rad);
            for (int i = 0; i < verts.Length; i++)
            {
                if (vn[i] == null) { normals[i] = Vector3.up; continue; }
                var own = Vector3.zero; foreach (var n in vn[i]) own += n;
                var on = own.normalized;
                var sum = Vector3.zero;
                foreach (int j in byPos[Key(verts[i])])
                    if (vn[j] != null)
                        foreach (var n in vn[j])
                            if (Vector3.Dot(n.normalized, on) > cos) sum += n;
                normals[i] = sum.sqrMagnitude > 0 ? sum.normalized : on;
            }
            mesh.normals = normals;
        }

        // ------------------------------------------------------------------ profiles

        struct K { public float z, v; public K(float z, float v) { this.z = z; this.v = v; } }

        static float Curve(K[] k, float z)
        {
            if (z <= k[0].z) return k[0].v;
            int n = k.Length;
            if (z >= k[n - 1].z) return k[n - 1].v;
            int i = 0;
            while (i < n - 2 && z > k[i + 1].z) i++;
            var a = k[Mathf.Max(0, i - 1)]; var b = k[i]; var c = k[i + 1]; var d = k[Mathf.Min(n - 1, i + 2)];
            float t = (z - b.z) / Mathf.Max(1e-4f, c.z - b.z);
            float t2 = t * t, t3 = t2 * t;
            float mono = Mathf.Lerp(b.v, c.v, t);
            float cr = 0.5f * (2f * b.v + (-a.v + c.v) * t + (2f * a.v - 5f * b.v + 4f * c.v - d.v) * t2 + (-a.v + 3f * b.v - 3f * c.v + d.v) * t3);
            return Mathf.Lerp(cr, mono, 0.35f);
        }

        class Prof
        {
            public K[] belt, bottom, half;
            public CarSpec s;
            public float ArchCut(float z)
            {
                float y = s.Sill;
                foreach (float az in new[] { s.FrontAxle, s.RearAxle })
                {
                    float ra = s.R + 0.075f, dz = z - az;
                    if (Mathf.Abs(dz) < ra) y = Mathf.Max(y, s.R + Mathf.Sqrt(ra * ra - dz * dz));
                }
                return y;
            }
            public float Belt(float z) => Curve(belt, z);
            public float Bottom(float z) => Mathf.Max(Curve(bottom, z), ArchCut(z));
            public float Half(float z) => Curve(half, z);
        }

        static Prof MakeProf(CarSpec s)
        {
            float hl = s.L / 2, hw = s.W / 2;
            var p = new Prof { s = s };
            p.belt = new[]
            {
                new K(-hl, s.TailY), new K(-hl + 0.05f, s.DeckY - 0.1f), new K(-hl + 0.22f, s.DeckY - 0.01f), new K(Mathf.Min(s.ZD, -hl + 0.6f), s.DeckY),
                new K(s.ZD, s.DeckY - 0.01f), new K((s.ZD + s.ZA) / 2, (s.DeckY + s.HoodY) / 2 + 0.01f), new K(s.ZA, s.HoodY),
                new K(s.ZA + (hl - s.ZA) * 0.5f, s.HoodY - (s.HoodY - s.NoseY) * 0.35f), new K(hl - 0.2f, s.NoseY + 0.12f), new K(hl - 0.04f, s.NoseY), new K(hl, s.NoseY - 0.12f),
            };
            if (s.Bed) p.belt[2] = new K(-hl + 0.22f, s.DeckY);
            p.bottom = new[] { new K(-hl, s.TailBottom + 0.12f), new K(-hl + 0.12f, s.TailBottom), new K(-hl + 0.5f, s.Sill), new K(hl - 0.5f, s.Sill), new K(hl - 0.1f, s.NoseBottom), new K(hl, s.NoseBottom + 0.1f) };
            p.half = new[]
            {
                new K(-hl, hw * s.TailNarrow), new K(-hl + 0.08f, hw * (s.TailNarrow + 0.1f)), new K(-hl + 0.4f, hw * 0.985f), new K(0, hw),
                new K(hl - 0.45f, hw * 0.98f), new K(hl - 0.1f, hw * (s.NoseNarrow + 0.1f)), new K(hl, hw * s.NoseNarrow),
            };
            return p;
        }

        // Superellipse section point; a = 0 bottom centre, going round through the right side (+x) to the top and down the left.
        static Vector3 Sec(float z, float hw, float y0, float y1, float a, float n)
        {
            float cx = Mathf.Sin(a), cy = -Mathf.Cos(a);
            float ex = Mathf.Sign(cx) * Mathf.Pow(Mathf.Abs(cx), 2f / n);
            float ey = Mathf.Sign(cy) * Mathf.Pow(Mathf.Abs(cy), 2f / n);
            float mid = (y0 + y1) / 2, hh = (y1 - y0) / 2;
            return new Vector3(ex * hw, mid + ey * hh, z);
        }

        // ------------------------------------------------------------------ build

        public static Built Get(CarDef def, int lod = 0)
        {
            string ck = def.Id + "_" + lod;
            if (cache.TryGetValue(ck, out var b)) return b;
            var s = def.Spec;
            plateCell = (def.Id.GetHashCode() & 0x7fffffff) % 16;
            if (def.Id.Contains("g30") || def.Id.Contains("bmw")) plateCell = 2;
            b = new Built { spec = s };
            var body = new SM();
            var door = new SM();
            var p = MakeProf(s);
            float hl = s.L / 2;
            int N = lod == 0 ? 64 : 30, M = lod == 0 ? 32 : 18;
            float doorZ0 = s.FrontAxle - s.R - 0.12f, doorZ1 = Mathf.Lerp(s.ZA, s.ZD, 0.47f);
            if (s.Bed) doorZ1 = s.ZD + 0.05f;
            b.doorPivot = new Vector3(-p.Half(doorZ0) * 0.995f, 0.5f, doorZ0);
            b.doorLen = doorZ0 - doorZ1;
            var doorXf = Matrix4x4.Translate(-b.doorPivot);
            bool InDoor(Vector3 c) => c.x < -0.2f && c.z < doorZ0 && c.z > doorZ1 && c.y > s.Sill + 0.04f;

            // lower body loft
            var rings = new Vector3[N + 1, M + 1];
            for (int i = 0; i <= N; i++)
            {
                float u = i / (float)N;
                float z = Mathf.Lerp(-hl, hl, (1 - Mathf.Cos(u * Mathf.PI)) / 2);
                float y0 = p.Bottom(z), y1 = p.Belt(z), hw = p.Half(z);
                float endK = Mathf.Clamp01((hl - Mathf.Abs(z)) / 0.06f);
                for (int j = 0; j <= M; j++)
                {
                    float a = j / (float)M * Mathf.PI * 2f;
                    var pt = Sec(z, hw, y0, y1, a, Mathf.Lerp(2.6f, 8f, endK));
                    // flatten the top a little (bonnet/boot are almost flat), keep the shoulder round
                    rings[i, j] = pt;
                }
            }
            for (int i = 0; i < N; i++)
                for (int j = 0; j < M; j++)
                {
                    Vector3 a = rings[i, j], bb = rings[i + 1, j], c = rings[i + 1, j + 1], d = rings[i, j + 1];
                    var cen = (a + bb + c + d) / 4;
                    // winding: outward normal
                    if (InDoor(cen))
                    {
                        door.Quad(Paint, doorXf.MultiplyPoint3x4(a), doorXf.MultiplyPoint3x4(d), doorXf.MultiplyPoint3x4(c), doorXf.MultiplyPoint3x4(bb));
                        var inA = doorXf.MultiplyPoint3x4(a + Vector3.right * 0.05f); var inB = doorXf.MultiplyPoint3x4(bb + Vector3.right * 0.05f);
                        var inC = doorXf.MultiplyPoint3x4(c + Vector3.right * 0.05f); var inD = doorXf.MultiplyPoint3x4(d + Vector3.right * 0.05f);
                        door.Quad(Interior, inA, inB, inC, inD);
                    }
                    else body.Quad(Paint, a, d, c, bb);
                }
            // end caps
            for (int end = 0; end < 2; end++)
            {
                int i = end == 0 ? 0 : N;
                var cen = Vector3.zero;
                for (int j = 0; j < M; j++) cen += rings[i, j];
                cen /= M;
                for (int j = 0; j < M; j++)
                    if (end == 1) body.Tri(Paint, cen, rings[i, j], rings[i, j + 1]);
                    else body.Tri(Paint, cen, rings[i, j + 1], rings[i, j]);
            }

            // glasshouse loft
            Greenhouse(body, door, p, doorXf, InDoor, lod);
            // dark wheel well liners
            foreach (float az in new[] { s.FrontAxle, s.RearAxle })
                foreach (float sx in new[] { -1f, 1f })
                    body.Cyl(Matte, new Vector3(sx * (p.Half(az) - 0.2f), s.R, az), new Vector3((s.R + 0.07f) * 2, 0.18f, (s.R + 0.07f) * 2), Quaternion.Euler(0, 0, 90));
            Details(body, door, p, doorXf, doorZ0, doorZ1);
            Cabin(body, p, b);
            b.body = body.Build(def.Id + "_body", true);
            b.door = door.Build(def.Id + "_door", true);
            b.wheel = Wheel(s, lod);
            cache[ck] = b;
            return b;
        }

        static float RoofLine(Prof p, float z)
        {
            var s = p.s;
            float belt = p.Belt(z);
            if (z >= s.ZA || z <= s.ZD) return belt;
            float roof = s.H;
            if (z > s.ZB) return Mathf.Lerp(roof - 0.015f, belt, Mathf.SmoothStep(0, 1, (z - s.ZB) / (s.ZA - s.ZB)));
            if (z < s.ZC) return Mathf.Lerp(roof - 0.02f, belt, Mathf.Pow((s.ZC - z) / (s.ZC - s.ZD), 1.4f));
            float mid = (s.ZB + s.ZC) / 2, half = (s.ZB - s.ZC) / 2;
            float k = (z - mid) / half;
            return roof - 0.03f * k * k;
        }

        static void Greenhouse(SM body, SM door, Prof p, Matrix4x4 doorXf, System.Func<Vector3, bool> inDoor, int lod)
        {
            var s = p.s;
            int N = lod == 0 ? 40 : 16, M = 16;
            var pts = new Vector3[N + 1, M + 1];
            var zs = new float[N + 1];
            for (int i = 0; i <= N; i++)
            {
                float z = Mathf.Lerp(s.ZD, s.ZA, i / (float)N);
                zs[i] = z;
                float belt = p.Belt(z) - 0.03f;
                float top = Mathf.Max(belt + 0.002f, RoofLine(p, z));
                float hwb = p.Half(z) * 0.965f, hwt = p.Half(z) * s.Tumble;
                for (int j = 0; j <= M; j++)
                {
                    // j: 0 right bottom → right side → roof → left side → 15 left bottom
                    float a = j / (float)M * Mathf.PI;           // 0..pi over the top
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    float ex = Mathf.Sign(ca) * Mathf.Pow(Mathf.Abs(ca), 0.35f);
                    float ey = Mathf.Pow(Mathf.Max(0f, sa), 0.45f);
                    float hwAt = Mathf.Lerp(hwb, hwt, ey);
                    pts[i, j] = new Vector3(ex * hwAt, Mathf.Lerp(belt, top, ey), z);
                }
            }
            float bp = Mathf.Lerp(s.ZA, s.ZD, 0.47f);
            for (int i = 0; i < N; i++)
                for (int j = 0; j < M; j++)
                {
                    Vector3 a = pts[i, j], b = pts[i + 1, j], c = pts[i + 1, j + 1], d = pts[i, j + 1];
                    var cen = (a + b + c + d) / 4;
                    float zc = cen.z;
                    bool roofBand = j >= 5 && j < M - 5;
                    bool edge = j == 4 || j == M - 5;
                    int slot;
                    if (roofBand) slot = zc < s.ZB - 0.04f && zc > s.ZC + 0.04f ? Paint : GlassS;
                    else if (edge) slot = Paint;
                    else
                    {
                        bool aPillar = zc > s.ZB - 0.02f;
                        bool bPillar = Mathf.Abs(zc - bp) < 0.055f;
                        bool cPillar = zc < s.ZC + (s.ZD - s.ZC) * -0.15f && zc < s.ZC;
                        slot = aPillar || bPillar || cPillar ? Paint : GlassS;
                        if (s.Bed) slot = aPillar || zc < s.ZD + 0.12f ? Paint : GlassS;
                    }
                    if (roofBand && (zc >= s.ZB - 0.04f)) slot = GlassS;     // windshield
                    if (inDoor(cen) && !roofBand && !edge)
                        door.Quad(slot, doorXf.MultiplyPoint3x4(a), doorXf.MultiplyPoint3x4(d), doorXf.MultiplyPoint3x4(c), doorXf.MultiplyPoint3x4(b));
                    else body.Quad(slot, a, d, c, b);
                }
            // close the front/back (only matters at the windshield base / rear window base, tiny)
            // chrome window surround along the belt
            float beltSide(float z) => p.Half(z) * 0.968f;
            var right = new List<Vector3>(); var left = new List<Vector3>();
            for (int i = 0; i <= 20; i++)
            {
                float z = Mathf.Lerp(s.ZD + 0.1f, s.ZA - 0.05f, i / 20f);
                right.Add(new Vector3(beltSide(z) + 0.004f, p.Belt(z) - 0.02f, z));
                left.Add(new Vector3(-beltSide(z) - 0.004f, p.Belt(z) - 0.02f, z));
            }
            body.Add(Human.Tube(null, right, 0.009f, 6), Matrix4x4.identity, Chrome);
            body.Add(Human.Tube(null, left, 0.009f, 6), Matrix4x4.identity, Chrome);
        }

        /// <summary>Front-most z of the lower body at (x, y) (for placing lights and grilles on the nose).</summary>
        static float FrontAt(Prof p, float x, float y, bool front)
        {
            var s = p.s;
            float hl = s.L / 2;
            for (int k = 0; k < 400; k++)
            {
                float z = front ? hl - k * 0.005f : -hl + k * 0.005f;
                float y0 = p.Bottom(z), y1 = p.Belt(z), hw = p.Half(z);
                if (y < y0 || y > y1) continue;
                float mid = (y0 + y1) / 2, hh = (y1 - y0) / 2;
                float ny = Mathf.Abs(y - mid) / Mathf.Max(0.01f, hh);
                float n = 8f;
                float lim = hw * Mathf.Pow(Mathf.Max(0, 1 - Mathf.Pow(ny, n)), 1f / n);
                if (Mathf.Abs(x) <= lim) return z;
            }
            return front ? hl - 0.3f : -hl + 0.3f;
        }

        static int plateCell;
        static Rect PlateUV => new Rect((plateCell % 4) * 0.25f + 0.002f, 1f - (plateCell / 4 + 1) * 0.25f + 0.004f, 0.246f, 0.242f);

        static void Details(SM body, SM door, Prof p, Matrix4x4 doorXf, float dz0, float dz1)
        {
            var s = p.s;
            float hl = s.L / 2;
            float hy = p.Belt(hl - 0.2f) - 0.12f;   // headlight height

            // --- front: grille
            if (s.Grille == 1)
            {
                foreach (float sx in new[] { -1f, 1f })
                {
                    float gx = sx * 0.15f, gy = hy - 0.02f;
                    float gz = FrontAt(p, gx, gy, true);
                    var ring = new List<Vector3>();
                    for (int k = 0; k < 24; k++)
                    {
                        float a = k / 24f * Mathf.PI * 2;
                        float ex = Mathf.Sign(Mathf.Cos(a)) * Mathf.Pow(Mathf.Abs(Mathf.Cos(a)), 0.5f), ey = Mathf.Sign(Mathf.Sin(a)) * Mathf.Pow(Mathf.Abs(Mathf.Sin(a)), 0.5f);
                        ring.Add(new Vector3(gx + ex * 0.13f, gy + ey * 0.09f, gz + 0.015f));
                    }
                    body.Add(Human.Tube(null, ring, 0.012f, 6, true), Matrix4x4.identity, Chrome);
                    body.Box(Gloss, new Vector3(gx, gy, gz - 0.005f), new Vector3(0.25f, 0.17f, 0.04f));
                    for (int k = -3; k <= 3; k++) body.Box(Chrome, new Vector3(gx + k * 0.032f, gy, gz + 0.01f), new Vector3(0.006f, 0.15f, 0.02f));
                }
            }
            else if (s.Grille == 2 || s.Grille == 3)
            {
                float gw = s.Grille == 3 ? s.W * 0.55f : s.W * 0.42f, gh = s.Grille == 3 ? 0.32f : 0.16f, gy = hy - (s.Grille == 3 ? 0.1f : 0.03f);
                float gz = FrontAt(p, 0, gy, true);
                body.Box(Gloss, new Vector3(0, gy, gz - 0.005f), new Vector3(gw, gh, 0.04f));
                for (int k = 0; k < 5; k++) body.Box(Chrome, new Vector3(0, gy - gh / 2 + (k + 0.5f) * gh / 5, gz + 0.012f), new Vector3(gw * 0.98f, 0.008f, 0.02f));
                body.Box(Chrome, new Vector3(0, gy + gh / 2, gz + 0.01f), new Vector3(gw + 0.02f, 0.015f, 0.02f));
            }
            else
            {
                // electric: a light bar across the nose
                float gz = FrontAt(p, 0, hy, true);
                body.Box(HeadL, new Vector3(0, hy + 0.04f, gz + 0.004f), new Vector3(s.W * 0.7f, 0.018f, 0.02f));
            }
            // lower intake and number plate
            float iy = s.NoseBottom + 0.12f;
            float iz = FrontAt(p, 0, iy, true);
            body.Box(Matte, new Vector3(0, iy, iz - 0.01f), new Vector3(s.W * 0.5f, 0.12f, 0.06f));
            body.Box(Plate, new Vector3(0, iy + 0.1f, FrontAt(p, 0, iy + 0.1f, true) + 0.005f), new Vector3(0.52f, 0.11f, 0.012f));
            body.Decal(PlateT, new Vector3(0, iy + 0.1f, FrontAt(p, 0, iy + 0.1f, true) + 0.0115f), Vector3.forward, 0.52f, 0.11f, PlateUV);
            foreach (float sx in new[] { -1f, 1f })
                body.Box(Matte, new Vector3(sx * s.W * 0.36f, iy + 0.02f, FrontAt(p, sx * s.W * 0.36f, iy + 0.02f, true) - 0.005f), new Vector3(0.2f, 0.1f, 0.05f));

            // --- headlights
            foreach (float sx in new[] { -1f, 1f })
            {
                float lx = sx * (s.W / 2 - (s.Head == 1 ? 0.28f : 0.3f));
                float lz = FrontAt(p, lx, hy, true);
                if (s.Head == 0)
                {
                    body.Box(Gloss, new Vector3(lx, hy, lz - 0.01f), new Vector3(0.38f, 0.1f, 0.06f), Quaternion.Euler(0, sx * 12, sx * -4));
                    foreach (float dx in new[] { -0.08f, 0.08f })
                    {
                        var ring = new List<Vector3>();
                        for (int k = 0; k < 16; k++) { float a = k / 16f * Mathf.PI * 2; ring.Add(new Vector3(lx + dx + Mathf.Cos(a) * 0.04f, hy + Mathf.Sin(a) * 0.035f, lz + 0.03f)); }
                        body.Add(Human.Tube(null, ring, 0.006f, 5, true), Matrix4x4.identity, HeadL);
                    }
                    body.Box(HeadL, new Vector3(lx - sx * 0.02f, hy + 0.045f, lz + 0.02f), new Vector3(0.3f, 0.008f, 0.02f), Quaternion.Euler(0, sx * 12, 0));
                }
                else if (s.Head == 1)
                {
                    body.Cyl(Chrome, new Vector3(lx, hy, lz - 0.01f), new Vector3(0.2f, 0.03f, 0.2f), Quaternion.Euler(90, 0, 0));
                    body.Cyl(HeadL, new Vector3(lx, hy, lz + 0.02f), new Vector3(0.16f, 0.01f, 0.16f), Quaternion.Euler(90, 0, 0));
                }
                else if (s.Head == 2)
                    body.Box(HeadL, new Vector3(lx, hy + 0.04f, lz + 0.005f), new Vector3(0.32f, 0.025f, 0.03f), Quaternion.Euler(0, sx * 15, 0));
                else
                {
                    body.Box(Gloss, new Vector3(lx, hy, lz - 0.01f), new Vector3(0.34f, 0.07f, 0.06f), Quaternion.Euler(0, sx * 18, sx * -10));
                    body.Box(HeadL, new Vector3(lx, hy - 0.01f, lz + 0.02f), new Vector3(0.28f, 0.015f, 0.02f), Quaternion.Euler(0, sx * 18, sx * -10));
                }
            }
            // badge on the bonnet
            if (s.Badge)
            {
                float bz = FrontAt(p, 0, p.Belt(hl - 0.15f) - 0.04f, true);
                var bpos = new Vector3(0, p.Belt(hl - 0.15f) - 0.03f, bz + 0.005f);
                body.Cyl(Chrome, bpos, new Vector3(0.08f, 0.006f, 0.08f), Quaternion.Euler(90, 0, 0));
                body.Cyl(Gloss, bpos + Vector3.forward * 0.006f, new Vector3(0.068f, 0.004f, 0.068f), Quaternion.Euler(90, 0, 0));
                body.Cyl(Screen, bpos + Vector3.forward * 0.009f, new Vector3(0.042f, 0.002f, 0.042f), Quaternion.Euler(90, 0, 0));
                body.Box(Plate, bpos + new Vector3(0.0105f, 0.0105f, 0.0105f), new Vector3(0.021f, 0.021f, 0.002f));
                body.Box(Plate, bpos + new Vector3(-0.0105f, -0.0105f, 0.0105f), new Vector3(0.021f, 0.021f, 0.002f));
            }

            // --- rear: tail lights, plate, exhaust, diffuser
            float ty = p.Belt(-hl + 0.2f) - 0.1f;
            foreach (float sx in new[] { -1f, 1f })
            {
                float tx = sx * (s.W / 2 - 0.25f);
                float tz = FrontAt(p, tx, ty, false);
                if (s.Tail == 0)
                {
                    body.Box(Gloss, new Vector3(tx, ty, tz + 0.01f), new Vector3(0.42f, 0.13f, 0.05f), Quaternion.Euler(0, -sx * 10, 0));
                    body.Box(TailL, new Vector3(tx, ty + 0.03f, tz - 0.018f), new Vector3(0.36f, 0.018f, 0.02f), Quaternion.Euler(0, -sx * 10, 0));
                    body.Box(TailL, new Vector3(tx + sx * 0.15f, ty - 0.01f, tz - 0.012f), new Vector3(0.02f, 0.09f, 0.02f), Quaternion.Euler(0, -sx * 10, 0));
                    body.Box(TailL, new Vector3(tx - sx * 0.02f, ty - 0.035f, tz - 0.018f), new Vector3(0.3f, 0.012f, 0.02f), Quaternion.Euler(0, -sx * 10, 0));
                }
                else if (s.Tail == 2)
                    body.Box(TailL, new Vector3(sx * (s.W / 2 - 0.07f), ty, FrontAt(p, sx * (s.W / 2 - 0.07f), ty, false) - 0.01f), new Vector3(0.08f, 0.35f, 0.04f));
                else
                    body.Box(TailL, new Vector3(tx, ty, tz - 0.012f), new Vector3(0.4f, 0.06f, 0.03f));
            }
            if (s.Tail == 1) body.Box(TailL, new Vector3(0, ty + 0.02f, FrontAt(p, 0, ty + 0.02f, false) - 0.01f), new Vector3(s.W * 0.8f, 0.018f, 0.02f));
            float py = s.TailBottom + 0.25f;
            body.Box(Plate, new Vector3(0, py, FrontAt(p, 0, py, false) - 0.006f), new Vector3(0.52f, 0.11f, 0.012f));
            body.Decal(PlateT, new Vector3(0, py, FrontAt(p, 0, py, false) - 0.0125f), Vector3.back, 0.52f, 0.11f, PlateUV);
            // shark-fin aerial and a third brake light at the back of the roof
            float finZ = Mathf.Lerp(s.ZC, s.ZB, 0.15f);
            body.Box(Gloss, new Vector3(0, RoofLine(p, finZ) + 0.03f, finZ), new Vector3(0.06f, 0.06f, 0.16f), Quaternion.Euler(-12, 0, 0));
            body.Box(TailL, new Vector3(0, RoofLine(p, s.ZC) - 0.03f, s.ZC - 0.02f), new Vector3(0.28f, 0.018f, 0.02f));
            // windscreen wipers resting at the base of the windshield
            foreach (float wx in new[] { -0.32f, 0.18f })
            {
                float wz = s.ZA - 0.03f, wy = p.Belt(wz) + 0.025f;
                body.Box(Matte, new Vector3(wx, wy, wz), new Vector3(0.55f, 0.012f, 0.025f), Quaternion.Euler(0, 6, 0));
                body.Cyl(Matte, new Vector3(wx + 0.26f, wy - 0.005f, wz + 0.01f), new Vector3(0.03f, 0.012f, 0.03f), Quaternion.identity);
            }
            if (s.Badge) body.Cyl(Chrome, new Vector3(0, ty + 0.12f, FrontAt(p, 0, ty + 0.12f, false) - 0.005f), new Vector3(0.07f, 0.005f, 0.07f), Quaternion.Euler(90, 0, 0));
            float dy = s.TailBottom + 0.08f;
            body.Box(Matte, new Vector3(0, dy, FrontAt(p, 0, dy, false) + 0.02f), new Vector3(s.W * 0.7f, 0.1f, 0.08f));
            for (int e = 0; e < s.Exhaust; e++)
            {
                float ex = s.Exhaust == 1 ? -s.W * 0.3f : (e % 2 == 0 ? -1 : 1) * s.W * (0.3f - (e / 2) * 0.07f);
                body.Cyl(Chrome, new Vector3(ex, dy, FrontAt(p, ex, dy, false) - 0.01f), new Vector3(0.085f, 0.04f, 0.085f), Quaternion.Euler(90, 0, 0));
                body.Cyl(Matte, new Vector3(ex, dy, FrontAt(p, ex, dy, false) - 0.03f), new Vector3(0.065f, 0.02f, 0.065f), Quaternion.Euler(90, 0, 0));
            }
            if (s.Spoiler) body.Box(Paint, new Vector3(0, p.Belt(-hl + 0.12f) + 0.01f, -hl + 0.14f), new Vector3(s.W * 0.75f, 0.02f, 0.14f), Quaternion.Euler(-8, 0, 0));
            if (s.Wing)
            {
                body.Box(Gloss, new Vector3(0, p.Belt(-hl + 0.2f) + 0.24f, -hl + 0.22f), new Vector3(s.W * 0.92f, 0.025f, 0.28f), Quaternion.Euler(-6, 0, 0));
                foreach (float sx in new[] { -0.45f, 0.45f }) body.Box(Gloss, new Vector3(sx, p.Belt(-hl + 0.2f) + 0.12f, -hl + 0.24f), new Vector3(0.025f, 0.24f, 0.12f));
            }
            if (s.RoofRails) foreach (float sx in new[] { -1f, 1f }) body.Box(Chrome, new Vector3(sx * (s.W / 2 * s.Tumble - 0.06f), s.H + 0.025f, (s.ZB + s.ZC) / 2), new Vector3(0.03f, 0.04f, (s.ZB - s.ZC) * 0.95f));
            if (s.Taxi)
            {
                body.Box(Plate, new Vector3(0, s.H + 0.08f, (s.ZB + s.ZC) / 2), new Vector3(0.62f, 0.14f, 0.2f));
                body.Box(HeadL, new Vector3(0, s.H + 0.08f, (s.ZB + s.ZC) / 2), new Vector3(0.55f, 0.08f, 0.21f));
            }

            // --- sides: mirrors (on the door for the driver), handles, door gaps, skirts
            foreach (float sx in new[] { -1f, 1f })
            {
                float mz = s.ZA - 0.08f, my = p.Belt(mz) + 0.08f, mx = sx * (p.Half(mz) + 0.1f);
                var target = sx < 0 ? door : body;
                var xf = sx < 0 ? doorXf : Matrix4x4.identity;
                target.Add(Prim.MeshOf(PrimitiveType.Sphere), xf * Matrix4x4.TRS(new Vector3(mx, my, mz), Quaternion.identity, new Vector3(0.2f, 0.11f, 0.13f)), Paint);
                target.Add(Prim.MeshOf(PrimitiveType.Cube), xf * Matrix4x4.TRS(new Vector3(mx - sx * 0.06f, my - 0.03f, mz + 0.02f), Quaternion.identity, new Vector3(0.12f, 0.03f, 0.05f)), Gloss);
                target.Add(Prim.MeshOf(PrimitiveType.Cube), xf * Matrix4x4.TRS(new Vector3(mx + sx * 0.005f, my, mz - 0.06f), Quaternion.identity, new Vector3(0.16f, 0.08f, 0.01f)), Chrome);
                // handles
                float h1 = Mathf.Lerp(dz0, dz1, 0.75f), hy2 = p.Belt(h1) - 0.1f;
                target.Add(Prim.MeshOf(PrimitiveType.Cube), xf * Matrix4x4.TRS(new Vector3(sx * (p.Half(h1) + 0.008f), hy2, h1), Quaternion.identity, new Vector3(0.02f, 0.03f, 0.16f)), Chrome);
                if (!s.Bed)
                {
                    float h2 = Mathf.Lerp(dz1, s.RearAxle + s.R + 0.15f, 0.7f);
                    body.Box(Chrome, new Vector3(sx * (p.Half(h2) + 0.008f), p.Belt(h2) - 0.1f, h2), new Vector3(0.02f, 0.03f, 0.16f));
                }
                // side skirt
                float skirtZ0 = s.FrontAxle - s.R - 0.1f, skirtZ1 = s.RearAxle + s.R + 0.1f;
                body.Box(Matte, new Vector3(sx * (p.Half(0) - 0.01f), s.Sill + 0.03f, (skirtZ0 + skirtZ1) / 2), new Vector3(0.03f, 0.07f, skirtZ0 - skirtZ1));
                // door gaps
                foreach (float gz in new[] { dz0, dz1, s.Bed ? 99f : Mathf.Lerp(dz1, s.RearAxle + s.R, 1f) })
                {
                    if (gz > 50) continue;
                    var gap = new List<Vector3>();
                    for (int k = 0; k <= 8; k++)
                    {
                        float y = Mathf.Lerp(s.Sill + 0.06f, p.Belt(gz) - 0.02f, k / 8f);
                        gap.Add(new Vector3(sx * (p.Half(gz) * Mathf.Lerp(0.995f, 0.97f, k / 8f) + 0.003f), y, gz));
                    }
                    body.Add(Human.Tube(null, gap, 0.004f, 4), Matrix4x4.identity, Gloss);
                }
            }
        }

        static void Cabin(SM body, Prof p, Built b)
        {
            var s = p.s;
            float floorY = s.Sill + 0.12f;
            float frontSeatZ = Mathf.Lerp(s.ZA, s.ZD, 0.47f) + 0.05f;
            float hw = p.Half(frontSeatZ) - 0.12f;
            // floor and dashboard
            body.Box(Interior, new Vector3(0, floorY, (s.ZA + s.ZD) / 2), new Vector3(hw * 2, 0.04f, s.ZA - s.ZD));
            float dashZ = s.ZA - 0.12f, dashY = p.Belt(dashZ) - 0.05f;
            body.Box(Interior, new Vector3(0, dashY - 0.12f, dashZ), new Vector3(hw * 2, 0.32f, 0.38f));
            body.Box(Gloss, new Vector3(0, dashY + 0.03f, dashZ - 0.1f), new Vector3(hw * 1.9f, 0.04f, 0.25f));
            // central screen
            body.Box(Gloss, new Vector3(0, dashY + 0.12f, dashZ - 0.16f), new Vector3(0.3f, 0.15f, 0.02f), Quaternion.Euler(-10, 0, 0));
            body.Box(Screen, new Vector3(0, dashY + 0.12f, dashZ - 0.172f), new Vector3(0.27f, 0.13f, 0.005f), Quaternion.Euler(-10, 0, 0));
            // seats (driver on the left)
            float seatX = -hw * 0.5f;
            foreach (float sx in new[] { seatX, -seatX })
            {
                body.Box(Interior, new Vector3(sx, floorY + 0.22f, frontSeatZ), new Vector3(0.5f, 0.12f, 0.5f));
                body.Box(Interior, new Vector3(sx, floorY + 0.55f, frontSeatZ - 0.27f), new Vector3(0.5f, 0.62f, 0.1f), Quaternion.Euler(-12, 0, 0));
                body.Box(Interior, new Vector3(sx, floorY + 0.92f, frontSeatZ - 0.33f), new Vector3(0.26f, 0.18f, 0.08f));
            }
            if (!s.Bed && s.ZD < frontSeatZ - 1.0f)
            {
                float rz = frontSeatZ - 0.85f;
                body.Box(Interior, new Vector3(0, floorY + 0.24f, rz), new Vector3(hw * 1.8f, 0.14f, 0.48f));
                body.Box(Interior, new Vector3(0, floorY + 0.56f, rz - 0.27f), new Vector3(hw * 1.8f, 0.6f, 0.1f), Quaternion.Euler(-14, 0, 0));
            }
            // steering wheel
            var sw = new Vector3(seatX, dashY + 0.02f, dashZ - 0.33f);
            var ring = new List<Vector3>();
            for (int k = 0; k < 20; k++) { float a = k / 20f * Mathf.PI * 2; ring.Add(sw + Quaternion.Euler(-25, 0, 0) * new Vector3(Mathf.Cos(a) * 0.18f, Mathf.Sin(a) * 0.18f, 0)); }
            body.Add(Human.Tube(null, ring, 0.018f, 6, true), Matrix4x4.identity, Gloss);
            body.Cyl(Gloss, sw + Quaternion.Euler(-25, 0, 0) * new Vector3(0, 0, 0.02f), new Vector3(0.11f, 0.03f, 0.11f), Quaternion.Euler(65, 0, 0));
            body.Cyl(Gloss, sw + new Vector3(0, -0.02f, 0.12f), new Vector3(0.05f, 0.12f, 0.05f), Quaternion.Euler(65, 0, 0));
            b.seat = new Vector3(seatX, s.Sill - 0.16f, frontSeatZ - 0.08f);
            b.steering = sw;
        }

        static readonly Dictionary<string, Mesh> statics = new Dictionary<string, Mesh>();

        /// <summary>Body + closed door + wheels in one mesh (parked cars, showroom cars).</summary>
        public static Mesh Static(CarDef def, int lod = 1, bool wheels = true)
        {
            string key = def.Id + "_s" + lod + (wheels ? "" : "_nw");
            if (statics.TryGetValue(key, out var m)) return m;
            var b = Get(def, lod);
            var sm = new SM();
            void AddAll(Mesh mesh, Matrix4x4 xf)
            {
                for (int s = 0; s < mesh.subMeshCount && s < Slots; s++) sm.AddSub(mesh, s, xf, s);
            }
            AddAll(b.body, Matrix4x4.identity);
            AddAll(b.door, Matrix4x4.Translate(b.doorPivot));
            if (wheels) foreach (var w in WheelSpots(b.spec)) AddAll(b.wheel, Matrix4x4.TRS(w, Quaternion.identity, new Vector3(w.x < 0 ? -1 : 1, 1, 1)));
            m = sm.Build(key, false);
            statics[key] = m;
            return m;
        }

        public static IEnumerable<Vector3> WheelSpots(CarSpec s)
        {
            float x = s.W / 2 - 0.15f;
            yield return new Vector3(-x, s.R, s.FrontAxle);
            yield return new Vector3(x, s.R, s.FrontAxle);
            yield return new Vector3(-x, s.R, s.RearAxle);
            yield return new Vector3(x, s.R, s.RearAxle);
        }

        // ------------------------------------------------------------------ wheel

        static Mesh Wheel(CarSpec s, int lod)
        {
            var m = new SM();
            float R = s.R, w = 0.24f, rimR = R * 0.68f;
            // tyre: revolve a rounded profile around X
            int seg = lod == 0 ? 36 : 16;
            var prof = new List<Vector2>();   // (x, r)
            for (int k = 0; k <= 10; k++)
            {
                float a = k / 10f * Mathf.PI;
                prof.Add(new Vector2(-Mathf.Cos(a) * w / 2, Mathf.Lerp(rimR + 0.01f, R - 0.035f, 1) + Mathf.Sin(a) * 0.035f));
            }
            prof.Insert(0, new Vector2(-w / 2 + 0.01f, rimR + 0.005f));
            prof.Add(new Vector2(w / 2 - 0.01f, rimR + 0.005f));
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2, a1 = (i + 1) / (float)seg * Mathf.PI * 2;
                for (int k = 0; k < prof.Count - 1; k++)
                {
                    Vector3 P(Vector2 pr, float a) => new Vector3(pr.x, Mathf.Cos(a) * pr.y, Mathf.Sin(a) * pr.y);
                    m.Quad(Rubber, P(prof[k], a0), P(prof[k], a1), P(prof[k + 1], a1), P(prof[k + 1], a0));
                }
            }
            // rim: barrel, face, spokes, hub; brake disc + caliper behind
            m.Cyl(RimS, new Vector3(-0.01f, 0, 0), new Vector3(rimR * 2, w * 0.45f, rimR * 2), Quaternion.Euler(0, 0, 90));
            m.Cyl(Gloss, new Vector3(0.05f, 0, 0), new Vector3(rimR * 1.9f, 0.01f, rimR * 1.9f), Quaternion.Euler(0, 0, 90));
            m.Cyl(Brake, new Vector3(-0.02f, 0, 0), new Vector3(rimR * 1.55f, 0.02f, rimR * 1.55f), Quaternion.Euler(0, 0, 90));
            m.Box(Brake, new Vector3(0.0f, rimR * 0.62f, -rimR * 0.2f), new Vector3(0.07f, 0.12f, 0.16f));
            var ringPts = new List<Vector3>();
            for (int k = 0; k < 28; k++) { float a = k / 28f * Mathf.PI * 2; ringPts.Add(new Vector3(w * 0.3f, Mathf.Cos(a) * rimR * 0.97f, Mathf.Sin(a) * rimR * 0.97f)); }
            m.Add(Human.Tube(null, ringPts, 0.018f, 6, true), Matrix4x4.identity, RimS);
            for (int k = 0; k < s.Spokes; k++)
            {
                float a = k / (float)s.Spokes * 360f;
                foreach (float off in new[] { -7f, 7f })
                {
                    var rot = Quaternion.Euler(a + off, 0, 0);
                    m.Box(RimS, new Vector3(w * 0.3f, 0, 0) + rot * new Vector3(0, rimR * 0.52f, 0), new Vector3(0.025f, rimR * 0.85f, 0.03f), rot);
                }
            }
            m.Cyl(RimS, new Vector3(w * 0.3f + 0.01f, 0, 0), new Vector3(rimR * 0.42f, 0.02f, rimR * 0.42f), Quaternion.Euler(0, 0, 90));
            m.Cyl(Chrome, new Vector3(w * 0.3f + 0.025f, 0, 0), new Vector3(rimR * 0.18f, 0.01f, rimR * 0.18f), Quaternion.Euler(0, 0, 90));
            // lug nuts round the hub
            for (int k = 0; k < 5; k++)
            {
                float a = k / 5f * Mathf.PI * 2;
                m.Cyl(Chrome, new Vector3(w * 0.3f + 0.028f, Mathf.Cos(a) * rimR * 0.13f, Mathf.Sin(a) * rimR * 0.13f), new Vector3(0.022f, 0.012f, 0.022f), Quaternion.Euler(0, 0, 90));
            }
            if (lod == 0)
            {   // tread: blocks across the tyre with grooves between them, and a raised sidewall ring
                int blocks = 44;
                for (int k = 0; k < blocks; k++)
                {
                    var rot = Quaternion.Euler(k / (float)blocks * 360f, 0, 0);
                    foreach (float bx in new[] { -0.075f, 0f, 0.075f })
                        m.Box(Rubber, rot * new Vector3(bx, R - 0.003f, 0), new Vector3(0.062f, 0.012f, R * 6.2832f / blocks * 0.72f), rot * Quaternion.Euler(0, (bx == 0 ? 0 : 18 * Mathf.Sign(bx)), 0));
                }
                var side = new List<Vector3>();
                for (int k = 0; k < 40; k++) { float a = k / 40f * Mathf.PI * 2; side.Add(new Vector3(w / 2 - 0.004f, Mathf.Cos(a) * (R - 0.06f), Mathf.Sin(a) * (R - 0.06f))); }
                m.Add(Human.Tube(null, side, 0.006f, 5, true), Matrix4x4.identity, Rubber);
            }
            return m.Build("wheel_" + s.R + "_" + lod, true);
        }
    }
}
