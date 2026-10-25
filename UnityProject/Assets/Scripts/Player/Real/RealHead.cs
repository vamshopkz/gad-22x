using System.Collections.Generic;
using UnityEngine;

namespace Gad
{
    /// <summary>
    /// 0.6: realistic heads from Blender Studio's Human Base Meshes (CC0), cut and baked by
    /// assets_src/tools/heads.py: a sculpted male and female head with baked skin relief, painted
    /// lips, brows, blush and lids, real eyeballs with an iris, and the face shapes as a deformation.
    /// The mesh origin is between the eyes, the face along +Z.
    /// </summary>
    public static partial class Human
    {
        const float RealTopM = 0.1107f, RealTopF = 0.1069f, RealEyeX = 0.0329f, RealEyeR = 0.012f;
        static readonly Dictionary<string, Mesh> realMeshes = new Dictionary<string, Mesh>();
        /// <summary>0.9: set while building a crowd person: the head with a fifth of the triangles.</summary>
        public static bool LowDetail;
        static readonly Dictionary<string, Texture2D> realTex = new Dictionary<string, Texture2D>();

        static Texture2D RealTex(string name)
        {
            if (!realTex.TryGetValue(name, out var t)) realTex[name] = t = Resources.Load<Texture2D>("Heads/" + name);
            return t;
        }

        public static bool RealHeadAvailable(bool female) => RealTex(female ? "head_f_a" : "head_m_a") != null;

