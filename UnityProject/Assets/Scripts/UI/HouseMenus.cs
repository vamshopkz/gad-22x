using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Gad
{
    /// <summary>0.7: building your own house on a plot — the catalogue of 100 projects with pictures.</summary>
    public static class HouseMenus
    {
        static int family = -1;   // -1 = all
        static readonly Color Green = new Color(0.15f, 0.65f, 0.3f), Blue = new Color(0.2f, 0.42f, 0.85f), Red = new Color(0.8f, 0.2f, 0.25f);

        public static void Plot(Bld b)
        {
            var mine = HouseWorld.PlotOf(b);
            Window.Show(mine == null ? "УЧАСТОК · ВЫБЕРИТЕ ПРОЕКТ ДОМА" : "МОЙ ДОМ · " + HouseDesign.Get(mine.design).name, w =>
            {
                w.Text(mine == null
                    ? $"{b.address}. Участок {HouseWorld.PlotPrice / 1000:0} 000 $ + цена дома. Выберите один из 100 проектов — дом построят сразу, со всей мебелью."
                    : "Можно перестроить дом по другому проекту (старый дом снесут, деньги за него вернут наполовину) или снести его.", 24, UIKit.Muted);
                if (mine != null)
                    w.Row("Снести дом", "Участок станет пустым, вернём половину цены дома", "СНЕСТИ", Red, () => { Demolish(b); Window.Close(); });
                var tabs = new List<(string, Color, bool, System.Action)> { ("Все", Blue, family < 0, () => { family = -1; Window.Refresh(); }) };
                for (int f = 0; f < HouseDesign.Families.Length; f++) { int k = f; tabs.Add((HouseDesign.Families[f], Blue, family == f, () => { family = k; Window.Refresh(); })); }
                w.Chips("Стиль", tabs);
                var host = UIKit.Rect("Projects", w.Content);
                var grid = host.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(286, 262);
                grid.spacing = new Vector2(12, 12);
                grid.padding = new RectOffset(6, 6, 6, 6);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 4;
                for (int i = 0; i < HouseDesign.Count; i++)
                {
                    var d = HouseDesign.Get(i);
                    if (family >= 0 && d.fam != family) continue;
                    var card = UIKit.Img(host, UIKit.Glass, Color.white, "Card", true);
                    if (mine != null && mine.design == i)
                    {
                        var ring = UIKit.Img(card.transform, UIKit.Btn, UIKit.Gold, "Sel");
                        UIKit.Fill(ring.rectTransform, -4, -4, -4, -4); ring.transform.SetAsFirstSibling();
                    }
                    var pic = UIKit.Rect("Pic", card.transform).gameObject.AddComponent<RawImage>();
                    UIKit.Place(pic.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(270, 170));
                    pic.color = new Color(1, 1, 1, 0.12f);
                    var ready = HouseIcons.Get(i, tex => { if (pic != null) { pic.texture = tex; pic.color = Color.white; } });
                    if (ready != null) { pic.texture = ready; pic.color = Color.white; }
                    var lbl = UIKit.Txt(card.transform, d.name, 20, Color.white, TMPro.TextAlignmentOptions.Center, false);
                    UIKit.Place(lbl.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 46), new Vector2(276, 34));
                    lbl.enableAutoSizing = true; lbl.fontSizeMin = 12; lbl.fontSizeMax = 20;
                    var price = UIKit.Txt(card.transform, Hud.Money(d.price) + " · " + d.floors + " эт. · " + d.Area + " м²", 18, UIKit.Gold, TMPro.TextAlignmentOptions.Center, false);
                    UIKit.Place(price.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(276, 30));
                    price.enableAutoSizing = true; price.fontSizeMin = 11; price.fontSizeMax = 18;
                    int id = i;
                    var btn = card.gameObject.AddComponent<UIButton>();
                    btn.Bg = card; btn.OnClick = () => Project(b, id);
                }
            }, 1300, 900);
        }

        static void Project(Bld b, int id)
        {
            var d = HouseDesign.Get(id);
            var mine = HouseWorld.PlotOf(b);
            long cost = d.price + (mine == null ? (long)HouseWorld.PlotPrice : -HouseDesign.Get(mine.design).price / 2);
            if (cost < 0) cost = 0;
            Window.Show(d.name, w =>
            {
                var bigHost = UIKit.Rect("Big", w.Content);
                bigHost.gameObject.AddComponent<LayoutElement>().minHeight = 360;
                var pic = UIKit.Rect("Pic", bigHost).gameObject.AddComponent<RawImage>();
                UIKit.Place(pic.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(540, 340));
                var tex = HouseIcons.Get(id, t => { if (pic != null) pic.texture = t; });
                if (tex != null) pic.texture = tex;
                w.Text(d.family + " · " + d.Features(), 26);
                w.Text($"Внутри: гостиная с диваном и телевизором, кухня со столовой{(d.floors > 1 ? ", спальни и ванные наверху, лестница" : ", спальня")}{(d.chimney ? ", камин" : "")}.", 22, UIKit.Muted);
                string what = mine == null ? $"Дом {Hud.Money(d.price)} + участок {Hud.Money((long)HouseWorld.PlotPrice)}" : $"Дом {Hud.Money(d.price)} − половина старого дома";
                w.Row(mine == null ? "Построить" : "Перестроить", what + $" = {Hud.Money(cost)}. У вас {Hud.Money(Save.D.money)}", "ПОСТРОИТЬ", Green, () => Build(b, id, cost), Save.D.money >= cost);
                w.Row("Назад", "К каталогу проектов", "НАЗАД", Blue, () => Plot(b));
            }, 1000, 860);
        }

        static void Build(Bld b, int id, long cost)
        {
            if (!Eco.Spend(cost, "Строительство дома")) return;
            var mine = HouseWorld.PlotOf(b);
            if (mine == null) Save.D.plots.Add(new PlotHouse { building = b.id, design = id });
            else mine.design = id;
            Save.Write();
            Window.Close();
            Hud.FadeThrough(() =>
            {
                HouseWorld.Refresh(b);
                Hud.Toast("Дом построен: " + HouseDesign.Get(id).name + "! Заходите внутрь.", new Color(0.5f, 1f, 0.6f));
            }, 1.4f);
        }

        static void Demolish(Bld b)
        {
            var mine = HouseWorld.PlotOf(b);
            if (mine == null) return;
            Save.D.money += HouseDesign.Get(mine.design).price / 2;
            Save.D.plots.Remove(mine);
            Save.Write();
            HouseWorld.Refresh(b);
            Hud.Toast("Дом снесён, участок свободен", UIKit.Cyan);
        }
    }

    /// <summary>Pictures of the house projects, rendered on demand far below the world (one per frame).</summary>
    public class HouseIcons : MonoBehaviour
    {
        static HouseIcons I;
        static readonly Vector3 O = new Vector3(9000f, -3000f, 0f);
        Camera cam;
        RenderTexture rt;
        readonly Dictionary<int, Texture2D> icons = new Dictionary<int, Texture2D>();
        readonly List<(int id, System.Action<Texture2D> done)> queue = new List<(int, System.Action<Texture2D>)>();

        static HouseIcons Get()
        {
            if (I != null) return I;
            I = new GameObject("HouseIcons").AddComponent<HouseIcons>();
            I.transform.position = O;
            I.cam = new GameObject("Cam").AddComponent<Camera>();
            I.cam.transform.SetParent(I.transform, false);
            I.cam.enabled = false;
            I.cam.clearFlags = CameraClearFlags.SolidColor;
            I.cam.backgroundColor = new Color(0.62f, 0.78f, 0.95f);
            I.cam.fieldOfView = 30f;
            I.cam.nearClipPlane = 0.3f; I.cam.farClipPlane = 120f;
            I.rt = new RenderTexture(540, 340, 24) { antiAliasing = 4 };
            var ground = Prim.Box(I.transform, new Vector3(0, -0.05f, 10), new Vector3(60, 0.1f, 60), WorldBuilder.Grass);
            foreach (var c in ground.GetComponentsInChildren<Collider>()) Destroy(c);
            return I;
        }

        public static Texture2D Get(int id, System.Action<Texture2D> done)
        {
            var h = Get();
            if (h.icons.TryGetValue(id, out var t)) return t;
            h.queue.Add((id, done));
            return null;
        }

        void LateUpdate()
        {
            if (queue.Count == 0) return;
            var (id, done) = queue[0];
            queue.RemoveAt(0);
            if (!icons.TryGetValue(id, out var tex))
            {
                var d = HouseDesign.Get(id);
                var house = HouseBuilder.Whole(transform, O, Quaternion.identity, d, 31);
                float span = Mathf.Max(d.W + (d.garage ? 3.8f : 0f), d.Height * 1.4f, d.D * 0.8f);
                var target = O + new Vector3(d.garage ? 1.9f : 0f, d.Height * 0.42f, d.D * 0.5f);
                var dir = Quaternion.Euler(14f, -32f, 0f) * Vector3.back;
                cam.transform.position = target + dir * (span * 2.3f);
                cam.transform.LookAt(target);
                cam.targetTexture = rt;
                cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply(false);
                RenderTexture.active = prev;
                cam.targetTexture = null;
                DestroyImmediate(house.GetComponent<MeshFilter>().sharedMesh);
                Destroy(house);
                icons[id] = tex;
            }
            done?.Invoke(tex);
        }
    }
}
