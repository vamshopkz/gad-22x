using System;
using UnityEngine;

namespace Gad
{
    /// <summary>
    /// The local player: walking (CharacterController), driving, the third-person camera and the
    /// context action (enter a building, use a counter, get in a car).
    /// </summary>
    public class PlayerCtl : MonoBehaviour
    {
        public static PlayerCtl I;
        public CharacterController CC;
        public Person Body;
        public Vector2 MoveInput;          // from the joystick (or keys)
        public bool RunHeld, JumpPressed, ActionPressed, CarPressed, BrakeHeld;
        public Car Car;                    // car being driven
        public Car MyCar;                  // the player's spawned own car
        public int Inside = -1;            // building id while indoors
        public bool Frozen;                // UI open / sleeping
        public string ActionLabel;         // shown on the action button
        public Action ActionDo;
        public string CarLabel;
        float vy;
        public float PlanarSpeed;

        // camera
        public Camera Cam;
        public float Yaw, Pitch = 14f, Dist = 5.2f;
        float camDist;
        public Vector2 LookDelta;          // from touch drag

        public static PlayerCtl Create()
        {
            var go = new GameObject("Player") { layer = 15 };
            var p = go.AddComponent<PlayerCtl>();
            I = p;
            p.CC = go.AddComponent<CharacterController>();
            p.CC.height = 1.8f; p.CC.radius = 0.32f; p.CC.center = new Vector3(0, 0.9f, 0);
            p.CC.stepOffset = 0.35f; p.CC.slopeLimit = 50f; p.CC.skinWidth = 0.04f;
            p.Body = Person.Create(go.transform, Save.D.skin, "Me", true);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            p.Cam = camGo.AddComponent<Camera>();
            p.Cam.nearClipPlane = 0.15f;
            p.Cam.farClipPlane = 2200f;
            p.Cam.fieldOfView = 62f;
            camGo.AddComponent<AudioListener>();
            return p;
        }

        public void Teleport(Vector3 pos, float yaw)
        {
            CC.enabled = false;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            CC.enabled = true;
            Yaw = yaw;
            vy = 0;
            camDist = Dist;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Keys();
            if (busy) { }
            else if (Taxi.Riding) { }
            else if (Car != null) DriveUpdate(dt);
            else if (CC.enabled) WalkUpdate(dt);
            FindAction();
            if (ActionPressed && ActionDo != null && !Frozen) ActionDo();
            if (CarPressed && !Frozen) CarButton();
            ActionPressed = CarPressed = JumpPressed = false;
        }

        void Keys()
        {
            if (UIRoot.TypingText || MainMenu.Open) return;
            var k = new Vector2((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0), (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
            if (Input.GetKey(KeyCode.RightArrow)) k.x = 1; if (Input.GetKey(KeyCode.LeftArrow)) k.x = -1;
            if (Input.GetKey(KeyCode.UpArrow)) k.y = 1; if (Input.GetKey(KeyCode.DownArrow)) k.y = -1;
            if (k != Vector2.zero) MoveInput = k.normalized;
            else if (keysWereUsed) MoveInput = Vector2.zero;
            keysWereUsed = k != Vector2.zero;
            if (Input.GetKeyDown(KeyCode.Space)) JumpPressed = true;
            if (Input.GetKeyDown(KeyCode.E)) ActionPressed = true;
            if (Input.GetKeyDown(KeyCode.F)) CarPressed = true;
            keyRun = Input.GetKey(KeyCode.LeftShift);
            keyBrake = Input.GetKey(KeyCode.Space);
            if (Input.GetMouseButton(1)) LookDelta += new Vector2(Input.GetAxis("Mouse X") * 6f, -Input.GetAxis("Mouse Y") * 4f);
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f) Dist = Mathf.Clamp(Dist - wheel * 0.6f, 2.5f, 14f);
        }
        bool keysWereUsed, keyRun, keyBrake;

        void WalkUpdate(float dt)
        {
            var input = Frozen ? Vector2.zero : MoveInput;
            bool run = RunHeld || keyRun;
            float speedMax = run ? 7.2f : 3.6f;
            if (Save.D.food < 5 || Save.D.energy < 5) speedMax *= 0.6f;
            var camFwd = Quaternion.Euler(0, Yaw, 0) * Vector3.forward;
            var camRight = Quaternion.Euler(0, Yaw, 0) * Vector3.right;
            var move = (camFwd * input.y + camRight * input.x);
            float mag = Mathf.Clamp01(move.magnitude);
            if (mag > 0.05f)
            {
                var target = Quaternion.LookRotation(move.normalized);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 720f * dt);
            }
            var vel = move.normalized * mag * speedMax;
            if (CC.isGrounded)
            {
                vy = -2f;
                if (JumpPressed && !Frozen) { vy = 6.2f; AudioFx.Play("jump"); }
            }
            else vy -= 20f * dt;
            CC.Move((vel + Vector3.up * vy) * dt);
            PlanarSpeed = new Vector3(CC.velocity.x, 0, CC.velocity.z).magnitude;
            Body.Speed = PlanarSpeed;
            Body.Grounded = CC.isGrounded || vy < 0 && vy > -3f;
            if (transform.position.y < -50 && Inside < 0) Teleport(City.Spawn, 0);
            if (run && PlanarSpeed > 1) Save.D.energy = Mathf.Max(0, Save.D.energy - dt * 0.05f);
        }