        /// <summary>The head mesh for a gender and face shape (shapes widen/narrow the jaw and cheeks, lengthen the face).</summary>
        static Mesh RealMesh(bool female, int face, bool lo = false)
        {
            string key = (female ? "f" : "m") + face + (lo ? "lo" : "");
            if (realMeshes.TryGetValue(key, out var m)) return m;
            var src = Resources.Load<TextAsset>("Heads/head_" + (female ? "f" : "m") + (lo ? "_lo" : ""));
            if (src == null && lo) return RealMesh(female, face, false);
            if (src == null) return null;
            var b = src.bytes;
            int nv = System.BitConverter.ToInt32(b, 0), nt = System.BitConverter.ToInt32(b, 4), o = 8;
            float F() { float v = System.BitConverter.ToSingle(b, o); o += 4; return v; }
            var v3 = new Vector3[nv]; var n3 = new Vector3[nv]; var uv = new Vector2[nv];
            for (int i = 0; i < nv; i++) { v3[i] = new Vector3(F(), F(), F()); n3[i] = new Vector3(F(), F(), F()); uv[i] = new Vector2(F(), F()); }
            var tris = new int[nt * 3];
            for (int i = 0; i < tris.Length; i++) { tris[i] = System.BitConverter.ToInt32(b, o); o += 4; }
            var fs = Faces[Mathf.Clamp(face, 0, Faces.Length - 1)];
            if (face != 0)
                for (int i = 0; i < nv; i++)
                {
                    var p = v3[i];
                    if (p.y >= 0.02f) continue;
                    // below the eyes: jaw at the chin, cheekbones at the eyes; above the brow unchanged
                    float w = p.y < 0f ? Mathf.Lerp(fs.jaw, fs.cheek, Mathf.InverseLerp(-0.11f, -0.02f, p.y)) : Mathf.Lerp(fs.cheek, 1f, p.y / 0.02f);
                    float front = Mathf.Clamp01((p.z + 0.06f) / 0.08f);   // only the face and jaw, not the back of the skull
                    w = Mathf.Lerp(1f, w, front);
                    float y = p.y < 0f ? p.y * Mathf.Lerp(1f, fs.len, front) : p.y;
                    if (p.y < -0.09f && fs.chin != 1f) y += (fs.chin - 1f) * 0.004f * front;   // a squarer chin sits a little lower
                    v3[i] = new Vector3(p.x * w, y, p.z);
                }
            m = new Mesh { name = "realhead_" + key, indexFormat = nv > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.vertices = v3; m.uv = uv; m.triangles = tris;
            m.normals = n3;   // the deformation is gentle; recalculating would split the normals along UV seams
            m.RecalculateTangents();
            m.RecalculateBounds();
            realMeshes[key] = m;
            return m;
        }

        public static Material RealHeadMat(Color skin, bool female, bool stubble) =>
            Mats.Shared("realhead_" + ColorUtility.ToHtmlStringRGB(skin) + (female ? "f" : stubble ? "s" : "m"),
                () => Mats.LitNormal(skin, RealTex(female ? "head_f_a" : stubble ? "head_m_s" : "head_m_a"), RealTex(female ? "head_f_n" : "head_m_n"), 1f, 0.36f));

        /// <summary>Put the realistic head on the head bone, sized so its crown meets the hair line of the old head.</summary>
        static bool BuildRealHead(HumanRig h, Color skin)
        {
            var s = h.shape;
            var mesh = RealMesh(s.female, s.face, LowDetail);
            if (mesh == null) return false;
            float hs = s.headSize, k = 1.06f * hs;
            float top = s.female ? RealTopF : RealTopM;
            float ey = s.headTop - top * k - 0.004f;
            float ez = s.head.FrontZ(ey, RealEyeX * k) - 0.014f * k;
            var go = Part("body_head", mesh, RealHeadMat(skin, s.female, false), h.head, new Vector3(0f, ey, ez), Quaternion.identity, Vector3.one * k);
            h.realHead = go.GetComponent<Renderer>();
            h.realEye = new Vector3(0f, ey, ez);
            h.realK = k;
            return true;
        }

        /// <summary>Eyeballs: a white ball with a faint warm edge and an iris disc on the front.</summary>
        static void RealEyes(Dress x, Wardrobe.SkinDef d)
        {
            var h = x.h;
            float k = h.realK, r = RealEyeR * k;
            var col = d != null && !d.none ? d.color : new Color(0.35f, 0.2f, 0.1f);
            var white = Mats.Shared("hum_sclera", () => Mats.Lit(new Color(0.93f, 0.9f, 0.88f), null, 0.85f));
            var iris = Mats.Shared("hum_iris_real_" + ColorUtility.ToHtmlStringRGB(col), () => { var m = Mats.Lit(Color.white, IrisTex(col), 0.9f); m.SetFloat("_Cull", 0f); return m; });
            foreach (float side in new[] { -1f, 1f })
            {
                var c = h.realEye + new Vector3(side * RealEyeX * k, 0f, -0.0015f * k);
                Acc(x, "eyeball", MeshOf(PrimitiveType.Sphere), white, h.head, c, null, Vector3.one * r * 1.9f);
                Acc(x, "iris", Disc(), iris, h.head, c + new Vector3(0f, 0f, r * 0.83f), null, new Vector3(r * 0.5f, r * 0.5f, r * 0.5f));
            }
        }

        static Mesh disc;
        /// <summary>A slightly domed disc facing +Z with radial UVs (0.5, 0.5 at the centre).</summary>
        static Mesh Disc()
        {
            if (disc != null) return disc;
            const int N = 24;
            var v = new Vector3[N + 1]; var uv = new Vector2[N + 1]; var t = new int[N * 3];
            v[0] = new Vector3(0, 0, 0.12f); uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < N; i++)
            {
                float a = i / (float)N * Mathf.PI * 2f;
                v[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                uv[i + 1] = new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f);
                t[i * 3] = 0; t[i * 3 + 1] = 1 + (i + 1) % N; t[i * 3 + 2] = 1 + i;
            }
            disc = new Mesh { name = "iris_disc", vertices = v, uv = uv, triangles = t };
            disc.RecalculateNormals();
            return disc;
        }

        static Mesh MeshOf(PrimitiveType p) => Gad.Prim.MeshOf(p);
    }
}
