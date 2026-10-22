using System.Collections.Generic;
using UnityEngine;

namespace Gad
{
    /// <summary>All sounds are synthesized at start (no audio files): UI clicks, coins, doors, engine, rain.</summary>
    public class AudioFx : MonoBehaviour
    {
        static AudioFx I;
        AudioSource ui, engine, rain, city;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        const int SR = 22050;

        public static void Create()
        {
            var go = new GameObject("Audio");
            I = go.AddComponent<AudioFx>();
            I.ui = go.AddComponent<AudioSource>();
            I.engine = go.AddComponent<AudioSource>();
            I.rain = go.AddComponent<AudioSource>();
            I.city = go.AddComponent<AudioSource>();
            I.Make();
            AudioListener.volume = Save.D.music == 0 ? 0 : 1;
        }

        delegate float Wave(float t);

        AudioClip Clip(string name, float len, Wave w, bool loop = false)
        {
            int n = (int)(SR * len);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(w((float)i / SR), -1, 1);
            if (loop)
            {
                int fade = SR / 20;
                for (int i = 0; i < fade; i++) { float k = (float)i / fade; data[i] = data[i] * k + data[n - fade + i] * (1 - k); }
            }
            var c = AudioClip.Create(name, n, 1, SR, false);
            c.SetData(data, 0);
            clips[name] = c;
            return c;
        }

        static float Env(float t, float a, float d) => t < a ? t / a : Mathf.Exp(-(t - a) / d);

        void Make()
        {
            var rnd = new System.Random(5);
            float Noise() => (float)(rnd.NextDouble() * 2 - 1);
            Clip("click", 0.06f, t => Mathf.Sin(t * 2 * Mathf.PI * 900) * Env(t, 0.002f, 0.015f) * 0.35f);
            Clip("coin", 0.35f, t => (Mathf.Sin(t * 2 * Mathf.PI * (t < 0.08f ? 1320 : 1760)) * 0.5f + Mathf.Sin(t * 2 * Mathf.PI * 2640) * 0.15f) * Env(t, 0.004f, 0.12f) * 0.45f);
            Clip("buy", 0.4f, t => (Mathf.Sin(t * 2 * Mathf.PI * 660) + Mathf.Sin(t * 2 * Mathf.PI * (t < 0.12f ? 880 : 1100))) * 0.25f * Env(t, 0.005f, 0.15f));
            Clip("error", 0.3f, t => Mathf.Sign(Mathf.Sin(t * 2 * Mathf.PI * 160)) * 0.18f * Env(t, 0.005f, 0.12f));
            Clip("door", 0.5f, t => (Noise() * 0.3f + Mathf.Sin(t * 2 * Mathf.PI * 90) * 0.5f) * Env(t, 0.01f, 0.08f) * 0.5f);
            Clip("jump", 0.2f, t => Mathf.Sin(t * 2 * Mathf.PI * (300 + t * 1200)) * Env(t, 0.005f, 0.07f) * 0.25f);
            Clip("eat", 0.5f, t => Noise() * (Mathf.Sin(t * 40) > 0.3f ? 1 : 0.1f) * Env(t, 0.01f, 0.25f) * 0.25f);
            Clip("chat", 0.25f, t => Mathf.Sin(t * 2 * Mathf.PI * (t < 0.1f ? 990 : 1320)) * Env(t, 0.004f, 0.06f) * 0.3f);
            Clip("crash", 0.8f, t => Noise() * Env(t, 0.003f, 0.2f) * 0.8f);
            float ph = 0;
            engine.clip = Clip("engine", 1f, t => { ph += 2 * Mathf.PI * 55f / SR; return (Mathf.Sin(ph) * 0.5f + Mathf.Sin(ph * 2) * 0.25f + Mathf.Sin(ph * 3.01f) * 0.12f + Noise() * 0.05f) * 0.5f; }, true);
            engine.loop = true; engine.volume = 0; engine.Play();
            float lp = 0;
            rain.clip = Clip("rain", 2f, t => { lp = lp * 0.6f + Noise() * 0.4f; return lp * 0.5f; }, true);
            rain.loop = true; rain.volume = 0; rain.Play();
            float lp2 = 0;
            city.clip = Clip("city", 3f, t => { lp2 = lp2 * 0.985f + Noise() * 0.015f; return lp2 * 2.2f + Mathf.Sin(t * 2 * Mathf.PI * 0.5f) * 0.01f; }, true);
            city.loop = true; city.volume = 0.25f; city.Play();
        }

        public static void Play(string name, float vol = 1f)
        {
            if (I == null || !I.clips.TryGetValue(name, out var c)) return;
            I.ui.PlayOneShot(c, vol);
        }

        public static void Crash(float impact) => Play("crash", Mathf.Clamp01(impact / 20f));

        public static void RainVolume(float v) { if (I != null) I.rain.volume = v * 0.5f; }

        void Update()
        {
            var pl = PlayerCtl.I;
            float sp = pl != null && pl.Car != null ? Mathf.Abs(pl.Car.Speed) : -1;
            engine.volume = Mathf.MoveTowards(engine.volume, sp >= 0 ? 0.35f : 0, Time.deltaTime * 2);
            if (sp >= 0) engine.pitch = 0.7f + sp / 25f + Mathf.Abs(pl.Car.Input.y) * 0.15f;
            city.volume = pl != null && pl.Inside >= 0 ? 0.05f : 0.25f;
        }
    }
}
