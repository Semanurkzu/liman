using System;
using UnityEngine;

namespace Liman.Game
{
    /// <summary>
    /// Placeholder sound effects synthesised at startup, so the game has audio feedback without any
    /// asset files. Swap the clips for recorded ones when real audio arrives.
    /// </summary>
    public sealed class SoundEffects : MonoBehaviour
    {
        private const string MutedKey = "liman.sound.muted";
        private const int SampleRate = 22050;

        private AudioSource _source;

        public AudioClip Pick { get; private set; }
        public AudioClip Drop { get; private set; }
        public AudioClip Load { get; private set; }
        public AudioClip Horn { get; private set; }
        public AudioClip Error { get; private set; }
        public AudioClip Win { get; private set; }
        public AudioClip Star { get; private set; }

        public bool Muted
        {
            get => PlayerPrefs.GetInt(MutedKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(MutedKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;

            var noise = new System.Random(1);
            Pick = Synth("Pick", 0.07f, t => Sine(880f, t) * Mathf.Exp(-t * 45f) * 0.5f);
            Drop = Synth("Drop", 0.14f, t =>
                (Sine(120f, t) + (float)(noise.NextDouble() * 2 - 1) * Mathf.Exp(-t * 40f) * 0.35f) * Mathf.Exp(-t * 28f) * 0.8f);
            Load = Synth("Load", 0.2f, t => (Sine(330f, t) + 0.5f * Sine(495f, t)) * Mathf.Exp(-t * 22f) * 0.5f);
            Horn = Synth("Horn", 0.75f, t =>
            {
                float envelope = Mathf.Min(1f, t / 0.05f) * Mathf.Min(1f, (0.75f - t) / 0.2f);
                return (float)Math.Tanh(3f * (Sine(98f, t) + 0.6f * Sine(147f, t))) * envelope * 0.35f;
            });
            Error = Synth("Error", 0.2f, t =>
            {
                bool gap = t > 0.08f && t < 0.11f;
                return gap ? 0f : Mathf.Sign(Sine(150f, t)) * 0.18f;
            });
            Star = Synth("Star", 0.3f, t => (Sine(1318.5f, t) + 0.4f * Sine(1975.5f, t)) * Mathf.Exp(-t * 14f) * 0.35f);
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            Win = Synth("Win", 0.75f, t =>
            {
                float sum = 0f;
                for (int i = 0; i < notes.Length; i++)
                {
                    float local = t - i * 0.1f;
                    if (local >= 0f) sum += Sine(notes[i], local) * Mathf.Exp(-local * 6f);
                }
                return sum * 0.22f;
            });
        }

        public void Play(AudioClip clip, float volume = 1f)
        {
            if (clip != null && !Muted) _source.PlayOneShot(clip, volume);
        }

        private static float Sine(float frequency, float t) => Mathf.Sin(2f * Mathf.PI * frequency * t);

        private static AudioClip Synth(string name, float duration, Func<float, float> sample)
        {
            int count = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
                data[i] = Mathf.Clamp(sample((float)i / SampleRate), -1f, 1f);
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
