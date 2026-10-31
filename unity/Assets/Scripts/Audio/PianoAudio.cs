using System;
using System.Collections.Generic;
using UnityEngine;

namespace Piamo
{
    /// <summary>
    /// 鍵盤の音と効果音を鳴らす。AudioSource をいくつか用意して、和音やグリッサンドでも重ねて鳴らす。
    ///  - はなすと、楽器ごとの時間で消える（おとを のばす ときは、たたく音は自然に消えるまで鳴らす）
    ///  - 同じ鍵盤をつづけて おすと、前の音は すぐに消す
    ///  - WebGL でも使えることだけ（音量・高さ・左右）で作る（エフェクトは使わない）
    /// </summary>
    public class PianoAudio : MonoBehaviour
    {
        [Serializable]
        public class NamedClip
        {
            public string name;
            public AudioClip clip;
        }

        public InstrumentBank bank;
        public List<NamedClip> sfx = new List<NamedClip>();
        public const int VoiceCount = 24;

        class Voice
        {
            public AudioSource src;
            public int note = -1;
            public bool held;
            public float gain;
            public float level;      // いまの大きさ（0〜1。gain をかける前）
            public float attack;
            public float fadeTime;   // 消えるのにかける時間（0 = 消していない）
            public float fadeLeft;
            public float startTime;
            public long serial;
        }

        Voice[] _voices;
        AudioSource _sfxSource;
        InstrumentBank.Instrument _current;
        InstrumentCatalog.Info _info;
        long _serial;
        readonly Dictionary<string, AudioClip> _sfx = new Dictionary<string, AudioClip>();

        public string CurrentInstrument => _info.id;
        public float Volume { get; set; } = 0.8f;
        public bool Sustain { get; set; }
        public bool UiSounds { get; set; } = true;
        /// <summary>テスト用：最後に鳴らした音（MIDI 番号）と、鳴らした回数。</summary>
        public int LastNote { get; private set; } = -1;
        public int NotesPlayed { get; private set; }
        public string LastSfx { get; private set; }

        public int ActiveVoices
        {
            get
            {
                int n = 0;
                if (_voices == null) return 0;
                foreach (var v in _voices) if (v.note >= 0 && v.src.isPlaying) n++;
                return n;
            }
        }

        public bool IsSounding(int note)
        {
            if (_voices == null) return false;
            foreach (var v in _voices) if (v.note == note && v.src.isPlaying && v.fadeTime <= 0f) return true;
            return false;
        }

        void Awake()
        {
            EnsureVoices();
            foreach (var s in sfx) if (s.clip != null) _sfx[s.name] = s.clip;
        }

        void EnsureVoices()
        {
            if (_voices != null) return;
            _voices = new Voice[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.dopplerLevel = 0f;
                _voices[i] = new Voice { src = src };
            }
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.spatialBlend = 0f;
            _info = InstrumentCatalog.All[0];
        }

        /// <summary>楽器を かえる。前の楽器の音のデータは、メモリからおろす。</summary>
        public void SetInstrument(string id)
        {
            EnsureVoices();
            if (bank == null) return;
            var next = bank.Find(id);
            if (next == null) return;
            if (_current == next) return;
            AllOff(true);
            if (_current != null)
            {
                foreach (var c in _current.clips) if (c != null && c.loadState == AudioDataLoadState.Loaded) c.UnloadAudioData();
            }
            _current = next;
            _info = InstrumentCatalog.Get(next.id);
            foreach (var c in _current.clips) if (c != null && c.loadState == AudioDataLoadState.Unloaded) c.LoadAudioData();
        }

