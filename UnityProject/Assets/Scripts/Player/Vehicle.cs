using System.Collections.Generic;
using UnityEngine;

namespace Gad
{
    /// <summary>
    /// A car: realistic body (CarMesh), a hinged driver's door, spinning/steering wheels, a seat where
    /// the driver sits (visible through the glass). Kinematic movement: we move it ourselves, sweeping
    /// its box against buildings and cars and following the ground with raycasts.
    /// Used for the player's car, other players' cars, traffic (with an NPC driver) and taken cars.
    /// </summary>
    public class Car : MonoBehaviour
    {
        public struct DimsT { public float L, W, wheelR; }

        public CarDef Def;
        public int ColorIndex;
        public int OwnedIndex = -1;
        public bool Traffic;
        public float Speed, Steer;
        public Vector2 Input;
        public bool Brake;
        public Transform[] Wheels;
        public Transform DoorPivot, Seat;
        public DimsT Dims;
        public Rigidbody Body;
        public BoxCollider Box;
        public Light HeadL;
        public Person Driver;
        public float DoorT, DoorTarget;
        float wheelSpin;
        public float Tune => OwnedIndex >= 0 && OwnedIndex < Save.D.cars.Count ? Save.D.cars[OwnedIndex].tune : 0;
        public static readonly List<Car> Live = new List<Car>();
        public const int SolidMask = (1 << 9) | (1 << 10) | (1 << 12);

        public static Color ColorOf(CarDef def, int color) => Palette.Get(Palette.Car, def.ForceColor >= 0 ? def.ForceColor : color);

        public static Car Spawn(CarDef def, int color, Vector3 pos, float yaw, bool traffic, int owned = -1, int lod = -1)
        {
            if (lod < 0) lod = traffic ? 1 : 0;
            var b = CarMesh.Get(def, lod);
            var s = def.Spec;
            var go = new GameObject((traffic ? "Traffic " : "Car ") + def.Title) { layer = 9 };
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var mats = CarMesh.Materials(ColorOf(def, color), s);

            var bodyGo = new GameObject("Body") { layer = 9 };
            bodyGo.transform.SetParent(go.transform, false);
            bodyGo.AddComponent<MeshFilter>().sharedMesh = b.body;
            bodyGo.AddComponent<MeshRenderer>().sharedMaterials = mats;

            var c = go.AddComponent<Car>();
            c.Def = def; c.ColorIndex = color; c.Traffic = traffic; c.OwnedIndex = owned;
            c.Dims = new DimsT { L = s.L, W = s.W, wheelR = s.R };

            c.DoorPivot = new GameObject("DoorPivot").transform;
            c.DoorPivot.SetParent(go.transform, false);
            c.DoorPivot.localPosition = b.doorPivot;
            var doorGo = new GameObject("Door") { layer = 9 };
            doorGo.transform.SetParent(c.DoorPivot, false);
            doorGo.AddComponent<MeshFilter>().sharedMesh = b.door;
            doorGo.AddComponent<MeshRenderer>().sharedMaterials = mats;

            c.Seat = new GameObject("Seat").transform;
            c.Seat.SetParent(go.transform, false);
            c.Seat.localPosition = b.seat;

            var wl = new List<Transform>();
            foreach (var w in CarMesh.WheelSpots(s))
            {
                var pivot = new GameObject("Wheel").transform;
                pivot.SetParent(go.transform, false);
                pivot.localPosition = w;
                var spin = new GameObject("Spin") { layer = 9 };
                spin.transform.SetParent(pivot, false);
                spin.transform.localScale = new Vector3(w.x < 0 ? -1 : 1, 1, 1);
                spin.AddComponent<MeshFilter>().sharedMesh = b.wheel;
                spin.AddComponent<MeshRenderer>().sharedMaterials = mats;
                wl.Add(pivot);
            }
            c.Wheels = wl.ToArray();

            c.Body = go.AddComponent<Rigidbody>();
            c.Body.isKinematic = true;
            c.Box = go.AddComponent<BoxCollider>();
            c.Box.center = new Vector3(0, (0.25f + s.H) / 2, 0);
            c.Box.size = new Vector3(s.W, s.H - 0.25f, s.L);

            if (traffic && Gfx.Level > 0)
            {
                c.Driver = Person.Create(c.Seat, Person.Random(new Rng((uint)UnityEngine.Random.Range(1, int.MaxValue))), "Driver");
                c.Driver.Pose = 4;
                c.Driver.transform.localPosition = Vector3.zero;
            }
            return c;
        }

