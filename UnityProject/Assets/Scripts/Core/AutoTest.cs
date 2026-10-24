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
            StartCoroutine(Multiplayer ? Mp() : Boot.Has("-homes") ? Homes() : Run());
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
                bool ok = pl.Inside == b.id && Interiors.Points.Count >= 1;
                if (n < 12 || t == BType.Villa || t == BType.Tower || t == BType.Airport || t == BType.Clothes) yield return Shot($"10_in_{n:00}_{t}");
                if (t == BType.Clothes) { pl.Yaw = 30; pl.Pitch = 8; yield return Wait(0.3f); yield return Shot("11_clothes_racks"); pl.Yaw = 140; yield return Wait(0.3f); yield return Shot("11_clothes_fitting"); }
                Check("вход: " + BInfo.All[t].Title, ok && near == b, Interiors.Points.Count + " действий");
                Interiors.Exit();
                yield return null;
                n++;
            }

            // walk into a shop through its automatic doors, then walk out
            {
                var shop0 = City.Nearest(BType.Supermarket, pl.transform.position);
                pl.Teleport(shop0.door + shop0.FrontDir * 4f, 0);
                pl.Yaw = Quaternion.LookRotation(-shop0.FrontDir).eulerAngles.y;
                pl.Pitch = 10;
                yield return Wait(0.3f);
                pl.MoveInput = new Vector2(0, 1);
                yield return Wait(0.9f);
                yield return Shot("11_autodoor_open");
                float td = Time.realtimeSinceStartup;
                while (pl.Inside != shop0.id && Time.realtimeSinceStartup - td < 6f) yield return null;
                pl.MoveInput = Vector2.zero;
                Check("вход шагом через автодвери", pl.Inside == shop0.id);
                yield return Wait(0.8f);
                pl.Yaw = 180; pl.MoveInput = new Vector2(0, 1);
                td = Time.realtimeSinceStartup;
                while (pl.Inside >= 0 && Time.realtimeSinceStartup - td < 8f) yield return null;
                pl.MoveInput = Vector2.zero;
                Check("выход шагом через двери", pl.Inside < 0);
                yield return Wait(0.6f);
            }

            // lift: ride to the 3rd floor, five flats on the corridor, buy one
            var ap = City.Nearest(BType.Apartment, pl.transform.position);
            Interiors.Enter(ap);
            yield return Wait(0.5f);
            pl.Teleport(Interiors.O + new Vector3(Interiors.EX, 0.1f, Interiors.EZ - 3.2f), 0);
            pl.Yaw = 0; pl.Pitch = 8;
            yield return Wait(1.6f);
            yield return Shot("21_lift_open");
            pl.Teleport(Interiors.O + new Vector3(Interiors.EX, 0.1f, Interiors.EZ), 180);
            yield return Wait(0.3f);
            var lift = Elevator.I;
            Check("лифт есть и игрок в кабине", lift != null && lift.PlayerInside);
            if (lift != null)
            {
                lift.Go(2);
                yield return Wait(1.6f);
                PlayerCtl.ShotCam = true;
                PlayerCtl.ShotPos = lift.transform.TransformPoint(new Vector3(0.6f, 2.2f, 0.9f)); PlayerCtl.ShotLook = lift.transform.TransformPoint(new Vector3(-0.2f, 1.0f, -1.0f));
                yield return Wait(0.2f);
                yield return Shot("22_lift_riding");
                PlayerCtl.ShotCam = false;
                float tl = Time.realtimeSinceStartup;
                while (lift.Riding && Time.realtimeSinceStartup - tl < 10f) yield return null;
                Check("лифт приехал на 3 этаж", Interiors.Floor == 2 && !lift.Riding);
                pl.Teleport(Interiors.O + new Vector3(Interiors.EX, 2 * Elevator.FH + 0.1f, Interiors.EZ - 2.8f), 180);
                pl.Yaw = 150; pl.Pitch = 12; pl.Dist = 4;
                yield return Wait(1.2f);
                yield return Shot("23_corridor_5_flats");
                int sale = -1;
                for (int k = 1; k <= 5; k++) if (Interiors.FlatStatus(ap, 20 + k) == 1) { sale = 20 + k; break; }
                Check("на этаже есть квартира на продажу", sale > 0);
                if (sale > 0)
                {
                    long m0 = Save.D.money;
                    Eco.BuyHome(ap, sale);
                    yield return Wait(0.6f);
                    Check("покупка квартиры", Eco.OwnsHome(ap.id, sale) && Save.D.money < m0);
                    Eco.Home(ap.id, sale).decor.AddRange(new[] { "sofa:3", "tv:0", "plant:4", "rug:2", "lamp:5", "art:6", "table:9", "pc:6" });
                    Interiors.BuildLevel(2);
                    float xk = Interiors.EX + ((sale % 10) - 3) * 7f;
                    pl.Teleport(Interiors.O + new Vector3(xk, 2 * Elevator.FH + 0.1f, Interiors.EZ - 4.6f - 1.2f), 180);
                    pl.Yaw = 200; pl.Dist = 3.5f;
                    yield return Wait(1.0f);
                    yield return Shot("24_my_flat");
                    Save.D.energy = 20;
                    Menus.Sleep();
                    yield return Wait(2.5f);
                    Check("сон дома", Save.D.energy > 99);
                }
            }
            Interiors.Exit();
            yield return Wait(0.3f);

            // the 20-floor real-estate agency in the centre
            var ag = City.OfType(BType.Agency);
            Check("агентство недвижимости есть", ag.Count == 1 && ag[0].height > 70);
            if (ag.Count == 1)
            {
                var agb = ag[0];
                pl.Teleport(agb.door + agb.FrontDir * 30f + Vector3.up * 0.2f, 0);
                PlayerCtl.ShotCam = true;
                PlayerCtl.ShotPos = agb.door + agb.FrontDir * 70f + Vector3.up * 6f - Vector3.Cross(agb.FrontDir, Vector3.up) * 25f; PlayerCtl.ShotLook = agb.door + Vector3.up * 35f;
                yield return Wait(1.2f);
                yield return Shot("25_agency_tower");
                PlayerCtl.ShotCam = false;
                Interiors.Enter(agb);
                pl.Yaw = 0; pl.Pitch = 12; pl.Dist = 6;
                yield return Wait(1.0f);
                yield return Shot("26_agency_lobby");
                Check("лифт агентства на 20 этажей", Interiors.TopFloor(agb) == 19);
                pl.Teleport(Interiors.O + new Vector3(Interiors.EX, 0.1f, Interiors.EZ), 180);
                yield return Wait(0.2f);
                Elevator.I.Go(19);
                float ta = Time.realtimeSinceStartup;
                while (Elevator.I.Riding && Time.realtimeSinceStartup - ta < 12f) yield return null;
                pl.Teleport(Interiors.O + new Vector3(Interiors.EX, 19 * Elevator.FH + 0.1f, Interiors.EZ - 4f), 180);
                pl.Yaw = 180; pl.Dist = 6;
                yield return Wait(1.0f);
                yield return Shot("27_sky_lounge");
                Check("20 этаж: лаунж", Interiors.Floor == 19);
                Interiors.Exit();
            }

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
                pl.EnterCar(car, true);
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
                yield return Wait(1.6f);
                Check("выход из машины", pl.Car == null && pl.Body.gameObject.activeSelf);
            }

            // car gallery: every model on the airport apron, BMW G30 close-ups, door opening, seated driver
            {
                var basePos = new Vector3(City.AirportX0 + 260, 0.02f, 7 * City.P + 150);
                pl.Teleport(basePos + new Vector3(0, 0, -30), 0);
                yield return Wait(0.5f);
                var gallery = new List<Car>();
                for (int i = 0; i < CarDef.All.Count; i++)
                {
                    var c = Car.Spawn(CarDef.All[i], i % 10, basePos + new Vector3((i % 5) * 7f - 14f, 0, (i / 5) * 9f), 200, false, -1, 0);
                    gallery.Add(c);
                }
                PlayerCtl.ShotCam = true;
                PlayerCtl.ShotPos = basePos + new Vector3(-22, 7, -16); PlayerCtl.ShotLook = basePos + new Vector3(0, 0.5f, 4);
                yield return Wait(0.6f);
                yield return Shot("31_cars_all");
                foreach (var c in gallery) if (c != null) Destroy(c.gameObject);
                var bmw = Car.Spawn(CarDef.Get("bmw_g30"), 3, basePos, 0, false, -1, 0);
                Check("BMW G30 создана", bmw != null && CarDef.Get("bmw_g30").Spec.L > 4.9f);
                (Vector3 cam, string name)[] views =
                {
                    (new Vector3(-4.2f, 1.5f, 4.8f), "32_bmw_front34"), (new Vector3(4.5f, 1.3f, -4.6f), "33_bmw_rear34"),
                    (new Vector3(-5.8f, 1.0f, 0.2f), "34_bmw_side"), (new Vector3(0.3f, 1.2f, 4.2f), "35_bmw_front"),
                };
                foreach (var v in views)
                {
                    PlayerCtl.ShotPos = basePos + v.cam; PlayerCtl.ShotLook = basePos + new Vector3(0, 0.75f, 0);
                    yield return Wait(0.3f);
                    yield return Shot(v.name);
                }
                // walk up and get in: door opens, driver visible inside
                PlayerCtl.ShotCam = false;
                pl.Teleport(bmw.DoorStand + bmw.transform.right * -1.5f, 90);
                pl.Yaw = 60; pl.Pitch = 12; pl.Dist = 5;
                yield return Wait(0.4f);
                pl.CarPressed = true;
                yield return Wait(0.95f);
                yield return Shot("36_bmw_door_open");
                yield return Wait(1.2f);
                Check("посадка в машину с анимацией", pl.Car == bmw);
                PlayerCtl.ShotCam = true;
                PlayerCtl.ShotPos = basePos + new Vector3(-2.6f, 1.45f, 0.9f); PlayerCtl.ShotLook = basePos + new Vector3(-0.3f, 1.0f, -0.1f);
                yield return Wait(0.3f);
                yield return Shot("37_bmw_driver_inside");
                PlayerCtl.ShotCam = false;
                pl.ExitCar();
                yield return Wait(1.6f);
                Check("выход с анимацией", pl.Car == null && pl.Body.transform.parent == pl.transform);
                Destroy(bmw.gameObject);
            }

            // take a parked car
            pl.Teleport(new Vector3(6 * City.P + 30, City.Curb, 7 * City.P + 40), 0);
            yield return Wait(1.5f);
            var spot = Streamer.ParkedNear(pl.transform.position, 200f);
            Check("припаркованные машины есть", spot != null);
            if (spot != null)
            {
                pl.Teleport(spot.DoorStand, 0);
                yield return Wait(0.3f);
                pl.CarPressed = true;
                yield return Wait(2.2f);
                Check("сел в чужую припаркованную машину", pl.Car != null);
                if (pl.Car != null) { pl.ExitCar(); yield return Wait(1.6f); }
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
            // the city of the future from above: day and night
            pl.Teleport(new Vector3(6 * City.P + 8, City.Curb, 7 * City.P + 30), 0);
            PlayerCtl.ShotCam = true;
            PlayerCtl.ShotPos = new Vector3(2.5f * City.P, 55, 3.2f * City.P); PlayerCtl.ShotLook = new Vector3(7 * City.P, 30, 7.5f * City.P);
            DayNight.TestHour = 16.5f;
            yield return Wait(2.5f);
            yield return Shot("56_future_day");
            DayNight.TestHour = 22f;
            yield return Wait(2.5f);
            yield return Shot("57_future_night");
            PlayerCtl.ShotPos = new Vector3(6 * City.P + 8, 2.2f, 7 * City.P + 30); PlayerCtl.ShotLook = new Vector3(6 * City.P + 8, 20f, 9 * City.P);
            yield return Wait(1.5f);
            yield return Shot("58_street_night");
            Weather.TestState = 2; DayNight.TestHour = 18.5f;
            PlayerCtl.ShotPos = new Vector3(6 * City.P + 3, 1.8f, 7 * City.P + 20); PlayerCtl.ShotLook = new Vector3(6 * City.P + 1, 1.0f, 7 * City.P + 60);
            yield return Wait(3f);
            yield return Shot("59_rain_wet");
            Weather.TestState = 0; DayNight.TestHour = 12f;
            PlayerCtl.ShotCam = false;
            // close-ups of homes from the street (daylight)
            DayNight.TestHour = 15.5f;
            foreach (var (bt, nm) in new[] { (BType.Villa, "70_villa"), (BType.House, "71_house"), (BType.Apartment, "72_flats"), (BType.Shop, "73_shop"), (BType.Tower, "74_tower") })
            {
                var hb = City.Nearest(bt, new Vector3(7 * City.P, 0, 3 * City.P));
                var fd = hb.FrontDir; var sd = Vector3.Cross(Vector3.up, fd);
                float far = bt == BType.Tower ? 45f : bt == BType.Apartment ? 26f : 17f;
                pl.Teleport(hb.door + fd * 6f, 0);
                PlayerCtl.ShotCam = true;
                PlayerCtl.ShotPos = hb.door + fd * far + sd * far * 0.55f + Vector3.up * (bt == BType.Tower ? 3f : 2.2f);
                PlayerCtl.ShotLook = hb.door + Vector3.up * (bt == BType.Tower ? 25f : bt == BType.Apartment ? 8f : 3.5f) - sd * 2f;
                yield return Wait(1.6f);
                yield return Shot(nm);
            }
            PlayerCtl.ShotCam = false; DayNight.TestHour = 12f;

            // windows
            pl.Teleport(City.Spawn, 0);
            yield return Wait(0.5f);
            Menus.Phone(); yield return Wait(0.4f); yield return Shot("60_phone"); Window.Close();
            Menus.Map(); yield return Wait(0.4f); yield return Shot("61_map"); Window.Close();
            Menus.Dress(false, true); yield return Wait(5f); yield return Shot("62_create"); Window.Close();
            Menus.CarDealer(dealer); yield return Wait(0.4f); yield return Shot("63_dealer"); Window.Close();
            Menus.Skins(); yield return Wait(0.4f); yield return Shot("64_skins"); Window.Close();
            int bad = 0;
            foreach (var p in Menus.SkinPresets)
                foreach (var id in new[] { p.look.hHair, p.look.hTop, p.look.hBottom, p.look.hShoes, p.look.hAcc, p.look.hSkin, p.look.hEyes, p.look.hMouth })
                    if (Wardrobe.Find(id) == null) { bad++; Debug.Log("[AutoTest] missing skin item " + id + " in " + p.name); }
            Check("все предметы скинов существуют", bad == 0, bad + " отсутствует");
            // face shapes close up
            {
                int fi = 0;
                foreach (var fid in new[] { "hface_oval", "hface_square", "hface_heart" })
                {
                    var look = new SkinSet { hFace = fid };
                    pl.Body.Rebuild(look);
                    pl.Yaw = 180; pl.Pitch = 0; pl.Dist = 1.1f;
                    yield return Wait(0.5f);
                    yield return Shot($"65_face_{fi++}_{fid}");
                }
                Check("формы лица в каталоге", Wardrobe.OfSlot(Wardrobe.Slot.HFace).Count >= 5, Wardrobe.OfSlot(Wardrobe.Slot.HFace).Count + " форм");
            }
            Save.D.skin = Menus.SkinPresets[4].look.Clone();
            pl.Body.Rebuild(Save.D.skin);
            pl.Yaw = 180; pl.Pitch = 6; pl.Dist = 3.2f;
            yield return Wait(0.6f);
            yield return Shot("65_skin_cyber");
            Save.D.skin = Menus.SkinPresets[3].look.Clone();
            pl.Body.Rebuild(Save.D.skin);
            yield return Wait(0.6f);
            yield return Shot("66_skin_boss_f");
            Check("сеть: подключение", WorldNet.Connected);

            float fps = 0; int frames = 0; float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 2f) { frames++; yield return null; }
            fps = frames / (Time.realtimeSinceStartup - t0);
            Check("FPS (Mac)", fps > 20, fps.ToString("0"));
            Finish();
        }

        IEnumerator Homes()
        {
            var pl = PlayerCtl.I;
            Weather.TestState = 0;
            float hour = float.TryParse(Boot.Arg("-hour"), out var hh) ? hh : 15.5f;
            DayNight.TestHour = hour;
            yield return Wait(2f);
            foreach (var (bt, nm) in new[] { (BType.Villa, "70_villa"), (BType.House, "71_house"), (BType.Apartment, "72_flats"), (BType.Shop, "73_shop"), (BType.Tower, "74_tower"), (BType.Hotel, "75_hotel") })
            {
                var hb = City.Nearest(bt, new Vector3(7 * City.P, 0, 3 * City.P));
                var fd = hb.FrontDir; var sd = Vector3.Cross(Vector3.up, fd);
                float far = bt == BType.Tower ? 45f : bt == BType.Apartment || bt == BType.Hotel ? 26f : 17f;
                pl.Teleport(hb.door + fd * 6f, 0);
                PlayerCtl.ShotCam = true;
                PlayerCtl.ShotPos = hb.door + fd * far + sd * far * 0.55f + Vector3.up * (bt == BType.Tower ? 3f : 2.2f);
                PlayerCtl.ShotLook = hb.door + Vector3.up * (bt == BType.Tower ? 25f : bt == BType.Apartment || bt == BType.Hotel ? 8f : 3.5f) - sd * 2f;
                yield return Wait(1.6f);
                yield return Shot(nm);
            }
            PlayerCtl.ShotCam = true;
            PlayerCtl.ShotPos = new Vector3(2.5f * City.P, 55, 3.2f * City.P); PlayerCtl.ShotLook = new Vector3(7 * City.P, 30, 7.5f * City.P);
            yield return Wait(2f);
            yield return Shot("76_aerial");
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
