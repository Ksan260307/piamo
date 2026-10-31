using System;
using UnityEngine;

namespace Piamo
{
    /// <summary>せってい。かえるとすぐに反映して、保存する（SaveSystem）。</summary>
    [Serializable]
    public class PiamoSettings
    {
        // おと
        public string instrument = "piano";
        public float volume = 0.8f;
        /// <summary>おとを のばす（ペダル）。はなしても すぐに止めない。</summary>
        public bool sustain = false;

        // けんばん
        /// <summary>けんばんの かず（オクターブの数 1〜5）。はじめは いちばん少ない 1 オクターブ（ド〜ド）。</summary>
        public int octaves = 1;
        /// <summary>オクターブ（音の高さを 12 半音ずつ ずらす）。</summary>
        public int octaveShift = 0;
        /// <summary>けんばんの おおきさ（0 ちいさい・1 ふつう・2 おおきい）。細くなりすぎるとスクロールになる。</summary>
        public int keySize = 1;
        public bool blackKeys = true;
        public LabelMode labels = LabelMode.Katakana;
        /// <summary>白鍵を、ドレミの いろ に ぬる。</summary>
        public bool rainbowKeys = false;
        /// <summary>ピアノの いろ（PianoColors の番号）。</summary>
        public int pianoColor = 0;

        // まねっこ
        /// <summary>おてほんの はやさ（0 ゆっくり・1 ふつう・2 はやい）。</summary>
        public int mimicSpeed = 1;
        /// <summary>1 回で まねする ながさ（0 みじかい・1 ふつう・2 ながい）。</summary>
        public int mimicLength = 0;
        /// <summary>まよったときに、正解の けんばんが ひかるまでの秒数（0 = ひからない）。</summary>
        public int hintSeconds = 10;
        /// <summary>おてほんを ひくとき、けんばんも ひからせる。</summary>
        public bool demoLights = true;

        // がめん
        public bool noteEffects = true;
        public bool uiSounds = true;
        /// <summary>はいけい（Themes の番号）。</summary>
        public int theme = 0;

        public static readonly int[] HintChoices = { 0, 5, 10, 15, 20 };

        public void Clamp()
        {
            if (string.IsNullOrEmpty(instrument)) instrument = "piano";
            volume = Mathf.Clamp01(volume);
            octaves = Mathf.Clamp(octaves, KeyboardLayout.MinOctaves, KeyboardLayout.MaxOctaves);
            octaveShift = KeyboardLayout.ClampShift(octaves, octaveShift);
            keySize = Mathf.Clamp(keySize, 0, 2);
            if (!Enum.IsDefined(typeof(LabelMode), labels)) labels = LabelMode.Katakana;
            pianoColor = Mathf.Clamp(pianoColor, 0, PianoColors.Count - 1);
            mimicSpeed = Mathf.Clamp(mimicSpeed, 0, 2);
            mimicLength = Mathf.Clamp(mimicLength, 0, 2);
            hintSeconds = Mathf.Clamp(hintSeconds, 0, 60);
            theme = Mathf.Clamp(theme, 0, Themes.Count - 1);
        }

        public PiamoSettings Clone() => (PiamoSettings)MemberwiseClone();

        /// <summary>
        /// 白鍵 1 本の、いちばん細いはば（UI の単位）。「ふつう」は、たて長のスマホ（375pt）でも
        /// はじめの 8 けんが ぜんぶ見える はば。
        /// </summary>
        public float MinKeyWidthUI => keySize == 0 ? 72f : keySize == 2 ? 140f : 96f;
    }

    /// <summary>ピアノのケースの いろ。</summary>
    public static class PianoColors
    {
        public static readonly string[] Names = { "ピンク", "みずいろ", "きいろ", "みどり", "むらさき", "あか", "くろ", "しろ" };
        public static readonly Color[] Colors =
        {
            new Color(0.98f, 0.62f, 0.72f),
            new Color(0.48f, 0.78f, 0.96f),
            new Color(0.99f, 0.84f, 0.38f),
            new Color(0.52f, 0.84f, 0.56f),
            new Color(0.72f, 0.6f, 0.92f),
            new Color(0.93f, 0.3f, 0.32f),
            new Color(0.17f, 0.16f, 0.22f),
            new Color(0.97f, 0.96f, 0.94f),
        };
        public static int Count => Colors.Length;
    }

    /// <summary>はいけいの いろ（上と下のグラデーション）。</summary>
    public static class Themes
    {
        public static readonly string[] Names = { "そら", "ゆうやけ", "よる", "もり" };
        public static readonly Color[] Top =
        {
            new Color(0.62f, 0.85f, 0.98f),
            new Color(1.0f, 0.72f, 0.55f),
            new Color(0.16f, 0.18f, 0.42f),
            new Color(0.62f, 0.86f, 0.62f),
        };
        public static readonly Color[] Bottom =
        {
            new Color(1.0f, 0.97f, 0.88f),
            new Color(1.0f, 0.9f, 0.78f),
            new Color(0.36f, 0.3f, 0.58f),
            new Color(0.95f, 0.97f, 0.82f),
        };
        public static int Count => Names.Length;
    }
}
