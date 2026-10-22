using System.Collections.Generic;
using UnityEngine;

namespace Gad
{
    /// <summary>
    /// City life around the player: cars driving on the right-hand lanes of the road grid
    /// (turning at crossroads, slowing behind obstacles) and pedestrians walking the sidewalks.
    /// </summary>
    public class Traffic : MonoBehaviour
    {
        class Driver
        {
            public Car car;
            public Vector2Int a, b, next;     // grid nodes
            public float t;                    // 0..1 along a→b straight part, then turning
            public float speed, cruise;
            public bool turning;
            public float turnT;
        }

        class Walker
        {
            public Human h;
            public int bx, bz;
            public float s;                    // position along the sidewalk loop (metres)
            public float dir, speed;
        }

        static Traffic I;
        readonly List<Driver> drivers = new List<Driver>();
        readonly List<Walker> walkers = new List<Walker>();
        Transform root;
        const float Lane = 3f, TurnIn = 9f;

        public static void Create()
        {
            var go = new GameObject("Traffic");
            I = go.AddComponent<Traffic>();
            I.root = go.transform;
        }

        public static void SetVisible(bool on)
        {
            if (I == null) return;
            I.root.gameObject.SetActive(on);
            foreach (var d in I.drivers) if (d.car != null) d.car.gameObject.SetActive(on);
        }

        static Vector3 Node(Vector2Int n) => new Vector3(n.x * City.P, 0.02f, n.y * City.P);
        static bool Valid(Vector2Int n) => n.x >= 0 && n.y >= 0 && n.x <= City.N && n.y <= City.N;

        void Update()
        {
            var pl = PlayerCtl.I;
            if (pl == null || !Streamer.Outdoor) return;
            var me = pl.Car != null ? pl.Car.transform.position : pl.transform.position;
            float dt = Time.deltaTime;

            // spawn / despawn cars
            for (int i = drivers.Count - 1; i >= 0; i--)
            {
                var d = drivers[i];
                if (d.car == null || (d.car.transform.position - me).sqrMagnitude > 320f * 320f) { if (d.car) Destroy(d.car.gameObject); drivers.RemoveAt(i); }
            }
            if (drivers.Count < Gfx.TrafficCars && City.InCity(me) || drivers.Count < Gfx.TrafficCars / 2) TrySpawnCar(me);
            foreach (var d in drivers) Drive(d, dt, me);

            // pedestrians
            for (int i = walkers.Count - 1; i >= 0; i--)
            {
                var w = walkers[i];
                if (w.h == null || (w.h.transform.position - me).sqrMagnitude > 170f * 170f) { if (w.h) Destroy(w.h.gameObject); walkers.RemoveAt(i); }
            }
            if (walkers.Count < Gfx.Pedestrians) TrySpawnWalker(me);
            foreach (var w in walkers) Walk(w, dt);
        }

        void TrySpawnCar(Vector3 me)
        {
            var c = new Vector2Int(Mathf.RoundToInt(me.x / City.P), Mathf.RoundToInt(me.z / City.P));
            var a = c + new Vector2Int(Random.Range(-3, 4), Random.Range(-3, 4));
            if (!Valid(a)) return;
            var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            var b = a + dirs[Random.Range(0, 4)];
            if (!Valid(b)) return;
            var d = new Driver { a = a, b = b, t = Random.Range(0.15f, 0.85f) };
            var pos = LanePos(d.a, d.b, d.t);
            float dist = (pos - me).magnitude;
            if (dist < 50f || dist > 260f) return;
            foreach (var o in drivers) if ((o.car.transform.position - pos).sqrMagnitude < 15f * 15f) return;
            int model = Random.value < 0.15f ? 2 : Random.Range(0, 7);
            if (model == 2 && Random.value > 0.5f) model = 1;
            var def = CarDef.All[Mathf.Min(model, CarDef.All.Count - 1)];
            d.car = Car.Spawn(def, Random.Range(0, Palette.Car.Length), pos, 0, true);
            d.car.transform.SetParent(root, true);
            d.cruise = Random.Range(9f, 13.5f);
            d.speed = d.cruise;
            d.next = PickNext(d.a, d.b);
            d.car.transform.rotation = Quaternion.LookRotation(Node(d.b) - Node(d.a));
            d.car.SetHeadlights(false);
            drivers.Add(d);
        }

