using System.Collections.Generic;
using UnityEngine;

namespace Gad
{
    /// <summary>0.8: a pistol from the gun shop.</summary>
    public class GunDef
    {
        public string Id, Title, Sub;
        public int Price;
        public Color Body, Grip;
        public bool Revolver;
        public float Size = 1f, Rate = 0.25f;

        public static readonly List<GunDef> All = new List<GunDef>
        {
            new GunDef { Id = "glock", Title = "Глок 17", Sub = "Надёжный, лёгкий", Price = 1200, Body = new Color(0.12f, 0.12f, 0.13f), Grip = new Color(0.1f, 0.1f, 0.1f), Rate = 0.18f },
            new GunDef { Id = "beretta", Title = "Беретта 92", Sub = "Классика, стальной", Price = 1500, Body = new Color(0.55f, 0.56f, 0.58f), Grip = new Color(0.25f, 0.16f, 0.1f), Size = 1.08f, Rate = 0.22f },
            new GunDef { Id = "colt", Title = "Кольт 1911", Sub = "Легенда 100 лет", Price = 1800, Body = new Color(0.2f, 0.2f, 0.22f), Grip = new Color(0.45f, 0.26f, 0.12f), Size = 1.06f, Rate = 0.28f },
            new GunDef { Id = "revolver", Title = "Револьвер", Sub = "Шесть патронов, ковбойский", Price = 2200, Body = new Color(0.7f, 0.7f, 0.72f), Grip = new Color(0.4f, 0.22f, 0.1f), Revolver = true, Size = 1.1f, Rate = 0.45f },
            new GunDef { Id = "deagle", Title = "Дезерт Игл", Sub = "Самый мощный", Price = 3500, Body = new Color(0.5f, 0.5f, 0.52f), Grip = new Color(0.08f, 0.08f, 0.08f), Size = 1.25f, Rate = 0.5f },
            new GunDef { Id = "gold", Title = "Золотой пистолет", Sub = "Для коллекции", Price = 25000, Body = new Color(1f, 0.78f, 0.25f), Grip = new Color(0.1f, 0.08f, 0.06f), Size = 1.15f, Rate = 0.3f },
        };
        public static GunDef Get(string id) => All.Find(g => g.Id == id);
    }

    /// <summary>
    /// The pistol in the player's hand: a model built from parts, the fire button, a crosshair, a
    /// flash and a sound, sparks where the bullet hits, and score on the shooting-range targets.
    /// </summary>
    public class Guns : MonoBehaviour
    {
        public static Guns I;
        public static bool Armed => I != null && I.held != null;
        GameObject held;
        Transform muzzle;
        float cool;
        GunDef def;

        public static void Create() { if (I == null) I = new GameObject("Guns").AddComponent<Guns>(); }

