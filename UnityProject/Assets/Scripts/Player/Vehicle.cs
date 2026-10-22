using System.Collections.Generic;
using UnityEngine;

namespace Gad
{
    /// <summary>Car bodies built from boxes; the same geometry for live cars and parked decorations.</summary>
    public static class CarModel
    {
        public struct Dims { public float L, W, bodyY, bodyH, cabL, cabOff, cabH, wheelR; }

        public static Dims Of(int shape) => shape switch
        {
            0 => new Dims { L = 3.8f, W = 1.75f, bodyY = 0.32f, bodyH = 0.75f, cabL = 2.1f, cabOff = -0.3f, cabH = 0.66f, wheelR = 0.32f },
            2 => new Dims { L = 4.8f, W = 2.0f, bodyY = 0.45f, bodyH = 0.95f, cabL = 2.9f, cabOff = -0.35f, cabH = 0.78f, wheelR = 0.42f },
            3 => new Dims { L = 4.5f, W = 1.95f, bodyY = 0.28f, bodyH = 0.6f, cabL = 1.9f, cabOff = -0.25f, cabH = 0.5f, wheelR = 0.35f },
            4 => new Dims { L = 5.3f, W = 2.0f, bodyY = 0.48f, bodyH = 0.9f, cabL = 1.9f, cabOff = 0.75f, cabH = 0.78f, wheelR = 0.42f },
            5 => new Dims { L = 5.0f, W = 2.0f, bodyY = 0.4f, bodyH = 0.95f, cabL = 3.9f, cabOff = -0.45f, cabH = 1.25f, wheelR = 0.38f },
            6 => new Dims { L = 4.6f, W = 2.05f, bodyY = 0.25f, bodyH = 0.55f, cabL = 1.7f, cabOff = -0.15f, cabH = 0.45f, wheelR = 0.36f },
            _ => new Dims { L = 4.6f, W = 1.85f, bodyY = 0.32f, bodyH = 0.7f, cabL = 2.4f, cabOff = -0.15f, cabH = 0.6f, wheelR = 0.34f },
        };

        static Material glass, rubber, chrome, lightF, lightR, dark;
        static void Init()
        {
            if (glass != null) return;
            glass = Mats.Lit(new Color(0.08f, 0.1f, 0.13f), 0.95f, 0.2f);
            rubber = Mats.Lit(new Color(0.06f, 0.06f, 0.06f), 0.1f);
            chrome = Mats.Lit(new Color(0.75f, 0.76f, 0.78f), 0.8f, 0.9f);
            lightF = Mats.Glow(new Color(1f, 0.97f, 0.85f));
            lightR = Mats.Glow(new Color(0.85f, 0.05f, 0.05f));
            dark = Mats.Lit(new Color(0.12f, 0.12f, 0.13f), 0.3f);
        }

        static void B(MeshB m, Matrix4x4 xf, Material mat, Vector3 c, Vector3 s) =>
            m.Add(Prim.MeshOf(PrimitiveType.Cube), xf * Matrix4x4.TRS(c, Quaternion.identity, s), mat);