        void OnEnable() => Live.Add(this);
        void OnDisable() => Live.Remove(this);

        void Update()
        {
            if (Mathf.Abs(DoorT - DoorTarget) > 0.001f)
            {
                DoorT = Mathf.MoveTowards(DoorT, DoorTarget, Time.deltaTime * 2.6f);
                DoorPivot.localRotation = Quaternion.Euler(0, 68f * Mathf.SmoothStep(0, 1, DoorT), 0);
            }
        }

        public void SetHeadlights(bool on)
        {
            if (on && HeadL == null && !Traffic)
            {
                var lg = new GameObject("Headlight");
                lg.transform.SetParent(transform, false);
                lg.transform.localPosition = new Vector3(0, 0.8f, Dims.L / 2 + 0.3f);
                lg.transform.localRotation = Quaternion.Euler(7, 0, 0);
                HeadL = lg.AddComponent<Light>();
                HeadL.type = LightType.Spot;
                HeadL.range = 45; HeadL.spotAngle = 75; HeadL.intensity = 5f; HeadL.color = new Color(0.95f, 0.97f, 1f);
                HeadL.shadows = LightShadows.None;
            }
            if (HeadL != null) HeadL.enabled = on;
        }

        public void Drive(float dt)
        {
            float max = Def.MaxSpeed * (1f + Tune * 0.12f);
            float acc = Def.Accel * (1f + Tune * 0.15f);
            float t = Input.y;
            if (Brake) Speed = Mathf.MoveTowards(Speed, 0, 25f * dt);
            else if (t > 0.05f) Speed = Speed < 0 ? Mathf.MoveTowards(Speed, 0, 18f * dt) : Mathf.MoveTowards(Speed, max * t, acc * dt * Mathf.Lerp(1f, 0.45f, Speed / max));
            else if (t < -0.05f) Speed = Speed > 0.5f ? Mathf.MoveTowards(Speed, 0, 18f * dt) : Mathf.MoveTowards(Speed, -8f * -t, 5f * dt);
            else Speed = Mathf.MoveTowards(Speed, 0, 3.5f * dt);

            Steer = Mathf.MoveTowards(Steer, Input.x, 3.5f * dt);
            float steerAngle = Steer * Mathf.Lerp(36f, 12f, Mathf.Abs(Speed) / 45f);
            float yawRate = Speed / (Def.Spec.WB) * Mathf.Tan(steerAngle * Mathf.Deg2Rad) * Mathf.Rad2Deg;
            Move(dt, yawRate);
        }

        public void Move(float dt, float yawRate)
        {
            var rot = transform.rotation * Quaternion.Euler(0, yawRate * dt, 0);
            rot = Quaternion.Euler(0, rot.eulerAngles.y, 0);
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
                        Hud.Toast("Авария! Машина повреждена: " + Mathf.RoundToInt(Save.D.cars[OwnedIndex].health) + "%");
                    }
                    if (impact > 6f) AudioFx.Crash(impact);
                    dist = Mathf.Sign(dist) * Mathf.Max(0, hit.distance - 0.06f);
                    Speed *= impact > 6f ? -0.25f : 0f;
                }
            }
            pos += fwd * dist;
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
                float steer = i < 2 ? Steer * 28f : 0f;
                Wheels[i].localRotation = Quaternion.Euler(0, steer, 0);
                var spin = Wheels[i].GetChild(0);
                spin.localRotation = Quaternion.Euler(wheelSpin, 0, 0);
            }
        }

        /// <summary>Where the driver stands to open the door (beside it, outside).</summary>
        public Vector3 DoorStand => transform.TransformPoint(DoorPivot.localPosition + new Vector3(-0.75f, -0.5f, -0.55f)) + Vector3.up * 0.2f;
        public Vector3 SeatExit => DoorStand;
    }
}
