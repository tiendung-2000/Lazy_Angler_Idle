using System;
using System.Collections.Generic;
using UnityEngine;

namespace LazyAngler.Systems
{
    public enum SfxId { Click, Cast, Splash, Bite, Coin, CoinBig, Reward, Quest, LevelUp, Hit, Miss, Win, Lose }

    /// <summary>
    /// Âm thanh hiệu ứng của game. Chưa có file âm thanh nên mọi âm được tổng hợp bằng code (sóng sin/vuông/nhiễu)
    /// một lần khi cần, sau đó cache. Có file âm thật thì gọi Sfx.Override(id, clip) để thay thế mà không phải sửa chỗ gọi.
    /// Bật/tắt và âm lượng lưu trong PlayerPrefs.
    /// </summary>
    public static class Sfx
    {
        private const string EnabledKey = "lazyangler.sfx.enabled";
        private const string VolumeKey = "lazyangler.sfx.volume";
        private const int SampleRate = 22050;

        private static readonly Dictionary<SfxId, AudioClip> Clips = new Dictionary<SfxId, AudioClip>();
        private static bool? _enabled;
        private static float? _volume;
        private static SfxHost _host;

        public static bool Enabled
        {
            get => _enabled ?? (_enabled = PlayerPrefs.GetInt(EnabledKey, 1) != 0).Value;
            set
            {
                _enabled = value;
                PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
            }
        }

