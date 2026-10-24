using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gad
{
    /// <summary>
    /// Day and night shared by all players: game time comes from the real clock
    /// (1 real minute = 1 game hour, a day lasts 24 minutes). Sun, sky, ambient light, fog,
    /// lit windows and street lamps at night.
    /// </summary>
    public class DayNight : MonoBehaviour
    {
        public static DayNight I;
        public static float Hour;           // 0..24
        public static bool IsNight => Hour < 6.2f || Hour > 19.8f;
        public static float NightK;         // 0 day .. 1 night
        public static float TestHour = -1;  // autotest override
        Light sun;
        Material sky, pano;
        bool lastNight;

        public static void Create()
        {
            var go = new GameObject("DayNight");
            I = go.AddComponent<DayNight>();
            var sg = new GameObject("Sun");
            sg.transform.SetParent(go.transform);
            I.sun = sg.AddComponent<Light>();
            I.sun.type = LightType.Directional;
            I.sun.shadows = LightShadows.Soft;
            I.sun.shadowStrength = 0.75f;
            RenderSettings.sun = I.sun;
            I.sky = RenderSettings.skybox != null ? new Material(RenderSettings.skybox) : null;
            if (I.sky != null) RenderSettings.skybox = I.sky;
            var hdr = Resources.Load<Texture>("Sky/sky_day");
            var pm = Resources.Load<Material>("Materials/SkyPano");
            if (hdr != null && pm != null)
            {
                I.pano = new Material(pm);
                I.pano.SetTexture("_MainTex", hdr);
                I.pano.SetFloat("_Mapping", 1);
                I.pano.SetFloat("_ImageType", 0);
                I.pano.SetFloat("_Rotation", 40);
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
        }

        public static string ClockText()
        {
            int h = (int)Hour, m = (int)((Hour - h) * 60);
            return $"{h:00}:{m:00}";
        }

        void Update()
        {
            double secs = Save.Now;
            Hour = TestHour >= 0 ? TestHour : (float)((secs / 60.0 + 9.0) % 24.0); // +9 so most short sessions start in daytime
            float sunAng = (Hour - 6f) / 12f * 180f;                 // 0 at 6:00, 180 at 18:00
            float elev = Mathf.Sin(sunAng * Mathf.Deg2Rad);          // >0 day
            NightK = Mathf.Clamp01(1f - (elev + 0.15f) / 0.35f);
            bool inside = PlayerCtl.I != null && PlayerCtl.I.Inside >= 0;
            float wx = Weather.Cloud;

            // sun / moon
            float pitch = elev > -0.1f ? Mathf.Clamp(sunAng, 6f, 174f) : 40f;   // moonlight from a fixed angle at night
            sun.transform.rotation = Quaternion.Euler(pitch, 30f + Hour * 7.5f, 0);
            var dayCol = Color.Lerp(new Color(1f, 0.6f, 0.35f), new Color(1f, 0.96f, 0.9f), Mathf.Clamp01(elev * 3f));
            sun.color = Color.Lerp(dayCol, new Color(0.45f, 0.55f, 0.85f), NightK);
            sun.intensity = inside ? 0 : Mathf.Lerp(1.25f, 0.18f, NightK) * Mathf.Lerp(1f, 0.45f, wx);
            sun.shadowStrength = Mathf.Lerp(0.8f, 0.3f, Mathf.Max(NightK, wx));

            Color skyTop = Color.Lerp(new Color(0.35f, 0.6f, 0.95f), new Color(0.02f, 0.03f, 0.08f), NightK);
            skyTop = Color.Lerp(skyTop, new Color(0.45f, 0.48f, 0.52f) * (1 - NightK * 0.85f), wx * 0.8f);
            Color horizon = Color.Lerp(new Color(0.75f, 0.83f, 0.92f), new Color(0.06f, 0.07f, 0.14f), NightK);
            if (elev > -0.1f && elev < 0.25f) horizon = Color.Lerp(horizon, new Color(1f, 0.55f, 0.35f), (1 - Mathf.Abs(elev - 0.07f) / 0.18f) * 0.6f * (1 - wx));
            horizon = Color.Lerp(horizon, new Color(0.6f, 0.62f, 0.65f) * (1 - NightK * 0.85f), wx * 0.7f);
            if (inside)
            {
                RenderSettings.ambientSkyColor = new Color(0.75f, 0.75f, 0.78f);
                RenderSettings.ambientEquatorColor = new Color(0.6f, 0.6f, 0.62f);
                RenderSettings.ambientGroundColor = new Color(0.4f, 0.38f, 0.36f);
                RenderSettings.fog = false;
            }
            else
            {
                RenderSettings.ambientSkyColor = Color.Lerp(skyTop * 1.1f, new Color(0.1f, 0.12f, 0.22f), NightK * 0.6f);
                RenderSettings.ambientEquatorColor = Color.Lerp(horizon, new Color(0.12f, 0.12f, 0.18f), NightK * 0.5f);
                RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.35f, 0.32f, 0.28f), new Color(0.05f, 0.05f, 0.07f), NightK);
                RenderSettings.fog = true;
                RenderSettings.fogColor = horizon;
            }
            bool usePano = pano != null && NightK < 0.6f && !inside;
            var wantSky = usePano ? pano : sky;
            if (wantSky != null && RenderSettings.skybox != wantSky) RenderSettings.skybox = wantSky;
            if (usePano)
            {
                float k = 1f - NightK / 0.6f;
                pano.SetFloat("_Exposure", Mathf.Lerp(0.12f, 1.05f, k) * Mathf.Lerp(1f, 0.6f, wx));
                var tint = Color.Lerp(new Color(1f, 0.62f, 0.45f), new Color(0.5f, 0.5f, 0.5f), Mathf.Clamp01(elev * 3f));
                pano.SetColor("_Tint", Color.Lerp(tint, new Color(0.42f, 0.43f, 0.45f), wx * 0.7f));
                if (!inside) RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, new Color(0.72f, 0.78f, 0.86f) * Mathf.Lerp(0.2f, 1f, k), 0.6f);
            }
            if (sky != null)
            {
                sky.SetFloat("_Exposure", Mathf.Lerp(1.25f, 0.12f, NightK) * Mathf.Lerp(1f, 0.75f, wx));
                sky.SetColor("_SkyTint", Color.Lerp(new Color(0.5f, 0.55f, 0.65f), new Color(0.6f, 0.62f, 0.65f), wx));
                sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(1f, 0.6f, NightK) + wx * 0.6f);
                sky.SetColor("_GroundColor", horizon);
            }
            if (PlayerCtl.I != null && PlayerCtl.I.Cam != null)
            {
                PlayerCtl.I.Cam.clearFlags = inside ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
                PlayerCtl.I.Cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
            }

            PostFx.Tick(NightK, inside);
            Shader.SetGlobalFloat("_GadNight", NightK);
            // windows and lamps
            float win = Mathf.SmoothStep(0, 1, NightK) * 0.85f + wx * 0.15f;
            foreach (var m in WorldBuilder.NightMats) m.SetColor("_EmissionColor", Color.white * win);
            if (WorldBuilder.LampGlow != null)
                WorldBuilder.LampGlow.color = Color.Lerp(new Color(0.75f, 0.75f, 0.72f), new Color(1f, 0.85f, 0.55f) * 1.6f, NightK);
            if (IsNight != lastNight)
            {
                lastNight = IsNight;
                if (PlayerCtl.I != null && PlayerCtl.I.Car != null) PlayerCtl.I.Car.SetHeadlights(IsNight);
            }
        }
    }

    /// <summary>
    /// Realtime reflections: one probe that follows the camera and re-renders every few seconds,
    /// so car paint, chrome and glass towers reflect the real sky and city (time of day, weather).
    /// </summary>
    public class Reflections : MonoBehaviour
    {
        ReflectionProbe probe;
        float next;
        Vector3 last;

        public static void Create()
        {
            var go = new GameObject("Reflections");
            var r = go.AddComponent<Reflections>();
            r.probe = go.AddComponent<ReflectionProbe>();
            r.probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            r.probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            r.probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
            r.probe.resolution = 128;
            r.probe.size = new Vector3(4000, 2000, 4000);
            r.probe.boxProjection = false;
            r.probe.hdr = false;
            r.probe.farClipPlane = 600;
            r.probe.importance = 1;
            r.probe.cullingMask = ~((1 << 5) | (1 << 8) | (1 << 15));
        }

        void Update()
        {
            var pl = PlayerCtl.I;
            if (pl == null || pl.Cam == null) return;
            var p = pl.Cam.transform.position;
            bool inside = pl.Inside >= 0;
            transform.position = inside ? p : new Vector3(p.x, Mathf.Max(p.y, 2f), p.z);
            float interval = Gfx.Level == 0 ? 12f : Gfx.Level == 1 ? 4f : 1.5f;
            if (Time.unscaledTime >= next || (p - last).sqrMagnitude > 80f * 80f)
            {
                next = Time.unscaledTime + interval;
                last = p;
                probe.RenderProbe();
            }
        }
    }

    /// <summary>Post-processing: bloom (neon, windows, headlights glow), ACES tonemapping, colour grading, vignette.</summary>
    public static class PostFx
    {
        static Volume vol;
        static Bloom bloom;
        static ColorAdjustments color;
        static Vignette vignette;

        public static void Create()
        {
            var go = new GameObject("PostFx");
            vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.72f);
            bloom.highQualityFiltering.Override(false);
            var tm = p.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);
            color = p.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.35f);
            color.contrast.Override(12f);
            color.saturation.Override(8f);
            vignette = p.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.45f);
            var wb = p.Add<WhiteBalance>(true);
            wb.temperature.Override(0f);
            vol.profile = p;
            Apply();
        }

        public static void Apply()
        {
            var cam = PlayerCtl.I != null ? PlayerCtl.I.Cam : null;
            if (cam != null)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = Gfx.Level > 0;
                data.antialiasing = Gfx.Level == 2 ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;
                cam.allowHDR = Gfx.Level > 0;
            }
            var urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null) urp.supportsHDR = Gfx.Level > 0;
        }

        /// <summary>Night: stronger bloom (neon city), cooler white balance.</summary>
        public static void Tick(float nightK, bool inside)
        {
            if (vol == null) return;
            bloom.intensity.value = inside ? 0.5f : Mathf.Lerp(0.6f, 1.2f, nightK);
            bloom.threshold.value = Mathf.Lerp(1.0f, 0.9f, nightK);
            color.postExposure.value = inside ? 0.25f : Mathf.Lerp(0.3f, 0.55f, nightK);
        }
    }

    /// <summary>Weather shared by everyone (derived from the real clock): clear, clouds, rain, fog.</summary>
    public class Weather : MonoBehaviour
    {
        public static int State;            // 0 clear, 1 clouds, 2 rain, 3 fog
        public static float Cloud, Rain, FogK;
        public static int TestState = -1;
        public static string Name => State switch { 0 => "Ясно", 1 => "Облачно", 2 => "Дождь", _ => "Туман" };
        ParticleSystem rain;

        public static void Create()
        {
            var go = new GameObject("Weather");
            var w = go.AddComponent<Weather>();
            w.rain = MakeRain(go.transform);
        }

        static ParticleSystem MakeRain(Transform parent)
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(parent);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.9f; main.startSpeed = 28f; main.startSize = 0.06f; main.maxParticles = 2500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.75f, 0.8f, 0.9f, 0.55f);
            var em = ps.emission; em.rateOverTime = 0;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(40, 1, 40);
            go.transform.rotation = Quaternion.Euler(90, 0, 0);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.06f; r.lengthScale = 1f;
            r.sharedMaterial = Mats.Particles(Tex.Streak());
            return ps;
        }

        void Update()
        {
            long seg = (long)(Save.Now / 900.0); // changes every 15 real minutes
            float h = H.F((int)seg, 1234);
            State = TestState >= 0 ? TestState : h < 0.5f ? 0 : h < 0.72f ? 1 : h < 0.9f ? 2 : 3;
            bool inside = PlayerCtl.I != null && PlayerCtl.I.Inside >= 0;
            float dt = Time.deltaTime * (TestState >= 0 ? 10f : 0.3f);
            Cloud = Mathf.MoveTowards(Cloud, State == 0 ? 0 : State == 3 ? 0.6f : 1f, dt);
            Rain = Mathf.MoveTowards(Rain, State == 2 ? 1 : 0, dt);
            FogK = Mathf.MoveTowards(FogK, State == 3 ? 1 : State == 2 ? 0.45f : 0, dt);
            float start = Mathf.Lerp(Gfx.FogStart, 15f, FogK), end = Mathf.Lerp(Gfx.FogEnd, 170f, FogK);
            RenderSettings.fogStartDistance = start;
            RenderSettings.fogEndDistance = end;
            var em = rain.emission;
            em.rateOverTime = inside ? 0 : Rain * (Gfx.Level == 0 ? 700 : 1800);
            if (PlayerCtl.I != null && PlayerCtl.I.Cam != null) rain.transform.position = PlayerCtl.I.Cam.transform.position + Vector3.up * 14f + PlayerCtl.I.Cam.transform.forward * 8f;
            AudioFx.RainVolume(inside ? 0 : Rain);
        }
    }

    /// <summary>Graphics quality (auto-picked for phones, changeable in Settings).</summary>
    public static class Gfx
    {
        public static int Level = 1;
        public static float FogStart = 180, FogEnd = 900;
        public static int DetailRadius = 2, TrafficCars = 14, Pedestrians = 12;

        public static void Init()
        {
            int q = Save.D.quality;
            if (q < 0) q = Application.isMobilePlatform ? (WebBridge.IsWeb ? 0 : 1) : 2;
            Set(q, false);
        }

        public static void Set(int level, bool save = true)
        {
            Level = Mathf.Clamp(level, 0, 2);
            if (save) { Save.D.quality = Level; Save.Write(); }
            var urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                urp.shadowDistance = Level == 0 ? 0 : Level == 1 ? 45 : 80;
                urp.renderScale = Level == 0 ? 0.75f : Level == 1 ? 0.9f : 1f;
                urp.msaaSampleCount = Level == 2 ? 4 : 1;
            }
            QualitySettings.shadows = Level == 0 ? UnityEngine.ShadowQuality.Disable : UnityEngine.ShadowQuality.All;
            FogStart = Level == 0 ? 120 : 180;
            FogEnd = Level == 0 ? 520 : Level == 1 ? 750 : 1000;
            DetailRadius = Level == 2 ? 3 : 2;
            TrafficCars = Level == 0 ? 8 : Level == 1 ? 14 : 22;
            Pedestrians = Level == 0 ? 6 : Level == 1 ? 12 : 18;
            if (PlayerCtl.I != null) PlayerCtl.I.Cam.farClipPlane = FogEnd + 200;
            PostFx.Apply();
            Application.targetFrameRate = 60;
        }
    }
}
