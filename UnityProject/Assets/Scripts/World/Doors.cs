using System.Collections;
using TMPro;
using UnityEngine;

namespace Gad
{
    /// <summary>
    /// Sliding glass entrance of a building: opens by itself when you walk up, and you go in by
    /// walking through it (no button).
    /// </summary>
    public class AutoDoor : MonoBehaviour
    {
        public Bld B;
        Transform l, r;
        float open, w;
        static float cooldown;

        public static AutoDoor Make(Transform parent, Bld b)
        {
            var go = new GameObject("AutoDoor " + b.name);
            go.transform.SetParent(parent, false);
            var dir = b.FrontDir;
            go.transform.SetPositionAndRotation(b.door + dir * 0.16f, Quaternion.LookRotation(dir));
            var d = go.AddComponent<AutoDoor>();
            d.B = b;
            d.w = b.type == BType.Mall || b.type == BType.Airport || b.type == BType.Station || b.type == BType.Agency ? 2.2f : 1.2f;
            bool house = b.type == BType.House || b.type == BType.Villa;
            var glass = house ? WorldBuilder.Wood : Mats.Glass(new Color(0.55f, 0.75f, 0.85f, 0.35f));
            float hh = b.type == BType.House ? 2.3f : 2.8f;
            d.l = Leaf(go.transform, glass, new Vector3(-d.w / 2, hh / 2, 0), new Vector3(d.w, hh, 0.05f), house);
            d.r = Leaf(go.transform, glass, new Vector3(d.w / 2, hh / 2, 0), new Vector3(d.w, hh, 0.05f), house);
            return d;
        }

        static Transform Leaf(Transform p, Material m, Vector3 pos, Vector3 size, bool wood)
        {
            var t = new GameObject("Leaf").transform;
            t.SetParent(p, false);
            t.localPosition = pos;
            Prim.Box(t, Vector3.zero, size, m);
            if (!wood)
            {
                Prim.Box(t, new Vector3(0, 0, 0), new Vector3(size.x, 0.06f, 0.07f), WorldBuilder.Metal).transform.localPosition = new Vector3(0, size.y / 2 - 0.03f, 0);
                Prim.Box(t, new Vector3(0, -size.y / 2 + 0.05f, 0), new Vector3(size.x, 0.1f, 0.07f), WorldBuilder.Metal);
                Prim.Box(t, new Vector3(Mathf.Sign(pos.x) * -(size.x / 2 - 0.12f), 0, 0.05f), new Vector3(0.04f, 0.8f, 0.04f), WorldBuilder.Metal);
            }
            return t;
        }

        void Update()
        {
            var pl = PlayerCtl.I;
            if (pl == null) return;
            var p = pl.transform.position;
            var to = p - B.door; to.y = 0;
            float dist = to.magnitude;
            bool walker = pl.Car == null && pl.Inside < 0;
            bool near = walker && dist < 4.5f;
            foreach (var rp in WorldNet.Remotes.Values) if (rp != null && rp.InsideCode < 0 && (rp.transform.position - B.door).sqrMagnitude < 9f) near = true;
            open = Mathf.MoveTowards(open, near ? 1 : 0, Time.deltaTime * 2.2f);
            float s = Mathf.SmoothStep(0, 1, open) * w * 0.95f;
            l.localPosition = new Vector3(-w / 2 - s, l.localPosition.y, 0);
            r.localPosition = new Vector3(w / 2 + s, r.localPosition.y, 0);

            // walking into the open doorway takes you in
            if (walker && open > 0.75f && dist < 1.25f && !pl.Frozen && Time.unscaledTime > cooldown)
            {
                var v = pl.CC.velocity; v.y = 0;
                var inward = -B.FrontDir;
                bool pushing = Vector3.Dot(pl.MoveInput.sqrMagnitude > 0.01f ? Quaternion.Euler(0, pl.Yaw, 0) * new Vector3(pl.MoveInput.x, 0, pl.MoveInput.y) : v, inward) > 0.3f;
                if (pushing)
                {
                    cooldown = Time.unscaledTime + 2f;
                    pl.Frozen = true;
                    Hud.FadeThrough(() => { pl.Frozen = false; Interiors.Enter(B); }, 4f);
                }
            }
        }
    }

