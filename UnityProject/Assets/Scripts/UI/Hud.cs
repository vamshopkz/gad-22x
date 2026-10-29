using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gad
{
    /// <summary>On-screen controls and information: money/needs, clock, minimap, GPS, toasts, chat.</summary>
    public class Hud : MonoBehaviour
    {
        public static Hud I;
        TextMeshProUGUI money, clock, place, status, online, gpsText, actionLabel;
        Image foodBar, energyBar, healthBar, workFill, arrow, mapPlayer, mapTarget;
        RectTransform workBar, gps;
        RawImage minimap;
        UIButton action, jump, run, carBtn, brake, phone, mapBtn, chatBtn, gunBtn, fireBtn;
        Image cross;
        readonly List<(TextMeshProUGUI t, float until)> toasts = new List<(TextMeshProUGUI, float)>();
        RectTransform toastBox, chatBox;
        readonly List<(TextMeshProUGUI t, float at)> chatLines = new List<(TextMeshProUGUI, float)>();
        Image fade;
        float fadeT, fadeDir;
        System.Action fadeMid;
        public static Vector3? Gps;
        public static string GpsName;
        GameObject gpsBeam;

        public static string Money(long v) => "$" + UIKit.Num(v);

        public static void Create()
        {
            var go = UIKit.Fill(UIKit.Rect("Hud", UIRoot.HudLayer)).gameObject;
            I = go.AddComponent<Hud>();
            I.Build();
        }

        void Build()
        {
            var t = transform;
            var safe = UIRoot.Safe();
            float sl = Mathf.Max(16, safe.x), sr = Mathf.Max(16, safe.z), st = Mathf.Max(12, safe.w);

            // camera drag everywhere (under the rest)
            var look = UIKit.Img(t, UIKit.White, new Color(0, 0, 0, 0), "Look", true);
            UIKit.Fill(look.rectTransform);
            look.gameObject.AddComponent<LookArea>();

            // joystick zone
            var zone = UIKit.Img(t, UIKit.White, new Color(0, 0, 0, 0), "JoyZone", true);
            zone.rectTransform.anchorMin = Vector2.zero; zone.rectTransform.anchorMax = new Vector2(0.4f, 0.62f);
            zone.rectTransform.offsetMin = zone.rectTransform.offsetMax = Vector2.zero;
            var jb = UIKit.Img(zone.transform, UIKit.Ring, new Color(1, 1, 1, 0.8f), "Base");
            UIKit.Place(jb.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(sl + 170, 190), new Vector2(240, 240));
            var knob = UIKit.Img(jb.transform, UIKit.Circle, new Color(0.3f, 0.9f, 1f, 0.75f), "Knob");
            UIKit.Place(knob.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 110));
            zone.gameObject.AddComponent<Joystick>().Init(jb.rectTransform, knob.rectTransform);

            // info panel
            var info = UIKit.Img(t, UIKit.Glass, Color.white, "Info");
            UIKit.Place(info.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(sl, -st), new Vector2(440, 196));
            money = UIKit.Txt(info.transform, "$0", 44, UIKit.Gold, TextAlignmentOptions.TopLeft);
            UIKit.Place(money.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -10), new Vector2(400, 54));
            clock = UIKit.Txt(info.transform, "", 24, Color.white, TextAlignmentOptions.TopLeft, false);
            UIKit.Place(clock.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -64), new Vector2(410, 30));
            place = UIKit.Txt(info.transform, "", 22, UIKit.Cyan, TextAlignmentOptions.TopLeft, false);
            UIKit.Place(place.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -94), new Vector2(410, 28));
            foodBar = Bar(info.transform, "ЕДА", 0, new Color(1f, 0.6f, 0.2f));
            energyBar = Bar(info.transform, "СИЛЫ", 1, new Color(0.3f, 0.85f, 1f));
            healthBar = Bar(info.transform, "ЗДОРОВЬЕ", 2, new Color(1f, 0.35f, 0.4f));

            // top-right buttons + minimap
            phone = UIKit.Button(t, "ТЕЛЕФОН", UIKit.Violet, PhoneUI.Toggle, 26);
            // 0.8: a phone button on the right side as well: the phone opens on half the screen
            var side = UIKit.Button(t, "ТЕЛЕ-\nФОН", UIKit.Violet, PhoneUI.Toggle, 24);
            UIKit.Place(side.Bg.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-sr, 40), new Vector2(120, 110));
            side.Label.textWrappingMode = TextWrappingModes.Normal;
            UIKit.Place(phone.Bg.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-sr, -st), new Vector2(200, 70));
            mapBtn = UIKit.Button(t, "КАРТА", new Color(0.15f, 0.55f, 0.85f), () => Menus.Map(), 26);
            UIKit.Place(mapBtn.Bg.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-sr - 210, -st), new Vector2(160, 70));
            chatBtn = UIKit.Button(t, "ЧАТ", new Color(0.2f, 0.6f, 0.45f), () => Menus.Chat(), 26);
            UIKit.Place(chatBtn.Bg.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-sr - 380, -st), new Vector2(130, 70));
            var mm = UIKit.Img(t, UIKit.Panel, Color.white, "Minimap");
            UIKit.Place(mm.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-sr, -st - 80), new Vector2(250, 250));
            minimap = UIKit.Rect("Map", mm.transform).gameObject.AddComponent<RawImage>();
            UIKit.Fill(minimap.rectTransform, 8, 8, 8, 8);
            minimap.texture = MapTex.Get();
            minimap.gameObject.AddComponent<RectMask2D>();
            mapTarget = UIKit.Img(minimap.transform, UIKit.Circle, UIKit.Gold, "Target");
            mapTarget.rectTransform.sizeDelta = new Vector2(18, 18);
            mapPlayer = UIKit.Img(mm.transform, UIKit.White, UIKit.Cyan, "Me");
            UIKit.Place(mapPlayer.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.3f), Vector2.zero, new Vector2(12, 22));
            online = UIKit.Txt(mm.transform, "", 20, UIKit.Lime, TextAlignmentOptions.Center, true);
            UIKit.Place(online.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(250, 30));

            // status (work / mission) + GPS arrow
            var sb = UIKit.Img(t, UIKit.Glass, Color.white, "Status");
            workBar = sb.rectTransform;
            UIKit.Place(workBar, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -st), new Vector2(620, 84));
            status = UIKit.Txt(sb.transform, "", 24, Color.white, TextAlignmentOptions.Center, false);
            UIKit.Fill(status.rectTransform, 16, 26, 16, 6);
            var wbg = UIKit.Img(sb.transform, UIKit.Pill, new Color(0, 0, 0, 0.5f), "BarBg");
            UIKit.Place(wbg.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(560, 16));
            workFill = UIKit.Img(wbg.transform, UIKit.Pill, UIKit.Lime, "Fill");
            workFill.type = Image.Type.Filled; workFill.fillMethod = Image.FillMethod.Horizontal;
            UIKit.Fill(workFill.rectTransform);

            gps = UIKit.Rect("GPS", t);
            UIKit.Place(gps, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -st - 92), new Vector2(200, 100));
            arrow = UIKit.Img(gps, UIKit.White, UIKit.Gold, "Arrow");
            UIKit.Place(arrow.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -26), new Vector2(16, 44));
            var head = UIKit.Img(arrow.transform, UIKit.Circle, UIKit.Gold, "Head");
            UIKit.Place(head.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            gpsText = UIKit.Txt(gps, "", 22, UIKit.Gold, TextAlignmentOptions.Center);
            UIKit.Place(gpsText.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(400, 30));

            // toasts and chat
            toastBox = UIKit.Rect("Toasts", t);
            UIKit.Place(toastBox, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -st - 200), new Vector2(1000, 300));
            chatBox = UIKit.Rect("Chat", t);
            UIKit.Place(chatBox, new Vector2(0, 1), new Vector2(0, 1), new Vector2(sl, -st - 210), new Vector2(560, 240));

            // right-hand buttons
            action = UIKit.Button(t, "", new Color(0.1f, 0.65f, 0.95f), () => { if (PlayerCtl.I) PlayerCtl.I.ActionPressed = true; }, 30, "Action");
            UIKit.Place(action.Bg.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-sr - 20, 300), new Vector2(520, 100));
            jump = UIKit.Round(t, "ПРЫЖОК", new Color(0.15f, 0.2f, 0.35f, 0.85f), () => { if (PlayerCtl.I) PlayerCtl.I.JumpPressed = true; }, 160, 26);
            jump.FireOnDown = true;
            UIKit.Place(jump.Bg.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-sr - 30, 40), new Vector2(170, 170));
            run = UIKit.Round(t, "БЕГ", new Color(0.15f, 0.2f, 0.35f, 0.85f), () => { if (PlayerCtl.I) PlayerCtl.I.RunHeld = !PlayerCtl.I.RunHeld; }, 130, 26);
            UIKit.Place(run.Bg.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-sr - 220, 40), new Vector2(130, 130));
            carBtn = UIKit.Round(t, "СЕСТЬ", new Color(0.95f, 0.55f, 0.1f, 0.95f), () => { if (PlayerCtl.I) PlayerCtl.I.CarPressed = true; }, 130, 26);
            UIKit.Place(carBtn.Bg.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-sr - 30, 180), new Vector2(120, 120));
            brake = UIKit.Round(t, "ТОРМОЗ", new Color(0.85f, 0.2f, 0.2f, 0.9f), null, 160, 26);
            UIKit.Place(brake.Bg.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-sr - 30, 40), new Vector2(170, 170));

            // 0.8: the pistol: take out / put away, fire, and a crosshair in the middle
            gunBtn = UIKit.Round(t, "ПИСТОЛЕТ", new Color(0.3f, 0.3f, 0.35f, 0.9f), Guns.Toggle, 120, 20);
            UIKit.Place(gunBtn.Bg.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-sr - 370, 40), new Vector2(120, 120));
            fireBtn = UIKit.Round(t, "ОГОНЬ", new Color(0.85f, 0.25f, 0.15f, 0.92f), Guns.Fire, 150, 26);
            fireBtn.FireOnDown = true;
            UIKit.Place(fireBtn.Bg.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-sr - 210, 190), new Vector2(150, 150));
            cross = UIKit.Img(t, UIKit.Ring, new Color(1, 1, 1, 0.85f), "Cross");
            UIKit.Place(cross.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(34, 34));

            fade = UIKit.Img(UIRoot.TopLayer, UIKit.White, new Color(0, 0, 0, 0), "Fade");
            UIKit.Fill(fade.rectTransform);
        }

        Image Bar(Transform p, string label, int row, Color c)
        {
            var l = UIKit.Txt(p, label, 16, UIKit.Muted, TextAlignmentOptions.MidlineLeft, false);
            UIKit.Place(l.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -124 - row * 22), new Vector2(110, 20));
            var bg = UIKit.Img(p, UIKit.Pill, new Color(0, 0, 0, 0.45f), "BarBg");
            UIKit.Place(bg.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(128, -127 - row * 22), new Vector2(290, 14));
            var f = UIKit.Img(bg.transform, UIKit.Pill, c, "Fill");
            f.type = Image.Type.Filled; f.fillMethod = Image.FillMethod.Horizontal;
            UIKit.Fill(f.rectTransform);
            return f;
        }

        public static void Toast(string msg, Color? c = null)
        {
            if (I == null) { Debug.Log("[Toast] " + msg); return; }
            Debug.Log("[Toast] " + msg);
            var t = UIKit.Txt(I.toastBox, msg, 30, c ?? Color.white, TextAlignmentOptions.Center);
            I.toasts.Insert(0, (t, Time.unscaledTime + 3.5f));
            if (I.toasts.Count > 4) { Destroy(I.toasts[4].t.gameObject); I.toasts.RemoveAt(4); }
        }

        public static void ChatLine(string who, string text, Color c)
        {
            if (I == null) return;
            var t = UIKit.Txt(I.chatBox, $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{who}:</color> {text}", 22, Color.white, TextAlignmentOptions.BottomLeft);
            t.richText = true;
            I.chatLines.Insert(0, (t, Time.unscaledTime));
            if (I.chatLines.Count > 6) { Destroy(I.chatLines[6].t.gameObject); I.chatLines.RemoveAt(6); }
        }

        /// <summary>Fade to black, run mid, fade back (sleeping, travelling).</summary>
        public static void FadeThrough(System.Action mid, float speed = 2.5f)
        {
            I.fadeMid = mid; I.fadeDir = speed; I.fadeT = 0;
        }

        public static void SetGps(Vector3 p, string name)
        {
            Gps = p; GpsName = name;
            if (I.gpsBeam != null) Destroy(I.gpsBeam);
            I.gpsBeam = new GameObject("GPS beam");
            Prim.Make(PrimitiveType.Cylinder, I.gpsBeam.transform, new Vector3(0, 40, 0), new Vector3(1.6f, 40, 1.6f), Mats.UnlitTransparent(new Color(1f, 0.85f, 0.2f, 0.3f)), "Beam");
            I.gpsBeam.transform.position = p;
            Toast("Метка GPS: " + name);
        }

        public static void ClearGps()
        {
            Gps = null;
            if (I.gpsBeam != null) Destroy(I.gpsBeam);
        }

        void Update()
        {
            var pl = PlayerCtl.I;
            if (pl == null) return;
            var d = Save.D;
            money.text = Money(d.money);
            clock.text = DayNight.ClockText() + "   " + Weather.Name;
            place.text = pl.Inside >= 0 && City.Get(pl.Inside) is Bld b ? b.name : City.DistrictAt(pl.transform.position) + (pl.Car != null ? "   " + Mathf.RoundToInt(Mathf.Abs(pl.Car.Speed) * 3.6f) + " км/ч" : "");
            foodBar.fillAmount = d.food / 100f;
            energyBar.fillAmount = d.energy / 100f;
            healthBar.fillAmount = d.health / 100f;
            online.text = "● в мире: " + (WorldNet.OnlineCount + 1);

            bool driving = pl.Car != null;
            action.gameObject.SetActive(pl.ActionLabel != null && UIRoot.Open == null);
            if (pl.ActionLabel != null) action.Set(pl.ActionLabel);
            carBtn.gameObject.SetActive(pl.CarLabel != null && UIRoot.Open == null);
            if (pl.CarLabel != null) carBtn.Set(pl.CarLabel);
            jump.gameObject.SetActive(!driving);
            run.gameObject.SetActive(!driving);
            brake.gameObject.SetActive(driving);
            bool riding = Taxi.Riding;
            gunBtn.gameObject.SetActive(Save.D.guns.Count > 0 && !driving && !riding);
            gunBtn.Bg.color = Guns.Armed ? new Color(0.85f, 0.55f, 0.15f, 0.95f) : new Color(0.3f, 0.3f, 0.35f, 0.9f);
            fireBtn.gameObject.SetActive(Guns.Armed && !driving && !riding && UIRoot.Open == null);
            cross.gameObject.SetActive(Guns.Armed && !driving && !riding);
            pl.BrakeHeld = driving && brake.Held;
            run.Bg.color = pl.RunHeld ? new Color(0.95f, 0.55f, 0.1f, 0.95f) : new Color(0.15f, 0.2f, 0.35f, 0.85f);

            // status bar
            string st = Work.Status();
            workBar.gameObject.SetActive(st != null);
            if (st != null)
            {
                status.text = st;
                workFill.transform.parent.gameObject.SetActive(Work.ShiftJob != null);
                workFill.fillAmount = Work.ShiftT;
            }

            // GPS / mission arrow
            Vector3? target = Work.MissionKind != 0 ? Work.Target : Gps;
            gps.gameObject.SetActive(target.HasValue && pl.Inside < 0);
            var me = pl.Car != null ? pl.Car.transform.position : pl.transform.position;
            if (target.HasValue && pl.Inside < 0)
            {
                var dir = target.Value - me; dir.y = 0;
                float ang = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg - pl.Yaw;
                arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -ang);
                gpsText.text = (Work.MissionKind == 0 ? GpsName + " · " : "") + Mathf.RoundToInt(dir.magnitude) + " м";
                if (Work.MissionKind == 0 && dir.magnitude < 6f) { ClearGps(); Toast("Вы на месте"); }
            }

            // minimap
            var uv = MapTex.UV(pl.Inside >= 0 && City.Get(pl.Inside) is Bld ib ? ib.door : me);
            float span = 0.12f;
            minimap.uvRect = new Rect(uv.x - span / 2, uv.y - span / 2, span, span);
            mapPlayer.rectTransform.localRotation = Quaternion.Euler(0, 0, -(pl.Car != null ? pl.Car.transform.eulerAngles.y : pl.transform.eulerAngles.y));
            mapTarget.gameObject.SetActive(target.HasValue);
            if (target.HasValue)
            {
                var tuv = MapTex.UV(target.Value);
                var r = minimap.rectTransform.rect;
                var off = new Vector2((tuv.x - uv.x) / span * r.width, (tuv.y - uv.y) / span * r.height);
                off = Vector2.ClampMagnitude(off, r.width / 2 - 8);
                mapTarget.rectTransform.anchoredPosition = off;
            }

            // toasts
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var (t, until) = toasts[i];
                float left = until - Time.unscaledTime;
                if (left <= 0) { Destroy(t.gameObject); toasts.RemoveAt(i); continue; }
                t.alpha = Mathf.Clamp01(left * 2);
                var rt = t.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1); rt.pivot = new Vector2(0.5f, 1);
                rt.sizeDelta = new Vector2(1000, 40);
                rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, new Vector2(0, -i * 44), Time.unscaledDeltaTime * 10);
            }
            for (int i = chatLines.Count - 1; i >= 0; i--)
            {
                var (t, at) = chatLines[i];
                float age = Time.unscaledTime - at;
                t.alpha = Mathf.Clamp01((40 - age) / 5f);
                var rt = t.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 0); rt.pivot = new Vector2(0, 0);
                rt.sizeDelta = new Vector2(560, 34);
                rt.anchoredPosition = new Vector2(0, i * 34);
            }

            // fade
            if (fadeDir != 0)
            {
                fadeT += Time.unscaledDeltaTime * fadeDir;
                if (fadeT >= 1f && fadeDir > 0) { fadeT = 1; fadeMid?.Invoke(); fadeMid = null; fadeDir = -Mathf.Abs(fadeDir) * 0.7f; }
                if (fadeT <= 0 && fadeDir < 0) { fadeT = 0; fadeDir = 0; }
                fade.color = new Color(0, 0, 0, Mathf.Clamp01(fadeT));
                fade.raycastTarget = fadeT > 0.05f;
            }
        }
    }

    /// <summary>The city map as a texture (minimap, full map window).</summary>
    public static class MapTex
    {
        public const float X0 = -1060, Z0 = -420, Span = 3200;   // the city, the lake in the west, the lighthouse and the airport
        static Texture2D tex;

        public static Vector2 UV(Vector3 p) => new Vector2((p.x - X0) / Span, (p.z - Z0) / Span);

        public static Texture2D Get()
        {
            if (tex != null) return tex;
            const int S = 800;
            tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Map" };
            var px = new Color32[S * S];
            float m = Span / S;
            Color grass = new Color(0.2f, 0.32f, 0.2f), sea = new Color(0.1f, 0.25f, 0.42f), sand = new Color(0.62f, 0.56f, 0.4f), road = new Color(0.32f, 0.33f, 0.36f),
                block = new Color(0.16f, 0.18f, 0.22f), mountain = new Color(0.36f, 0.3f, 0.26f), apron = new Color(0.4f, 0.4f, 0.42f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float wx = X0 + x * m, wz = Z0 + y * m;
                    Color c = grass;
                    if (wz < City.SeaZ) c = sea;
                    else if (wz < -City.Road) c = sand;
                    else if (wz > City.MountainZ) c = Color.Lerp(grass, mountain, Mathf.Clamp01((wz - City.MountainZ) / 200f));
                    if (wx > City.AirportX0 && wx < City.AirportX1 && wz > 7 * City.P && wz < City.AirportZ1) c = apron;
                    if (wx >= -City.Road && wx <= City.Size + City.Road && wz >= -City.Road && wz <= City.Size + City.Road)
                    {
                        float lx = Mathf.Repeat(wx, City.P), lz = Mathf.Repeat(wz, City.P);
                        bool isRoad = lx < City.Road || lx > City.P - City.Road || lz < City.Road || lz > City.P - City.Road || wx < 0 || wz < 0 || wx > City.Size || wz > City.Size;
                        if (isRoad) c = road;
                        else
                        {
                            var (bx, bz) = City.ChunkOf(new Vector3(wx, 0, wz));
                            c = bx >= 0 && bz >= 0 && bx < City.N && bz < City.N && City.Districts[bx, bz] == District.Park ? new Color(0.2f, 0.45f, 0.22f) : block;
                        }
                    }
                    if (Mathf.Abs(wx - City.NorthRoadX) < City.Road && wz > City.Size && wz < City.TunnelZ1 + 160) c = road;
                    if (wz > 7 * City.P - City.Road && wz < 7 * City.P + City.Road && wx > City.Size && wx < City.AirportX0 + 74) c = road;
                    if (wx > City.AirportX0 + 20 && wx < City.AirportX1 - 20 && wz > City.AirportZ1 - 90 && wz < City.AirportZ1 - 40) c = road;
                    px[y * S + x] = c;
                }
            // the lake and the roads out of town
            var lc = OutRoads.LakeCentre;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float wx = X0 + x * m, wz = Z0 + y * m;
                    float dl = Vector2.Distance(new Vector2(wx, wz), new Vector2(lc.x, lc.z));
                    if (dl < 175f) px[y * S + x] = sea; else if (dl < 188f) px[y * S + x] = sand;
                }
            foreach (var r in OutRoads.All)
                foreach (var p in r.pts)
                {
                    int cx = (int)((p.x - X0) / m), cz = (int)((p.z - Z0) / m);
                    for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++)
                    {
                        int xx = cx + dx, yy = cz + dy;
                        if (xx >= 0 && yy >= 0 && xx < S && yy < S) px[yy * S + xx] = road;
                    }
                }
            foreach (var b in City.All)
            {
                var col = Cat(b.type);
                int x0 = (int)((b.rect.xMin - X0) / m), x1 = (int)((b.rect.xMax - X0) / m), z0 = (int)((b.rect.yMin - Z0) / m), z1 = (int)((b.rect.yMax - Z0) / m);
                for (int y = z0; y <= z1; y++)
                    for (int x = x0; x <= x1; x++)
                        if (x >= 0 && y >= 0 && x < S && y < S) px[y * S + x] = col;
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        public static Color Cat(BType t) => t switch
        {
            BType.Apartment or BType.House or BType.Villa => new Color(0.55f, 0.6f, 0.68f),
            BType.Tower or BType.Office or BType.Bank => new Color(0.35f, 0.55f, 0.85f),
            BType.Shop or BType.Supermarket or BType.Mall or BType.Clothes or BType.Furniture or BType.GasStation => new Color(0.95f, 0.55f, 0.2f),
            BType.Restaurant or BType.Cafe or BType.Hotel => new Color(0.9f, 0.35f, 0.5f),
            BType.CarDealer or BType.Garage or BType.Parking => new Color(0.95f, 0.85f, 0.25f),
            BType.Hospital or BType.FireStation => new Color(0.95f, 0.25f, 0.25f),
            BType.School or BType.University or BType.Library => new Color(0.4f, 0.85f, 0.5f),
            BType.JobCenter => new Color(0.3f, 0.95f, 0.95f),
            BType.Plot => new Color(0.35f, 0.8f, 0.35f),
            _ => new Color(0.6f, 0.55f, 0.5f),
        };
    }
}