        public static float Volume
        {
            get => _volume ?? (_volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 0.7f))).Value;
            set
            {
                _volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, _volume.Value);
            }
        }

        /// <summary>Thay âm tổng hợp bằng clip thật (vd. kéo file vào Assets/_Project/Audio/SFX rồi gán lúc khởi động).</summary>
        public static void Override(SfxId id, AudioClip clip) => Clips[id] = clip;

        public static void Play(SfxId id, float volume = 1f, float pitch = 1f)
        {
            if (!Enabled || Volume <= 0f) return;
            if (!Application.isPlaying) return;
            if (_host == null)
            {
                var go = new GameObject("Sfx") { hideFlags = HideFlags.HideAndDontSave };
                _host = go.AddComponent<SfxHost>();
            }
            _host.Play(GetClip(id), volume * Volume, pitch);
        }

        private static AudioClip GetClip(SfxId id)
        {
            if (Clips.TryGetValue(id, out var clip) && clip != null) return clip;
            clip = Synthesize(id);
            Clips[id] = clip;
            return clip;
        }

        // ---------- Tổng hợp ----------

        private enum Wave { Sine, Square, Noise }

        private static AudioClip Synthesize(SfxId id)
        {
            var s = new SampleBuilder();
            switch (id)
            {
                case SfxId.Click:
                    s.Tone(1800f, 0.04f, Wave.Square, 0.0f, 0.25f, 40f);
                    break;
                case SfxId.Cast: // vút: nhiễu mềm tăng rồi giảm
                    s.Sweep(0.28f, 0.22f);
                    break;
                case SfxId.Splash: // tõm: nhiễu tắt dần + tiếng ục thấp
                    s.Noise(0.32f, 0.55f, 9f, smooth: 3);
                    s.Tone(180f, 0.18f, Wave.Sine, 0.0f, 0.35f, 14f);
                    break;
                case SfxId.Bite: // tinh tinh: hai nốt lên nhanh
                    s.Tone(880f, 0.09f, Wave.Sine, 0.00f, 0.6f, 18f);
                    s.Tone(1320f, 0.16f, Wave.Sine, 0.08f, 0.6f, 14f);
                    s.Tone(2640f, 0.10f, Wave.Sine, 0.08f, 0.15f, 20f);
                    break;
                case SfxId.Coin: // ting
                    s.Tone(1319f, 0.07f, Wave.Sine, 0.00f, 0.5f, 22f);
                    s.Tone(1760f, 0.22f, Wave.Sine, 0.06f, 0.5f, 12f);
                    s.Tone(3520f, 0.12f, Wave.Sine, 0.06f, 0.12f, 20f);
                    break;
                case SfxId.CoinBig: // ting ting ting
                    s.Tone(1319f, 0.08f, Wave.Sine, 0.00f, 0.5f, 20f);
                    s.Tone(1568f, 0.08f, Wave.Sine, 0.07f, 0.5f, 20f);
                    s.Tone(2093f, 0.28f, Wave.Sine, 0.14f, 0.55f, 9f);
                    s.Tone(4186f, 0.12f, Wave.Sine, 0.14f, 0.12f, 18f);
                    break;
                case SfxId.Reward: // nhận thưởng: arpeggio đi lên
                    Arpeggio(s, new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.085f, 0.3f);
                    break;
                case SfxId.Quest: // hoàn thành nhiệm vụ: hai nốt
                    s.Tone(784f, 0.12f, Wave.Sine, 0.00f, 0.5f, 12f);
                    s.Tone(1175f, 0.3f, Wave.Sine, 0.11f, 0.55f, 7f);
                    break;
                case SfxId.LevelUp: // fanfare: arpeggio dài + hợp âm giữ
                    Arpeggio(s, new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f }, 0.1f, 0.22f);
                    s.Tone(1046.5f, 0.55f, Wave.Square, 0.5f, 0.12f, 3.5f);
                    s.Tone(1318.5f, 0.55f, Wave.Sine, 0.5f, 0.3f, 3.5f);
                    s.Tone(1568f, 0.55f, Wave.Sine, 0.5f, 0.3f, 3.5f);
                    break;
                case SfxId.Hit:
                    s.Tone(1046f, 0.06f, Wave.Sine, 0.00f, 0.55f, 18f);
                    s.Tone(1568f, 0.14f, Wave.Sine, 0.05f, 0.55f, 11f);
                    break;
                case SfxId.Miss:
                    s.Tone(220f, 0.16f, Wave.Square, 0.00f, 0.25f, 9f);
                    s.Tone(165f, 0.2f, Wave.Square, 0.1f, 0.25f, 9f);
                    break;
                case SfxId.Win:
                    Arpeggio(s, new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 783.99f, 1046.5f }, 0.09f, 0.28f);
                    break;
                case SfxId.Lose: // đi xuống
                    s.Tone(392f, 0.18f, Wave.Sine, 0.00f, 0.45f, 8f);
                    s.Tone(311f, 0.18f, Wave.Sine, 0.16f, 0.45f, 8f);
                    s.Tone(233f, 0.4f, Wave.Sine, 0.32f, 0.45f, 5f);
                    break;
            }
            var data = s.Build(out float seconds);
            var clip = AudioClip.Create("sfx_" + id, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            return clip;
        }

        private static void Arpeggio(SampleBuilder s, float[] notes, float step, float length)
        {
            for (int i = 0; i < notes.Length; i++)
            {
                s.Tone(notes[i], length, Wave.Sine, i * step, 0.5f, 8f);
                s.Tone(notes[i] * 2f, length * 0.6f, Wave.Sine, i * step, 0.1f, 12f);
            }
        }

        /// <summary>Trộn nhiều âm vào một bộ đệm mono. decay: tốc độ tắt dần theo hàm mũ (lớn = tắt nhanh).</summary>
        private class SampleBuilder
        {
            private float[] _buffer = new float[SampleRate / 4];
            private readonly System.Random _rng = new System.Random(1234);

            private void Ensure(int length)
            {
                if (length > _buffer.Length) Array.Resize(ref _buffer, length);
            }

            public void Tone(float freq, float seconds, Wave wave, float startSeconds, float amplitude, float decay)
            {
                int start = (int)(startSeconds * SampleRate), count = (int)(seconds * SampleRate);
                Ensure(start + count);
                for (int i = 0; i < count; i++)
                {
                    float t = i / (float)SampleRate;
                    float env = Mathf.Exp(-decay * t) * Mathf.Min(1f, i / 60f); // vào nhẹ 60 mẫu để không bị tách
                    float phase = 2f * Mathf.PI * freq * t;
                    float v = wave == Wave.Square ? Mathf.Sign(Mathf.Sin(phase)) : Mathf.Sin(phase);
                    _buffer[start + i] += v * env * amplitude;
                }
            }

            public void Noise(float seconds, float amplitude, float decay, int smooth)
            {
                int count = (int)(seconds * SampleRate);
                Ensure(count);
                float prev = 0f;
                for (int i = 0; i < count; i++)
                {
                    float t = i / (float)SampleRate;
                    float raw = (float)(_rng.NextDouble() * 2.0 - 1.0);
                    prev += (raw - prev) / Mathf.Max(1, smooth); // lọc thông thấp đơn giản
                    _buffer[i] += prev * Mathf.Exp(-decay * t) * amplitude * 2f;
                }
            }

            public void Sweep(float seconds, float amplitude)
            {
                int count = (int)(seconds * SampleRate);
                Ensure(count);
                float prev = 0f;
                for (int i = 0; i < count; i++)
                {
                    float u = i / (float)count;
                    float raw = (float)(_rng.NextDouble() * 2.0 - 1.0);
                    float k = Mathf.Lerp(0.05f, 0.5f, Mathf.Sin(u * Mathf.PI)); // lọc mở dần rồi khép
                    prev += (raw - prev) * k;
                    _buffer[i] += prev * Mathf.Sin(u * Mathf.PI) * amplitude * 2.5f;
                }
            }

            public float[] Build(out float seconds)
            {
                int length = _buffer.Length;
                while (length > 1 && Mathf.Abs(_buffer[length - 1]) < 1e-4f) length--;
                var data = new float[length];
                float peak = 0f;
                for (int i = 0; i < length; i++) peak = Mathf.Max(peak, Mathf.Abs(_buffer[i]));
                float norm = peak > 0.9f ? 0.9f / peak : 1f; // tránh méo khi trộn nhiều âm
                for (int i = 0; i < length; i++) data[i] = _buffer[i] * norm;
                seconds = length / (float)SampleRate;
                return data;
            }
        }

        /// <summary>Nguồn phát: vài AudioSource luân phiên để các âm chồng lên nhau được.</summary>
        private class SfxHost : MonoBehaviour
        {
            private const int Voices = 8;
            private AudioSource[] _sources;
            private int _next;

            private void Awake()
            {
                // Scene do builder dựng không có AudioListener; thiếu thì không nghe được gì.
                if (FindFirstObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
                _sources = new AudioSource[Voices];
                for (int i = 0; i < Voices; i++)
                {
                    _sources[i] = gameObject.AddComponent<AudioSource>();
                    _sources[i].playOnAwake = false;
                    _sources[i].spatialBlend = 0f;
                }
            }

            public void Play(AudioClip clip, float volume, float pitch)
            {
                if (clip == null || _sources == null) return;
                var src = _sources[_next];
                _next = (_next + 1) % Voices;
                src.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
                src.PlayOneShot(clip, Mathf.Clamp01(volume));
            }
        }
    }
}