    /// <summary>The lobby's glass doors from inside: walk out through them to leave the building.</summary>
    public class InteriorExit : MonoBehaviour
    {
        Transform l, r;
        float open;
        static float cooldown;

        public static void Make(Transform level)
        {
            var go = new GameObject("ExitDoor");
            go.transform.SetParent(level, false);
            go.transform.localPosition = new Vector3(0, 0, -0.15f);
            var d = go.AddComponent<InteriorExit>();
            var glass = Mats.Glass(new Color(0.55f, 0.75f, 0.85f, 0.35f));
            d.l = new GameObject("L").transform; d.l.SetParent(go.transform, false);
            d.r = new GameObject("R").transform; d.r.SetParent(go.transform, false);
            Prim.Box(d.l, new Vector3(0, 1.35f, 0), new Vector3(1.4f, 2.7f, 0.05f), glass);
            Prim.Box(d.r, new Vector3(0, 1.35f, 0), new Vector3(1.4f, 2.7f, 0.05f), glass);
            cooldown = Time.unscaledTime + 1.5f;
        }

        void Update()
        {
            var pl = PlayerCtl.I;
            if (pl == null || pl.Inside < 0) return;
            var lp = transform.parent.InverseTransformPoint(pl.transform.position);
            bool near = lp.z < 3.2f && Mathf.Abs(lp.x) < 2.5f && Mathf.Abs(lp.y) < 1.5f;
            open = Mathf.MoveTowards(open, near ? 1 : 0, Time.deltaTime * 2.2f);
            float s = Mathf.SmoothStep(0, 1, open) * 1.35f;
            l.localPosition = new Vector3(-0.7f - s, 0, 0);
            r.localPosition = new Vector3(0.7f + s, 0, 0);
            if (open > 0.75f && lp.z < 0.9f && Mathf.Abs(lp.x) < 1.3f && !pl.Frozen && Time.unscaledTime > cooldown)
            {
                var dir = Quaternion.Euler(0, pl.Yaw, 0) * new Vector3(pl.MoveInput.x, 0, pl.MoveInput.y);
                var back = -transform.parent.forward;
                if (Vector3.Dot(dir, back) > 0.3f || Vector3.Dot(pl.CC.velocity, back) > 0.5f)
                {
                    cooldown = Time.unscaledTime + 2f;
                    pl.Frozen = true;
                    Hud.FadeThrough(() => { pl.Frozen = false; Interiors.Exit(); }, 4f);
                }
            }
        }
    }

    /// <summary>A flat's front door: swings open for its owner (or for a viewing if it is for sale).</summary>
    public class FlatDoor : MonoBehaviour
    {
        public bool Allowed;
        float open;
        Transform hinge;

        public static FlatDoor Make(Transform parent, Vector3 local, bool allowed, Color col)
        {
            var go = new GameObject("FlatDoor");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var d = go.AddComponent<FlatDoor>();
            d.Allowed = allowed;
            d.hinge = new GameObject("Hinge").transform;
            d.hinge.SetParent(go.transform, false);
            d.hinge.localPosition = new Vector3(-0.7f, 0, 0);
            var leaf = Prim.Box(d.hinge, new Vector3(0.7f, 1.15f, 0), new Vector3(1.38f, 2.3f, 0.06f), col);
            leaf.layer = 10;
            leaf.AddComponent<BoxCollider>();
            Prim.Box(d.hinge, new Vector3(1.22f, 1.05f, 0.06f), new Vector3(0.14f, 0.03f, 0.05f), WorldBuilder.Metal);
            Prim.Box(d.hinge, new Vector3(1.22f, 1.05f, -0.06f), new Vector3(0.14f, 0.03f, 0.05f), WorldBuilder.Metal);
            return d;
        }

        void Update()
        {
            var pl = PlayerCtl.I;
            if (pl == null) return;
            float dist = Vector3.Distance(pl.transform.position, transform.position + transform.up);
            bool near = Allowed && dist < 2.6f;
            open = Mathf.MoveTowards(open, near ? 1 : 0, Time.deltaTime * 2f);
            hinge.localRotation = Quaternion.Euler(0, -95f * Mathf.SmoothStep(0, 1, open), 0);
        }
    }