        void DriveUpdate(float dt)
        {
            Car.Input = Frozen ? Vector2.zero : MoveInput;
            Car.Brake = BrakeHeld || keyBrake;
            Car.Drive(dt);
            transform.position = Car.transform.position;
            PlanarSpeed = Mathf.Abs(Car.Speed);
        }

        public static bool ShotCam;
        public static Vector3 ShotPos, ShotLook;

        void LateUpdate()
        {
            if (ShotCam) { Cam.transform.position = ShotPos; Cam.transform.LookAt(ShotLook); return; }
            float dt = Time.deltaTime;
            Yaw += LookDelta.x * 0.25f;
            Pitch = Mathf.Clamp(Pitch + LookDelta.y * 0.2f, -25f, 70f);
            LookDelta = Vector2.zero;
            Vector3 target;
            float want = Dist;
            if (Taxi.Riding && Taxi.CarOf != null)
            {   // 0.8: riding in a taxi: the camera follows the taxi
                want = Mathf.Max(Dist + 3.5f, 8f);
                var tc = Taxi.CarOf;
                target = tc.transform.position + Vector3.up * 1.6f;
                if (tc.Speed > 3f && !UIRoot.LookActive) Yaw = Mathf.LerpAngle(Yaw, tc.transform.eulerAngles.y, dt * 2.2f);
            }
            else if (Car != null)
            {
                want = Mathf.Max(Dist + 3.5f, 8f);
                target = Car.transform.position + Vector3.up * 1.6f;
                // follow behind the car when driving forward and nobody touches the camera
                if (Mathf.Abs(Car.Speed) > 3f && !UIRoot.LookActive)
                    Yaw = Mathf.LerpAngle(Yaw, Car.transform.eulerAngles.y + (Car.Speed < 0 ? 180 : 0), dt * 2.2f);
            }
            else
            {
                target = transform.position + Vector3.up * 1.55f;
                if (Inside >= 0) want = Mathf.Min(Dist, 4.2f);
            }
            var rot = Quaternion.Euler(Pitch, Yaw, 0);
            var back = rot * Vector3.back;
            float d = want;
            if (Physics.SphereCast(target, 0.25f, back, out var hit, want, (1 << 10) | (1 << 13) | (1 << 12), QueryTriggerInteraction.Ignore))
                d = Mathf.Max(0.6f, hit.distance - 0.1f);
            d = InPlace.ClampCam(target, back, d);
            camDist = d < camDist ? d : Mathf.Lerp(camDist, d, dt * 4f);
            Cam.transform.SetPositionAndRotation(target + back * camDist, rot);
        }

        // ------------------------------------------------------------------ actions

        void FindAction()
        {
            ActionLabel = null; ActionDo = null; CarLabel = null; nearCar = null; nearParked = null;
            if (busy) return;
            if (Taxi.Riding) { CarLabel = "ВЫЙТИ ИЗ ТАКСИ"; return; }
            if (Car != null) { CarLabel = "ВЫЙТИ"; return; }
            var p = transform.position;
            if (Inside >= 0)
            {
                var ap = Interiors.NearestPoint(p, 2.2f);
                if (ap != null) { ActionLabel = ap.Label; ActionDo = ap.Act; }
                return;
            }
            // 0.7: actions out in the world (real houses, plots)
            var wp = WorldActions.Nearest(p, 2.2f);
            if (wp != null) { ActionLabel = wp.Label; ActionDo = wp.Act; }
            // any car nearby can be taken: yours, parked, or one stopped in traffic
            float nd = 3.2f;
            foreach (var c in Car.Live)
            {
                if (c == null || (c.Traffic && Mathf.Abs(c.Speed) > 2.5f)) continue;
                float d = Vector3.Distance(c.DoorStand, p);
                if (d < nd) { nd = d; nearCar = c; }
            }
            var spot = Streamer.ParkedNear(p, nd);
            if (spot != null) { nearParked = spot; nearCar = null; }
            if (nearCar != null || nearParked != null) CarLabel = "ЗАЙТИ В МАШИНУ";
        }
        Car nearCar;
        Streamer.ParkedCar nearParked;
        bool busy;

        void CarButton()
        {
            if (busy) return;
            if (Taxi.Riding) { Taxi.I.ExitNow(); return; }
            if (Car != null) { ExitCar(); return; }
            if (nearParked != null) { var c = Streamer.TakeParked(nearParked); if (c != null) EnterCar(c); return; }
            if (nearCar != null) EnterCar(nearCar);
        }

        public void EnterCar(Car c, bool instant = false)
        {
            if (c.OwnedIndex >= 0 && c.OwnedIndex < Save.D.cars.Count && Save.D.cars[c.OwnedIndex].health <= 0)
            {
                Hud.Toast("Машина сломана — отремонтируйте в автомастерской (Телефон → Мои машины)");
                return;
            }
            if (c.Traffic) Gad.Traffic.TakeOver(c);
            if (instant) { SitIn(c); c.DoorT = c.DoorTarget = 0; return; }
            StartCoroutine(EnterSeq(c));
        }