        public void NoteOn(int note, float velocity = 1f)
        {
            EnsureVoices();
            LastNote = note;
            NotesPlayed++;
            if (_current == null || _current.clips.Length == 0) return;
            // 同じ鍵盤の、まだ鳴っている音は すぐに消す（重なって大きくならないように）
            foreach (var v in _voices)
            {
                if (v.note == note && v.src.isPlaying) StartFade(v, 0.05f);
            }
            int idx = InstrumentBank.Nearest(_current.roots, note);
            var clip = _current.clips[idx];
            if (clip == null) return;
            var voice = PickVoice();
            voice.note = note;
            voice.held = true;
            voice.serial = ++_serial;
            voice.startTime = Time.unscaledTime;
            voice.gain = Mathf.Clamp01(velocity) * _info.volume;
            voice.attack = _info.attack;
            voice.level = _info.attack > 0f ? 0f : 1f;
            voice.fadeTime = 0f;
            voice.fadeLeft = 0f;
            var src = voice.src;
            src.Stop();
            src.clip = clip;
            src.loop = _info.loop;
            src.pitch = InstrumentBank.PitchFor(_current.roots[idx], note);
            src.panStereo = Mathf.Clamp((note - 66) / 40f, -0.5f, 0.5f);
            src.volume = voice.level * voice.gain * Volume;
            src.Play();
        }

        public void NoteOff(int note)
        {
            if (_voices == null) return;
            foreach (var v in _voices)
            {
                if (v.note != note || !v.held) continue;
                v.held = false;
                if (_info.loop) StartFade(v, Sustain ? _info.sustainRelease : _info.release);
                else if (!Sustain) StartFade(v, _info.release);
            }
        }

        /// <summary>すべての音を止める（immediate なら すぐ、そうでなければ みじかく消す）。</summary>
        public void AllOff(bool immediate)
        {
            if (_voices == null) return;
            foreach (var v in _voices)
            {
                v.held = false;
                if (immediate)
                {
                    v.src.Stop();
                    v.note = -1;
                    v.fadeTime = 0f;
                }
                else if (v.src.isPlaying) StartFade(v, 0.12f);
            }
        }

        static void StartFade(Voice v, float time)
        {
            if (v.fadeTime > 0f && v.fadeLeft <= time) return;
            v.fadeTime = Mathf.Max(0.01f, time);
            v.fadeLeft = v.fadeTime;
        }

        Voice PickVoice()
        {
            // あいている → 消えかけ → いちばん古い
            foreach (var v in _voices) if (!v.src.isPlaying) return v;
            Voice best = null;
            foreach (var v in _voices)
            {
                if (v.fadeTime > 0f && (best == null || v.serial < best.serial)) best = v;
            }
            if (best != null) return best;
            foreach (var v in _voices)
            {
                if (!v.held && (best == null || v.serial < best.serial)) best = v;
            }
            if (best != null) return best;
            foreach (var v in _voices)
            {
                if (best == null || v.serial < best.serial) best = v;
            }
            return best;
        }

        void Update()
        {
            if (_voices == null) return;
            float dt = Time.unscaledDeltaTime;
            foreach (var v in _voices)
            {
                if (v.note < 0) continue;
                if (!v.src.isPlaying)
                {
                    v.note = -1;
                    continue;
                }
                if (v.attack > 0f && v.level < 1f && v.fadeTime <= 0f)
                {
                    v.level = Mathf.Min(1f, v.level + dt / v.attack);
                }
                if (v.fadeTime > 0f)
                {
                    v.fadeLeft -= dt;
                    float k = Mathf.Clamp01(v.fadeLeft / v.fadeTime);
                    v.level = Mathf.Min(v.level, k * k);
                    if (v.fadeLeft <= 0f)
                    {
                        v.src.Stop();
                        v.note = -1;
                        v.fadeTime = 0f;
                        continue;
                    }
                }
                v.src.volume = v.level * v.gain * Volume;
            }
        }

        public void PlaySfx(string name, float volume = 1f, bool isUi = false)
        {
            EnsureVoices();
            if (isUi && !UiSounds) return;
            LastSfx = name;
            if (_sfx.Count == 0) foreach (var s in sfx) if (s.clip != null) _sfx[s.name] = s.clip;
            if (!_sfx.TryGetValue(name, out var clip) || clip == null) return;
            _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume) * Mathf.Max(0.25f, Volume));
        }

        public void ResetCounters()
        {
            NotesPlayed = 0;
            LastNote = -1;
            LastSfx = null;
        }
    }
}