        /// <summary>Body (no wheels) into a mesh builder, local origin = ground under the car centre, +z forward.</summary>
        public static void Body(MeshB m, Matrix4x4 xf, int shape, Color color, bool taxi = false)
        {
            Init();
            var d = Of(shape);
            var paint = Mats.Lit(color, 0.75f, 0.3f);
            float by = d.bodyY + d.bodyH / 2;
            B(m, xf, paint, new Vector3(0, by, 0), new Vector3(d.W, d.bodyH, d.L));
            // hood slope hint + bumpers
            B(m, xf, dark, new Vector3(0, d.bodyY + 0.12f, d.L / 2 + 0.05f), new Vector3(d.W * 0.98f, 0.24f, 0.14f));
            B(m, xf, dark, new Vector3(0, d.bodyY + 0.12f, -d.L / 2 - 0.05f), new Vector3(d.W * 0.98f, 0.24f, 0.14f));
            // cabin: dark glass with a painted roof
            float cy = d.bodyY + d.bodyH + d.cabH / 2;
            B(m, xf, glass, new Vector3(0, cy, d.cabOff), new Vector3(d.W * 0.86f, d.cabH, d.cabL));
            B(m, xf, paint, new Vector3(0, d.bodyY + d.bodyH + d.cabH + 0.04f, d.cabOff), new Vector3(d.W * 0.88f, 0.08f, d.cabL * 0.92f));
            // pillars
            foreach (float sx in new[] { -1f, 1f })
                B(m, xf, paint, new Vector3(sx * d.W * 0.43f, cy, d.cabOff), new Vector3(0.06f, d.cabH, 0.12f));
            if (shape == 4)
            {
                // pickup bed walls
                float bz = d.cabOff - d.cabL / 2 - 1.3f;
                foreach (float sx in new[] { -1f, 1f })
                    B(m, xf, paint, new Vector3(sx * (d.W / 2 - 0.06f), d.bodyY + d.bodyH + 0.25f, bz), new Vector3(0.1f, 0.5f, 2.4f));
                B(m, xf, paint, new Vector3(0, d.bodyY + d.bodyH + 0.25f, -d.L / 2 + 0.06f), new Vector3(d.W, 0.5f, 0.1f));
            }
            if (shape == 3 || shape == 6)
            {
                // spoiler
                B(m, xf, dark, new Vector3(0, d.bodyY + d.bodyH + 0.3f, -d.L / 2 + 0.25f), new Vector3(d.W * 0.9f, 0.06f, 0.35f));
                foreach (float sx in new[] { -0.6f, 0.6f })
                    B(m, xf, dark, new Vector3(sx, d.bodyY + d.bodyH + 0.15f, -d.L / 2 + 0.25f), new Vector3(0.06f, 0.3f, 0.2f));
            }
            if (taxi)
            {
                B(m, xf, Mats.Glow(new Color(1f, 0.85f, 0.2f)), new Vector3(0, d.bodyY + d.bodyH + d.cabH + 0.2f, d.cabOff), new Vector3(0.7f, 0.22f, 0.3f));
            }
            // lights
            foreach (float sx in new[] { -1f, 1f })
            {
                B(m, xf, lightF, new Vector3(sx * d.W * 0.36f, by + 0.1f, d.L / 2 + 0.01f), new Vector3(d.W * 0.2f, 0.14f, 0.04f));
                B(m, xf, lightR, new Vector3(sx * d.W * 0.38f, by + 0.1f, -d.L / 2 - 0.01f), new Vector3(d.W * 0.18f, 0.12f, 0.04f));
                // mirrors
                B(m, xf, paint, new Vector3(sx * (d.W / 2 + 0.1f), d.bodyY + d.bodyH + 0.12f, d.cabOff + d.cabL / 2 - 0.1f), new Vector3(0.18f, 0.12f, 0.1f));
            }
            // grille
            B(m, xf, chrome, new Vector3(0, by, d.L / 2 + 0.01f), new Vector3(d.W * 0.36f, 0.16f, 0.03f));
        }

        public static IEnumerable<Vector3> WheelPositions(int shape)
        {
            var d = Of(shape);
            float wz = d.L / 2 - d.wheelR - 0.35f;
            foreach (float sz in new[] { 1f, -1f })
                foreach (float sx in new[] { -1f, 1f })
                    yield return new Vector3(sx * (d.W / 2 - 0.12f), d.wheelR, sz * wz);
        }

        public static void Wheels(MeshB m, Matrix4x4 xf, int shape)
        {
            Init();
            var d = Of(shape);
            foreach (var w in WheelPositions(shape))
            {
                m.Add(Prim.MeshOf(PrimitiveType.Cylinder), xf * Matrix4x4.TRS(w, Quaternion.Euler(0, 0, 90), new Vector3(d.wheelR * 2, 0.13f, d.wheelR * 2)), rubber);
                m.Add(Prim.MeshOf(PrimitiveType.Cylinder), xf * Matrix4x4.TRS(w + new Vector3(Mathf.Sign(w.x) * 0.05f, 0, 0), Quaternion.Euler(0, 0, 90), new Vector3(d.wheelR * 1.1f, 0.1f, d.wheelR * 1.1f)), chrome);
            }
        }

