#if !UNITY_WEBGL && (UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX)
#define GAD_WEBVIEW
#endif
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if GAD_WEBVIEW
using Gree.UnityWebView;
#endif

namespace Gad
{
    /// <summary>
    /// 0.8: the phone in your hand — it takes the right half of the screen while the game goes on in
    /// the left half. Real Google, YouTube and Google Play open inside its screen (a web view on
    /// Android and iPhone; on the web version Google opens in the phone and YouTube / Google Play in a
    /// new browser tab, because those sites refuse to be shown inside another page). The game's own
    /// apps (work, cars, homes, map, taxi...) open the usual windows.
    /// </summary>
    public class PhoneUI : MonoBehaviour
    {
        public static PhoneUI I;
        public static bool Open => I != null && I.gameObject.activeSelf;

        RectTransform frame, screen;
        GameObject home, webBar;
        TextMeshProUGUI clock, webTitle, note;
        string app;
#if GAD_WEBVIEW
        WebViewObject wv;
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void GadFrameShow(string url, float x, float y, float w, float h);
        [DllImport("__Internal")] static extern void GadFrameHide();
        [DllImport("__Internal")] static extern void GadOpenTab(string url);
#endif

        public static void Toggle() { if (Open) Close(); else Show(); }

        bool desk;   // the office computer: a wide monitor over the whole screen

        public static void Show() => OpenAs(false);

        /// <summary>The computer on the desk of your own office (same apps, a big screen, and office work).</summary>
        public static void ShowComputer() => OpenAs(true);

        static void OpenAs(bool computer)
        {
            if (I == null)
            {
                var rt = UIKit.Rect("Phone", UIRoot.Root);
                rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                rt.SetSiblingIndex(UIRoot.WinLayer.GetSiblingIndex());   // over the HUD, under the windows
                I = rt.gameObject.AddComponent<PhoneUI>();
                I.Build();
            }
            var r = (RectTransform)I.transform;
            r.anchorMin = computer ? Vector2.zero : new Vector2(0.5f, 0f);
            if (I.desk != computer || I.home.transform.childCount == 0) { I.desk = computer; I.FillApps(); }
            I.gameObject.SetActive(true);
            I.Home();
        }

        public static void Close()
        {
            if (I == null) return;
            I.HideWeb();
            I.gameObject.SetActive(false);
        }

        void Build()
        {
            // catches touches on the right half so they do not turn the camera
            var back = UIKit.Img(transform, UIKit.White, new Color(0, 0, 0, 0.35f), "Shade", true);
            UIKit.Fill(back.rectTransform);
            frame = UIKit.Img(transform, UIKit.Btn, new Color(0.05f, 0.05f, 0.07f), "Body").rectTransform;
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            var scr = UIKit.Img(frame, UIKit.White, new Color(0.07f, 0.09f, 0.14f), "Screen", true);
            screen = scr.rectTransform;
            UIKit.Fill(screen, 16, 58, 16, 44);
            // status bar
            clock = UIKit.Txt(frame, "", 22, Color.white, TextAlignmentOptions.Left, false);
            UIKit.Place(clock.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -10), new Vector2(160, 36));
            var net = UIKit.Txt(frame, "Herbal 5G  ▮▮▮  87%", 18, Color.white, TextAlignmentOptions.Right, false);
            UIKit.Place(net.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -12), new Vector2(260, 32));
            // bottom navigation: back, home, close
            float bw = 1f / 3f;
            string[] labels = { "НАЗАД", "ДОМОЙ", "ЗАКРЫТЬ" };
            System.Action[] acts = { Back, Home, Close };
            for (int i = 0; i < 3; i++)
            {
                var b = UIKit.Button(frame, labels[i], new Color(0.12f, 0.14f, 0.2f), acts[i], 18);
                b.Bg.rectTransform.anchorMin = new Vector2(i * bw, 0); b.Bg.rectTransform.anchorMax = new Vector2((i + 1) * bw, 0);
                b.Bg.rectTransform.pivot = new Vector2(0.5f, 0); b.Bg.rectTransform.sizeDelta = new Vector2(-12, 38); b.Bg.rectTransform.anchoredPosition = new Vector2(0, 4);
            }

            // home screen with the app icons
            home = UIKit.Rect("Home", screen).gameObject;
            UIKit.Fill((RectTransform)home.transform, 10, 10, 10, 10);
            var grid = home.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(118, 132); grid.spacing = new Vector2(8, 4);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperCenter;
            FillApps();

