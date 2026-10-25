using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gad
{
    /// <summary>
    /// The title screen: the live city behind (a slow cinematic camera circling the player's street),
    /// the HERBAL logo and the main buttons — play, character, settings. Shown on every start and from
    /// the phone; the HUD and the player's controls are off while it is open.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        public static MainMenu I;
        public static bool Open => I != null && I.gameObject.activeSelf;

        TextMeshProUGUI nick, online;
        Vector3 centre;
        float t;

        public static void Show()
        {
            if (I == null)
            {
                var rt = UIKit.Fill(UIKit.Rect("MainMenu", UIRoot.Root));
                rt.SetSiblingIndex(UIRoot.WinLayer.GetSiblingIndex());   // above the HUD, under the windows
                I = rt.gameObject.AddComponent<MainMenu>();
                I.Build();
            }
            if (UIRoot.Open != null) Window.Close();
            I.gameObject.SetActive(true);
            I.centre = PlayerCtl.I != null ? PlayerCtl.I.transform.position : City.Spawn;
            if (PlayerCtl.I != null) PlayerCtl.I.MoveInput = Vector2.zero;
            UIRoot.HudLayer.gameObject.SetActive(false);
            PlayerCtl.ShotCam = true;
            I.Refresh();
        }

        public static void Hide()
        {
            if (I == null) return;
            I.gameObject.SetActive(false);
            UIRoot.HudLayer.gameObject.SetActive(true);
            PlayerCtl.ShotCam = false;
        }

        void Build()
        {
            var t = transform;
            var safe = UIRoot.Safe();
            float sl = Mathf.Max(40, safe.x + 24);

            // a dark band on the left so the logo and buttons read over any part of the city
            var band = UIKit.Img(t, UIKit.White, new Color(0.02f, 0.04f, 0.08f, 0.72f), "Band", true);
            band.rectTransform.anchorMin = Vector2.zero; band.rectTransform.anchorMax = new Vector2(0.46f, 1f);
            band.rectTransform.offsetMin = band.rectTransform.offsetMax = Vector2.zero;
            var edge = UIKit.Img(band.transform, UIKit.White, new Color(0.3f, 1f, 0.45f, 0.55f), "Edge");
            edge.rectTransform.anchorMin = new Vector2(1, 0); edge.rectTransform.anchorMax = Vector2.one;
            edge.rectTransform.offsetMin = new Vector2(-4, 0); edge.rectTransform.offsetMax = Vector2.zero;

            var col = UIKit.Rect("Column", band.transform);
            col.anchorMin = Vector2.zero; col.anchorMax = Vector2.one;
            col.offsetMin = new Vector2(sl, 40); col.offsetMax = new Vector2(-40, -40);

            // logo: HERBAL with a green leaf, and the tagline
            var logo = UIKit.Txt(col, "HERBAL", 150, Color.white, TextAlignmentOptions.Left);
            UIKit.Place(logo.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -20), new Vector2(820, 170));
            logo.enableAutoSizing = true; logo.fontSizeMin = 60; logo.fontSizeMax = 150;
            logo.outlineColor = new Color32(20, 120, 50, 255); logo.outlineWidth = 0.18f;
            var bar = UIKit.Img(col, UIKit.Pill, UIKit.Lime, "Bar");
            UIKit.Place(bar.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(6, -196), new Vector2(260, 10));
            var tag = UIKit.Txt(col, "ГОРОД БУДУЩЕГО · ОНЛАЙН", 30, UIKit.Muted, TextAlignmentOptions.Left);
            UIKit.Place(tag.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(4, -214), new Vector2(820, 44));

            // buttons
            float y = -300;
            UIButton Btn(string label, Color c, System.Action a, float h, float font)
            {
                var b = UIKit.Button(col, label, c, a, font);
                UIKit.Place(b.Bg.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y), new Vector2(560, h));
                y -= h + 18;
                return b;
            }
            Btn("ИГРАТЬ", new Color(0.15f, 0.72f, 0.32f), Play, 120, 52);
            Btn("ПЕРСОНАЖ", new Color(0.2f, 0.42f, 0.85f), () => Menus.Dress(false), 86, 34);
            Btn("НАСТРОЙКИ", new Color(0.32f, 0.36f, 0.45f), Menus.Settings, 86, 34);
            // other games are not ready yet: the button is shown greyed out and does nothing
            var more = Btn("ДРУГИЕ ИГРЫ · СКОРО", new Color(0.22f, 0.24f, 0.28f, 0.85f), null, 76, 28);
            more.Interactable = false;
            more.Label.color = new Color(1f, 1f, 1f, 0.45f);

            nick = UIKit.Txt(col, "", 28, Color.white, TextAlignmentOptions.BottomLeft);
            UIKit.Place(nick.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0, 46), new Vector2(820, 40));
            online = UIKit.Txt(col, "", 24, UIKit.Lime, TextAlignmentOptions.BottomLeft);
            UIKit.Place(online.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0, 8), new Vector2(820, 34));
        }

        void Refresh()
        {
            bool hasNick = Save.D.created && !string.IsNullOrEmpty(Save.D.name) && Save.D.name.Length >= 3;
            nick.text = hasNick ? "Игрок: " + Save.D.name + "   " + Hud.Money(Save.D.money) : "Новый игрок";
        }

        void Play()
        {
            Hide();
            if (!Save.D.created || string.IsNullOrEmpty(Save.D.name) || Save.D.name.Length < 3) Menus.Nick();
            else Hud.Toast("С возвращением, " + Save.D.name + "!", UIKit.Cyan);
        }

        void LateUpdate()
        {
            // slow orbit round the player's street, high enough to see the rooftops and the skyline
            t += Time.unscaledDeltaTime;
            float a = t * 0.06f;
            var pos = centre + new Vector3(Mathf.Sin(a) * 80f, 48f + Mathf.Sin(t * 0.13f) * 5f, Mathf.Cos(a) * 80f);
            PlayerCtl.ShotCam = true;
            PlayerCtl.ShotPos = pos;
            PlayerCtl.ShotLook = centre + Vector3.up * 8f;
            online.text = "● в мире: " + (WorldNet.OnlineCount + 1) + (WorldNet.Connected ? "" : "  (подключение…)");
            if (UIRoot.Open == null) Refresh();
        }
    }
}
