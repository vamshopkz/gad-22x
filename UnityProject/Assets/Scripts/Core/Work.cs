using UnityEngine;

namespace Gad
{
    /// <summary>
    /// Jobs in action: a shift inside the building (stay at work while the bar fills up), courier
    /// deliveries (take the parcel to a door marked on the map) and taxi rides (drive a passenger).
    /// </summary>
    public class Work : MonoBehaviour
    {
        public static Work I;
        public static JobDef ShiftJob;
        public static float ShiftT;
        public static Bld ShiftAt;

        public static int MissionKind;          // 0 none, 1 courier, 2 taxi pickup, 3 taxi drive
        public static Vector3 Target;
        public static string TargetLabel;
        public static Bld TargetBld;
        public static JobDef MissionJob;
        static float missionStartDist;
        static GameObject beam;

        public static void StartShift(JobDef job, Bld b)
        {
            if (Save.D.energy < 10) { Hud.Toast("Слишком устали — сначала поспите"); return; }
            ShiftJob = job; ShiftAt = b; ShiftT = 0;
            PlayerCtl.I.Body.Pose = 1;
            Hud.Toast("Смена началась: " + job.Title + ". Оставайтесь в здании!");
        }

        public static void Cancel(string why)
        {
            if (ShiftJob == null) return;
            ShiftJob = null;
            if (PlayerCtl.I != null) PlayerCtl.I.Body.Pose = 0;
            Hud.Toast(why);
        }

        public static void StartMission(JobDef job)
        {
            MissionJob = job;
            if (job.Kind == 2)
            {
                if (PlayerCtl.I.Car == null && PlayerCtl.I.MyCar == null && Save.D.cars.Count == 0)
                {
                    Hud.Toast("Для такси нужна своя машина — купите в автосалоне");
                    MissionJob = null;
                    return;
                }
                NextTaxi();
            }
            else NextDelivery();
        }

        public static void StopMission()
        {
            MissionKind = 0; MissionJob = null; TargetBld = null;
            if (beam != null) Destroy(beam);
            Hud.Toast("Задание завершено");
        }

        static Bld RandomDoor(Vector3 from, float min, float max)
        {
            for (int i = 0; i < 60; i++)
            {
                var b = City.All[Random.Range(0, City.All.Count)];
                float d = Vector3.Distance(b.door, from);
                if (d > min && d < max && b.cx >= 0) return b;
            }
            return City.All[Random.Range(0, City.All.Count)];
        }

        static void NextDelivery()
        {
            var from = PlayerCtl.I.Inside >= 0 && City.Get(PlayerCtl.I.Inside) is Bld cur ? cur.door : PlayerCtl.I.transform.position;
            TargetBld = RandomDoor(from, 150, 600);
            Target = TargetBld.door;
            TargetLabel = "Доставьте посылку: " + TargetBld.name + ", " + TargetBld.address;
            MissionKind = 1;
            missionStartDist = Vector3.Distance(from, Target);
            PlaceBeam(new Color(1f, 0.8f, 0.2f));
            Hud.Toast(TargetLabel);
        }

        static void NextTaxi()
        {
            var p = PlayerCtl.I.transform.position;
            var near = p + new Vector3(Random.Range(-250f, 250f), 0, Random.Range(-250f, 250f));
            near.x = Mathf.Clamp(near.x, 20, City.Size - 20); near.z = Mathf.Clamp(near.z, 20, City.Size - 20);
            Target = City.NearestRoad(near, out _) ;
            Target.y = 0;
            TargetLabel = "Заберите пассажира (метка на карте)";
            MissionKind = 2;
            PlaceBeam(new Color(0.3f, 0.9f, 1f));
            Hud.Toast(TargetLabel);
        }

        static void PlaceBeam(Color c)
        {
            if (beam != null) Destroy(beam);
            beam = new GameObject("Mission marker");
            var col = c; col.a = 0.35f;
            Prim.Make(PrimitiveType.Cylinder, beam.transform, new Vector3(0, 30, 0), new Vector3(2.2f, 30, 2.2f), Mats.UnlitTransparent(col), "Beam");
            Prim.Make(PrimitiveType.Cylinder, beam.transform, new Vector3(0, 0.05f, 0), new Vector3(4f, 0.02f, 4f), Mats.Glow(c), "Ring").AddComponent<Pulse>();
            beam.transform.position = Target + Vector3.up * (City.InCity(Target) ? 0.01f : 0f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var pl = PlayerCtl.I;
            if (pl == null) return;
            if (ShiftJob != null)
            {
                if (pl.Inside != ShiftAt.id) { Cancel("Смена прервана: вы ушли с работы"); }
                else
                {
                    ShiftT += dt / ShiftJob.Seconds;
                    pl.Body.Pose = pl.PlanarSpeed > 0.5f ? 0 : 1;
                    if (ShiftT >= 1f)
                    {
                        var j = ShiftJob;
                        ShiftJob = null;
                        pl.Body.Pose = 0;
                        Save.D.shifts++;
                        Save.D.energy = Mathf.Max(0, Save.D.energy - 8);
                        long pay = j.Pay + Mathf.Min(10, Save.D.shifts / 10) * j.Pay / 20;
                        Eco.Earn(pay, "зарплата: " + j.Title);
                    }
                }
            }
            if (MissionKind != 0)
            {
                var p = pl.Car != null ? pl.Car.transform.position : pl.transform.position;
                var d = Target - p; d.y = 0;
                if (MissionKind == 1 && d.magnitude < 3.5f && pl.Inside < 0)
                {
                    long pay = MissionJob.Pay + (long)(missionStartDist * 0.4f);
                    Save.D.shifts++;
                    Eco.Earn(pay, "доставка выполнена");
                    NextDelivery();
                }
                else if (MissionKind == 2 && d.magnitude < 6f && pl.Car != null && Mathf.Abs(pl.Car.Speed) < 2f)
                {
                    var dest = RandomDoor(p, 250, 700);
                    TargetBld = dest;
                    Target = City.NearestRoad(dest.door, out _);
                    missionStartDist = Vector3.Distance(p, Target);
                    TargetLabel = "Отвезите пассажира: " + dest.address;
                    MissionKind = 3;
                    PlaceBeam(new Color(0.3f, 1f, 0.4f));
                    Hud.Toast("Пассажир сел. " + TargetLabel);
                    AudioFx.Play("door");
                }
                else if (MissionKind == 3 && d.magnitude < 8f && pl.Car != null && Mathf.Abs(pl.Car.Speed) < 2f)
                {
                    long pay = MissionJob.Pay + (long)(missionStartDist * 0.8f);
                    Save.D.shifts++;
                    Eco.Earn(pay, "оплата поездки");
                    NextTaxi();
                }
            }
        }

        public static string Status()
        {
            if (ShiftJob != null) return "Смена: " + ShiftJob.Title;
            if (MissionKind != 0) return TargetLabel;
            return null;
        }
    }
}