            // the title bar of a web app and a note for the web version
            webBar = UIKit.Img(screen, UIKit.White, new Color(0.95f, 0.95f, 0.96f), "WebBar").gameObject;
            var wbr = (RectTransform)webBar.transform;
            wbr.anchorMin = new Vector2(0, 1); wbr.anchorMax = Vector2.one; wbr.pivot = new Vector2(0.5f, 1); wbr.sizeDelta = new Vector2(0, 44); wbr.anchoredPosition = Vector2.zero;
            webTitle = UIKit.Txt(webBar.transform, "", 22, new Color(0.15f, 0.15f, 0.2f), TextAlignmentOptions.Center, false);
            UIKit.Fill(webTitle.rectTransform);
            note = UIKit.Txt(screen, "", 24, Color.white, TextAlignmentOptions.Center, false);
            UIKit.Fill(note.rectTransform, 20, 60, 20, 60);
            webBar.SetActive(false);
        }

        void FillApps()
        {
            for (int i = home.transform.childCount - 1; i >= 0; i--) Destroy(home.transform.GetChild(i).gameObject);
            home.transform.DetachChildren();
            home.GetComponent<GridLayoutGroup>().constraintCount = desk ? 6 : 3;
            if (desk) App("Работа в офисе", new Color(0.1f, 0.45f, 0.75f), "$", Color.white, OfficeWork.Open);
            App("YouTube", new Color(0.93f, 0.1f, 0.1f), ">", Color.white, () => Web("YouTube", "https://m.youtube.com/", "https://m.youtube.com/results?search_query="));
            App("Google", Color.white, "G", new Color(0.26f, 0.52f, 0.96f), () => Web("Google", "https://www.google.com/", "https://www.google.com/search?q="));
            App("Google Play", Color.white, ">", new Color(0.1f, 0.7f, 0.4f), () => Web("Google Play", "https://play.google.com/store/apps", "https://play.google.com/store/search?c=apps&q="));
            App("Работа", new Color(0.2f, 0.42f, 0.85f), "Р", Color.white, Menus.MyJob);
            App("Машины", new Color(0.95f, 0.55f, 0.15f), "М", Color.white, () => Menus.Garage(null));
            App("Дома", new Color(0.15f, 0.65f, 0.3f), "Д", Color.white, Menus.Homes);
            App("Бизнес", new Color(0.85f, 0.65f, 0.15f), "Б", Color.white, Menus.Business);
            App("Карта", new Color(0.15f, 0.55f, 0.85f), "К", Color.white, Menus.Map);
            App("Такси", new Color(1f, 0.82f, 0.1f), "Т", new Color(0.1f, 0.1f, 0.1f), () => Menus.Travel(null));
            App("Чат", new Color(0.2f, 0.6f, 0.45f), "Ч", Color.white, Menus.Chat);
            App("Скины", new Color(0.55f, 0.3f, 0.9f), "С", Color.white, Menus.Skins);
            App("Ещё", new Color(0.35f, 0.38f, 0.45f), "≡", Color.white, Menus.Phone);
            App("Настройки", new Color(0.3f, 0.32f, 0.38f), "Н", Color.white, Menus.Settings);
            if (!desk) App("Меню", new Color(0.12f, 0.5f, 0.25f), "H", Color.white, () => { Close(); MainMenu.Show(); });
        }

        void App(string label, Color bg, string glyph, Color fg, System.Action act)
        {
            var cell = UIKit.Rect(label, home.transform);
            var tile = UIKit.Img(cell, UIKit.Btn, bg, "Icon", true);
            UIKit.Place(tile.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(88, 88));
            if (glyph == ">")
            {   // a play triangle (the font has no such glyph)
                var tri = UIKit.Img(tile.transform, Triangle(), fg, "Play");
                UIKit.Place(tri.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(4, 0), new Vector2(46, 46));
            }
            else
            {
                var g = UIKit.Txt(tile.transform, glyph, 54, fg, TextAlignmentOptions.Center, false);
                UIKit.Fill(g.rectTransform);
            }
            var l = UIKit.Txt(cell, label, 18, Color.white, TextAlignmentOptions.Center, false);
            UIKit.Place(l.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(124, 44));
            l.enableAutoSizing = true; l.fontSizeMin = 11; l.fontSizeMax = 18;
            var b = tile.gameObject.AddComponent<UIButton>(); b.Bg = tile; b.OnClick = act;
        }

        static Sprite tri;
        static Sprite Triangle()
        {
            if (tri != null) return tri;
            const int S = 64;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S, v = (y + 0.5f) / S;
                    float half = 0.5f * (1f - u);   // pointing right
                    float a = Mathf.Clamp01((half - Mathf.Abs(v - 0.5f)) * S * 0.7f) * Mathf.Clamp01(u * S * 0.7f);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return tri = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        }

        // the web version: a search page drawn by the game (the sites refuse to show inside another page)
        GameObject search;
        TMP_InputField query;
        string searchUrl;

        void SearchPage(string title, string url)
        {
            searchUrl = url;
            if (search == null)
            {
                search = UIKit.Rect("Search", screen).gameObject;
                UIKit.Fill((RectTransform)search.transform, 14, 60, 14, 60);
                var logo = UIKit.Txt(search.transform, "", 56, Color.white, TextAlignmentOptions.Center, false);
                logo.name = "Logo";
                UIKit.Place(logo.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(460, 80));
                var row = UIKit.Rect("Row", search.transform);
                UIKit.Place(row, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(460, 74));
                query = UIKit.Input(row, "Поиск…", 40);
                UIKit.Fill(query.GetComponent<RectTransform>());
                var go = UIKit.Button(search.transform, "НАЙТИ", new Color(0.26f, 0.52f, 0.96f), Find, 26);
                UIKit.Place(go.Bg.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -244), new Vector2(240, 70));
                var hint = UIKit.Txt(search.transform, "Результаты откроются в новой вкладке браузера. В приложении для Android сайт открывается прямо здесь, в телефоне.", 18, UIKit.Muted, TextAlignmentOptions.Top, false);
                hint.name = "Hint";
                UIKit.Place(hint.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -330), new Vector2(440, 160));
            }
            search.SetActive(true);
            var l = search.transform.Find("Logo").GetComponent<TextMeshProUGUI>();
            l.text = title == "Google" ? "<color=#4285F4>G</color><color=#EA4335>o</color><color=#FBBC05>o</color><color=#4285F4>g</color><color=#34A853>l</color><color=#EA4335>e</color>"
                   : title == "YouTube" ? "<color=#FF0000>You</color>Tube" : "<color=#34A853>Google</color> Play";
            query.text = "";
        }

        void Find()
        {
            var q = query.text.Trim();
            if (q.Length == 0) return;
            var url = searchUrl + UnityEngine.Networking.UnityWebRequest.EscapeURL(q);
#if UNITY_WEBGL && !UNITY_EDITOR
            GadOpenTab(url);
#else
            Application.OpenURL(url);
#endif
        }

        void Home()
        {
            if (search != null) search.SetActive(false);
            HideWeb();
            app = null;
            home.SetActive(true);
            webBar.SetActive(false);
            note.text = "";
        }

        void Web(string title, string url, string searchBase)
        {
            app = title;
            home.SetActive(false);
            webBar.SetActive(true);
            webTitle.text = title;
            note.text = "Загрузка…";
#if UNITY_WEBGL && !UNITY_EDITOR
            note.text = "";
            SearchPage(title, searchBase);
#elif GAD_WEBVIEW
            if (wv == null)
            {
                wv = new GameObject("WebView").AddComponent<WebViewObject>();
                wv.transform.SetParent(transform, false);
                wv.Init(ld: _ => { if (note != null) note.text = ""; }, hooked: OpenInStore, transparent: false, zoom: true);
                // an app's page in Google Play opens in the real Play Store app: "Install" works there without signing in again
                wv.SetURLPattern("", "", "^https?://play\\.google\\.com/store/apps/details|^market://|^intent://");
            }
            Place();
            wv.LoadURL(url);
            wv.SetVisibility(true);
#else
            note.text = "";
            SearchPage(title, searchBase);
#endif
        }

        /// <summary>Open an app's page in the phone's own Play Store (or a link the web view cannot handle).</summary>
        static void OpenInStore(string url)
        {
            string id = null;
            int k = url.IndexOf("id=");
            if (k >= 0) { id = url.Substring(k + 3); int amp = id.IndexOf('&'); if (amp >= 0) id = id.Substring(0, amp); }
            if (id != null && url.Contains("play.google.com")) Application.OpenURL("market://details?id=" + id);
            else Application.OpenURL(url);
        }

        void Back()
        {
#if GAD_WEBVIEW
            if (wv != null && app != null && wv.CanGoBack()) { wv.GoBack(); return; }
#endif
            Home();
        }

        void HideWeb()
        {
#if GAD_WEBVIEW
            if (wv != null) wv.SetVisibility(false);
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
            GadFrameHide();
#endif
        }

        /// <summary>The phone's screen below the title bar, in screen pixels (origin bottom-left).</summary>
        Rect ScreenRect()
        {
            var c = new Vector3[4];
            screen.GetWorldCorners(c);
            var a = RectTransformUtility.WorldToScreenPoint(null, c[0]);
            var b = RectTransformUtility.WorldToScreenPoint(null, c[2]);
            float k = screen.rect.height > 0 ? (b.y - a.y) / screen.rect.height : 1f;
            return new Rect(a.x, a.y, b.x - a.x, b.y - a.y - 44f * k);
        }

#if GAD_WEBVIEW
        void Place()
        {
            var r = ScreenRect();
            wv.SetMargins((int)r.xMin, (int)(Screen.height - r.yMax), (int)(Screen.width - r.xMax), (int)r.yMin);
        }
#endif

        void LateUpdate()
        {
            // the phone: as tall as the screen allows, phone-shaped, centred in the right half
            var host = (RectTransform)transform;
            float h = host.rect.height * 0.94f, w = Mathf.Min(h * 0.52f, host.rect.width * 0.94f);
            if (desk) { w = host.rect.width * 0.92f; h = Mathf.Min(host.rect.height * 0.94f, w * 0.6f); }
            frame.sizeDelta = new Vector2(w, h);
            clock.text = DayNight.ClockText();
            // a game window opened from an app goes over the phone: the native web view would cover it
            bool covered = UIRoot.Open != null;
#if GAD_WEBVIEW
            if (wv != null && app != null) { wv.SetVisibility(!covered); if (!covered) Place(); }
#endif
        }

        void OnDisable() => HideWeb();
    }
}
