using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Gad
{
    [Serializable]
    public class Look
    {
        public int gender;          // 0 man, 1 woman
        public int skin = 1, hair = 0, hairStyle = 0, shirt = 3, pants = 12, shoes = 1, top = 0; // top: 0 t-shirt, 1 jacket, 2 hoodie, 3 suit
        public int hat = 0;         // 0 none, 1 cap, 2 beanie
        public float height = 1f;

        public string Pack() => $"{gender},{skin},{hair},{hairStyle},{shirt},{pants},{shoes},{top},{hat},{(int)(height * 100)}";

        public static Look Unpack(string s)
        {
            var l = new Look();
            if (string.IsNullOrEmpty(s)) return l;
            var p = s.Split(',');
            int I(int i) => i < p.Length && int.TryParse(p[i], out var v) ? v : 0;
            l.gender = I(0); l.skin = I(1); l.hair = I(2); l.hairStyle = I(3); l.shirt = I(4); l.pants = I(5); l.shoes = I(6); l.top = I(7); l.hat = I(8);
            l.height = Mathf.Clamp(I(9) / 100f, 0.9f, 1.1f);
            if (l.height < 0.9f) l.height = 1f;
            return l;
        }

        public Look Clone() => Unpack(Pack());
    }

    [Serializable]
    public class OwnedCar
    {
        public string model;
        public int color;
        public int tune;            // engine tuning level 0..3
        public float health = 100;
    }

    [Serializable]
    public class OwnedHome
    {
        public int building;        // building id
        public int unit;            // flat number (apartments/towers), 0 for houses
        public List<string> decor = new List<string>(); // "slot:style"
    }

    [Serializable]
    public class OwnedBiz
    {
        public int building;
        public int level = 1;
        public double lastCollect;  // unix seconds
    }

    [Serializable]
    public class GameData
    {
        public int version = 2;
        public string id;
        public string name = "";
        public Look look = new Look();
        public SkinSet skin;
        public List<string> skins = new List<string>();   // bought skin presets
        public long money = 5000;
        public long bank = 0;
        public float food = 80, energy = 90, health = 100;
        public int edu;             // 0 none, 1 school, 2 university
        public float eduProgress;   // 0..1 towards the next level
        public string job = "";
        public int jobBuilding = -1;
        public int shifts;
        public List<OwnedCar> cars = new List<OwnedCar>();
        public List<OwnedHome> homes = new List<OwnedHome>();
        public List<OwnedBiz> biz = new List<OwnedBiz>();
        public List<string> wardrobe = new List<string>();
        public float px = 0, py = 0, pz = 0, pyaw = 0;
        public bool created;
        public int music = 1;
        public int quality = -1;    // -1 auto
        public double bankLast;
    }

    public static class Save
    {
        public static GameData D;
        static string Path => System.IO.Path.Combine(Application.persistentDataPath, Boot.SaveName);
        static float lastSave;

        public static void Load()
        {
            try
            {
                if (File.Exists(Path)) D = JsonUtility.FromJson<GameData>(File.ReadAllText(Path));
            }
            catch (Exception e) { Debug.LogWarning("[Save] load failed: " + e.Message); }
            if (D == null) D = new GameData();
            if (string.IsNullOrEmpty(D.id)) D.id = Guid.NewGuid().ToString("N").Substring(0, 10);
            if (D.look == null) D.look = new Look();
            if (D.version < 2)
            {
                foreach (var h in D.homes)
                    if (h.unit > 0 && h.unit < 10) h.unit = ((h.unit - 1) / 5 + 1) * 10 + (h.unit - 1) % 5 + 1;
                D.version = 2;
            }
            if (D.skin == null || string.IsNullOrEmpty(D.skin.hGender)) D.skin = D.created ? Person.FromOld(D.look) : new SkinSet();
        }

        public static void Write()
        {
            try
            {
                var tmp = Path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(D));
                if (File.Exists(Path)) File.Delete(Path);
                File.Move(tmp, Path);
                WebBridge.SyncFiles();
            }
            catch (Exception e) { Debug.LogWarning("[Save] write failed: " + e.Message); }
            lastSave = Time.unscaledTime;
        }

        public static void Tick()
        {
            if (Time.unscaledTime - lastSave > 15f) Write();
        }

        public static double Now => (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
    }
}