        void SitIn(Car c)
        {
            Car = c;
            CC.enabled = false;
            Body.transform.SetParent(c.Seat, false);
            Body.transform.localPosition = Vector3.zero;
            Body.transform.localRotation = Quaternion.identity;
            Body.Pose = 4; Body.Speed = 0; Body.Grounded = true;
            c.SetHeadlights(DayNight.IsNight);
            Yaw = c.transform.eulerAngles.y;
        }

        System.Collections.IEnumerator EnterSeq(Car c)
        {
            busy = true; Frozen = true;
            CC.enabled = false;
            // walk up to the door
            var from = transform.position; var to = c.DoorStand; to.y = from.y;
            var face = Quaternion.LookRotation(c.transform.right);
            for (float t = 0; t < 1; t += Time.deltaTime / 0.35f)
            {
                transform.SetPositionAndRotation(Vector3.Lerp(from, to, t), Quaternion.Slerp(transform.rotation, face, t));
                Body.Speed = 2f;
                yield return null;
            }
            Body.Speed = 0;
            c.DoorTarget = 1;
            AudioFx.Play("door");
            yield return new WaitForSeconds(0.45f);
            // step in and sit down
            var start = Body.transform.position;
            Body.transform.SetParent(c.Seat, true);
            var sp = Body.transform.localPosition; var sr = Body.transform.localRotation;
            for (float t = 0; t < 1; t += Time.deltaTime / 0.45f)
            {
                Body.transform.localPosition = Vector3.Lerp(sp, Vector3.zero, t);
                Body.transform.localRotation = Quaternion.Slerp(sr, Quaternion.identity, t);
                if (t > 0.4f) Body.Pose = 4;
                yield return null;
            }
            SitIn(c);
            c.DoorTarget = 0;
            yield return new WaitForSeconds(0.35f);
            AudioFx.Play("door");
            Frozen = false; busy = false;
        }

        public void ExitCar()
        {
            if (Car == null || busy) return;
            if (Mathf.Abs(Car.Speed) > 6f) { Hud.Toast("Сначала остановитесь"); return; }
            StartCoroutine(ExitSeq(Car));
        }

        public void ExitCarInstant()
        {
            if (Car == null) return;
            var c = Car;
            Car = null;
            c.Speed = 0; c.Input = Vector2.zero; c.DoorT = c.DoorTarget = 0;
            Body.transform.SetParent(transform, false);
            Body.transform.localPosition = Vector3.zero; Body.transform.localRotation = Quaternion.identity;
            Body.Pose = 0;
            Teleport(c.DoorStand, c.transform.eulerAngles.y - 90);
            c.SetHeadlights(false);
        }

        System.Collections.IEnumerator ExitSeq(Car c)
        {
            busy = true; Frozen = true;
            c.Speed = 0; c.Input = Vector2.zero;
            c.DoorTarget = 1;
            AudioFx.Play("door");
            yield return new WaitForSeconds(0.4f);
            var stand = c.DoorStand;
            if (Physics.CheckCapsule(stand + Vector3.up * 0.4f, stand + Vector3.up * 1.6f, 0.3f, Car.SolidMask & ~(1 << 9)))
                stand = c.transform.position + c.transform.right * (c.Dims.W / 2 + 0.9f) + Vector3.up * 0.2f;
            Car = null;
            Body.Pose = 0;
            var sp = Body.transform.position; var sr = Body.transform.rotation;
            Body.transform.SetParent(transform, false);
            transform.SetPositionAndRotation(stand, Quaternion.Euler(0, c.transform.eulerAngles.y - 90, 0));
            for (float t = 0; t < 1; t += Time.deltaTime / 0.4f)
            {
                Body.transform.position = Vector3.Lerp(sp, stand, t);
                Body.transform.rotation = Quaternion.Slerp(sr, transform.rotation, t);
                yield return null;
            }
            Body.transform.localPosition = Vector3.zero; Body.transform.localRotation = Quaternion.identity;
            Teleport(stand, c.transform.eulerAngles.y - 90);
            c.SetHeadlights(false);
            c.DoorTarget = 0;
            yield return new WaitForSeconds(0.3f);
            AudioFx.Play("door");
            Frozen = false; busy = false;
        }

        /// <summary>Spawns (or moves) the player's own car on the nearest road.</summary>
        public Car CallCar(int index, Vector3 near)
        {
            if (index < 0 || index >= Save.D.cars.Count) return null;
            if (Car != null) ExitCarInstant();
            if (MyCar != null) Destroy(MyCar.gameObject);
            var oc = Save.D.cars[index];
            var pos = City.NearestRoad(near, out float yaw);
            if (Inside >= 0 || near.y < -100) return null;
            MyCar = Car.Spawn(CarDef.Get(oc.model), oc.color, pos, yaw, false, index);
            return MyCar;
        }
    }
}
