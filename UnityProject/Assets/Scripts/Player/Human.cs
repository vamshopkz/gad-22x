using UnityEngine;

namespace Gad
{
    /// <summary>
    /// A person built from primitives (player, other players, pedestrians, shop staff) with a
    /// procedural walk/run/idle/work animation. Rebuild() applies a new Look (wardrobe).
    /// </summary>
    public class Human : MonoBehaviour
    {
        public Look Look;
        public float Speed;          // m/s, drives the walk cycle
        public bool Grounded = true;
        public int Pose;             // 0 normal, 1 working (arms busy), 2 sitting, 3 waving
        Transform body, legL, legR, armL, armR, head, torso;
        float phase, bob;

        public static Human Create(Transform parent, Look look, string name = "Human")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<Human>();
            h.Rebuild(look);
            return h;
        }

        public void Rebuild(Look look)
        {
            Look = look.Clone();
            if (body != null) Destroy(body.gameObject);
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            body.localScale = Vector3.one * look.height;
            bool fem = look.gender == 1;
            var skin = Palette.Get(Palette.Skin, look.skin);
            var hair = Palette.Get(Palette.Hair, look.hair);
            var shirt = Palette.Get(Palette.Cloth, look.shirt);
            var pants = Palette.Get(Palette.Cloth, look.pants);
            var shoes = Palette.Get(Palette.Cloth, look.shoes);
            float sw = fem ? 0.9f : 1f;

            // legs (pivot at the hip)
            legL = Pivot("LegL", new Vector3(-0.1f, 0.92f, 0));
            legR = Pivot("LegR", new Vector3(0.1f, 0.92f, 0));
            foreach (var leg in new[] { legL, legR })
            {
                Prim.Cap(leg, new Vector3(0, -0.44f, 0), new Vector3(0.16f, 0.45f, 0.16f), pants);
                Prim.Box(leg, new Vector3(0, -0.87f, 0.04f), new Vector3(0.15f, 0.1f, 0.28f), shoes);
            }
            Prim.Box(body, new Vector3(0, 0.95f, 0), new Vector3(fem ? 0.36f : 0.34f, 0.2f, 0.22f), pants);

            torso = new GameObject("Torso").transform;
            torso.SetParent(body, false);
            torso.localPosition = new Vector3(0, 1.0f, 0);
            Prim.Cap(torso, new Vector3(0, 0.3f, 0), new Vector3(0.42f * sw, 0.32f, 0.25f), shirt);
            if (look.top == 1 || look.top == 3)
            {
                // jacket / suit: darker panels with an open front
                var jc = look.top == 3 ? new Color(0.12f, 0.13f, 0.17f) : shirt * 0.6f;
                jc.a = 1;
                Prim.Box(torso, new Vector3(-0.11f * sw, 0.3f, 0.06f), new Vector3(0.2f * sw, 0.56f, 0.18f), jc);
                Prim.Box(torso, new Vector3(0.11f * sw, 0.3f, 0.06f), new Vector3(0.2f * sw, 0.56f, 0.18f), jc);
                Prim.Box(torso, new Vector3(0, 0.3f, -0.05f), new Vector3(0.42f * sw, 0.56f, 0.16f), jc);
                if (look.top == 3) Prim.Box(torso, new Vector3(0, 0.36f, 0.135f), new Vector3(0.05f, 0.3f, 0.02f), new Color(0.7f, 0.1f, 0.15f));
            }
            else if (look.top == 2)
            {
                // hoodie: hood + pocket
                Prim.Ball(torso, new Vector3(0, 0.62f, -0.12f), new Vector3(0.3f, 0.2f, 0.16f), shirt * 0.9f);
                Prim.Box(torso, new Vector3(0, 0.12f, 0.12f), new Vector3(0.26f, 0.12f, 0.03f), shirt * 0.85f);
            }
            if (fem) Prim.Ball(torso, new Vector3(0, 0.38f, 0.08f), new Vector3(0.32f, 0.16f, 0.14f), shirt);

            Prim.Cyl(torso, new Vector3(0, 0.66f, 0), new Vector3(0.11f, 0.06f, 0.11f), skin);
            head = new GameObject("Head").transform;
            head.SetParent(torso, false);
            head.localPosition = new Vector3(0, 0.84f, 0);
            Prim.Ball(head, Vector3.zero, new Vector3(0.22f, 0.26f, 0.24f), skin);
            foreach (float sx in new[] { -0.045f, 0.045f })
            {
                Prim.Ball(head, new Vector3(sx, 0.02f, 0.105f), new Vector3(0.035f, 0.035f, 0.02f), Color.white);
                Prim.Ball(head, new Vector3(sx, 0.02f, 0.115f), new Vector3(0.018f, 0.02f, 0.01f), new Color(0.1f, 0.08f, 0.06f));
            }
            Prim.Box(head, new Vector3(0, -0.06f, 0.112f), new Vector3(0.05f, 0.012f, 0.01f), new Color(0.6f, 0.3f, 0.3f));
            Prim.Box(head, new Vector3(0, -0.005f, 0.12f), new Vector3(0.025f, 0.05f, 0.03f), skin * 0.95f);
            int hs = look.hairStyle;
            if (hs != 2)
            {
                Prim.Ball(head, new Vector3(0, 0.05f, -0.015f), new Vector3(0.235f, 0.22f, 0.25f), hair);
                if (hs == 1) Prim.Box(head, new Vector3(0, -0.12f, -0.07f), new Vector3(0.23f, 0.34f, 0.1f), hair);
                if (hs == 3) Prim.Ball(head, new Vector3(0, 0.02f, -0.15f), new Vector3(0.1f, 0.1f, 0.1f), hair);
            }
            if (look.hat == 1)
            {
                var cap = Palette.Get(Palette.Cloth, look.shirt + 3);
                Prim.Ball(head, new Vector3(0, 0.07f, -0.01f), new Vector3(0.25f, 0.2f, 0.26f), cap);
                Prim.Box(head, new Vector3(0, 0.08f, 0.14f), new Vector3(0.2f, 0.02f, 0.12f), cap);
            }
            else if (look.hat == 2) Prim.Ball(head, new Vector3(0, 0.08f, -0.01f), new Vector3(0.26f, 0.22f, 0.27f), Palette.Get(Palette.Cloth, look.pants + 5));

            armL = Pivot("ArmL", new Vector3(-0.25f * sw, 0.52f, 0), torso);
            armR = Pivot("ArmR", new Vector3(0.25f * sw, 0.52f, 0), torso);
            var sleeve = look.top == 3 ? new Color(0.12f, 0.13f, 0.17f) : look.top == 1 ? shirt * 0.6f : shirt;
            sleeve.a = 1;
            bool longSleeve = look.top != 0;
            foreach (var arm in new[] { armL, armR })
            {
                Prim.Cap(arm, new Vector3(0, -0.15f, 0), new Vector3(0.13f, 0.17f, 0.13f), sleeve);
                Prim.Cap(arm, new Vector3(0, -0.4f, 0), new Vector3(0.11f, 0.18f, 0.11f), longSleeve ? sleeve : skin);
                Prim.Ball(arm, new Vector3(0, -0.6f, 0), new Vector3(0.1f, 0.11f, 0.1f), skin);
            }
            foreach (var r in body.GetComponentsInChildren<MeshRenderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        Transform Pivot(string n, Vector3 p, Transform parent = null)
        {
            var t = new GameObject(n).transform;
            t.SetParent(parent ?? body, false);
            t.localPosition = p;
            return t;
        }

        void Update()
        {
            if (body == null) return;
            float dt = Time.deltaTime;
            float s = Mathf.Clamp(Speed, 0, 9f);
            float freq = s < 0.1f ? 0 : Mathf.Lerp(5.5f, 10f, Mathf.InverseLerp(1.5f, 7f, s));
            phase += dt * freq;
            float amp = Mathf.Clamp01(s / 3.5f) * (s > 4.5f ? 50f : 32f);
            float sw = Mathf.Sin(phase);
            if (!Grounded)
            {
                legL.localRotation = Quaternion.Lerp(legL.localRotation, Quaternion.Euler(-35, 0, 0), dt * 10);
                legR.localRotation = Quaternion.Lerp(legR.localRotation, Quaternion.Euler(15, 0, 0), dt * 10);
                armL.localRotation = Quaternion.Lerp(armL.localRotation, Quaternion.Euler(-150, 0, -10), dt * 8);
                armR.localRotation = Quaternion.Lerp(armR.localRotation, Quaternion.Euler(-150, 0, 10), dt * 8);
                return;
            }
            if (Pose == 2)
            {
                legL.localRotation = legR.localRotation = Quaternion.Euler(-90, 0, 0);
                armL.localRotation = Quaternion.Euler(-40, 0, 0);
                armR.localRotation = Quaternion.Euler(-40, 0, 0);
                body.localPosition = new Vector3(0, -0.45f, 0);
                return;
            }
            body.localPosition = Vector3.zero;
            legL.localRotation = Quaternion.Euler(sw * amp, 0, 0);
            legR.localRotation = Quaternion.Euler(-sw * amp, 0, 0);
            if (Pose == 1)
            {
                float w = Mathf.Sin(Time.time * 7f) * 15f;
                armL.localRotation = Quaternion.Euler(-70 + w, 0, -10);
                armR.localRotation = Quaternion.Euler(-70 - w, 0, 10);
            }
            else if (Pose == 3)
            {
                armL.localRotation = Quaternion.Euler(0, 0, -5);
                armR.localRotation = Quaternion.Euler(-170, 0, 20 + Mathf.Sin(Time.time * 10f) * 20f);
            }
            else
            {
                float idle = s < 0.1f ? Mathf.Sin(Time.time * 1.8f) * 2f : 0;
                armL.localRotation = Quaternion.Euler(-sw * amp * 0.9f + idle, 0, -6);
                armR.localRotation = Quaternion.Euler(sw * amp * 0.9f + idle, 0, 6);
            }
            bob = s > 0.1f ? Mathf.Abs(Mathf.Cos(phase)) * 0.04f : 0;
            torso.localPosition = new Vector3(0, 1.0f + bob * 0.5f, 0);
            torso.localRotation = Quaternion.Euler(s > 4.5f ? 10 : 2, sw * amp * 0.12f, 0);
        }
    }
}
