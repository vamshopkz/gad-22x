using System.Collections.Generic;
using UnityEngine;

namespace Gad
{
    /// <summary>
    /// 0.8: shops, cafés, restaurants, gyms... are built inside their own building, behind a real glass
    /// shop front: you see the shop from the street and simply walk in through the sliding doors —
    /// no jump to a separate interior. The interiors of the nearby shops are built while the player is
    /// close; walking in makes that shop the current interior (its counters and actions work).
    /// </summary>
    public class InPlace : MonoBehaviour
    {
        public static InPlace I;
        const float Near = 42f, Far = 60f;
        public const float Ground = 4.4f;     // height of the glazed ground floor (the mall: 8 m)
        public static float GroundOf(Bld b) => b.type == BType.Mall ? 8f : Ground;

        class Inst { public Bld b; public GameObject root; public List<ActionPoint> points; }
        readonly Dictionary<int, Inst> built = new Dictionary<int, Inst>();
        readonly List<Bld> shops = new List<Bld>();
        Inst current;
        float scanT;

        /// <summary>The room of each kind of shop (as Interiors builds it), to see if it fits the building.</summary>
        static Vector2 RoomSize(Bld b) => b.type switch
        {
            BType.Shop => new Vector2(14, 12),
            BType.Mall => new Vector2(40, 32),
            BType.Supermarket => new Vector2(26, 22),
            BType.Cafe => new Vector2(14, 12),
            BType.Restaurant => new Vector2(20, 16),
            BType.Clothes => new Vector2(18, 15),
            BType.Furniture => new Vector2(18, 14),
            BType.Gym => new Vector2(20, 16),
            BType.GunShop => new Vector2(16, 14),
            _ => Vector2.zero,
        };

        /// <summary>How much the shop's room must shrink to fit inside the building (1 = not at all).</summary>
        public static float Fit(Bld b)
        {
            var rs = RoomSize(b);
            if (rs == Vector2.zero || b.cx < 0) return 0f;
            float fw = b.front <= 1 ? b.rect.width : b.rect.height, fd = b.front <= 1 ? b.rect.height : b.rect.width;
            return Mathf.Min(1f, (fw - 0.8f) / (rs.x + 0.6f), (fd - 0.6f) / (rs.y + 0.3f));
        }

        public static bool Eligible(Bld b) => b != null && Fit(b) >= 0.72f;

        public static void Create()
        {
            I = new GameObject("InPlace").AddComponent<InPlace>();
            foreach (var b in City.All) if (Eligible(b)) I.shops.Add(b);
            Debug.Log($"[InPlace] {I.shops.Count} shops walk-in");
        }

        public static void Place(Bld b, out Vector3 pos, out Quaternion rot)
        {
            pos = new Vector3(b.door.x, City.Curb, b.door.z);
            rot = Quaternion.LookRotation(-b.FrontDir);
        }

        void Update()
        {
            var pl = PlayerCtl.I;
            if (pl == null) return;
            var p = pl.transform.position;
            // inside or outside the current shop?
            if (current != null)
            {
                if (!Inside(current.b, p) || current.root == null) Leave();
            }
            else if (pl.Inside < 0 && pl.Car == null)
            {
                foreach (var kv in built)
                    if (kv.Value.root != null && Inside(kv.Value.b, p)) { Arrive(kv.Value); break; }
            }
            scanT -= Time.deltaTime;
            if (scanT > 0f) return;
            scanT = 0.5f;
            bool outdoors = p.y > -50f;
            // nearest shops first
            shops.Sort((a, c) => (a.rect.center - new Vector2(p.x, p.z)).sqrMagnitude.CompareTo((c.rect.center - new Vector2(p.x, p.z)).sqrMagnitude));
            foreach (var b in shops)
            {
                float d = Vector2.Distance(new Vector2(p.x, p.z), b.rect.center);
                bool have = built.TryGetValue(b.id, out var inst) && inst.root != null;
                if (!have && outdoors && d < Near && (pl.Inside < 0 || current != null))
                {
                    built[b.id] = Build(b);
                    return;   // one per scan: no hitch
                }
                if (have && (d > Far || !outdoors) && inst != current) { Destroy(inst.root); built.Remove(b.id); }
            }
        }

        static bool Inside(Bld b, Vector3 p)
        {
            var r = b.rect;
            return p.x > r.xMin + 0.2f && p.x < r.xMax - 0.2f && p.z > r.yMin + 0.2f && p.z < r.yMax - 0.2f && p.y < City.Curb + GroundOf(b);
        }

        Inst Build(Bld b)
        {
            Place(b, out var pos, out var rot);
            var root = Interiors.BuildInPlace(b, pos, rot, Fit(b), out var points);
            root.transform.SetParent(transform, true);
            return new Inst { b = b, root = root, points = points };
        }

        void Arrive(Inst inst)
        {
            current = inst;
            Interiors.UseInPlace(inst.b, inst.points);
            PlayerCtl.I.Inside = inst.b.id;
            Hud.Toast(inst.b.name);
        }

        void Leave()
        {
            if (current == null) return;
            current = null;
            Interiors.LeaveInPlace();
            if (PlayerCtl.I != null) PlayerCtl.I.Inside = -1;
        }

        /// <summary>Go straight into a shop (taxi, tests, menus): build it if needed and stand inside the door.</summary>
        public static void EnterNow(Bld b)
        {
            if (I == null) return;
            if (!I.built.TryGetValue(b.id, out var inst) || inst.root == null) I.built[b.id] = inst = I.Build(b);
            var pl = PlayerCtl.I;
            if (pl.Car != null) pl.ExitCarInstant();
            pl.Teleport(b.door - b.FrontDir * 2.4f + Vector3.up * 0.15f, Quaternion.LookRotation(-b.FrontDir).eulerAngles.y);
            I.Arrive(inst);
        }

        /// <summary>Step out of the current shop onto the street.</summary>
        public static void ExitNow()
        {
            if (I == null || I.current == null) return;
            var b = I.current.b;
            I.Leave();
            PlayerCtl.I.Teleport(b.ExitPoint, Quaternion.LookRotation(b.FrontDir).eulerAngles.y);
        }

        public static bool Active => I != null && I.current != null;

        /// <summary>Inside a shop the camera stays inside its walls (outside the door hang awnings and signs).</summary>
        public static float ClampCam(Vector3 target, Vector3 back, float d)
        {
            if (!Active) return d;
            var r = I.current.b.rect;
            float lo = 0.6f;
            for (float t = 0.6f; t <= d; t += 0.1f)
            {
                var c = target + back * t;
                if (c.x < r.xMin + 0.5f || c.x > r.xMax - 0.5f || c.z < r.yMin + 0.5f || c.z > r.yMax - 0.5f || c.y > City.Curb + GroundOf(I.current.b) - 0.4f) break;
                lo = t;
            }
            return lo;
        }
    }
}