        /// <summary>A pistol model, about 20 cm long, barrel along +Z, grip down.</summary>
        public static GameObject Model(GunDef g, Transform parent, out Transform muzzle)
        {
            var root = new GameObject("Gun " + g.Title);
            root.transform.SetParent(parent, false);
            var body = Mats.Lit(g.Body, g.Id == "gold" ? 0.85f : 0.6f, g.Id == "gold" || g.Id == "beretta" || g.Id == "revolver" || g.Id == "deagle" ? 0.9f : 0.3f);
            var grip = Mats.Lit(g.Grip, 0.3f);
            var dark = Mats.Lit(new Color(0.05f, 0.05f, 0.05f), 0.4f);
            float s = g.Size;
            void P(PrimitiveType t, Vector3 p, Vector3 sc, Material m, Vector3? rot = null)
            {
                var go = Prim.Make(t, root.transform, p * s, sc * s, m);
                if (rot.HasValue) go.transform.localRotation = Quaternion.Euler(rot.Value);
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            if (g.Revolver)
            {
                P(PrimitiveType.Cylinder, new Vector3(0, 0.035f, 0.1f), new Vector3(0.022f, 0.08f, 0.022f), body, new Vector3(90, 0, 0));
                P(PrimitiveType.Cylinder, new Vector3(0, 0.03f, 0.02f), new Vector3(0.042f, 0.022f, 0.042f), body, new Vector3(90, 0, 0));
                P(PrimitiveType.Cube, new Vector3(0, 0.04f, -0.02f), new Vector3(0.025f, 0.035f, 0.07f), body);
            }
            else
            {
                P(PrimitiveType.Cube, new Vector3(0, 0.04f, 0.05f), new Vector3(0.028f, 0.032f, 0.19f), body);           // slide
                P(PrimitiveType.Cube, new Vector3(0, 0.017f, 0.04f), new Vector3(0.026f, 0.018f, 0.15f), body);           // frame
                for (int i = 0; i < 6; i++) P(PrimitiveType.Cube, new Vector3(0, 0.05f, -0.03f + i * 0.007f), new Vector3(0.03f, 0.012f, 0.002f), dark);   // serrations
                P(PrimitiveType.Cylinder, new Vector3(0, 0.04f, 0.148f), new Vector3(0.012f, 0.004f, 0.012f), dark, new Vector3(90, 0, 0));
            }
            P(PrimitiveType.Cube, new Vector3(0, -0.035f, -0.025f), new Vector3(0.026f, 0.09f, 0.035f), grip, new Vector3(-15, 0, 0));    // grip
            P(PrimitiveType.Cube, new Vector3(0, -0.005f, 0.025f), new Vector3(0.006f, 0.022f, 0.035f), body);                            // trigger guard
            P(PrimitiveType.Cube, new Vector3(0, 0.0f, 0.02f), new Vector3(0.004f, 0.016f, 0.004f), dark);                              // trigger
            P(PrimitiveType.Cube, new Vector3(0, 0.06f, 0.13f), new Vector3(0.004f, 0.006f, 0.006f), dark);                             // front sight
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root.transform, false);
            muzzle.localPosition = new Vector3(0, 0.04f, (g.Revolver ? 0.18f : 0.15f)) * s;
            return root;
        }

        /// <summary>Take the gun out (or put it away).</summary>
        public static void Equip(string id, bool quiet = false)
        {
            Create();
            I.Holster();
            var g = GunDef.Get(id);
            var pl = PlayerCtl.I;
            var rig = pl != null && pl.Body != null ? pl.Body.Rig : null;
            if (g == null || rig == null || rig.handR == null) return;
            I.def = g;
            I.held = Model(g, rig.handR, out I.muzzle);
            // in the palm, barrel forward
            I.held.transform.localPosition = new Vector3(0.0f, -0.09f, 0.02f);
            I.held.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Save.D.gun = id;
            if (!quiet) Hud.Toast(g.Title + " в руке. Кнопка «ОГОНЬ» — выстрел", UIKit.Cyan);
        }

        public void Holster()
        {
            if (held != null) Destroy(held);
            held = null; def = null;
        }

        public static void Toggle()
        {
            if (Armed) { I.Holster(); Save.D.gun = ""; return; }
            if (Save.D.guns.Count == 0) { Hud.Toast("Сначала купите пистолет в оружейном магазине"); return; }
            Equip(string.IsNullOrEmpty(Save.D.gun) || !Save.D.guns.Contains(Save.D.gun) ? Save.D.guns[0] : Save.D.gun);
        }

