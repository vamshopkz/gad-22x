using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gad
{
    /// <summary>
    /// A small photo studio far below the world: renders a live, slowly turning preview of the
    /// character for the wardrobe, and thumbnail pictures of every clothing item worn on the body
    /// (hair close-up, the top on the torso, trousers on the legs, shoes on the feet…).
    /// </summary>
    public class Studio : MonoBehaviour
    {
        public static Studio I;
        static readonly Vector3 O = new Vector3(5000, -3000, 0);
        Camera cam;
        Person live, iconPerson;
        public RenderTexture LiveRT;
        RenderTexture iconRT;
        readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();
        readonly Queue<(string key, SkinSet look, Wardrobe.Slot slot, System.Action<Texture2D> done)> queue = new Queue<(string, SkinSet, Wardrobe.Slot, System.Action<Texture2D>)>();
        float yaw = 200f;

        public static Studio Get()
        {
            if (I != null) return I;
            var go = new GameObject("Studio");
            go.transform.position = O;
            I = go.AddComponent<Studio>();
            I.Build();
            return I;
        }

        void Build()
        {
            var t = transform;
            // backdrop + floor
            Prim.Box(t, new Vector3(0, 1.5f, 2.5f), new Vector3(8, 6, 0.1f), new Color(0.12f, 0.14f, 0.2f));
            Prim.Cyl(t, new Vector3(0, -0.02f, 0), new Vector3(2.2f, 0.02f, 2.2f), new Color(0.2f, 0.22f, 0.28f));
            var key = new GameObject("Key").AddComponent<Light>();
            key.transform.SetParent(t, false); key.transform.localPosition = new Vector3(-1.5f, 2.6f, -2f);
            key.type = LightType.Spot; key.spotAngle = 70; key.range = 8; key.intensity = 6f; key.transform.LookAt(t.position + Vector3.up);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(t, false); fill.transform.localPosition = new Vector3(2f, 1.4f, -1.5f);
            fill.type = LightType.Point; fill.range = 6; fill.intensity = 2.2f; fill.color = new Color(0.8f, 0.88f, 1f);
            var rim = new GameObject("Rim").AddComponent<Light>();
            rim.transform.SetParent(t, false); rim.transform.localPosition = new Vector3(0.5f, 2.5f, 1.6f);
            rim.type = LightType.Point; rim.range = 5; rim.intensity = 2.5f; rim.color = new Color(0.4f, 0.85f, 1f);
            var cg = new GameObject("StudioCam");
            cg.transform.SetParent(t, false);
            cam = cg.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.12f, 0.18f, 1f);
            cam.fieldOfView = 24; cam.nearClipPlane = 0.1f; cam.farClipPlane = 20f;
            LiveRT = new RenderTexture(480, 800, 24) { name = "StudioLive", antiAliasing = 2 };
            iconRT = new RenderTexture(192, 192, 24) { name = "StudioIcon", antiAliasing = 2 };
        }

        public void SetLive(SkinSet look)
        {
            if (live != null) Destroy(live.gameObject);
            live = Person.Create(transform, look, "LivePreview", true);
            live.transform.localPosition = Vector3.zero;
        }

        public void Hide()
        {
            if (live != null) Destroy(live.gameObject);
            live = null;
            queue.Clear();
        }

        static (float y, float size) Frame(Wardrobe.Slot s) => s switch
        {
            Wardrobe.Slot.HHair or Wardrobe.Slot.HEyes or Wardrobe.Slot.HMouth or Wardrobe.Slot.HFace => (1.66f, 0.42f),
            Wardrobe.Slot.HAcc => (1.68f, 0.46f),
            Wardrobe.Slot.HTop => (1.25f, 0.85f),
            Wardrobe.Slot.HBottom => (0.62f, 1.15f),
            Wardrobe.Slot.HShoes => (0.1f, 0.42f),
            _ => (0.92f, 2.0f),
        };

        void Aim(float y, float size, float turn)
        {
            float dist = size / 2f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) + 0.3f;
            var c = O + Vector3.up * y;
            var dir = Quaternion.Euler(8, turn, 0) * Vector3.back;
            cam.transform.position = c + dir * dist;
            cam.transform.LookAt(c);
        }

        /// <summary>Picture of one item (cached). The callback runs when it is ready (a few per frame).</summary>
        public Texture2D Icon(string key, SkinSet look, Wardrobe.Slot slot, System.Action<Texture2D> done)
        {
            if (icons.TryGetValue(key, out var t)) return t;
            queue.Enqueue((key, look, slot, done));
            return null;
        }

        void LateUpdate()
        {
            if (live != null)
            {
                yaw += Time.unscaledDeltaTime;
                // faces the camera, swinging ±50° to show the sides and a glimpse of the back
                live.transform.localRotation = Quaternion.Euler(0, 180f + Mathf.Sin(yaw * 0.6f) * 50f, 0);
                cam.aspect = LiveRT.width / (float)LiveRT.height;
                cam.fieldOfView = 24;
                Aim(0.95f, 2.15f, 0);
                cam.targetTexture = LiveRT;
                if (iconPerson != null) iconPerson.gameObject.SetActive(false);
                cam.Render();
            }
            // one thumbnail per frame
            if (queue.Count > 0)
            {
                var (key, look, slot, done) = queue.Dequeue();
                if (icons.TryGetValue(key, out var have)) { done?.Invoke(have); return; }
                if (live != null) live.gameObject.SetActive(false);
                if (iconPerson != null) Destroy(iconPerson.gameObject);
                iconPerson = Person.Create(transform, look, "IconPerson", true);
                iconPerson.transform.localPosition = Vector3.zero;
                iconPerson.transform.localRotation = Quaternion.Euler(0, slot == Wardrobe.Slot.HShoes ? 150 : 180 + 20, 0);
                iconPerson.enabled = false;
                var (y, size) = Frame(slot);
                cam.aspect = 1; cam.targetTexture = iconRT;
                Aim(y, size, 0);
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = iconRT;
                var tex = new Texture2D(iconRT.width, iconRT.height, TextureFormat.RGB24, false) { name = "icon " + key };
                tex.ReadPixels(new Rect(0, 0, iconRT.width, iconRT.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                icons[key] = tex;
                Destroy(iconPerson.gameObject); iconPerson = null;
                if (live != null) live.gameObject.SetActive(true);
                done?.Invoke(tex);
            }
        }
    }
}
