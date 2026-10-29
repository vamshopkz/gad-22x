using System.Collections.Generic;
using UnityEngine;

namespace Gad
{
    /// <summary>
    /// Every building is entered for real: its ground floor is built inside the building itself while
    /// the player is near, and you walk in through the doors — no jump to a separate world. Shops,
    /// cafés and the mall have a glass shop front (you see in from the street); other buildings keep
    /// their walls, and their windows inside are real glass onto the street. Buildings with a lift
    /// (flats, towers, offices, hotels) build each floor at its real height inside the building.
    /// (0.8: shops; 1.0: every building.)
    /// </summary>
    public class InPlace : MonoBehaviour
    {
        public static InPlace I;
        const float Near = 30f, Far = 46f;
        const int MaxBuilt = 4;     // interiors (with their people and lamps) kept around the player at once
        public const float Ground = 4.4f;     // height of the ground floor (the mall: 8 m)
        public static float GroundOf(Bld b) => b.type == BType.Mall ? 8f : HasLiftType(b) ? 4.0f : Mathf.Max(Ground, Mathf.Min(b.height - 0.2f, RoomHeight(b) + 0.4f));
        static bool HasLiftType(Bld b) => Interiors.HasLift(b);

        class Inst { public Bld b; public GameObject root; public List<ActionPoint> points; }
        readonly Dictionary<int, Inst> built = new Dictionary<int, Inst>();
        readonly List<Bld> shops = new List<Bld>();
        Inst current;
        float scanT;

        /// <summary>The ground-floor room of each kind of building (as Interiors builds it).</summary>
        static Vector2 RoomSize(Bld b) => b.type switch
        {
            BType.Shop => new Vector2(14, 12),
            BType.Mall => new Vector2(40, 32),
            BType.Supermarket => new Vector2(26, 22),
            BType.GasStation => new Vector2(14, 12),
            BType.Cafe => new Vector2(14, 12),
            BType.Restaurant => new Vector2(20, 16),
            BType.Clothes => new Vector2(18, 15),
            BType.Furniture => new Vector2(18, 14),
            BType.Gym => new Vector2(20, 16),
            BType.GunShop => new Vector2(16, 14),
            BType.Apartment or BType.Tower => new Vector2(14, 12),
            BType.Hotel => new Vector2(18, 14),
            BType.Agency => new Vector2(30, 22),
            BType.Office or BType.Corp => new Vector2(22, 16),
            BType.Hospital => new Vector2(24, 18),
            BType.FireStation => new Vector2(22, 18),
            BType.School or BType.University => new Vector2(20, 16),
            BType.Library => new Vector2(20, 16),
            BType.Bank => new Vector2(18, 14),
            BType.JobCenter => new Vector2(16, 12),
            BType.CarDealer => new Vector2(30, 24),
            BType.Garage => new Vector2(20, 16),
            BType.Factory or BType.Warehouse => new Vector2(30, 24),
            BType.Station or BType.Port or BType.Airport => new Vector2(34, 22),
            _ => Vector2.zero,
        };

        static float RoomHeight(Bld b) => b.type switch
        {
            BType.Mall => 7f, BType.CarDealer or BType.FireStation => 6f, BType.Garage => 5.5f, BType.Warehouse or BType.Factory => 8f,
            BType.Station or BType.Port => 7f, BType.Airport => 9f, BType.Agency => 7f, BType.Bank => 5f, BType.Library or BType.Gym or BType.Hotel => 4.5f,
            _ => 4f,
        };

        /// <summary>How much the room must shrink to fit inside the building (1 = not at all).</summary>
        public static float Fit(Bld b)
        {
            var rs = RoomSize(b);
            if (rs == Vector2.zero || HouseWorld.IsReal(b)) return 0f;
            float fw = b.front <= 1 ? b.rect.width : b.rect.height, fd = b.front <= 1 ? b.rect.height : b.rect.width;
            return Mathf.Min(1f, (fw - 0.8f) / (rs.x + 0.6f), (fd - 0.95f) / (rs.y + 0.3f));
        }

