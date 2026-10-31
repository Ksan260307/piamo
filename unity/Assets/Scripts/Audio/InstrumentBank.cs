using System;
using System.Collections.Generic;
using UnityEngine;

namespace Piamo
{
    /// <summary>
    /// 楽器（おと）の いちらん。tools/make_audio.py の INSTRUMENTS と同じ id。
    /// </summary>
    public static class InstrumentCatalog
    {
        public struct Info
        {
            public string id;
            public string label;
            /// <summary>おしている間なりつづける（ループ）音か。</summary>
            public bool loop;
            /// <summary>鳴りはじめの ふえる時間（秒）。</summary>
            public float attack;
            /// <summary>はなしたあと消える時間（秒）。</summary>
            public float release;
            /// <summary>おとを のばす（ペダル）ときの、はなしたあと消える時間（ループの音だけ）。</summary>
            public float sustainRelease;
            public float volume;
        }

        public static readonly Info[] All =
        {
            new Info { id = "piano", label = "ピアノ", attack = 0f, release = 0.22f, volume = 1f },
            new Info { id = "toy", label = "トイピアノ", attack = 0f, release = 0.18f, volume = 0.9f },
            new Info { id = "musicbox", label = "オルゴール", attack = 0f, release = 0.5f, volume = 0.9f },
            new Info { id = "glock", label = "てっきん", attack = 0f, release = 0.35f, volume = 0.85f },
            new Info { id = "marimba", label = "もっきん", attack = 0f, release = 0.2f, volume = 1f },
            new Info { id = "organ", label = "オルガン", loop = true, attack = 0.03f, release = 0.12f, sustainRelease = 1.4f, volume = 0.6f },
            new Info { id = "flute", label = "ふえ", loop = true, attack = 0.07f, release = 0.15f, sustainRelease = 1.2f, volume = 0.75f },
            new Info { id = "harp", label = "ハープ", attack = 0f, release = 0.4f, volume = 1f },
            new Info { id = "cat", label = "ねこ", attack = 0f, release = 0.12f, volume = 0.9f },
            new Info { id = "dog", label = "いぬ", attack = 0f, release = 0.1f, volume = 0.9f },
        };

        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].id == id) return i;
            return 0;
        }

        public static Info Get(string id) => All[IndexOf(id)];
    }

    /// <summary>
    /// 楽器の音（AudioClip）のたば。ProjectSetup が Assets/Audio/Instruments から作る。
    /// 楽器ごとに、鳴らせる はんい（C2〜C7）の すべての半音の音がある。
    /// AudioSource.pitch で音の高さを変えると、WebGL では Unity が 48000/44100 倍などに ずらしてしまうので、
    /// どの音も その高さの音を pitch = 1 のまま鳴らす。
    /// </summary>
    [CreateAssetMenu(menuName = "Piamo/Instrument Bank")]
    public class InstrumentBank : ScriptableObject
    {
        [Serializable]
        public class Instrument
        {
            public string id;
            /// <summary>clips[i] の音の高さ（MIDI 番号）。小さい順。</summary>
            public int[] roots = Array.Empty<int>();
            public AudioClip[] clips = Array.Empty<AudioClip>();
        }

        public List<Instrument> instruments = new List<Instrument>();

        public Instrument Find(string id)
        {
            foreach (var i in instruments) if (i.id == id) return i;
            return instruments.Count > 0 ? instruments[0] : null;
        }

        /// <summary>note の音の番号（なければ -1）。</summary>
        public static int IndexOf(int[] roots, int note) => Array.IndexOf(roots, note);
    }
}