        public static void Fire()
        {
            if (!Armed || I.cool > 0f) return;
            var pl = PlayerCtl.I;
            if (pl == null || pl.Car != null || Taxi.Riding) return;
            I.cool = I.def.Rate;
            AudioFx.Play("shot", I.def.Id == "deagle" ? 1f : 0.75f);
            // face where the camera looks
            pl.transform.rotation = Quaternion.Euler(0, pl.Cam.transform.eulerAngles.y, 0);
            // flash at the muzzle
            var flash = new GameObject("Flash").AddComponent<Light>();
            flash.transform.position = I.muzzle.position;
            flash.type = LightType.Point; flash.range = 6f; flash.intensity = 4f; flash.color = new Color(1f, 0.75f, 0.35f);
            var fx = Prim.Make(PrimitiveType.Sphere, null, I.muzzle.position, Vector3.one * 0.08f, Mats.Glow(new Color(1f, 0.85f, 0.4f)));
            foreach (var c in fx.GetComponentsInChildren<Collider>()) Destroy(c);
            Destroy(flash.gameObject, 0.05f); Destroy(fx, 0.05f);
            // the bullet goes where the crosshair is
            var cam = pl.Cam.transform;
            var ray = new Ray(cam.position + cam.forward * 2f, cam.forward);
            if (Physics.Raycast(ray, out var hit, 150f, ~0, QueryTriggerInteraction.Ignore))
            {
                var spark = Prim.Make(PrimitiveType.Sphere, null, hit.point, Vector3.one * 0.07f, Mats.Glow(new Color(1f, 0.9f, 0.5f)));
                foreach (var c in spark.GetComponentsInChildren<Collider>()) Destroy(c);
                Destroy(spark, 0.12f);
                var target = hit.collider.GetComponentInParent<RangeTarget>();
                if (target != null) target.Hit(hit.point);
            }
        }

        void Update()
        {
            if (cool > 0f) cool -= Time.deltaTime;
            var pl = PlayerCtl.I;
            // the body is rebuilt when clothes change: give the gun back to the new hand
            if (held == null && pl != null && !string.IsNullOrEmpty(Save.D.gun) && Save.D.guns.Contains(Save.D.gun) && pl.Body != null && pl.Body.Rig != null) Equip(Save.D.gun, true);
            // put the gun away while driving or riding; take it back out afterwards
            if (held != null && pl != null && (pl.Car != null || Taxi.Riding)) held.SetActive(false);
            else if (held != null) held.SetActive(true);
        }
    }

    /// <summary>The gun shop counter: buy pistols, take one in your hand.</summary>
    public static class GunMenus
    {
        public static void Shop()
        {
            Window.Show("ОРУЖЕЙНЫЙ МАГАЗИН", w =>
            {
                w.Text("Пистолеты для тира и коллекции. Купленный пистолет — кнопка «ПИСТОЛЕТ» на экране.", 24, UIKit.Muted);
                foreach (var g in GunDef.All)
                {
                    bool own = Save.D.guns.Contains(g.Id);
                    if (own) w.Row(g.Title, g.Sub + " · уже ваш", "В РУКУ", new Color(0.15f, 0.55f, 0.85f), () => { Window.Close(); Guns.Equip(g.Id); });
                    else w.Row(g.Title, g.Sub + " · " + Hud.Money(g.Price), "КУПИТЬ", new Color(0.15f, 0.65f, 0.3f), () =>
                    {
                        if (!Eco.Spend(g.Price, "Пистолет " + g.Title)) return;
                        Save.D.guns.Add(g.Id); Save.Write();
                        AudioFx.Play("buy");
                        Window.Close(); Guns.Equip(g.Id);
                    }, Save.D.money >= g.Price);
                }
            }, 1000, 820);
        }
    }

    /// <summary>A round target on the shooting range: shows where you hit and counts points.</summary>
    public class RangeTarget : MonoBehaviour
    {
        public static int Score, Shots;
        public void Hit(Vector3 p)
        {
            float r = Vector3.Distance(transform.position, p) / (transform.lossyScale.x * 0.5f);
            int pts = r < 0.2f ? 10 : r < 0.4f ? 8 : r < 0.6f ? 6 : r < 0.8f ? 4 : 2;
            Score += pts; Shots++;
            var hole = Prim.Make(PrimitiveType.Sphere, transform, transform.InverseTransformPoint(p), Vector3.one * 0.03f / transform.lossyScale.x, Mats.Lit(Color.black));
            foreach (var c in hole.GetComponentsInChildren<Collider>()) Destroy(c);
            Hud.Toast(pts == 10 ? "В десятку! +10" : "+" + pts + " очков · всего " + Score, pts == 10 ? UIKit.Gold : Color.white);
            AudioFx.Play("coin", 0.4f);
        }
    }
}
