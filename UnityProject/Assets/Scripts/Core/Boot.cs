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
                Save.D.look = new Look { gender = Has("-female") ? 1 : 0, hairStyle = Has("-female") ? 1 : 0, shirt = 2, pants = 1, shoes = 0, top = 1, hair = 1 };
            }

            UIRoot.Create();
            City.Build();
            WorldBuilder.Build();
            DayNight.Create();
            Weather.Create();
            AudioFx.Create();
            PlayerCtl.Create();
            Gfx.Init();
            Streamer.Create();
            Traffic.Create();
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
            else if (!Save.D.created) Menus.Wardrobe(false, true);
            else Hud.Toast("С возвращением, " + Save.D.name + "!", UIKit.Cyan);
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
            if (Input.GetKeyDown(KeyCode.Escape)) { if (UIRoot.Open != null) Window.Close(); else Menus.Phone(); }
            if (Input.GetKeyDown(KeyCode.M) && !UIRoot.TypingText) { if (UIRoot.Open != null) Window.Close(); else Menus.Map(); }
            if (Input.GetKeyDown(KeyCode.Return) && !UIRoot.TypingText && UIRoot.Open == null) Menus.Chat();
        }

        void OnApplicationPause(bool paused) { if (paused && !Has("-autotest")) Save.Write(); }
        void OnApplicationQuit() { if (!Has("-autotest") && !Has("-mptest")) Save.Write(); }
    }
}