        /// <summary>A live car object: body mesh + 4 separate wheels (they spin and steer).</summary>
        public static GameObject Build(int shape, Color color, bool taxi, out Transform[] wheels)
        {
            Init();
            var root = new GameObject("Car");
            var mb = new MeshB();
            Body(mb, Matrix4x4.identity, shape, color, taxi);
            mb.Build("Body", root.transform, 9, true);
            var d = Of(shape);
            var list = new List<Transform>();
            foreach (var w in WheelPositions(shape))
            {
                var pivot = new GameObject("Wheel").transform;
                pivot.SetParent(root.transform, false);
                pivot.localPosition = w;
                var wm = new MeshB();
                wm.Add(Prim.MeshOf(PrimitiveType.Cylinder), Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, 90), new Vector3(d.wheelR * 2, 0.13f, d.wheelR * 2)), rubber);
                wm.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(new Vector3(Mathf.Sign(w.x) * 0.07f, 0, 0), Quaternion.identity, new Vector3(0.04f, d.wheelR * 1.1f, d.wheelR * 0.35f)), chrome);
                wm.Add(Prim.MeshOf(PrimitiveType.Cube), Matrix4x4.TRS(new Vector3(Mathf.Sign(w.x) * 0.07f, 0, 0), Quaternion.identity, new Vector3(0.04f, d.wheelR * 0.35f, d.wheelR * 1.1f)), chrome);
                var spin = wm.Build("Spin", pivot, 9, true).transform;
                list.Add(pivot);
            }
            wheels = list.ToArray();
            return root;
        }
    }

    /// <summary>
    /// A drivable car (player, other players' cars shown by RemoteAvatar, and traffic). Kinematic:
    /// we move it ourselves, sweeping the box against buildings/cars and following the ground
    /// height with raycasts — predictable on phones and simple to network.
    /// </summary>
    public class Car : MonoBehaviour
    {
        public CarDef Def;
        public int ColorIndex;
        public int OwnedIndex = -1;    // index in Save.D.cars for the player's own cars
        public bool Traffic;
        public float Speed;            // m/s along forward
        public float Steer;            // -1..1 current wheel angle factor
        public Vector2 Input;          // x steer, y throttle
        public bool Brake;
        public Transform[] Wheels;
        public CarModel.Dims Dims;
        public Rigidbody Body;
        public BoxCollider Box;
        public Light HeadL;
        float wheelSpin;
        public float Tune => OwnedIndex >= 0 && OwnedIndex < Save.D.cars.Count ? Save.D.cars[OwnedIndex].tune : 0;
        public static readonly List<Car> Live = new List<Car>();

        public static Car Spawn(CarDef def, int color, Vector3 pos, float yaw, bool traffic, int owned = -1)
        {
            var go = CarModel.Build(def.Shape, Palette.Get(Palette.Car, color), def.Id == "taxi", out var wheels);
            go.name = (traffic ? "Traffic " : "Car ") + def.Title;
            go.layer = 9;
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var c = go.AddComponent<Car>();
            c.Def = def; c.ColorIndex = color; c.Traffic = traffic; c.Wheels = wheels; c.OwnedIndex = owned;
            c.Dims = CarModel.Of(def.Shape);
            c.Body = go.AddComponent<Rigidbody>();
            c.Body.isKinematic = true;
            c.Box = go.AddComponent<BoxCollider>();
            float top = c.Dims.bodyY + c.Dims.bodyH + c.Dims.cabH;
            c.Box.center = new Vector3(0, (0.25f + top) / 2, 0);
            c.Box.size = new Vector3(c.Dims.W, top - 0.25f, c.Dims.L + 0.2f);
            return c;
        }

        void OnEnable() => Live.Add(this);
        void OnDisable() => Live.Remove(this);

        public void SetHeadlights(bool on)
        {
            if (on && HeadL == null && !Traffic)
            {
                var lg = new GameObject("Headlight");
                lg.transform.SetParent(transform, false);
                lg.transform.localPosition = new Vector3(0, 0.9f, Dims.L / 2 + 0.3f);
                lg.transform.localRotation = Quaternion.Euler(8, 0, 0);
                HeadL = lg.AddComponent<Light>();
                HeadL.type = LightType.Spot;
                HeadL.range = 40; HeadL.spotAngle = 70; HeadL.intensity = 4f; HeadL.color = new Color(1f, 0.95f, 0.85f);
                HeadL.shadows = LightShadows.None;
            }
            if (HeadL != null) HeadL.enabled = on;
        }

        public const int SolidMask = (1 << 9) | (1 << 10) | (1 << 12);

        /// <summary>Physics step for a driven car (player). Traffic uses its own path code (Traffic.cs).</summary>
        public void Drive(float dt)
        {
            float max = Def.MaxSpeed * (1f + Tune * 0.12f);
            float acc = Def.Accel * (1f + Tune * 0.15f);
            float t = Input.y;
            if (Brake) Speed = Mathf.MoveTowards(Speed, 0, 25f * dt);
            else if (t > 0.05f) Speed = Speed < 0 ? Mathf.MoveTowards(Speed, 0, 18f * dt) : Mathf.MoveTowards(Speed, max * t, acc * dt);
            else if (t < -0.05f) Speed = Speed > 0.5f ? Mathf.MoveTowards(Speed, 0, 18f * dt) : Mathf.MoveTowards(Speed, -8f * -t, 5f * dt);
            else Speed = Mathf.MoveTowards(Speed, 0, 3.5f * dt);

            Steer = Mathf.MoveTowards(Steer, Input.x, 3.5f * dt);
            float steerAngle = Steer * Mathf.Lerp(38f, 14f, Mathf.Abs(Speed) / 45f);
            float yawRate = Speed / (Dims.L * 0.62f) * Mathf.Tan(steerAngle * Mathf.Deg2Rad) * Mathf.Rad2Deg;
            Move(dt, yawRate);
        }

        /// <summary>Moves forward at Speed while turning at yawRate (deg/s), stopping at obstacles.</summary>
        public void Move(float dt, float yawRate)
        {
            var rot = transform.rotation * Quaternion.Euler(0, yawRate * dt, 0);
            var fwd = rot * Vector3.forward;
            float dist = Speed * dt;
            var dir = fwd * Mathf.Sign(dist);
            var pos = transform.position;
            var center = transform.TransformPoint(Box.center);
            if (Mathf.Abs(dist) > 0.0001f && Physics.BoxCast(center, Box.size * 0.49f, dir, out var hit, rot, Mathf.Abs(dist) + 0.05f, SolidMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform != transform && !hit.collider.transform.IsChildOf(transform))
                {
                    float impact = Mathf.Abs(Speed);
                    if (!Traffic && impact > 9f && OwnedIndex >= 0 && OwnedIndex < Save.D.cars.Count)
                    {
                        Save.D.cars[OwnedIndex].health = Mathf.Max(0, Save.D.cars[OwnedIndex].health - impact * 0.6f);
                        AudioFx.Crash(impact);
                        Hud.Toast("Авария! Машина повреждена: " + Mathf.RoundToInt(Save.D.cars[OwnedIndex].health) + "%");
                    }
                    dist = Mathf.Sign(dist) * Mathf.Max(0, hit.distance - 0.06f);
                    Speed *= impact > 6f ? -0.25f : 0f;
                }
            }
            pos += fwd * dist;
            // follow the ground (sidewalks, ramps, the mountain road)
            float gy = GroundY(pos, transform.position.y);
            pos.y = Mathf.Lerp(transform.position.y, gy, Mathf.Clamp01(dt * 12f));
            transform.SetPositionAndRotation(pos, rot);
            SpinWheels(dist);
        }

        public static float GroundY(Vector3 p, float current)
        {
            if (Physics.Raycast(new Vector3(p.x, current + 2.5f, p.z), Vector3.down, out var h, 6f, 1 << 13, QueryTriggerInteraction.Ignore))
                return h.point.y;
            return current;
        }

        public void SpinWheels(float dist)
        {
            if (Wheels == null) return;
            wheelSpin += dist / Mathf.Max(0.2f, Dims.wheelR) * Mathf.Rad2Deg;
            for (int i = 0; i < Wheels.Length; i++)
            {
                float steer = i < 2 ? Steer * 30f : 0f;
                Wheels[i].localRotation = Quaternion.Euler(0, steer, 0);
                var spin = Wheels[i].childCount > 0 ? Wheels[i].GetChild(0) : null;
                if (spin != null) spin.localRotation = Quaternion.Euler(wheelSpin, 0, 0);
            }
        }

        public Vector3 SeatExit => transform.position + transform.right * -(Dims.W / 2 + 0.9f) + Vector3.up * 0.3f;
    }
}
