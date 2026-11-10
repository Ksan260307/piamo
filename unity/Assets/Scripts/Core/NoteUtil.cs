using UnityEngine;

namespace Piamo
{
    /// <summary>けんばんに書く音の名前（せっていの「おんめい」）。</summary>
    public enum LabelMode
    {
        None = 0,
        Katakana = 1,   // ドレミ
        Hiragana = 2,   // どれみ
        Letters = 3,    // CDE
        ColorOnly = 4,  // いろの まるだけ
    }

    /// <summary>音の高さ（MIDI 番号。C4 = 60 が まんなかのド）にかかわる計算。</summary>
    public static class NoteUtil
    {
        public const int MiddleC = 60;

        static readonly bool[] BlackPattern = { false, true, false, true, false, false, true, false, true, false, true, false };
        static readonly string[] Katakana = { "ド", "ド♯", "レ", "レ♯", "ミ", "ファ", "ファ♯", "ソ", "ソ♯", "ラ", "ラ♯", "シ" };
        static readonly string[] Hiragana = { "ど", "ど♯", "れ", "れ♯", "み", "ふぁ", "ふぁ♯", "そ", "そ♯", "ら", "ら♯", "し" };
        static readonly string[] Letters = { "C", "C♯", "D", "D♯", "E", "F", "F♯", "G", "G♯", "A", "A♯", "B" };
        // ♭の 調（ヘ長調 など）で つかう 黒鍵の 名前
        static readonly string[] KatakanaFlat = { "ド", "レ♭", "レ", "ミ♭", "ミ", "ファ", "ソ♭", "ソ", "ラ♭", "ラ", "シ♭", "シ" };
        static readonly string[] HiraganaFlat = { "ど", "れ♭", "れ", "み♭", "み", "ふぁ", "そ♭", "そ", "ら♭", "ら", "し♭", "し" };
        static readonly string[] LettersFlat = { "C", "D♭", "D", "E♭", "E", "F", "G♭", "G", "A♭", "A", "B♭", "B" };

        // ドレミの いろ（ド あか・レ だいだい・ミ きいろ・ファ みどり・ソ みずいろ・ラ あお・シ むらさき）
        static readonly Color[] WhiteColors =
        {
            new Color(0.96f, 0.36f, 0.40f),
            new Color(0.98f, 0.62f, 0.27f),
            new Color(0.97f, 0.84f, 0.25f),
            new Color(0.45f, 0.80f, 0.40f),
            new Color(0.33f, 0.76f, 0.92f),
            new Color(0.38f, 0.52f, 0.92f),
            new Color(0.70f, 0.48f, 0.90f),
        };

        static readonly int[] WhiteDegree = { 0, -1, 1, -1, 2, 3, -1, 4, -1, 5, -1, 6 };

        public static int Mod(int a, int m) => ((a % m) + m) % m;
        public static int PitchClass(int midi) => Mod(midi, 12);
        public static bool IsBlack(int midi) => BlackPattern[PitchClass(midi)];
        /// <summary>オクターブの番号（C4 = 60 は 4）。</summary>
        public static int Octave(int midi) => Mathf.FloorToInt(midi / 12f) - 1;

        /// <summary>白鍵なら ド=0 … シ=6、黒鍵なら -1。</summary>
        public static int Degree(int midi) => WhiteDegree[PitchClass(midi)];

        public static float Frequency(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        public static string Name(int midi, LabelMode mode) => Name(midi, mode, false);

        /// <summary>音の名前。flats なら 黒鍵を ♭ で（シ♭ など）。</summary>
        public static string Name(int midi, LabelMode mode, bool flats)
        {
            int pc = PitchClass(midi);
            switch (mode)
            {
                case LabelMode.Katakana: return flats ? KatakanaFlat[pc] : Katakana[pc];
                case LabelMode.Hiragana: return flats ? HiraganaFlat[pc] : Hiragana[pc];
                case LabelMode.Letters: return flats ? LettersFlat[pc] : Letters[pc];
                default: return "";
            }
        }

        /// <summary>音の いろ。黒鍵は、となりの白鍵 2 つのあいだの いろ。</summary>
        public static Color NoteColor(int midi)
        {
            int pc = PitchClass(midi);
            int d = WhiteDegree[pc];
            if (d >= 0) return WhiteColors[d];
            int lo = WhiteDegree[pc - 1];
            int hi = WhiteDegree[(pc + 1) % 12];
            return Color.Lerp(WhiteColors[lo], WhiteColors[hi], 0.5f);
        }

        /// <summary>MIDI 番号の白鍵の通し番号（C-1 の ド を 0 とする）。黒鍵は、すぐ下の白鍵の番号。</summary>
        public static int WhiteNumber(int midi)
        {
            int oct = Mathf.FloorToInt(midi / 12f);
            int pc = PitchClass(midi);
            int d = WhiteDegree[pc];
            if (d < 0) d = WhiteDegree[pc - 1];
            return oct * 7 + d;
        }

        /// <summary>白鍵の通し番号から MIDI 番号へ。</summary>
        public static int FromWhiteNumber(int white)
        {
            int oct = Mathf.FloorToInt(white / 7f);
            int d = Mod(white, 7);
            int[] pcs = { 0, 2, 4, 5, 7, 9, 11 };
            return oct * 12 + pcs[d];
        }
    }
}