    /// <summary>
    /// A lift: a real cabin with sliding doors. Walk in, choose a floor on its panel, the doors close
    /// and you ride up or down (the floor counter changes), then the doors open on the new floor.
    /// </summary>
    public class Elevator : MonoBehaviour
    {
        public static Elevator I;
        public Bld B;
        public int Floor;
        public bool Riding;
        public const float FH = 3.6f;
        Transform l, r;
        TextMeshPro inside, outside;
        float open;

        public bool PlayerInside
        {
            get
            {
                var pl = PlayerCtl.I;
                if (pl == null) return false;
                var lp = transform.InverseTransformPoint(pl.transform.position);
                return Mathf.Abs(lp.x) < 1.0f && lp.z > -0.9f && lp.z < 1.05f && Mathf.Abs(lp.y) < 1.5f;
            }
        }

        public static Elevator Make(Transform root, Bld b, int floor, Vector3 local)
        {
            var go = new GameObject("Elevator");
            go.transform.SetParent(root, false);
            go.transform.localPosition = local + Vector3.up * floor * FH;
            var e = go.AddComponent<Elevator>();
            I = e;
            e.B = b; e.Floor = floor;
            var t = go.transform;
            var wall = Mats.Lit(new Color(0.72f, 0.74f, 0.78f), null, 0.75f, 0.9f);
            var dark = Mats.Lit(new Color(0.12f, 0.12f, 0.14f), null, 0.6f, 0.3f);
            void Solid(Vector3 c, Vector3 s, Material m)
            {
                var g = Prim.Box(t, c, s, m);
                g.layer = 10;
                g.AddComponent<BoxCollider>();
            }
            Prim.Box(t, new Vector3(0, -0.05f, 0), new Vector3(2.3f, 0.1f, 2.3f), Mats.Tex(Tex.Tiles("liftfloor", new Color(0.25f, 0.25f, 0.27f), new Color(0.15f, 0.15f, 0.16f), 2), Color.white, 0.7f));
            Solid(new Vector3(0, 1.4f, 1.12f), new Vector3(2.3f, 2.8f, 0.08f), wall);
            Solid(new Vector3(-1.12f, 1.4f, 0), new Vector3(0.08f, 2.8f, 2.3f), wall);
            Solid(new Vector3(1.12f, 1.4f, 0), new Vector3(0.08f, 2.8f, 2.3f), wall);
            Prim.Box(t, new Vector3(0, 2.82f, 0), new Vector3(2.3f, 0.06f, 2.3f), wall);
            Prim.Box(t, new Vector3(0, 2.78f, 0), new Vector3(1.4f, 0.02f, 1.4f), Mats.Glow(new Color(1f, 0.97f, 0.9f)));
            Prim.Box(t, new Vector3(0, 1.5f, 1.07f), new Vector3(1.6f, 1.6f, 0.02f), Mats.Lit(new Color(0.8f, 0.85f, 0.9f), null, 0.98f, 1f));   // mirror
            Prim.Box(t, new Vector3(0, 0.95f, 1.0f), new Vector3(1.9f, 0.04f, 0.05f), WorldBuilder.Metal);                                         // handrail
            // front frame and the panel
            Solid(new Vector3(-0.95f, 1.4f, -1.12f), new Vector3(0.42f, 2.8f, 0.1f), dark);
            Solid(new Vector3(0.95f, 1.4f, -1.12f), new Vector3(0.42f, 2.8f, 0.1f), dark);
            Prim.Box(t, new Vector3(0, 2.6f, -1.12f), new Vector3(1.5f, 0.4f, 0.1f), dark);
            Prim.Box(t, new Vector3(1.06f, 1.3f, -0.4f), new Vector3(0.02f, 0.5f, 0.25f), dark);
            for (int k = 0; k < 6; k++) Prim.Box(t, new Vector3(1.045f, 1.1f + (k / 2) * 0.13f, -0.47f + (k % 2) * 0.14f), new Vector3(0.02f, 0.07f, 0.07f), Mats.Glow(new Color(0.4f, 0.8f, 1f)));
            var lg = new GameObject("Light"); lg.transform.SetParent(t, false); lg.transform.localPosition = new Vector3(0, 2.4f, 0);
            var light = lg.AddComponent<Light>(); light.type = LightType.Point; light.range = 4; light.intensity = 1.2f; light.shadows = LightShadows.None;
            e.l = new GameObject("DoorL").transform; e.l.SetParent(t, false);
            e.r = new GameObject("DoorR").transform; e.r.SetParent(t, false);
            foreach (var leaf in new[] { e.l, e.r })
            {
                var g = Prim.Box(leaf, new Vector3(0, 1.25f, 0), new Vector3(0.74f, 2.5f, 0.04f), Mats.Lit(new Color(0.75f, 0.77f, 0.8f), null, 0.85f, 1f));
                g.layer = 10; g.AddComponent<BoxCollider>();
            }
            e.inside = Text(t, new Vector3(0, 2.6f, -1.06f), 180, 1.2f);
            e.outside = Text(t, new Vector3(0, 2.6f, -1.18f), 0, 1.2f);
            e.SetDisplay(floor);
            return e;
        }

