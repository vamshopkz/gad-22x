using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Gad
{
    /// <summary>
    /// Self-test for Mac builds: `-autotest -shots DIR` walks through the main features, takes
    /// screenshots and writes DIR/autotest.txt (PASS/FAIL per check). `-mptest -name X -shots DIR`
    /// on two instances checks that players see each other through the public relays.
    /// </summary>
    public class AutoTest : MonoBehaviour
    {
        public bool Multiplayer;
        string dir;
        readonly List<string> report = new List<string>();
        int fails;

        void Start()
        {
            dir = Boot.Arg("-shots") ?? Path.Combine(Application.persistentDataPath, "shots");
            Directory.CreateDirectory(dir);
            StartCoroutine(Multiplayer ? Mp() : Run());
        }

        void Check(string name, bool ok, string info = "")
        {
            report.Add((ok ? "PASS " : "FAIL ") + name + (info != "" ? " — " + info : ""));
            if (!ok) fails++;
            Debug.Log("[AutoTest] " + report[report.Count - 1]);
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            yield return null;
            yield return null;
        }

        IEnumerator Wait(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }

        void Finish()
        {
            report.Add($"ИТОГО: {report.Count - fails} ок, {fails} ошибок");
            File.WriteAllLines(Path.Combine(dir, Multiplayer ? "mptest_" + Save.D.name + ".txt" : "autotest.txt"), report);
            Application.Quit();
        }

        IEnumerator Run()
        {
            var pl = PlayerCtl.I;
            DayNight.TestHour = 13f;
            Weather.TestState = 0;
            yield return Wait(2.5f);
            pl.Yaw = 30; pl.Pitch = 12;
            yield return Wait(0.5f);
            yield return Shot("01_street");
            Check("город построен", City.All.Count > 300, City.All.Count + " зданий");
            Check("детали подгружены", Streamer.LoadedCount > 5, Streamer.LoadedCount + " кварталов");

            // walking
            var p0 = pl.transform.position;
            pl.MoveInput = new Vector2(0, 1);
            yield return Wait(1.5f);
            pl.MoveInput = Vector2.zero;
            Check("ходьба", (pl.transform.position - p0).magnitude > 2f, (pl.transform.position - p0).magnitude.ToString("0.0") + " м");

            // high view of downtown
            pl.Teleport(new Vector3(5 * City.P + 30, City.Curb, 5 * City.P + 40), 45);
            pl.Pitch = 20; pl.Dist = 14;
            yield return Wait(1.5f);
            yield return Shot("02_downtown");
            pl.Dist = 5.2f;

            // every kind of building can be entered
            var types = new[] { BType.Supermarket, BType.Restaurant, BType.Cafe, BType.CarDealer, BType.Garage, BType.Mall, BType.Hospital, BType.Office, BType.School, BType.Apartment, BType.Factory, BType.Bank, BType.Hotel, BType.Parking, BType.Airport, BType.Library, BType.FireStation, BType.Gym, BType.Warehouse, BType.Villa, BType.Tower, BType.Station, BType.Port, BType.JobCenter, BType.Clothes, BType.Furniture, BType.GasStation, BType.House, BType.University, BType.Shop };
            int n = 0;
            foreach (var t in types)
            {
                var b = City.Nearest(t, pl.transform.position);
                if (b == null) { Check("здание " + t, false, "нет в городе"); continue; }
                pl.Teleport(b.door + b.FrontDir * 1.5f, 0);
                yield return null;
                var near = City.DoorNear(pl.transform.position, 3.2f);
                Interiors.Enter(b);
                yield return Wait(0.6f);
                pl.Yaw = 0; pl.Pitch = 18;
                yield return Wait(0.3f);
                bool ok = pl.Inside == b.id && Interiors.Points.Count >= 2;
                if (n < 12 || t == BType.Villa || t == BType.Tower || t == BType.Airport) yield return Shot($"10_in_{n:00}_{t}");
                Check("вход: " + BInfo.All[t].Title, ok && near == b, Interiors.Points.Count + " действий");
                Interiors.Exit();
                yield return null;
                n++;
            }

            // buy a flat and decorate it
            var ap = City.Nearest(BType.Apartment, pl.transform.position);
            long m0 = Save.D.money;
            Eco.BuyHome(ap, 3);
            yield return Wait(0.5f);
            Check("покупка квартиры", Eco.OwnsHome(ap.id, 3) && Save.D.money < m0 && pl.Inside == ap.id);
            Eco.Spend(0);
            Eco.Home(ap.id, 3).decor.AddRange(new[] { "sofa:3", "tv:0", "plant:4", "rug:2", "lamp:5", "art:6", "table:9", "pc:6" });
            Interiors.Enter(ap, 3);
            yield return Wait(0.6f);
            yield return Shot("20_my_flat");
            Save.D.energy = 20;
            Menus.Sleep();
            yield return Wait(2.5f);
            Check("сон дома", Save.D.energy > 99);
            Interiors.Exit();

            // car: buy, drive, crash test
            var dealer = City.Nearest(BType.CarDealer, pl.transform.position);
            Interiors.Enter(dealer);
            yield return Wait(0.3f);
            Save.D.cars.Add(new OwnedCar { model = "sport", color = 0 });
            Interiors.Exit();
            var car = pl.CallCar(Save.D.cars.Count - 1, pl.transform.position);
            Check("машина вызвана", car != null);
            yield return Wait(0.3f);
            if (car != null)
            {
                pl.EnterCar(car);
                var c0 = car.transform.position;
                pl.MoveInput = new Vector2(0, 1);
                yield return Wait(2.5f);
                yield return Shot("30_driving");
                Check("езда", (car.transform.position - c0).magnitude > 15f, (car.transform.position - c0).magnitude.ToString("0") + " м, " + (car.Speed * 3.6f).ToString("0") + " км/ч");
                pl.MoveInput = new Vector2(0.6f, 1);
                yield return Wait(1.5f);
                pl.MoveInput = Vector2.zero;
                pl.BrakeHeld = true;
                yield return Wait(2f);
                pl.BrakeHeld = false;
                pl.ExitCar();
                Check("выход из машины", pl.Car == null && pl.Body.gameObject.activeSelf);
            }

            // job shift
            var shop = City.Nearest(BType.Supermarket, pl.transform.position);
            Interiors.Enter(shop);
            var job = JobDef.Get("cashier");
            Save.D.job = job.Id;
            long m1 = Save.D.money;
            job.Seconds = 1.5f;
            Work.StartShift(job, shop);
            yield return Wait(2.5f);
            Check("рабочая смена", Save.D.money > m1, "+" + (Save.D.money - m1));
            yield return Shot("40_work");
            Interiors.Exit();

            // business income
            var cafe = City.Nearest(BType.Cafe, pl.transform.position);
            Eco.BuyBiz(cafe);
            var z = Eco.Biz(cafe.id);
            Check("покупка бизнеса", z != null);
            if (z != null)
            {
                z.lastCollect -= 600;
                long m2 = Save.D.money;
                Eco.CollectAll();
                Check("доход бизнеса", Save.D.money > m2, "+" + (Save.D.money - m2));
            }

            // courier mission
            Save.D.job = "courier";
            Work.StartMission(JobDef.Get("courier"));
            Check("задание курьера", Work.MissionKind == 1 && Work.TargetBld != null);
            long m3 = Save.D.money;
            pl.Teleport(Work.Target + Vector3.back * 1f, 0);
            yield return Wait(0.5f);
            Check("доставка", Save.D.money > m3);
            Work.StopMission();

            // night + rain + fog, the tunnel, the airport, the beach
            pl.Teleport(new Vector3(6 * City.P + 8, City.Curb, 7 * City.P + 30), 20);
            pl.Dist = 7; pl.Pitch = 10;
            DayNight.TestHour = 22.5f;
            yield return Wait(2f);
            yield return Shot("50_night");
            Weather.TestState = 2;
            DayNight.TestHour = 15f;
            yield return Wait(2.5f);
            yield return Shot("51_rain");
            Weather.TestState = 3;
            yield return Wait(2f);
            yield return Shot("52_fog");
            Weather.TestState = 0;
            DayNight.TestHour = 12f;
            pl.Teleport(new Vector3(City.NorthRoadX + 3, 0.1f, City.TunnelZ0 - 30), 0);
            yield return Wait(1.5f);
            yield return Shot("53_tunnel");
            pl.Teleport(new Vector3(City.AirportX0 + 70, 0.1f, 7 * City.P + 10), 90);
            yield return Wait(1.5f);
            yield return Shot("54_airport");
            pl.Teleport(new Vector3(8 * City.P, 0.1f, -40), 180);
            pl.Pitch = 8;
            yield return Wait(1.5f);
            yield return Shot("55_beach");

            // windows
            pl.Teleport(City.Spawn, 0);
            yield return Wait(0.5f);
            Menus.Phone(); yield return Wait(0.4f); yield return Shot("60_phone"); Window.Close();
            Menus.Map(); yield return Wait(0.4f); yield return Shot("61_map"); Window.Close();
            Menus.Wardrobe(false, true); yield return Wait(0.4f); yield return Shot("62_create"); Window.Close();
            Menus.CarDealer(dealer); yield return Wait(0.4f); yield return Shot("63_dealer"); Window.Close();
            Check("сеть: подключение", WorldNet.Connected);

            float fps = 0; int frames = 0; float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 2f) { frames++; yield return null; }
            fps = frames / (Time.realtimeSinceStartup - t0);
            Check("FPS (Mac)", fps > 20, fps.ToString("0"));
            Finish();
        }

        IEnumerator Mp()
        {
            var pl = PlayerCtl.I;
            DayNight.TestHour = 12;
            pl.Teleport(City.Spawn + new Vector3(Boot.Has("-female") ? 3 : -3, 0, 2), Boot.Has("-female") ? 270 : 90);
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 45f && WorldNet.Remotes.Count == 0) yield return null;
            Check("видим другого игрока", WorldNet.Remotes.Count > 0, (Time.realtimeSinceStartup - t0).ToString("0") + " с");
            WorldNet.SendChat("Привет от " + Save.D.name);
            yield return Wait(4f);
            pl.Yaw = Boot.Has("-female") ? 270 : 90;
            yield return Wait(1f);
            yield return Shot("mp_" + Save.D.name);
            Check("чат получен", WorldNet.ChatLog.Count >= 2, WorldNet.ChatLog.Count + " сообщений");
            yield return Wait(3f);
            Finish();
        }
    }
}
