using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gad
{
    /// <summary>
    /// 0.8: a real taxi ride. Ordered from the phone: the fare for the whole way is paid up front, a
    /// yellow taxi with a driver comes along the roads (GPS shows it and the waiting time), you walk to
    /// it and get in beside the driver, it drives you there, and you can get out at any moment — the
    /// part of the fare you did not ride is given back.
    /// </summary>
    public class Taxi : MonoBehaviour
    {
        public static Taxi I;
        public static bool Riding => I != null && I.state == State.Ride;
        public static bool Active => I != null && I.state != State.Done;
        public static bool Waiting => I != null && I.state == State.Waiting;
        /// <summary>For tests and the action button: get in now.</summary>
        public void BoardNow() { if (state == State.Waiting) StartCoroutine(Board()); }
        public const float PerMetre = 0.1f, MinFare = 20f;

        enum State { Coming, Waiting, Boarding, Ride, Done }
        State state;
        Car car;
        List<Vector3> path;
        int wp;
        float total, travelled, stuck, leaveT;
        long fare;
        string destName;
        List<Vector3> ridePath;
        Transform seat;
        WorldActions.Owner door;

        // ------------------------------------------------------------------ routes along the road grid

        /// <summary>A drivable route from a to b: along the grid roads in the right-hand lane.</summary>
        public static List<Vector3> Route(Vector3 a, Vector3 b)
        {
            // 0.9: out to the lake or the lighthouse: through town to the start of that road, then along its curves
            var tail = OutRoads.TailTo(b);
            if (tail != null && OutRoads.TailTo(a) == null)
            {
                var r = Route(a, tail[0]);
                for (int i = 0; i < tail.Count; i += 2)
                {
                    var d = (tail[Mathf.Min(tail.Count - 1, i + 1)] - tail[Mathf.Max(0, i - 1)]).normalized;
                    r.Add(tail[i] + new Vector3(d.z, 0, -d.x) * 2.8f + Vector3.up * 0.05f);
                }
                return r;
            }
            float P = City.P;
            Vector3 Snap(Vector3 p, out Vector3 cross)
            {
                City.NearestRoad(p, out float yaw);
                float gx = Mathf.Clamp(Mathf.Round(p.x / P) * P, 0, City.Size), gz = Mathf.Clamp(Mathf.Round(p.z / P) * P, 0, City.Size);
                Vector3 on;
                if (yaw == 0) on = new Vector3(gx, 0, Mathf.Clamp(p.z, 0, City.Size));
                else on = new Vector3(Mathf.Clamp(p.x, 0, City.Size), 0, gz);
                cross = new Vector3(Mathf.Round(on.x / P) * P, 0, Mathf.Round(on.z / P) * P);
                return on;
            }
            var A = Snap(a, out var ia);
            var B = Snap(b, out var ib);
            var c = new List<Vector3> { A, ia, new Vector3(ib.x, 0, ia.z), ib, B };
            if (Vector2.Distance(new Vector2(B.x, B.z), new Vector2(b.x, b.z)) > 25f) c.Add(new Vector3(b.x, 0, b.z));   // the last bit out of town
            // drop repeats and points in a straight line
            var pts = new List<Vector3>();
            foreach (var p in c)
                if (pts.Count == 0 || (p - pts[pts.Count - 1]).sqrMagnitude > 0.5f) pts.Add(p);
            for (int i = pts.Count - 2; i >= 1; i--)
            {
                var d0 = (pts[i] - pts[i - 1]).normalized; var d1 = (pts[i + 1] - pts[i]).normalized;
                if (Vector3.Dot(d0, d1) > 0.999f) pts.RemoveAt(i);
            }
            // keep to the right-hand lane, 3 m from the middle of the road
            var lane = new List<Vector3>();
            for (int i = 0; i < pts.Count; i++)
            {
                // the right of the way in plus the right of the way out: at a corner that is the corner of the lane
                var off = Vector3.zero;
                if (i > 0) { var d = (pts[i] - pts[i - 1]).normalized; off += new Vector3(d.z, 0, -d.x) * 3f; }
                if (i < pts.Count - 1) { var d = (pts[i + 1] - pts[i]).normalized; off += new Vector3(d.z, 0, -d.x) * 3f; }
                lane.Add(pts[i] + off + Vector3.up * 0.05f);
            }
            return lane;
        }

        public static float Length(List<Vector3> p) { float s = 0; for (int i = 1; i < p.Count; i++) s += Vector3.Distance(p[i - 1], p[i]); return s; }

        public static long FareTo(Vector3 to)
        {
            var pl = PlayerCtl.I;
            if (pl == null) return 0;
            return (long)Mathf.Max(MinFare, Mathf.Round(Length(Route(pl.transform.position, to)) * PerMetre));
        }

        // ------------------------------------------------------------------ ordering

        public static void Order(Vector3 to, string name)
        {
            var pl = PlayerCtl.I;
            if (pl == null) return;
            if (pl.Inside >= 0) { Hud.Toast("Выйдите на улицу — такси приедет к дороге"); return; }
            if (pl.Car != null) { Hud.Toast("Сначала выйдите из машины"); return; }
            if (I != null) I.Finish(true);
            long f = FareTo(to);
            if (!Eco.Spend(f, "Такси")) return;
            I = new GameObject("Taxi ride").AddComponent<Taxi>();
            I.fare = f; I.destName = name;
            I.destination = to;
            I.StartCoroutine(I.Run(to));
        }

        IEnumerator Run(Vector3 to)
        {
            var pl = PlayerCtl.I;
            var pick = City.NearestRoad(pl.transform.position, out _);
            // the taxi starts a couple of blocks away
            var from = pick + new Vector3(Random.value < 0.5f ? -150f : 150f, 0, Random.value < 0.5f ? -110f : 110f);
            from.x = Mathf.Clamp(from.x, 20f, City.Size - 20f); from.z = Mathf.Clamp(from.z, 20f, City.Size - 20f);
            path = Route(from, pick);
            var dir = path.Count > 1 ? path[1] - path[0] : Vector3.forward;
            car = Car.Spawn(CarDef.Get("taxi"), 5, path[0], Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, false, -1, 0);
            car.name = "Taxi";
            car.Driver = Person.Create(car.Seat, Person.Random(new Rng((uint)Random.Range(1, int.MaxValue))), "TaxiDriver");
            car.Driver.Pose = 4; car.Driver.transform.localPosition = Vector3.zero;
            seat = new GameObject("PassengerSeat").transform;
            seat.SetParent(car.transform, false);
            seat.localPosition = new Vector3(-car.Seat.localPosition.x, car.Seat.localPosition.y, car.Seat.localPosition.z);
            wp = 1;
            state = State.Coming;
            Hud.Toast("Такси вызвано! Оплачено " + Hud.Money(fare) + ". Ждите у дороги", new Color(1f, 0.85f, 0.2f));
            while (state == State.Coming)
            {
                if (car == null) { Save.D.money += fare; Hud.Toast("Такси не приедет — деньги вернули"); Finish(true); yield break; }
                Hud.Gps = car.transform.position;
                float left = Length(path.GetRange(wp - 1, path.Count - wp + 1));
                Hud.GpsName = "Такси · " + Mathf.CeilToInt(left / 11f) + " с";
                if (Drive(Time.deltaTime, 12f)) state = State.Waiting;
                yield return null;
            }
            // arrived: wait with the passenger door open-ish
            car.Speed = 0;
            AudioFx.Play("door");
            Hud.Toast("Такси приехало — подойдите и садитесь", new Color(1f, 0.85f, 0.2f));
            door = car.gameObject.AddComponent<WorldActions.Owner>();
            door.points.Add(new WorldActions.Point { Label = "СЕСТЬ В ТАКСИ", Act = () => { if (state == State.Waiting) StartCoroutine(Board()); } });
            WorldActions.Register(door);
            float waited = 0;
            while (state == State.Waiting)
            {
                door.points[0].Pos = SideStand();
                Hud.Gps = car.transform.position; Hud.GpsName = "Такси ждёт";
                waited += Time.deltaTime;
                if (waited > 180f) { Hud.Toast("Такси уехало без вас — деньги вернули"); Save.D.money += fare; Finish(true); yield break; }
                yield return null;
            }
        }

        Vector3 SideStand() => car.transform.TransformPoint(new Vector3(car.Dims.W / 2f + 0.7f, 0f, car.Seat.localPosition.z)) + Vector3.up * 0.2f;

        /// <summary>Walk to the passenger door, open it, sit down beside the driver.</summary>
        IEnumerator Board()
        {
            state = State.Boarding;
            var pl = PlayerCtl.I;
            pl.Frozen = true;
            pl.CC.enabled = false;
            var from = pl.transform.position; var to = SideStand(); to.y = from.y;
            float dur = Mathf.Clamp(Vector3.Distance(from, to) / 2.2f, 0.3f, 3f);
            var face = Quaternion.LookRotation(-car.transform.right);
            for (float t = 0; t < 1; t += Time.deltaTime / dur)
            {
                var step = Vector3.Lerp(from, to, t);
                var look = (to - from); look.y = 0;
                pl.transform.SetPositionAndRotation(step, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : pl.transform.rotation);
                pl.Body.Speed = 2.2f;
                yield return null;
            }
            for (float t = 0; t < 1; t += Time.deltaTime / 0.25f) { pl.transform.rotation = Quaternion.Slerp(pl.transform.rotation, face, t); yield return null; }
            pl.Body.Speed = 0;
            AudioFx.Play("door");
            yield return new WaitForSeconds(0.3f);
            var body = pl.Body.transform;
            body.SetParent(seat, true);
            var sp = body.localPosition; var sr = body.localRotation;
            for (float t = 0; t < 1; t += Time.deltaTime / 0.6f)
            {
                body.localPosition = Vector3.Lerp(sp, Vector3.zero, t);
                body.localRotation = Quaternion.Slerp(sr, Quaternion.identity, t);
                if (t > 0.4f) pl.Body.Pose = 4;
                yield return null;
            }
            body.localPosition = Vector3.zero; body.localRotation = Quaternion.identity;
            AudioFx.Play("door");
            Destroy(door); door = null;
            pl.Frozen = false;
            // the ride
            ridePath = Route(car.transform.position, destination);
            ridePath.Insert(0, car.transform.position);
            path = ridePath; wp = 1;
            total = Mathf.Max(1f, Length(path)); travelled = 0;
            state = State.Ride;
            Hud.Toast("Поехали: " + destName + ". Выйти можно в любой момент", new Color(1f, 0.85f, 0.2f));
            while (state == State.Ride)
            {
                Hud.Gps = path[path.Count - 1]; Hud.GpsName = destName;
                if (Drive(Time.deltaTime, 15f)) { Arrive(); yield break; }
                yield return null;
            }
        }
        Vector3 destination;

        /// <summary>Follow the path; true when the end is reached.</summary>
        bool Drive(float dt, float cruise)
        {
            if (wp >= path.Count) { car.Speed = 0; return true; }
            var pos = car.transform.position;
            var target = path[wp]; target.y = pos.y;
            var to = target - pos;
            float dist = to.magnitude;
            bool last = wp == path.Count - 1;
            float turn = wp < path.Count - 1 ? Vector3.Angle(path[wp] - path[wp - 1], path[wp + 1] - path[wp]) : 0f;
            float want = cruise;
            if (last) want = Mathf.Min(cruise, Mathf.Max(2f, dist * 0.8f));
            else if (turn > 30f && dist < 22f) want = Mathf.Min(cruise, 6.5f + dist * 0.3f);
            car.Speed = Mathf.MoveTowards(car.Speed, want, (car.Speed < want ? 5f : 9f) * dt);
            float yawErr = Vector3.SignedAngle(car.transform.forward, to, Vector3.up);
            float yawRate = Mathf.Clamp(yawErr * 2.2f, -75f, 75f);
            // the taxi is driven along its route by script: it never gets stuck on a lamp post or a stopped car
            var before = car.transform.position;
            var rot = Quaternion.Euler(0, car.transform.eulerAngles.y + yawRate * dt, 0);
            var np = before + rot * Vector3.forward * car.Speed * dt;
            np.y = Mathf.Lerp(before.y, Car.GroundY(np, before.y), Mathf.Clamp01(dt * 12f));
            car.transform.SetPositionAndRotation(np, rot);
            car.SpinWheels(car.Speed * dt);
            float moved = Vector3.Distance(before, np);
            if (state == State.Ride) { travelled += moved; PlayerCtl.I.transform.position = car.transform.position; }
            if (dist < (last ? 1.2f : 4f)) wp++;
            return wp >= path.Count;
        }

        void Arrive()
        {
            Hud.Toast("Приехали! " + destName, new Color(0.5f, 1f, 0.6f));
            GetOut(false);
        }

        /// <summary>Get out now (the player pressed the button): pay only for the part ridden.</summary>
        public void ExitNow()
        {
            if (state != State.Ride) return;
            float part = Mathf.Clamp01(travelled / total);
            long used = (long)Mathf.Max(MinFare, Mathf.Round(fare * part));
            long back = Mathf.Max(0, (int)(fare - used));
            if (back > 0) { Save.D.money += back; Save.Write(); }
            Hud.Toast($"Поездка {Mathf.RoundToInt(travelled)} м: {Hud.Money(used)}. Возврат {Hud.Money(back)}", new Color(1f, 0.85f, 0.2f));
            GetOut(true);
        }

        void GetOut(bool early)
        {
            state = State.Done;
            car.Speed = 0;
            var pl = PlayerCtl.I;
            var body = pl.Body.transform;
            body.SetParent(pl.transform, false);
            body.localPosition = Vector3.zero; body.localRotation = Quaternion.identity;
            pl.Body.Pose = 0;
            var stand = SideStand();
            pl.Teleport(stand + car.transform.right * 0.6f, car.transform.eulerAngles.y + 90f);
            AudioFx.Play("door");
            Hud.Gps = null;
            leaveT = 0;
            StartCoroutine(DriveOff());
        }

        IEnumerator DriveOff()
        {
            yield return new WaitForSeconds(0.8f);
            path = new List<Vector3> { car.transform.position, car.transform.position + car.transform.forward * 200f };
            wp = 1;
            while (leaveT < 14f) { leaveT += Time.deltaTime; Drive(Time.deltaTime, 12f); yield return null; }
            Finish(false);
        }

        void Finish(bool refund)
        {
            state = State.Done;
            if (car != null) Destroy(car.gameObject);
            if (I == this) I = null;
            if (Hud.GpsName != null && Hud.GpsName.StartsWith("Такси")) Hud.Gps = null;
            Destroy(gameObject);
        }

        public static Car CarOf => I != null ? I.car : null;

        // set by Order before the coroutine needs it
        public static void SetDestination(Vector3 d) { if (I != null) I.destination = d; }
    }
}