        static TextMeshPro Text(Transform p, Vector3 pos, float yaw, float size)
        {
            var go = new GameObject("Display");
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var t = go.AddComponent<TextMeshPro>();
            t.font = UIKit.Font; t.fontSize = size; t.color = new Color(1f, 0.55f, 0.15f);
            t.alignment = TextAlignmentOptions.Center;
            t.rectTransform.sizeDelta = new Vector2(1.4f, 0.4f);
            return t;
        }

        public static string FloorName(int f) => f == 0 ? "1 · ЛОББИ" : (f + 1).ToString();

        void SetDisplay(int f, string arrow = "")
        {
            string s = arrow + (f + 1);
            inside.text = s; outside.text = s;
        }

        void Update()
        {
            if (Riding) return;
            var pl = PlayerCtl.I;
            if (pl == null) return;
            var lp = transform.InverseTransformPoint(pl.transform.position);
            bool near = Mathf.Abs(lp.y) < 1.5f && Mathf.Abs(lp.x) < 1.6f && lp.z > -3.2f && lp.z < 1.2f;
            open = Mathf.MoveTowards(open, near ? 1 : 0, Time.deltaTime * 1.8f);
            Slide();
        }

        void Slide()
        {
            float s = Mathf.SmoothStep(0, 1, open) * 0.62f;
            l.localPosition = new Vector3(-0.37f - s, 0, -1.1f);
            r.localPosition = new Vector3(0.37f + s, 0, -1.1f);
        }

        public void Go(int target)
        {
            if (Riding || target == Floor) return;
            StartCoroutine(Ride(target));
        }

        IEnumerator Ride(int target)
        {
            Riding = true;
            var pl = PlayerCtl.I;
            pl.Frozen = true;
            while (open > 0) { open = Mathf.MoveTowards(open, 0, Time.deltaTime * 2.2f); Slide(); yield return null; }
            AudioFx.Play("click");
            pl.CC.enabled = false;
            pl.transform.SetParent(transform, true);
            int from = Floor;
            // the new floor is built around the shaft while the doors are shut
            Interiors.BuildLevel(target);
            float y0 = from * FH, y1 = target * FH;
            float dur = Mathf.Clamp(1.4f + Mathf.Abs(target - from) * 0.18f, 1.6f, 7f);
            var basePos = transform.localPosition;
            string arrow = target > from ? "▲ " : "▼ ";
            for (float t = 0; t < 1; t += Time.deltaTime / dur)
            {
                float k = Mathf.SmoothStep(0, 1, t);
                float y = Mathf.Lerp(y0, y1, k);
                transform.localPosition = new Vector3(basePos.x, y, basePos.z);
                SetDisplay(Mathf.RoundToInt(y / FH), arrow);
                yield return null;
            }
            transform.localPosition = new Vector3(basePos.x, y1, basePos.z);
            Floor = target;
            SetDisplay(target);
            pl.transform.SetParent(null, true);
            pl.Teleport(pl.transform.position + Vector3.up * 0.05f, pl.transform.eulerAngles.y);
            AudioFx.Play("chat");
            Hud.Toast(target == 0 ? "Лобби" : "Этаж " + (target + 1));
            pl.Frozen = false;
            Riding = false;
        }
    }
}
