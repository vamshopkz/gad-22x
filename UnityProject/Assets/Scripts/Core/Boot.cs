using System;
using UnityEngine;

namespace Gad
{
    /// <summary>Entry point (the only object in the scene): builds the whole game from code.</summary>
    public class Boot : MonoBehaviour
    {
        public static string SaveName = "gad22x_save.json";
        public static string[] Args = Array.Empty<string>();

        public static string Arg(string key)
        {
            int i = Array.IndexOf(Args, key);
            return i >= 0 && i + 1 < Args.Length ? Args[i + 1] : null;
        }
        public static bool Has(string key) => Array.IndexOf(Args, key) >= 0;

        void Awake()
        {
            Args = Environment.GetCommandLineArgs();
            if (Has("-autotest") || Has("-mptest")) SaveName = "test_" + (Arg("-name") ?? "auto") + ".json";
            if (Arg("-save") != null) SaveName = Arg("-save");
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Save.Load();
            if (Has("-autotest") || Has("-mptest"))
            {
                Save.D = new GameData { id = Save.D.id, created = true, name = Arg("-name") ?? "Тестер", money = 900000 };
                Save.D.skin = Has("-female") ? new SkinSet { hGender = "hg_f", hHair = "hh_long_brown", hEyes = "he_lashes", hMouth = "hm_lips", hTop = "ht_hoodie_pink", hBottom = "hb_jeans_dark" } : new SkinSet { hHair = "hh_short_black", hTop = "ht_flannel_red", hBottom = "hb_jeans_dark", hShoes = "hf_sneakers_white" };
            }

            UIRoot.Create();
            City.Build();
            WorldBuilder.Build();
            FutureCity.Create();
            DayNight.Create();
            Weather.Create();
            Reflections.Create();
            RoomCubes.Build();
            AudioFx.Create();
            PlayerCtl.Create();
            Gfx.Init();
            PostFx.Create();
            Streamer.Create();
            HouseWorld.Create();
            InPlace.Create();
            Traffic.Create();
            Guns.Create();
            OutRoads.Traffic();
            new GameObject("Work").AddComponent<Work>();
            WorldNet.Create();
            Hud.Create();

            var pl = PlayerCtl.I;
            var saved = new Vector3(Save.D.px, Save.D.py, Save.D.pz);
            if (Save.D.created && saved.sqrMagnitude > 1 && saved.y > -50) pl.Teleport(saved + Vector3.up * 0.3f, Save.D.pyaw);
            else pl.Teleport(City.Spawn, 0);
            Streamer.ForceNear();

            if (Has("-autotest")) gameObject.AddComponent<AutoTest>();
            else if (Has("-mptest")) gameObject.AddComponent<AutoTest>().Multiplayer = true;
            else MainMenu.Show();
        }

        void Update()
        {
            Eco.Tick(Time.deltaTime);
            var pl = PlayerCtl.I;
            if (pl != null && pl.Inside < 0 && pl.Car == null)
            {
                var p = pl.transform.position;
                Save.D.px = p.x; Save.D.py = p.y; Save.D.pz = p.z; Save.D.pyaw = pl.transform.eulerAngles.y;
            }
            if (!Has("-autotest") && !Has("-mptest")) Save.Tick();
            if (Input.GetKeyDown(KeyCode.Escape)) { if (UIRoot.Open != null) Window.Close(); else if (PhoneUI.Open) PhoneUI.Close(); else if (!MainMenu.Open) PhoneUI.Show(); }
            if (Input.GetKeyDown(KeyCode.M) && !UIRoot.TypingText && !MainMenu.Open) { if (UIRoot.Open != null) Window.Close(); else Menus.Map(); }
            if (Input.GetKeyDown(KeyCode.Return) && !UIRoot.TypingText && UIRoot.Open == null && !MainMenu.Open) Menus.Chat();
        }

        void OnApplicationPause(bool paused) { if (paused && !Has("-autotest")) Save.Write(); }
        void OnApplicationQuit() { if (!Has("-autotest") && !Has("-mptest")) Save.Write(); }
    }
}