        public static bool Eligible(Bld b) => b != null && b.type != BType.Parking && Fit(b) >= 0.8f;

        /// <summary>Shops, cafés, the mall: a glass front instead of a wall.</summary>
        public static bool Storefront(Bld b) => b.type == BType.Shop || b.type == BType.Cafe || b.type == BType.Restaurant || b.type == BType.Supermarket || b.type == BType.Clothes
                                                || b.type == BType.Furniture || b.type == BType.Gym || b.type == BType.GunShop || b.type == BType.Mall;

        public static void Create()
        {
            I = new GameObject("InPlace").AddComponent<InPlace>();
            foreach (var b in City.All) if (Eligible(b)) I.shops.Add(b);
            Debug.Log($"[InPlace] {I.shops.Count} buildings walk-in");
        }

        /// <summary>Where the ground floor stands: at the door, facing in (walled buildings a little inside so the wall stays in).</summary>
        public static void Place(Bld b, out Vector3 pos, out Quaternion rot)
        {
            pos = new Vector3(b.door.x, City.Curb + 0.04f, b.door.z) - b.FrontDir * (Storefront(b) ? 0f : 0.35f);   // just above the ground of the lot
            rot = Quaternion.LookRotation(-b.FrontDir);
        }

        void Update()
        {
            var pl = PlayerCtl.I;
            if (pl == null) return;
            var p = pl.transform.position;
            // inside or outside the current building?
            if (current != null)
            {
                if (current.root == null || !StillInside(current, p)) Leave();
            }
            else if (pl.Inside < 0 && pl.Car == null)
            {
                foreach (var kv in built)
                    if (kv.Value.root != null && Inside(kv.Value.b, p)) { Arrive(kv.Value); break; }
            }
            scanT -= Time.deltaTime;
            if (scanT > 0f) return;
            scanT = 0.5f;
            // real lamps only in the building you are in or right at: every lamp also lights the big city meshes round it
            foreach (var kv in built)
            {
                var inst = kv.Value;
                if (inst.root == null) continue;
                bool on = inst == current || Vector2.Distance(new Vector2(p.x, p.z), inst.b.rect.center) < Mathf.Max(inst.b.rect.width, inst.b.rect.height) * 0.5f + 6f;
                foreach (var l in inst.root.GetComponentsInChildren<Light>(true)) if (l.enabled != on) l.enabled = on;
                // the staff and visitors too: they come to life when you come in
                foreach (var pr in inst.root.GetComponentsInChildren<Person>(true)) if (pr.gameObject.activeSelf != on) pr.gameObject.SetActive(on);
            }
            bool outdoors = p.y > -50f;
            // nearest first
            shops.Sort((a, c) => (a.rect.center - new Vector2(p.x, p.z)).sqrMagnitude.CompareTo((c.rect.center - new Vector2(p.x, p.z)).sqrMagnitude));
            int count = 0;
            foreach (var b in shops)
            {
                float d = Vector2.Distance(new Vector2(p.x, p.z), b.rect.center);
                bool have = built.TryGetValue(b.id, out var inst) && inst.root != null;
                if (have) count++;
                if (have && count > MaxBuilt && inst != current) { Drop(inst); built.Remove(b.id); continue; }
                if (!have && count < MaxBuilt && outdoors && d < Near + Mathf.Max(b.rect.width, b.rect.height) * 0.5f)
                {
                    built[b.id] = Build(b);
                    return;   // one per scan: no hitch
                }
                if (have && (d > Far + Mathf.Max(b.rect.width, b.rect.height) * 0.5f || !outdoors) && inst != current) { Drop(inst); built.Remove(b.id); }
            }
        }

        /// <summary>Remove a built interior (never with the player inside it: the lift carries the player as its child).</summary>
        static void Drop(Inst inst)
        {
            var pl = PlayerCtl.I;
            if (pl != null && inst.root != null && pl.transform.IsChildOf(inst.root.transform)) pl.transform.SetParent(null, true);
            Destroy(inst.root);
        }

