using System.Collections.Generic;
using UnityEngine;

namespace Gad
{
    /// <summary>
    /// Real CC0 assets (Poly Haven): photo materials with normal maps, composed facade sets and
    /// furniture models. Everything is loaded from Resources on demand and cached.
    /// </summary>
    public static class Photo
    {
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static Material litN, facadeBase;

        /// <summary>A photo material (Resources/PH/name_diff + name_nor).</summary>
        public static Material Mat(string name, Color? tint = null, float smooth = 0.25f, float bump = 1f)
        {
            var c = tint ?? Color.white;
            string key = name + ColorUtility.ToHtmlStringRGBA(c) + smooth.ToString("0.00");
            if (mats.TryGetValue(key, out var m) && m != null) return m;
            if (litN == null) litN = Resources.Load<Material>("Materials/LitNormal");
            var diff = Resources.Load<Texture2D>("PH/" + name + "_diff");
            var nor = Resources.Load<Texture2D>("PH/" + name + "_nor");
            m = new Material(litN) { enableInstancing = true, name = "Photo " + name };
            m.SetColor("_BaseColor", c);
            if (diff != null) m.SetTexture("_BaseMap", diff);
            if (nor != null) { m.SetTexture("_BumpMap", nor); m.SetFloat("_BumpScale", bump); }
            m.SetFloat("_Smoothness", smooth);
            mats[key] = m;
            return m;
        }

        /// <summary>A facade set (Resources/Facades/f{style}_a/_n/_m/_e): windows with depth, glass gloss, lit rooms at night.</summary>
        public static Material Facade(int style)
        {
            string key = "facade" + style;
            if (mats.TryGetValue(key, out var m) && m != null) return m;
            if (facadeBase == null) facadeBase = Resources.Load<Material>("Materials/LitNormalEmissive");
            string p = "Facades/f" + style;
            var a = Resources.Load<Texture2D>(p + "_a");
            if (a == null || facadeBase == null) return null;
            m = new Material(facadeBase) { name = "Facade " + style, enableInstancing = false };
            m.SetTexture("_BaseMap", a);
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BumpMap", Resources.Load<Texture2D>(p + "_n"));
            m.SetFloat("_BumpScale", 1f);
            m.SetFloat("_Smoothness", style == 0 || style == 7 ? 0.82f : 0.42f);
            m.SetFloat("_Metallic", style == 0 || style == 7 ? 0.35f : 0f);
            m.SetTexture("_EmissionMap", Resources.Load<Texture2D>(p + "_e"));
            m.SetColor("_EmissionColor", Color.black);
            m.EnableKeyword("_NORMALMAP"); m.EnableKeyword("_EMISSION");
            mats[key] = m;
            return m;
        }
    }

    /// <summary>Furniture and props from Resources/Models/&lt;name&gt;/&lt;name&gt;.fbx with their photo textures.</summary>
    public static class ModelLib
    {
        class Entry { public GameObject prefab; public Material mat; public float scale = 1f; public Bounds bounds; }
        static readonly Dictionary<string, Entry> cache = new Dictionary<string, Entry>();
        static Material lit;

        static Entry Get(string name)
        {
            if (cache.TryGetValue(name, out var e)) return e;
            e = new Entry();
            e.prefab = Resources.Load<GameObject>("Models/" + name + "/" + name);
            if (e.prefab != null)
            {
                if (lit == null) lit = Resources.Load<Material>("Materials/Lit");
                var diff = Resources.Load<Texture2D>("Models/" + name + "/" + name + "_diff_1k");
                e.mat = new Material(lit) { name = "Model " + name, enableInstancing = true };
                if (diff != null) e.mat.SetTexture("_BaseMap", diff);
                e.mat.SetFloat("_Smoothness", 0.3f);
                // measure once
                var probe = Object.Instantiate(e.prefab);
                var rs = probe.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    var bb = rs[0].bounds;
                    foreach (var r in rs) bb.Encapsulate(r.bounds);
                    e.bounds = bb;
                    // some FBX files come in centimetres
                    if (bb.size.magnitude > 30f) { e.scale = 0.01f; e.bounds = new Bounds(bb.center * 0.01f, bb.size * 0.01f); }
                }
                Object.DestroyImmediate(probe);
            }
            cache[name] = e;
            return e;
        }

        public static bool Has(string name) => Get(name).prefab != null;

        /// <summary>
        /// Places a model standing on the floor at local position pos (container space), turned by yaw.
        /// fit &gt; 0 scales it so its longest horizontal side is that many metres. Adds a box collider.
        /// </summary>
        public static GameObject Place(Transform parent, string name, Vector3 pos, float yaw, float fit = 0, bool collide = true, Color? tint = null)
        {
            var e = Get(name);
            if (e.prefab == null) return null;
            var go = Object.Instantiate(e.prefab, parent, false);
            go.name = name;
            float s = e.scale;
            float longest = Mathf.Max(e.bounds.size.x, e.bounds.size.z);
            if (fit > 0 && longest > 0.01f) s *= fit / longest;
            go.transform.localScale = e.prefab.transform.localScale * s;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0) * e.prefab.transform.localRotation;
            // put the bottom on the floor and centre it
            var b = e.bounds;
            float k = fit > 0 && longest > 0.01f ? fit / longest : 1f;
            var off = new Vector3(b.center.x, b.min.y, b.center.z) * k;
            go.transform.localPosition = pos - Quaternion.Euler(0, yaw, 0) * off;
            var mat = e.mat;
            if (tint.HasValue)
            {
                mat = new Material(e.mat) { color = tint.Value };
            }
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var arr = new Material[r.sharedMaterials.Length == 0 ? 1 : r.sharedMaterials.Length];
                for (int i = 0; i < arr.Length; i++) arr[i] = mat;
                r.sharedMaterials = arr;
                r.gameObject.layer = parent != null ? parent.gameObject.layer : 0;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            if (collide)
            {
                var col = new GameObject("Col") { layer = 12 };
                col.transform.SetParent(go.transform, false);
                var bc = col.AddComponent<BoxCollider>();
                // bounds were measured with the prefab's own rotation: undo it for the local box
                var inv = Quaternion.Inverse(e.prefab.transform.localRotation);
                var ps = e.prefab.transform.localScale;
                var sz = inv * (b.size / e.scale); sz = new Vector3(sz.x / ps.x, sz.y / ps.y, sz.z / ps.z);
                var cc = inv * (b.center / e.scale); bc.center = new Vector3(cc.x / ps.x, cc.y / ps.y, cc.z / ps.z);
                bc.size = new Vector3(Mathf.Abs(sz.x), Mathf.Abs(sz.y), Mathf.Abs(sz.z));
            }
            return go;
        }
    }
}
