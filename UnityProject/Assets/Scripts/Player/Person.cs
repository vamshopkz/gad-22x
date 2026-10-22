using System.Collections.Generic;
using UnityEngine;

namespace Gad
{
    /// <summary>
    /// A person in the world (player, other players, pedestrians, staff): the realistic human from
    /// <see cref="Human.Build"/> (smooth jointed body, face, hair, clothes) driven by
    /// <see cref="HumanAnimator"/>. Crowd people are merged into one mesh per bone so a street full of
    /// them stays cheap on phones.
    /// </summary>
    public class Person : MonoBehaviour
    {
        public SkinSet Look;
        public float Speed;
        public bool Grounded = true;
        /// <summary>0 normal, 1 working with the hands, 2 sitting, 3 waving, 4 driving, 5 typing at a desk, 6 dancing.</summary>
        public int Pose;
        public bool Full;
        public float Height = 1.78f;
        Wardrobe.Built built;
        HumanAnimator anim;
        float workT;

        public HumanRig Rig => built?.human;

        public static Person Create(Transform parent, SkinSet look, string name = "Person", bool full = false, float height = 1.78f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<Person>();
            p.Full = full;
            p.Height = height;
            p.Rebuild(look);
            return p;
        }

        public void Rebuild(SkinSet look)
        {
            Look = (look ?? new SkinSet()).Clone();
            if (built != null && built.root != null) Destroy(built.root);
            built = Human.Build(Look, Height, false);
            built.root.transform.SetParent(transform, false);
            built.root.transform.localPosition = Vector3.zero;
            built.root.transform.localRotation = Quaternion.identity;
            anim = built.humanAnim;
            foreach (var r in built.root.GetComponentsInChildren<Renderer>()) r.gameObject.layer = gameObject.layer;
            if (!Full) Merge();
        }

        /// <summary>One mesh per bone and material (fingers go with the hand, eyelids with the head).</summary>
        void Merge()
        {
            var rig = built.human;
            var joints = new HashSet<Transform>(rig.Joints);
            var groups = new Dictionary<(Transform, Material), List<CombineInstance>>();
            foreach (var mf in built.root.GetComponentsInChildren<MeshFilter>())
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mf.sharedMesh == null || !mr.enabled) continue;
                var j = mf.transform;
                while (j != null && !joints.Contains(j)) j = j.parent;
                if (j == null) continue;
                var mesh = mf.sharedMesh;
                for (int s = 0; s < mesh.subMeshCount && s < mr.sharedMaterials.Length; s++)
                {
                    var key = (j, mr.sharedMaterials[s]);
                    if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh = mesh, subMeshIndex = s, transform = j.worldToLocalMatrix * mf.transform.localToWorldMatrix });
                }
                mr.enabled = false;
            }
            foreach (var kv in groups)
            {
                var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                m.CombineMeshes(kv.Value.ToArray(), true, true);
                var go = new GameObject("merged");
                go.layer = gameObject.layer;
                go.transform.SetParent(kv.Key.Item1, false);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = kv.Key.Item2;
            }
        }

        void Update()
        {
            if (anim == null) return;
            string st;
            if (Pose == 2) st = "sit";
            else if (Pose == 4) st = "drive";
            else if (Pose == 5) st = "type";
            else if (Pose == 6) st = "dance";
            else if (!Grounded) st = "fall";
            else if (Speed > 4.5f) st = "sprint";
            else if (Speed > 0.25f) st = "walk";
            else if (Pose == 3) st = "wave";
            else if (Pose == 1)
            {
                workT -= Time.deltaTime;
                if (workT <= 0) { workT = 1.4f; anim.Play("interact-right", 0.15f, true); }
                return;
            }
            else st = "idle";
            anim.Play(st, 0.2f);
            if (st == "walk") anim.SetSpeed(Speed / 1.6f);
            else if (st == "sprint") anim.SetSpeed(Speed / 6.5f);
        }

        // ------------------------------------------------------------------ looks

        static List<string> Ids(Wardrobe.Slot s, System.Func<Wardrobe.SkinDef, bool> ok = null)
        {
            var l = new List<string>();
            foreach (var d in Wardrobe.OfSlot(s)) if (!d.none && (ok == null || ok(d))) l.Add(d.id);
            return l;
        }

        /// <summary>An everyday random outfit for city people.</summary>
        public static SkinSet Random(Rng r)
        {
            bool f = r.Chance(0.5f);
            string Pick(List<string> l) => l.Count == 0 ? null : l[r.I(l.Count)];
            var skins = new[] { "hs_light", "hs_peach", "hs_tan", "hs_olive", "hs_brown", "hs_dark" };
            var hair = Ids(Wardrobe.Slot.HHair, d => !d.id.Contains("blue") && !d.id.Contains("pink") && !d.id.Contains("mohawk") && (f ? !d.id.Contains("buzz") : !d.id.Contains("long") && !d.id.Contains("ponytail") && !d.id.Contains("bun")));
            var tops = Ids(Wardrobe.Slot.HTop, d => d.style == "tee" || d.style == "hoodie" || d.style == "flannel" || d.style == "shirt" || d.style == "blazer" || d.style == "tank" || (f && d.style == "dress"));
            var bottoms = Ids(Wardrobe.Slot.HBottom, d => d.style == "pants" || d.style == "cargo" || d.style == "joggers" || d.style == "shorts" || (f && d.style == "skirt"));
            var shoes = Ids(Wardrobe.Slot.HShoes, d => d.style == "sneakers" || d.style == "boots" || d.style == "loafers" || d.style == "hightop");
            var s = new SkinSet
            {
                hGender = f ? "hg_f" : "hg_m",
                hSkin = skins[r.I(skins.Length)],
                hHair = Pick(hair) ?? "hh_short_brown",
                hEyes = r.Chance(0.5f) ? "he_brown" : f ? "he_lashes" : "he_blue",
                hMouth = f ? (r.Chance(0.5f) ? "hm_lips" : "hm_smile") : (r.Chance(0.25f) ? "hm_beard" : r.Chance(0.5f) ? "hm_neutral" : "hm_smile"),
                hTop = Pick(tops) ?? "ht_tee_white",
                hBottom = Pick(bottoms) ?? "hb_jeans_blue",
                hShoes = Pick(shoes) ?? "hf_sneakers_white",
                hAcc = r.Chance(0.2f) ? (r.Chance(0.5f) ? "ha_sunglasses" : "ha_glasses") : "ha_none",
            };
            if (Wardrobe.Find(s.hTop)?.style == "dress") s.hBottom = "hb_none";
            return s;
        }

        /// <summary>Converts the first version's simple look (old saves) into an outfit.</summary>
        public static SkinSet FromOld(Look l)
        {
            var s = new SkinSet { hGender = l.gender == 1 ? "hg_f" : "hg_m" };
            s.hSkin = new[] { "hs_light", "hs_peach", "hs_tan", "hs_brown", "hs_dark" }[Mathf.Clamp(l.skin, 0, 4)];
            string hc = new[] { "black", "brown", "blonde", "blonde", "red", "white", "blue", "pink" }[Mathf.Clamp(l.hair, 0, 7)];
            string hs = l.hairStyle switch { 1 => "long", 2 => null, 3 => "bun", _ => "short" };
            s.hHair = hs == null ? "hh_none" : $"hh_{hs}_{hc}";
            s.hTop = l.top switch { 1 => "ht_flannel_red", 2 => "ht_hoodie_black", 3 => "ht_suit_navy", _ => "ht_tee_white" };
            if (Wardrobe.Find(s.hTop) == null) s.hTop = "ht_tee_white";
            return s;
        }
    }
}