        static Vector3 Right(Vector2Int a, Vector2Int b)
        {
            var f = (Node(b) - Node(a)).normalized;
            return new Vector3(f.z, 0, -f.x);
        }

        static Vector3 LanePos(Vector2Int a, Vector2Int b, float t)
        {
            var A = Node(a); var B = Node(b);
            var f = (B - A).normalized;
            var start = A + f * TurnIn; var end = B - f * TurnIn;
            return Vector3.Lerp(start, end, t) + Right(a, b) * Lane;
        }

        Vector2Int PickNext(Vector2Int a, Vector2Int b)
        {
            var f = b - a;
            var options = new List<Vector2Int> { b + f, b + new Vector2Int(f.y, -f.x), b + new Vector2Int(-f.y, f.x) };
            options.RemoveAll(n => !Valid(n));
            if (options.Count == 0) return a; // dead end: U-turn
            // prefer going straight
            if (Valid(b + f) && Random.value < 0.55f) return b + f;
            return options[Random.Range(0, options.Count)];
        }

        void Drive(Driver d, float dt, Vector3 me)
        {
            var tr = d.car.transform;
            // obstacle ahead? (player, player's car, other cars, people)
            float want = d.cruise;
            var fwd = tr.forward;
            var pos = tr.position;
            var pl = PlayerCtl.I;
            float Block(Vector3 p)
            {
                var v = p - pos; v.y = 0;
                float ahead = Vector3.Dot(v, fwd);
                float side = Mathf.Abs(Vector3.Dot(v, tr.right));
                return ahead > 0 && ahead < 14f && side < 2.2f ? ahead : 999f;
            }
            float near = Block(pl.Car != null ? pl.Car.transform.position : pl.transform.position);
            foreach (var o in drivers) if (o != d && o.car != null) near = Mathf.Min(near, Block(o.car.transform.position));
            foreach (var c in Car.Live) if (!c.Traffic && c != pl.Car) near = Mathf.Min(near, Block(c.transform.position));
            foreach (var r in WorldNet.Remotes.Values) near = Mathf.Min(near, Block(r.transform.position));
            if (near < 14f) want = Mathf.Clamp((near - 5.5f) * 1.5f, 0, d.cruise);
            d.speed = Mathf.MoveTowards(d.speed, want, (want < d.speed ? 14f : 4f) * dt);
            float step = d.speed * dt;

            if (!d.turning)
            {
                float len = City.P - 2 * TurnIn;
                d.t += step / len;
                if (d.t >= 1f) { d.turning = true; d.turnT = 0; }
                var p = LanePos(d.a, d.b, Mathf.Min(1, d.t));
                tr.rotation = Quaternion.LookRotation(Node(d.b) - Node(d.a));
                Place(d, p, step);
            }
            else
            {
                // quadratic curve through the crossroads into the next street
                var B = Node(d.b);
                var f1 = (B - Node(d.a)).normalized;
                var f2 = (Node(d.next) - B).normalized;
                var p0 = B - f1 * TurnIn + Right(d.a, d.b) * Lane;
                var p2 = B + f2 * TurnIn + Right(d.b, d.next) * Lane;
                bool uturn = Vector3.Dot(f1, f2) < -0.5f;
                var p1 = uturn ? B + f1 * 4f : B + Right(d.a, d.b) * Lane + Right(d.b, d.next) * Lane + (Vector3.Dot(f1, f2) > 0.5f ? Vector3.zero : Vector3.zero);
                if (Vector3.Dot(f1, f2) > 0.5f) p1 = (p0 + p2) / 2;
                float approxLen = Vector3.Distance(p0, p1) + Vector3.Distance(p1, p2);
                d.turnT += step / Mathf.Max(1f, approxLen);
                float t = Mathf.Min(1, d.turnT);
                var p = (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * p1 + t * t * p2;
                var tan = 2 * (1 - t) * (p1 - p0) + 2 * t * (p2 - p1);
                if (tan.sqrMagnitude > 0.001f) tr.rotation = Quaternion.LookRotation(tan.normalized);
                d.car.Steer = Mathf.Clamp(Vector3.SignedAngle(f1, f2, Vector3.up) / 90f, -1, 1) * (1 - Mathf.Abs(t - 0.5f) * 2);
                Place(d, p, step);
                if (d.turnT >= 1f)
                {
                    d.a = d.b; d.b = d.next; d.next = PickNext(d.a, d.b);
                    d.turning = false; d.t = 0; d.car.Steer = 0;
                }
            }
        }

        static void Place(Driver d, Vector3 p, float step)
        {
            p.y = 0.02f;
            d.car.transform.position = p;
            d.car.SpinWheels(step);
        }

        // ------------------------------------------------------------------ pedestrians

        static float Loop => 4 * (City.P - 16f);

        static Vector3 WalkPos(int bx, int bz, float s, out Vector3 dir)
        {
            float side = City.P - 16f;
            s = Mathf.Repeat(s, Loop);
            float x0 = bx * City.P + 8f, z0 = bz * City.P + 8f;
            int seg = (int)(s / side); float u = s - seg * side;
            switch (seg)
            {
                case 0: dir = Vector3.right; return new Vector3(x0 + u, City.Curb, z0);
                case 1: dir = Vector3.forward; return new Vector3(x0 + side, City.Curb, z0 + u);
                case 2: dir = Vector3.left; return new Vector3(x0 + side - u, City.Curb, z0 + side);
                default: dir = Vector3.back; return new Vector3(x0, City.Curb, z0 + side - u);
            }
        }

        void TrySpawnWalker(Vector3 me)
        {
            var (cx, cz) = City.ChunkOf(me);
            int bx = cx + Random.Range(-1, 2), bz = cz + Random.Range(-1, 2);
            if (bx < 0 || bz < 0 || bx >= City.N || bz >= City.N) return;
            var w = new Walker { bx = bx, bz = bz, s = Random.Range(0, Loop), dir = Random.value < 0.5f ? 1 : -1, speed = Random.Range(1.1f, 1.6f) };
            var p = WalkPos(bx, bz, w.s, out _);
            if ((p - me).sqrMagnitude < 20f * 20f) return;
            var look = new Look { gender = Random.Range(0, 2), skin = Random.Range(0, 5), hair = Random.Range(0, 6), hairStyle = Random.Range(0, 4), shirt = Random.Range(0, 14), pants = Random.Range(0, 14), shoes = Random.Range(0, 14), top = Random.Range(0, 4), hat = Random.value < 0.2f ? 1 : 0, height = Random.Range(0.93f, 1.06f) };
            w.h = Human.Create(root, look, "Pedestrian");
            w.h.transform.position = p;
            walkers.Add(w);
        }

        void Walk(Walker w, float dt)
        {
            // stop for the player standing in the way
            var pl = PlayerCtl.I;
            var cur = WalkPos(w.bx, w.bz, w.s, out var dir);
            dir *= w.dir;
            var toPl = pl.transform.position - cur; toPl.y = 0;
            bool blocked = Vector3.Dot(toPl, dir) > 0 && Vector3.Dot(toPl, dir) < 1.6f && Vector3.Cross(dir, toPl).magnitude < 0.8f;
            float sp = blocked ? 0 : w.speed;
            w.s += sp * dt * w.dir;
            var p = WalkPos(w.bx, w.bz, w.s, out var d2);
            w.h.transform.position = p;
            var want = Quaternion.LookRotation(d2 * w.dir);
            w.h.transform.rotation = Quaternion.RotateTowards(w.h.transform.rotation, want, 360 * dt);
            w.h.Speed = sp;
        }
    }
}
