using UnityEngine;

namespace Gad
{
    /// <summary>
    /// 0.9: what you see through the windows of the tall buildings. At start the game builds four real
    /// furnished rooms (living room, bedroom, office, kitchen) out of sight, lights them and records each
    /// as a cubemap; the facade shader (Gad/FacadeInterior) looks into these cubemaps through every
    /// window, so the rooms have real 3D furniture and the right perspective as you walk past.
    /// Also renders the view from a place in the city (WindowView): flats and offices show the real
    /// city outside their windows.
    /// </summary>
    public static class RoomCubes
    {
        public const float W = 4f, H = 3f, Dp = 5f;       // the recorded room: 4 m wide, 3 m high, 5 m deep
        static readonly Vector3 Far = new Vector3(0, -2600, 0);
        const int Layer = 31;
        public static Cubemap[] Cubes = new Cubemap[4];

        public static void Build()
        {
            for (int k = 0; k < 4; k++)
            {
                var root = new GameObject("RoomCube" + k);
                root.transform.position = Far + Vector3.right * k * 20f;
                var m = new MeshB();
                Shell(m, k);
                var mesh = m.Build("Room", root.transform, Layer, false);
                Furnish(root.transform, k);
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
                foreach (var c in root.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
                // warm lamps (a couple of bright spots make it look lived in)
                var lights = new[] { new Vector3(0, 2.6f, 2.5f), new Vector3(-1.2f, 2.2f, 4.2f), new Vector3(1.3f, 1.6f, 1.2f) };
                foreach (var lp in lights)
                {
                    var l = new GameObject("L").AddComponent<Light>();
                    l.transform.SetParent(root.transform, false); l.transform.localPosition = lp;
                    l.type = LightType.Point; l.range = 7f; l.intensity = lp.y > 2.5f ? 2.2f : 1.0f; l.color = new Color(1f, 0.93f, 0.82f);
                    l.cullingMask = 1 << Layer; l.shadows = LightShadows.None;
                }
                var cube = new Cubemap(256, TextureFormat.RGBA32, true) { name = "room" + k, filterMode = FilterMode.Trilinear };
                var cam = new GameObject("CubeCam").AddComponent<Camera>();
                cam.transform.position = root.transform.position + new Vector3(0, H / 2f, Dp / 2f);
                cam.cullingMask = 1 << Layer;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
                cam.nearClipPlane = 0.05f; cam.farClipPlane = 20f;
                Capture(cam, cube);
                Object.Destroy(cam.gameObject);
                Object.Destroy(root);
                Cubes[k] = cube;
                Shader.SetGlobalTexture("_RoomCube" + k, cube);
            }
            Shader.SetGlobalVector("_RoomDims", new Vector4(W, H, Dp, 0));
        }

        static void Shell(MeshB m, int k)
        {
            Color wall = k switch { 0 => new Color(0.95f, 0.88f, 0.78f), 1 => new Color(0.82f, 0.86f, 0.95f), 2 => new Color(0.92f, 0.93f, 0.95f), _ => new Color(0.96f, 0.94f, 0.88f) };
            var wm = Photo.Mat("plastered_wall", wall, 0.1f);
            var fm = Photo.Mat(k == 2 ? "interior_tiles" : k == 3 ? "terrazzo_tiles" : k == 1 ? "wood_floor" : "laminate_floor_02", Color.white, 0.4f);
            var cm = Mats.Lit(new Color(0.95f, 0.95f, 0.94f), 0.1f);
            float hw = W / 2f;
            // inside faces: floor, ceiling, back, left, right (the window wall is never seen)
            m.Quad(fm, new Vector3(-hw, 0, 0), new Vector3(-hw, 0, Dp), new Vector3(hw, 0, Dp), new Vector3(hw, 0, 0), new Vector2(0, 0), new Vector2(0, Dp / 2), new Vector2(W / 2, Dp / 2), new Vector2(W / 2, 0));
            m.Quad(cm, new Vector3(-hw, H, Dp), new Vector3(-hw, H, 0), new Vector3(hw, H, 0), new Vector3(hw, H, Dp), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
            m.Quad(wm, new Vector3(-hw, 0, Dp), new Vector3(-hw, H, Dp), new Vector3(hw, H, Dp), new Vector3(hw, 0, Dp), Vector2.zero, new Vector2(0, H / 2), new Vector2(W / 2, H / 2), new Vector2(W / 2, 0));
            m.Quad(wm, new Vector3(-hw, 0, 0), new Vector3(-hw, H, 0), new Vector3(-hw, H, Dp), new Vector3(-hw, 0, Dp), Vector2.zero, new Vector2(0, H / 2), new Vector2(Dp / 2, H / 2), new Vector2(Dp / 2, 0));
            m.Quad(wm, new Vector3(hw, 0, Dp), new Vector3(hw, H, Dp), new Vector3(hw, H, 0), new Vector3(hw, 0, 0), Vector2.zero, new Vector2(0, H / 2), new Vector2(Dp / 2, H / 2), new Vector2(Dp / 2, 0));
            // skirting, a door in the side wall, pictures on the back wall
            var sk = Mats.Lit(wall * 0.55f, 0.2f);
            m.Box(sk, sk, new Vector3(-hw, 0, Dp - 0.04f), new Vector3(hw, 0.1f, Dp), new Vector2(1, 1));
            m.Box(Mats.Lit(new Color(0.5f, 0.35f, 0.22f), 0.4f), null, new Vector3(-hw, 0, 3.2f), new Vector3(-hw + 0.03f, 2.1f, 4.1f), new Vector2(1, 1));
            if (k != 2)
            {
                var frame = Mats.Lit(new Color(0.1f, 0.1f, 0.1f), 0.5f);
                m.Box(frame, frame, new Vector3(-0.9f, 1.5f, Dp - 0.03f), new Vector3(0.2f, 2.3f, Dp), new Vector2(1, 1));
                m.Box(Mats.Lit(k == 1 ? new Color(0.75f, 0.45f, 0.35f) : new Color(0.35f, 0.55f, 0.75f), 0.3f), null, new Vector3(-0.85f, 1.55f, Dp - 0.035f), new Vector3(0.15f, 2.25f, Dp - 0.03f), new Vector2(1, 1));
            }
            // a lamp under the ceiling
            m.Box(Mats.Glow(new Color(1f, 0.95f, 0.85f)), null, new Vector3(-0.35f, H - 0.06f, 2.2f), new Vector3(0.35f, H, 2.9f), new Vector2(1, 1));
        }

        static void Furnish(Transform root, int k)
        {
            void Mod(string n, Vector3 p, float yaw, float fit, Color? tint = null) => ModelLib.Place(root, n, p, yaw, fit, false, tint);
            switch (k)
            {
                case 0: // living room
                    Mod("sofa_02", new Vector3(0, 0, 4.35f), 180, 2.2f, new Color(0.75f, 0.7f, 0.65f));
                    Mod("modern_coffee_table_01", new Vector3(0, 0, 3.2f), 0, 1.1f);
                    Mod("Television_01", new Vector3(1.75f, 0.5f, 2.6f), 270, 1.2f);
                    Mod("side_table_01", new Vector3(1.75f, 0, 2.6f), 0, 1.2f);
                    Mod("wooden_bookshelf_worn", new Vector3(-1.8f, 0, 1.6f), 90, 1.4f);
                    Mod("potted_plant_04", new Vector3(1.6f, 0, 4.5f), 0, 0.8f);
                    Mod("modern_arm_chair_01", new Vector3(-1.2f, 0, 2.9f), 120, 0.9f);
                    break;
                case 1: // bedroom
                    Mod("GothicBed_01", new Vector3(0, 0, 3.9f), 180, 2.0f);
                    Mod("ClassicNightstand_01", new Vector3(-1.4f, 0, 4.6f), 180, 0.5f);
                    Mod("ClassicNightstand_01", new Vector3(1.4f, 0, 4.6f), 180, 0.5f);
                    Mod("wooden_bookshelf_worn", new Vector3(1.8f, 0, 1.6f), 270, 1.3f);
                    Mod("potted_plant_04", new Vector3(-1.6f, 0, 1.2f), 0, 0.6f);
                    break;
                case 2: // office
                    for (int i = 0; i < 2; i++)
                    {
                        var p = new Vector3(-0.9f + i * 1.8f, 0, 3.6f);
                        Mod("metal_office_desk", p, 180, 1.5f);
                        Mod("classic_laptop", p + new Vector3(0, 0.76f, 0), 0, 0.35f);
                        Mod("ArmChair_01", p + new Vector3(0, 0, -0.9f), 180, 0.65f, new Color(0.25f, 0.25f, 0.28f));
                    }
                    Mod("steel_frame_shelves_01", new Vector3(-1.8f, 0, 1.6f), 90, 1.4f);
                    Mod("potted_plant_04", new Vector3(1.7f, 0, 1.0f), 0, 0.8f);
                    break;
                default: // kitchen and dining
                {
                    var b = new MeshB();
                    var oak = Photo.Mat("oak_veneer_01", Color.white, 0.4f);
                    var top = Photo.Mat("marble_01", Color.white, 0.7f);
                    b.Box(oak, top, new Vector3(-2f, 0, Dp - 0.6f), new Vector3(1.2f, 0.9f, Dp), new Vector2(1, 1));
                    b.Box(oak, oak, new Vector3(-2f, 1.5f, Dp - 0.35f), new Vector3(1.2f, 2.2f, Dp), new Vector2(1, 1));
                    b.Box(Mats.Lit(new Color(0.8f, 0.8f, 0.82f), 0.8f, 0.6f), null, new Vector3(1.25f, 0, Dp - 0.7f), new Vector3(2f, 2.0f, Dp), new Vector2(1, 1));
                    b.Build("Kitchen", root, Layer, false);
                    Mod("wooden_table_02", new Vector3(0, 0, 2.4f), 0, 1.5f);
                    Mod("ArmChair_01", new Vector3(-0.9f, 0, 2.4f), 90, 0.6f, new Color(0.6f, 0.45f, 0.3f));
                    Mod("ArmChair_01", new Vector3(0.9f, 0, 2.4f), 270, 0.6f, new Color(0.6f, 0.45f, 0.3f));
                    Mod("potted_plant_04", new Vector3(-1.7f, 0, 1.0f), 0, 0.7f);
                    break;
                }
            }
        }

        static RenderTexture faceRT;
        static Texture2D faceTex;

        /// <summary>
        /// Six 90° pictures into a cubemap (URP has no Camera.RenderToCubemap). Faces follow the
        /// cubemap layout: the side faces upright, +Y looking up with -Z at the top of the picture,
        /// -Y looking down with +Z at the top; rows go top to bottom.
        /// </summary>
        static void Capture(Camera cam, Cubemap cube)
        {
            int S = cube.width;
            if (faceRT == null || faceRT.width != S) { faceRT = new RenderTexture(S, S, 24, RenderTextureFormat.ARGB32); faceTex = new Texture2D(S, S, TextureFormat.RGBA32, false); }
            cam.fieldOfView = 90f; cam.aspect = 1f; cam.targetTexture = faceRT;
            (CubemapFace face, Vector3 fwd, Vector3 up)[] faces =
            {
                (CubemapFace.PositiveX, Vector3.right, Vector3.up), (CubemapFace.NegativeX, Vector3.left, Vector3.up),
                (CubemapFace.PositiveY, Vector3.up, Vector3.back), (CubemapFace.NegativeY, Vector3.down, Vector3.forward),
                (CubemapFace.PositiveZ, Vector3.forward, Vector3.up), (CubemapFace.NegativeZ, Vector3.back, Vector3.up),
            };
            var prev = RenderTexture.active;
            foreach (var (face, fwd, up) in faces)
            {
                cam.transform.rotation = Quaternion.LookRotation(fwd, up);
                cam.Render();
                RenderTexture.active = faceRT;
                faceTex.ReadPixels(new Rect(0, 0, S, S), 0, 0, false);
                var px = faceTex.GetPixels();
                // ReadPixels gives the bottom row first; cubemap faces start at the top
                var flipped = new Color[px.Length];
                for (int y = 0; y < S; y++) System.Array.Copy(px, y * S, flipped, (S - 1 - y) * S, S);
                cube.SetPixels(flipped, face);
            }
            RenderTexture.active = prev;
            cam.targetTexture = null;
            cube.Apply(true);
        }

        // ------------------------------------------------------------------ the view out of a window

        static Cubemap view;
        public static Cubemap ViewCube => view;
        static Material viewMat;

        /// <summary>
        /// Record the city around a point (a flat or an office at its floor height) and return the
        /// window material that shows it: every window then looks out on the real city from there.
        /// </summary>
        public static Material ViewFrom(Vector3 eye)
        {
            if (view == null) view = new Cubemap(256, TextureFormat.RGBA32, false) { name = "view" };
            var cam = new GameObject("ViewCam").AddComponent<Camera>();
            cam.transform.position = eye;
            cam.cullingMask = ~((1 << Layer) | (1 << 15) | (1 << 8) | (1 << 11) | (1 << 5));   // the city without people and UI
            cam.nearClipPlane = 1.5f; cam.farClipPlane = 1500f;
            cam.clearFlags = CameraClearFlags.Skybox;
            // indoors the city is switched off to save time: switch it on just for the picture
            var city = WorldBuilder.Root != null ? WorldBuilder.Root.gameObject : null;
            bool was = city != null && city.activeSelf;
            if (city != null) city.SetActive(true);
            Capture(cam, view);
            if (city != null) city.SetActive(was);
            Object.Destroy(cam.gameObject);
            if (viewMat == null)
            {
                var bm = Resources.Load<Material>("Materials/WindowView");
                viewMat = bm != null ? new Material(bm) : Mats.Glow(new Color(0.65f, 0.82f, 0.98f));
            }
            viewMat.SetTexture("_View", view);
            return viewMat;
        }
    }
}
