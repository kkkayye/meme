using System;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Juice
{
    /// <summary>Synthesized sound effects (AudioClip.Create at startup) played through a small pool of AudioSources with cosmetic pitch jitter.</summary>
    public sealed class SfxSynth
    {
        public enum Sound { Cast, Hit, Kill, Pickup, Click, Reveal }

        private const int PoolSize = 8;
        private readonly AudioClip[] _clips = new AudioClip[6];
        private readonly AudioSource[] _sources;
        private int _next;
        private static readonly System.Random Noise = new System.Random(12345);

        public SfxSynth(GameObject host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            _sources = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                AudioSource source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                _sources[i] = source;
            }
            _clips[(int)Sound.Cast] = Build("sfx_cast", 0.12f, (t, d) => Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(300f, 900f, t / d) * t) * Envelope(t, d, 0.01f, 0.06f));
            _clips[(int)Sound.Hit] = Build("sfx_hit", 0.08f, (t, d) => NoiseSample() * Envelope(t, d, 0.002f, 0.05f));
            _clips[(int)Sound.Kill] = Build("sfx_kill", 0.28f, (t, d) => (Mathf.Sin(2f * Mathf.PI * (t < d * 0.5f ? 440f : 660f) * t) * 0.7f + NoiseSample() * 0.2f) * Envelope(t, d, 0.005f, 0.15f));
            _clips[(int)Sound.Pickup] = Build("sfx_pickup", 0.26f, (t, d) => Mathf.Sin(2f * Mathf.PI * Arpeggio(t, d) * t) * Envelope(t, d, 0.005f, 0.08f));
            _clips[(int)Sound.Click] = Build("sfx_click", 0.03f, (t, d) => Mathf.Sin(2f * Mathf.PI * 1200f * t) * Envelope(t, d, 0.001f, 0.02f));
            _clips[(int)Sound.Reveal] = Build("sfx_reveal", 0.32f, (t, d) => (Mathf.Sin(2f * Mathf.PI * 880f * t) + Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.5f) * 0.6f * Envelope(t, d, 0.01f, 0.2f));
        }

        /// <summary>Plays a sound at the configured volume with ±5% pitch jitter (UnityEngine.Random is fine here: cosmetic only).</summary>
        public void Play(Sound sound, float volumeScale = 1f)
        {
            AudioClip clip = _clips[(int)sound];
            if (clip == null) return;
            AudioSource source = _sources[_next];
            _next = (_next + 1) % _sources.Length;
            source.pitch = 1f + UnityEngine.Random.Range(-GameConstants.SfxPitchJitter, GameConstants.SfxPitchJitter);
            source.PlayOneShot(clip, GameConstants.SfxVolume * volumeScale);
        }

        private static AudioClip Build(string name, float seconds, Func<float, float, float> generator)
        {
            int rate = GameConstants.SfxSampleRate;
            int samples = Mathf.Max(1, Mathf.RoundToInt(seconds * rate));
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / rate;
                data[i] = Mathf.Clamp(generator(t, seconds), -1f, 1f);
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Envelope(float t, float duration, float attack, float release)
        {
            float a = attack > 0f ? Mathf.Clamp01(t / attack) : 1f;
            float r = Mathf.Clamp01((duration - t) / Mathf.Max(0.001f, release));
            return Mathf.Min(a, r);
        }

        private static float Arpeggio(float t, float duration)
        {
            float third = duration / 3f;
            if (t < third) return 523f;
            if (t < third * 2f) return 659f;
            return 784f;
        }

        private static float NoiseSample()
        {
            return (float)(Noise.NextDouble() * 2.0 - 1.0);
        }
    }
}