        static bool Inside(Bld b, Vector3 p)
        {
            var r = b.rect;
            return p.x > r.xMin + 0.2f && p.x < r.xMax - 0.2f && p.z > r.yMin + 0.2f && p.z < r.yMax - 0.2f && p.y < City.Curb + GroundOf(b) && p.y > City.Curb - 2f;
        }

        /// <summary>On an upper floor the corridors may reach beyond the outline: stay "inside" until you are far off or back down.</summary>
        static bool StillInside(Inst inst, Vector3 p)
        {
            // in the lift or on the ground floor: inside while within the walls at any height
            var r = inst.b.rect;
            bool within = p.x > r.xMin + 0.2f && p.x < r.xMax - 0.2f && p.z > r.yMin + 0.2f && p.z < r.yMax - 0.2f && p.y > City.Curb - 2f;
            if (Interiors.Floor == 0 && (Elevator.I == null || !Elevator.I.Riding)) return Interiors.HasLift(inst.b) ? within : Inside(inst.b, p);
            if (within) return true;
            return Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(inst.b.rect.center.x, 0, inst.b.rect.center.y)) < 70f && p.y > City.Curb + 1f;
        }

        Inst Build(Bld b)
        {
            Place(b, out var pos, out var rot);
            var root = Interiors.BuildInPlace(b, pos, rot, Fit(b), Storefront(b), out var points);
            root.transform.SetParent(transform, true);
            return new Inst { b = b, root = root, points = points };
        }

        void Arrive(Inst inst)
        {
            current = inst;
            Interiors.UseInPlace(inst.b, inst.root.transform, inst.points);
            PlayerCtl.I.Inside = inst.b.id;
            WorldBuilder.SetBuildingWalls(inst.b, true);
            Hud.Toast(inst.b.name);
        }

        void Leave()
        {
            if (current == null) return;
            var b = current.b;
            current = null;
            Interiors.LeaveInPlace();
            WorldBuilder.SetBuildingWalls(b, true);
            if (PlayerCtl.I != null) PlayerCtl.I.Inside = -1;
        }

        /// <summary>Go straight in (taxi, tests, menus): build it if needed and stand inside the door.</summary>
        public static void EnterNow(Bld b)
        {
            if (I == null) return;
            if (I.current != null) I.Leave();
            if (!I.built.TryGetValue(b.id, out var inst) || inst.root == null) I.built[b.id] = inst = I.Build(b);
            var pl = PlayerCtl.I;
            if (pl.Car != null) pl.ExitCarInstant();
            pl.Teleport(b.door - b.FrontDir * 2.6f + Vector3.up * 0.15f, Quaternion.LookRotation(-b.FrontDir).eulerAngles.y);
            I.Arrive(inst);
        }

        /// <summary>Step out of the current building onto the street.</summary>
        public static void ExitNow()
        {
            if (I == null || I.current == null) return;
            var b = I.current.b;
            I.Leave();
            PlayerCtl.I.Teleport(b.ExitPoint, Quaternion.LookRotation(b.FrontDir).eulerAngles.y);
        }

        public static bool Active => I != null && I.current != null;
        public static Bld Current => I != null && I.current != null ? I.current.b : null;

        /// <summary>The upper floors may stick out of the building: while on them its outer walls let you through.</summary>
        public static void FloorChanged(int floor)
        {
            if (I == null || I.current == null) return;
            WorldBuilder.SetBuildingWalls(I.current.b, floor == 0);
        }

        /// <summary>Inside on the ground floor the camera stays inside the walls (outside hang awnings and signs).</summary>
        public static float ClampCam(Vector3 target, Vector3 back, float d)
        {
            if (!Active || Interiors.Floor != 0) return d;
            var r = I.current.b.rect;
            if (!r.Contains(new Vector2(target.x, target.z))) return d;
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
