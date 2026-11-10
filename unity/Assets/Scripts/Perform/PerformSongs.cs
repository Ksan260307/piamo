using System;
using System.Collections.Generic;
using System.Globalization;

namespace Piamo
{
    /// <summary>えんそうの曲の なかま（曲えらびの見出し）。</summary>
    public enum PerformCategory { Japan, World, Event, Classic }

    /// <summary>ばんそう（ドミソの おと）の ひきかた。</summary>
    public enum PerformStyle
    {
        Pop,    // 2・4 拍子：ベース → 和音 →（4 拍子は 5 どの ベース → 和音）
        Waltz,  // 3 拍子：ベース → 和音 → 和音
        Arp,    // こもりうた：8 分音符で 下から上へ、上から下へ
        Elise,  // 16 分音符で ベース・和音（エリーゼのために の 左手）
        Drone,  // 根音と 5 どを のばすだけ（わらべうた）
        None,   // ばんそう なし
    }

    public struct PerformNote
    {
        /// <summary>MIDI 番号（まんなかのド C4 = 60）。</summary>
        public int midi;
        /// <summary>はじまる 拍（4 分音符 = 1。曲のはじめの音が 0）。</summary>
        public float on;
        /// <summary>長さ（拍）。</summary>
        public float beats;
        /// <summary>何小節目か（0 から。弱起は 0 小節目）。</summary>
        public int bar;
        public float Off => on + beats;
    }

    public struct ChordSpan
    {
        public string name;
        /// <summary>根音（0 = ド … 11 = シ）。</summary>
        public int root;
        /// <summary>和音の音（0 = ド … 11 = シ。根音から）。</summary>
        public int[] tones;
        public float on;
        public float beats;
        public float Off => on + beats;
    }

    public class PerformSong
    {
        public string id, title, animal, source;
        public PerformCategory category;
        /// <summary>むずかしさ（1〜3）。</summary>
        public int stars;
        /// <summary>はやさ（1 分間の 4 分音符の数）。</summary>
        public float bpm;
        public int meterTop, meterBottom;
        public PerformStyle style;
        /// <summary>♭の 調（くろい けんばんを ♭ で よぶ。ヘ長調の ハッピーバースデー など）。</summary>
        public bool flats;
        public readonly List<PerformNote> notes = new List<PerformNote>();
        public readonly List<ChordSpan> chords = new List<ChordSpan>();
        /// <summary>小節の はじまりの拍（弱起なら 0 小節目は みじかい）。</summary>
        public readonly List<float> barStarts = new List<float>();
        public float Length { get; internal set; }
        public int Low { get; internal set; }
        public int High { get; internal set; }
        public bool UsesBlackKeys { get; internal set; }

        /// <summary>1 小節の長さ（拍）。8 分の 6 なら 3。</summary>
        public float BarBeats => meterTop * 4f / meterBottom;
        /// <summary>カウントの 1 つぶんの長さ（拍）。8 分の 6 は 付点 4 分、8 分の 3 は 8 分。</summary>
        public float Pulse => meterBottom == 8 ? (meterTop % 3 == 0 && meterTop > 3 ? 1.5f : 0.5f) : 4f / meterBottom;
        public string Meter => $"{meterTop}/{meterBottom}";
        public float BarLength(int bar) => (bar + 1 < barStarts.Count ? barStarts[bar + 1] : Length) - barStarts[bar];
    }

    /// <summary>
    /// えんそうモードの曲（30 曲）。どれも 作った人が なくなって 長い年月が たった、著作権の切れた メロディ
    /// （日本の わらべうた・童謡・唱歌、外国の民謡、クラシック）。日本の歌の多くは Wikipedia の楽譜と くらべてある。
    /// アニメの歌や いまの はやりの歌は 著作権があるので、入れていない。
    /// 書き方：小節を「|」で区切る。音は「C4」「F#4:0.5」（: のあとは長さ。4 分音符 = 1）、「R」は休み、「~」で つぎの同じ音に つなぐ。
    /// 和音も 小節ごとに「C」「G7 C」「F:2 G:1」（長さを書かないものは のこりを等分）、「R」は ばんそう なし。
    /// </summary>
    public static class PerformSongs
    {
        public static readonly string[] CategoryLabels = { "にほんの うた", "せかいの うた", "おいわい・きせつ", "クラシック" };

        public static readonly List<PerformSong> All = new List<PerformSong>
        {
            // ---- にほんの うた ----
            Make("katatsumuri", "かたつむり", PerformCategory.Japan, "frog", "文部省唱歌（1911）", 1, 104, "4/4", PerformStyle.Pop,
                "G4:0.75 G4:0.25 G4:0.5 E4:0.5 C4:0.75 C4:0.25 C4:0.5 D4:0.5 | E4:0.75 E4:0.25 D4:0.5 C4:0.5 D4 R | E4:0.75 F4:0.25 G4:0.5 A4:0.5 G4:0.75 G4:0.25 G4:0.5 E4:0.5 | " +
                "D4:0.75 D4:0.25 C4:0.5 D4:0.5 E4 R | G4:0.5 C5:0.5 C5:0.5 G4:0.5 E4:0.5 G4:0.5 G4:0.5 E4:0.5 | C4:0.5 E4:0.5 E4:0.75 D4:0.25 C4 R",
                "C | C G | C | G C | C | C"),
            Make("momotaro", "ももたろう", PerformCategory.Japan, "monkey", "文部省唱歌（1911）", 1, 100, "2/4", PerformStyle.Pop,
                "G4:1.5 A4:0.5 | G4:0.5 G4:0.5 E4 | G4:0.5 G4:0.5 E4:0.5 C4:0.5 | D4:1.5 R:0.5 | C4:0.5 C4:0.5 D4:0.5 D4:0.5 | " +
                "E4:0.5 E4:0.5 D4 | E4:0.5 E4:0.5 A4:0.5 A4:0.5 | G4:1.5 R:0.5 | C5:0.5 C5:0.5 G4 | E4:0.5 E4:0.5 A4:0.5 A4:0.5 | " +
                "G4:0.5 G4:0.5 E4:0.5 D4:0.5 | C4:1.5 R:0.5",
                "C | C | C | G | C G | C G | C F | C | C | C F | C:1.5 G:0.5 | C"),
            Make("yuki", "ゆき", PerformCategory.Japan, "penguin", "文部省唱歌（1911）", 1, 96, "2/4", PerformStyle.Pop,
                "G4:0.75 A4:0.25 G4:0.75 A4:0.25 | G4 E4:0.5 R:0.5 | E4:0.75 F4:0.25 E4:0.75 F4:0.25 | E4 C4:0.5 R:0.5 | " +
                "E4 C4:0.5 D4:0.5 | E4 C4:0.5 D4:0.5 | E4:0.75 F4:0.25 G4:0.5 G4:0.5 | D4:0.75 E4:0.25 D4:0.5 R:0.5 | " +
                "G4:0.75 A4:0.25 G4:0.75 A4:0.25 | G4:0.5 G4:0.5 E4:0.5 E4:0.5 | E4:0.75 F4:0.25 E4:0.75 F4:0.25 | E4:0.5 E4:0.5 C4:0.5 R:0.5 | " +
                "A4 G4:0.5 E4:0.5 | G4:0.5 G4:0.5 E4:0.5 C4:0.5 | E4:0.5 E4:0.5 D4:0.75 D4:0.25 | C4 R",
                "C | C | C | C | C | C | C | G | C | C | C | C | F C | C | C G | C"),
            Make("shabondama", "シャボンだま", PerformCategory.Japan, "pig", "中山晋平（1922）", 2, 88, "2/4", PerformStyle.Pop,
                "D4:0.5 G4:0.25 G4:0.25 G4:0.5 A4:0.5 | B4:0.5 D5:0.5 D5:0.5 R:0.5 | E5:0.5 C5:0.5 G5:0.5 E5:0.5 | D5:0.5 E5:0.5 D5:0.5 R:0.5 | " +
                "B4:0.5 B4:0.5 A4:0.5 G4:0.5 | A4:0.5 D5:0.5 D5:0.5 R:0.5 | E5:0.5 E5:0.5 D5:0.5 G4:0.5 | B4:0.5 A4:0.5 G4:0.5 R:0.5 | " +
                "G5:0.75 G5:0.25 G5:0.5 G5:0.5 | G5:0.5 E5:0.5 D5:0.5 R:0.5 | G4:0.5 G4:0.25 A4:0.25 B4:0.5 D5:0.5 | " +
                "B4:0.5 A4:0.5 G4:0.5 R:0.5",
                "G | G | C | G | G | D7 | C D7 | G | C | C G | G | G"),
            Make("furusato", "ふるさと", PerformCategory.Japan, "rabbit", "岡野貞一（1914）", 2, 84, "3/4", PerformStyle.Waltz,
                "C5 C5 C5 | D5:1.5 E5:0.5 D5 | E5 E5 F5 | G5:2 R | F5 G5 A5 | E5:1.5 F5:0.5 E5 | D5 D5 B4 | C5:2 R | " +
                "D5:0.5 C5:0.5 D5 G4 | C5:0.5 D5:0.5 E5 E5 | F5:0.5 E5:0.5 F5:1.5 A5:0.5 | G5:0.5 F5:0.5 E5 R | G5 G5 G5 | " +
                "C5:1.5 D5:0.5 E5 | F5 F5 D5 | C5:2 R",
                "C | G | C | C | F | C | G | C | G | C | F | C | C | C | F:2 G:1 | C"),
            Make("kagome", "かごめかごめ", PerformCategory.Japan, "duck", "わらべうた", 2, 76, "2/4", PerformStyle.Drone,
                "A4 A4:0.5 B4:0.5 | A4:0.5 A4:0.5 A4:0.5 R:0.5 | A4:0.5 A4:0.25 A4:0.25 A4:0.5 G4:0.25 G4:0.25 | A4:0.5 A4:0.25 G4:0.25 E4:0.5 R:0.5 | " +
                "A4:0.5 G4:0.5 A4:0.5 G4:0.5 | A4:0.5 A4:0.25 G4:0.25 E4:0.5 R:0.5 | A4:0.5 A4:0.5 A4:0.5 B4:0.5 | A4:0.5 A4:0.5 A4:0.5 R:0.5 | " +
                "A4:0.5 G4:0.25 G4:0.25 A4:0.5 G4:0.25 G4:0.25 | A4:0.5 A4:0.25 G4:0.25 E4:0.5 R:0.5 | A4:0.25 A4:0.25 A4:0.25 A4:0.25 A4:0.5 B4:0.5 | " +
                "A4:0.75 G4:0.25 A4:0.5 R:0.5",
                "A5 | A5 | A5 | A5 | A5 | A5 | A5 | A5 | A5 | A5 | A5 | A5"),
            Make("kutsuganaru", "くつがなる", PerformCategory.Japan, "dog", "弘田龍太郎（1919）", 2, 116, "4/4", PerformStyle.Pop,
                "C4 C4:0.5 D4:0.5 E4 R | G4 G4:0.5 A4:0.5 G4 R | E4 E4:0.5 D4:0.5 C4 C4:0.5 D4:0.5 | E4 D4:0.5 C4:0.5 D4 R | " +
                "E4 E4:0.5 G4:0.5 A4 R | C5 A4:0.5 C5:0.5 G4 R | A4:1.5 A4:0.5 G4 E4:0.5 G4:0.5 | A4:2 A4 R | C5:2 A4 C5 | " +
                "G4 A4:0.5 G4:0.5 E4 C4 | E4 D4:0.5 C4:0.5 D4:1.5 G4:0.5 | G4:3 R | C5:2 A4 C5 | G4 A4:0.5 G4:0.5 E4 C4 | " +
                "D4 C4:0.5 D4:0.5 E4:1.5 D4:0.5 | C4:3 R",
                "C | C | C | C G | C F | F C | F C | F | F | C | C G | G | F | C | G C | C"),
            Make("harugakita", "はるがきた", PerformCategory.Japan, "chick", "岡野貞一（1910）", 2, 112, "4/4", PerformStyle.Pop,
                "G4 E4:0.5 F4:0.5 G4 A4 | G4 E4:0.5 F4:0.5 G4 C5 | A4 G4 E4:1.5 C4:0.5 | D4:3 R | G4 A4:0.5 G4:0.5 E4 G4 | " +
                "C5 D5:0.5 C5:0.5 A4 C5 | G4 E5 D5:1.5 G4:0.5 | C5:3 R",
                "C | C | F C | G | C | F | C G | C"),
            Make("yuyake", "ゆうやけこやけ", PerformCategory.Japan, "owl", "草川信（1923）", 2, 108, "4/4", PerformStyle.Pop,
                "G4:2 G4 A4 | G4 G4 G4 E4 | C4 C4 D4 E4 | D4:3 R | E4:2 E4 G4 | A4 C5 C5 A4 | G4 G4 A4 G4 | C5:3 R | " +
                "C5:1.5 D5:0.5 C5 A4 | C5 C5 G4 G4 | A4 G4 A4 G4 | E4:3 R | G4 E4 D4 C4 | D4 D4 C4 D4 | E4 G4 A4 G4 | " +
                "C5:4",
                "C | C | C | G | C | F | C | C | F | C | F | C | C G | G | C F | C"),
            Make("usagi", "うさぎ", PerformCategory.Japan, "rabbit", "わらべうた", 2, 76, "2/4", PerformStyle.None,
                "F4 F4:0.5 A4:0.5 | B4:0.5 A4:0.5 B4:0.5 R:0.5 | F4:0.5 F4:0.5 F4:0.5 A4:0.5 | B4:0.5 A4:0.5 B4:0.5 R:0.5 | " +
                "A4:0.5 B4:0.5 C5:0.5 C5:0.5 | B4:0.5 A4:0.25 A4:0.25 F4:0.5 E4:0.5 | A4:0.5 F4:0.5 E4 | F4:0.5 E4:0.5 D4 | " +
                "E4:2",
                "R | R | R | R | R | R | R | R | R"),
            Make("nanatsunoko", "ななつのこ", PerformCategory.Japan, "bird", "本居長世（1921）", 3, 84, "4/4", PerformStyle.Pop,
                "E5 D5:0.5 C5:0.5 D5 R | E5:0.5 C5:0.5 A4:0.5 C5:0.5 G4 R | A4:0.5 G4:0.5 E4:0.5 G4:0.5 A4 C5 | D5:3 R | " +
                "E5:1.5 F5:0.5 G5 E5 | G5 A5:0.5 G5:0.5 E5 C5 | D5:0.5 D5:0.5 E5:0.5 C5:0.5 A4 G4 | C5:3 R | D5 D5 D5 R | " +
                "D5:1.5 D5:0.5 D5 E5 | F5 E5 D5 D5 | D5 E5 A4 R | G4 G4 G4 R | C5:1.5 C5:0.5 C5 D5 | E5 F5 A4 C5 | D5:3 R | " +
                "E5 D5:0.5 C5:0.5 D5 R | E5:0.5 C5:0.5 A4:0.5 C5:0.5 G4 R | A4:0.5 G4:0.5 E4:0.5 G4:0.5 A4 C5 | D5:3 R | " +
                "E5:1.5 F5:0.5 G5 E5 | G5 A5:0.5 G5:0.5 E5 C5 | D5 E5:0.5 C5:0.5 A4 G4 | C5:3 R",
                "C G | Am C | Am F | G | C | C | G C F C | C | G | G | Dm G | G Am | C | C | C F | G | C G | Am C | Am F | " +
                "G | C | C | G C F C | C"),
            Make("akatombo", "あかとんぼ", PerformCategory.Japan, "fox", "やまだ こうさく（1927）", 3, 66, "3/4", PerformStyle.Arp,
                "C4:0.5 F4:0.5 F4:1.5 G4:0.5 | A4:0.5 C5:0.5 F5:0.5 D5:0.5 C5 | D5:0.5 F4:0.5 F4 G4 | A4:2 R | A4:0.5 D5:0.5 C5:1.5 D5:0.5 | " +
                "F5:0.5 D5:0.5 C5:0.5 D5:0.5 C5:0.5 A4:0.5 | C5:0.5 A4:0.5 F4:0.5 A4:0.5 G4:0.5 F4:0.5 | F4:2 R",
                "F | F | Bb F C | F | Dm:1 F:2 | Bb:1 F:2 | F:2 C:1 | F"),

            // ---- せかいの うた ----
            Make("twinkle", "きらきらぼし", PerformCategory.World, "owl", "フランス民謡", 1, 100, "4/4", PerformStyle.Pop,
                "C4 C4 G4 G4 | A4 A4 G4:2 | F4 F4 E4 E4 | D4 D4 C4:2 | G4 G4 F4 F4 | E4 E4 D4:2 | G4 G4 F4 F4 | E4 E4 D4:2 | " +
                "C4 C4 G4 G4 | A4 A4 G4:2 | F4 F4 E4 E4 | D4 D4 C4:2",
                "C | F C | F C | G C | C F | C G | C F | C G | C | F C | F C | G C"),
            Make("kaeru", "かえるのうた", PerformCategory.World, "frog", "ドイツ民謡", 1, 112, "4/4", PerformStyle.Pop,
                "C4 D4 E4 F4 | E4 D4 C4:2 | E4 F4 G4 A4 | G4 F4 E4:2 | C4 R C4 R | C4 R C4 R | C4:0.5 C4:0.5 D4:0.5 D4:0.5 E4:0.5 E4:0.5 F4:0.5 F4:0.5 | " +
                "E4 D4 C4:2",
                "C | C:1 G:1 C:2 | C | C:1 G7:1 C:2 | C | C | C:1 G:1 C:1 F:1 | C:1 G:1 C:2"),
            Make("mary", "メリーさんのひつじ", PerformCategory.World, "sheep", "アメリカ民謡", 1, 116, "4/4", PerformStyle.Pop,
                "E4:1.5 D4:0.5 C4 D4 | E4 E4 E4 R | D4 D4 D4 R | E4 G4 G4 R | E4:1.5 D4:0.5 C4 D4 | E4 E4 E4 R | D4 D4 E4 D4 | " +
                "C4:2 R:2",
                "C | C | G | C | C | C | G | C"),
            Make("london", "ロンドンばし", PerformCategory.World, "lion", "イギリス民謡", 1, 96, "2/4", PerformStyle.Pop,
                "G4:0.75 A4:0.25 G4:0.5 F4:0.5 | E4:0.5 F4:0.5 G4 | D4:0.5 E4:0.5 F4 | E4:0.5 F4:0.5 G4 | G4:0.75 A4:0.25 G4:0.5 F4:0.5 | " +
                "E4:0.5 F4:0.5 G4 | D4 G4 | E4:0.5 C4:1.5",
                "C | C | G7 | C | C | C | G7 | C"),
            Make("chocho", "ちょうちょう", PerformCategory.World, "bee", "ドイツ民謡", 1, 100, "2/4", PerformStyle.Pop,
                "G4:0.5 E4:0.5 E4 | F4:0.5 D4:0.5 D4 | C4:0.5 D4:0.5 E4:0.5 F4:0.5 | G4:0.5 G4:0.5 G4 | G4:0.5 E4:0.5 E4 | " +
                "F4:0.5 D4:0.5 D4 | C4:0.5 E4:0.5 G4:0.5 G4:0.5 | C4 R | D4:0.5 D4:0.5 D4:0.5 D4:0.5 | D4:0.5 E4:0.5 F4 | " +
                "E4:0.5 E4:0.5 E4:0.5 E4:0.5 | E4:0.5 F4:0.5 G4 | G4:0.5 E4:0.5 E4 | F4:0.5 D4:0.5 D4 | C4:0.5 E4:0.5 G4:0.5 G4:0.5 | " +
                "C4:2",
                "C | G7 | C | C | C | G7 | C G7 | C | G | G7 | C | C | C | G7 | C G7 | C"),
            Make("bunbun", "ぶんぶんぶん", PerformCategory.World, "bee", "ボヘミア民謡", 1, 100, "2/4", PerformStyle.Pop,
                "G4 F4 | E4 R | D4:0.5 E4:0.5 F4:0.5 D4:0.5 | C4 R | E4:0.5 F4:0.5 G4:0.5 E4:0.5 | D4:0.5 E4:0.5 F4:0.5 D4:0.5 | " +
                "E4:0.5 F4:0.5 G4:0.5 E4:0.5 | D4:0.5 E4:0.5 F4:0.5 D4:0.5 | G4 F4 | E4 R | D4:0.5 E4:0.5 F4:0.5 D4:0.5 | " +
                "C4 R",
                "C G7 | C | G7 | C | C | G7 | C | G7 | C G7 | C | G7 | C"),
            Make("kogitsune", "こぎつね", PerformCategory.World, "fox", "ドイツ民謡", 1, 116, "4/4", PerformStyle.Pop,
                "C4 D4 E4 F4 | G4 G4 G4 G4 | A4 F4 C5 A4 | G4:4 | A4 F4 C5 A4 | G4:4 | G4 F4 F4 F4 | F4 E4 E4 E4 | E4 D4 E4 D4 | " +
                "C4 E4 G4:2 | G4 F4 F4 F4 | F4 E4 E4 E4 | E4 D4 E4 D4 | C4:4",
                "C | C | F | C | F | C | G7 | G7:1 C:3 | C:1 G7:3 | C | G7 | G7:1 C:3 | C:1 G7:3 | C"),
            Make("musunde", "むすんでひらいて", PerformCategory.World, "bear", "ルソー（18せいき）", 1, 120, "4/4", PerformStyle.Pop,
                "E4 E4:0.5 D4:0.5 C4 C4 | D4 D4 E4:0.5 D4:0.5 C4 | G4 G4:0.5 F4:0.5 E4 E4 | D4:0.5 C4:0.5 D4:0.5 E4:0.5 C4 R | " +
                "E4 E4:0.5 F4:0.5 G4 G4 | A4 A4 G4:0.5 F4:0.5 E4 | E4 E4:0.5 F4:0.5 G4 G4 | A4 A4 G4 R | E4 E4:0.5 D4:0.5 C4 C4 | " +
                "D4 D4 E4:0.5 D4:0.5 C4 | G4 G4:0.5 F4:0.5 E4 E4 | D4:0.5 C4:0.5 D4:0.5 E4:0.5 C4 R",
                "C | G C | C | G C | C | F C | C | F C | C | G C | C | G C"),
            Make("kakko", "かっこう", PerformCategory.World, "bird", "ドイツ民謡", 1, 132, "3/4", PerformStyle.Waltz,
                "G4 E4 R | G4 E4 R | D4 C4 D4 | C4:3 | D4 D4 E4 | F4:2 D4 | E4 E4 F4 | G4:2 E4 | G4:2 E4 | G4:2 E4 | " +
                "F4 E4 D4 | C4:3",
                "C | C | G | C | G | G7 | C | C | C | C | G7 | C"),
            Make("gucho", "グーチョキパー", PerformCategory.World, "crab", "フランス民謡", 2, 100, "4/4", PerformStyle.Pop,
                "G4 A4 B4 G4 | G4 A4 B4 G4 | B4 C5 D5:2 | B4 C5 D5:2 | D5:0.5 E5:0.5 D5:0.5 C5:0.5 B4 G4 | D5:0.5 E5:0.5 D5:0.5 C5:0.5 B4 G4 | " +
                "G4 D4 G4:2 | G4 D4 G4:2",
                "G:1 D7:1 G:2 | G:1 D7:1 G:2 | G:1 D7:1 G:2 | G:1 D7:1 G:2 | G:1 D7:1 G:2 | G:1 D7:1 G:2 | G:1 D7:1 G:2 | " +
                "G:1 D7:1 G:2"),
            Make("alps", "アルプスいちまんじゃく", PerformCategory.World, "sheep", "アメリカ民謡", 2, 104, "2/4", PerformStyle.Pop,
                "C5:0.5 C5:0.5 D5:0.5 E5:0.5 | C5:0.5 E5:0.5 D5:0.5 G4:0.5 | C5:0.5 C5:0.5 D5:0.5 E5:0.5 | C5 B4 | C5:0.5 C5:0.5 D5:0.5 E5:0.5 | " +
                "F5:0.5 E5:0.5 D5:0.5 C5:0.5 | B4:0.5 G4:0.5 A4:0.5 B4:0.5 | C5 C5 | A4:0.5 B4:0.5 A4:0.5 G4:0.5 | A4:0.5 B4:0.5 C5 | " +
                "G4:0.5 A4:0.5 G4:0.5 F4:0.5 | E4 G4 | A4:0.5 B4:0.5 A4:0.5 G4:0.5 | A4:0.5 B4:0.5 C5:0.5 A4:0.5 | G4:0.5 C5:0.5 B4:0.5 D5:0.5 | " +
                "C5 C5",
                "C | C G | C | C G | C | F G | G | C | F | F C | C G7 | C | F | F | C G | C"),

            // ---- おいわい・きせつ ----
            Make("birthday", "ハッピーバースデー", PerformCategory.Event, "cat", "ヒル姉妹（1893）", 2, 96, "3/4", PerformStyle.Waltz,
                "C4:0.75 C4:0.25 | D4 C4 F4 | E4:2 C4:0.75 C4:0.25 | D4 C4 G4 | F4:2 C4:0.75 C4:0.25 | C5 A4 F4 | E4 D4 Bb4:0.75 Bb4:0.25 | " +
                "A4 F4 G4 | F4:2",
                "R | F6 | C | Gm:1 C7:2 | F | F | C7:1 Bb:2 | F:2 C:1 | F", flats: true),
            Make("jingle", "ジングルベル", PerformCategory.Event, "penguin", "ピアポント（1857）", 2, 120, "4/4", PerformStyle.Pop,
                "E4 E4 E4:2 | E4 E4 E4:2 | E4 G4 C4:1.5 D4:0.5 | E4:4 | F4 F4 F4:1.5 F4:0.5 | F4 E4 E4 E4:0.5 E4:0.5 | " +
                "E4 D4 D4 E4 | D4:2 G4:2 | E4 E4 E4:2 | E4 E4 E4:2 | E4 G4 C4:1.5 D4:0.5 | E4:4 | F4 F4 F4:1.5 F4:0.5 | " +
                "F4 E4 E4 E4:0.5 E4:0.5 | G4 G4 F4 D4 | C4:4",
                "C | C | C | C | F | F:1 C:3 | C:1 G:3 | G | C | C | C | C | F | F:1 C:3 | G7 | C"),
            Make("silentnight", "きよしこのよる", PerformCategory.Event, "elephant", "グルーバー（1818）", 3, 72, "3/4", PerformStyle.Arp,
                "G4:1.5 A4:0.5 G4 | E4:3 | G4:1.5 A4:0.5 G4 | E4:3 | D5:2 D5 | B4:3 | C5:2 C5 | G4:3 | A4:2 A4 | C5:1.5 B4:0.5 A4 | " +
                "G4:1.5 A4:0.5 G4 | E4:3 | A4:2 A4 | C5:1.5 B4:0.5 A4 | G4:1.5 A4:0.5 G4 | E4:3 | D5:2 D5 | F5:1.5 D5:0.5 B4 | " +
                "C5:3 | E5:3 | C5:1.5 G4:0.5 E4 | G4:1.5 F4:0.5 D4 | C4:3",
                "C | C | C | C | G | G | C | C | F | C | C | C | F | C | C | C | G | G7 | C | C | C | G7 | C"),

            // ---- クラシック ----
            Make("ode", "よろこびのうた", PerformCategory.Classic, "lion", "ベートーヴェン（1824）", 2, 108, "4/4", PerformStyle.Pop,
                "B4 B4 C5 D5 | D5 C5 B4 A4 | G4 G4 A4 B4 | B4:1.5 A4:0.5 A4:2 | B4 B4 C5 D5 | D5 C5 B4 A4 | G4 G4 A4 B4 | " +
                "A4:1.5 G4:0.5 G4:2 | A4 A4 B4 G4 | A4 B4:0.5 C5:0.5 B4 G4 | A4 B4:0.5 C5:0.5 B4 A4 | G4 A4 D4 B4~ | " +
                "B4 B4 C5 D5 | D5 C5 B4 A4 | G4 G4 A4 B4 | A4:1.5 G4:0.5 G4:2",
                "G | D G | G D | G D | G | D G | G D | D G | D G | D:1 G:3 | D:1 G:2 D:1 | G D | G | D G | G D | D G"),
            Make("brahms", "ブラームスのこもりうた", PerformCategory.Classic, "sheep", "ブラームス（1868）", 2, 76, "3/4", PerformStyle.Arp,
                "E4:0.5 E4:0.5 | G4:1.5 E4:0.5 E4 | G4 R E4:0.5 G4:0.5 | C5 B4:1.5 A4:0.5 | A4 G4 D4:0.5 E4:0.5 | F4 D4 D4:0.5 E4:0.5 | " +
                "F4 R D4:0.5 F4:0.5 | B4:0.5 A4:0.5 G4 B4 | C5 R C4:0.5 C4:0.5 | C5:2 A4:0.5 F4:0.5 | G4:2 E4:0.5 C4:0.5 | " +
                "F4 G4 A4 | G4:2 C4:0.5 C4:0.5 | C5:2 A4:0.5 F4:0.5 | G4:2 E4:0.5 C4:0.5 | F4 E4 D4 | C4:2",
                "R | C | C | C:1 G7:2 | Dm:1 G7:2 | G7 | G7 | G7 | C | F | C | F | C | F | C | G7 | C"),
            Make("elise", "エリーゼのために", PerformCategory.Classic, "koala", "ベートーヴェン（1810）", 3, 66, "3/8", PerformStyle.Elise,
                "E5:0.25 D#5:0.25 | E5:0.25 D#5:0.25 E5:0.25 B4:0.25 D5:0.25 C5:0.25 | A4:0.5 R:0.25 C4:0.25 E4:0.25 A4:0.25 | " +
                "B4:0.5 R:0.25 E4:0.25 G#4:0.25 B4:0.25 | C5:0.5 R:0.25 E4:0.25 E5:0.25 D#5:0.25 | E5:0.25 D#5:0.25 E5:0.25 B4:0.25 D5:0.25 C5:0.25 | " +
                "A4:0.5 R:0.25 C4:0.25 E4:0.25 A4:0.25 | B4:0.5 R:0.25 E4:0.25 C5:0.25 B4:0.25 | A4:1.5",
                "R | R | Am | E | Am | R | Am | E | Am"),
            Make("minuet", "メヌエット", PerformCategory.Classic, "cat", "ペツォールト（1725）", 3, 104, "3/4", PerformStyle.Waltz,
                "D5 G4:0.5 A4:0.5 B4:0.5 C5:0.5 | D5 G4 G4 | E5 C5:0.5 D5:0.5 E5:0.5 F#5:0.5 | G5 G4 G4 | C5 D5:0.5 C5:0.5 B4:0.5 A4:0.5 | " +
                "B4 C5:0.5 B4:0.5 A4:0.5 G4:0.5 | F#4 G4:0.5 A4:0.5 B4:0.5 G4:0.5 | A4:3 | D5 G4:0.5 A4:0.5 B4:0.5 C5:0.5 | " +
                "D5 G4 G4 | E5 C5:0.5 D5:0.5 E5:0.5 F#5:0.5 | G5 G4 G4 | C5 D5:0.5 C5:0.5 B4:0.5 A4:0.5 | B4 C5:0.5 B4:0.5 A4:0.5 G4:0.5 | " +
                "A4 B4:0.5 A4:0.5 G4:0.5 F#4:0.5 | G4:3",
                "G | G | C | G | Am | G | D | D | G | G | C | G | Am | G | D | G"),
        };

        public static PerformSong Find(string id)
        {
            foreach (var s in All) if (s.id == id) return s;
            return null;
        }

        public static PerformSong Make(string id, string title, PerformCategory category, string animal, string source, int stars, float bpm,
            string meter, PerformStyle style, string melody, string chords, bool flats = false)
        {
            var s = new PerformSong { id = id, title = title, category = category, animal = animal, source = source, stars = stars, bpm = bpm, style = style, flats = flats };
            var m = meter.Split('/');
            s.meterTop = int.Parse(m[0], CultureInfo.InvariantCulture);
            s.meterBottom = int.Parse(m[1], CultureInfo.InvariantCulture);

            // メロディ
            float t = 0f;
            int bar = 0;
            bool tie = false;
            foreach (string b in melody.Split('|'))
            {
                s.barStarts.Add(t);
                foreach (string raw in b.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    bool tieNext = raw.EndsWith("~", StringComparison.Ordinal);
                    var n = Songs.ParseNote(tieNext ? raw.Substring(0, raw.Length - 1) : raw);
                    if (!n.IsRest)
                    {
                        int last = s.notes.Count - 1;
                        if (tie && last >= 0 && s.notes[last].midi == n.midi)
                        {
                            var p = s.notes[last];
                            p.beats += n.beats;
                            s.notes[last] = p;
                        }
                        else s.notes.Add(new PerformNote { midi = n.midi, on = t, beats = n.beats, bar = bar });
                    }
                    tie = tieNext && !n.IsRest;
                    t += n.beats;
                }
                bar++;
            }
            s.Length = t;

            // 和音
            var chordBars = chords.Split('|');
            if (chordBars.Length != s.barStarts.Count)
                throw new ArgumentException($"{id}: 和音の小節の数 {chordBars.Length} が メロディ {s.barStarts.Count} と ちがう");
            for (int i = 0; i < chordBars.Length; i++)
            {
                var toks = chordBars[i].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                float len = s.BarLength(i), fixedBeats = 0f;
                int free = 0;
                foreach (string tok in toks)
                {
                    int c = tok.IndexOf(':');
                    if (c >= 0) fixedBeats += float.Parse(tok.Substring(c + 1), CultureInfo.InvariantCulture);
                    else free++;
                }
                float each = free > 0 ? (len - fixedBeats) / free : 0f;
                float at = s.barStarts[i];
                foreach (string tok in toks)
                {
                    int c = tok.IndexOf(':');
                    string name = c >= 0 ? tok.Substring(0, c) : tok;
                    float d = c >= 0 ? float.Parse(tok.Substring(c + 1), CultureInfo.InvariantCulture) : each;
                    if (name != "R")
                    {
                        ParseChord(name, out int root, out int[] tones);
                        s.chords.Add(new ChordSpan { name = name, root = root, tones = tones, on = at, beats = d });
                    }
                    at += d;
                }
            }

            int lo = 999, hi = -1;
            foreach (var n in s.notes)
            {
                lo = Math.Min(lo, n.midi);
                hi = Math.Max(hi, n.midi);
                if (NoteUtil.IsBlack(n.midi)) s.UsesBlackKeys = true;
            }
            s.Low = lo;
            s.High = hi;
            return s;
        }

        /// <summary>「C」「F#m」「G7」「Bb」「A5」などを、根音と 和音の音にする。</summary>
        public static void ParseChord(string name, out int root, out int[] tones)
        {
            int[] pcs = { 9, 11, 0, 2, 4, 5, 7 };   // A B C D E F G
            root = pcs[char.ToUpperInvariant(name[0]) - 'A'];
            int i = 1;
            if (i < name.Length && name[i] == '#') { root++; i++; }
            else if (i < name.Length && name[i] == 'b') { root--; i++; }
            root = (root + 12) % 12;
            int[] steps;
            switch (name.Substring(i))
            {
                case "": steps = new[] { 0, 4, 7 }; break;
                case "m": steps = new[] { 0, 3, 7 }; break;
                case "7": steps = new[] { 0, 4, 7, 10 }; break;
                case "m7": steps = new[] { 0, 3, 7, 10 }; break;
                case "6": steps = new[] { 0, 4, 7, 9 }; break;
                case "5": steps = new[] { 0, 7 }; break;
                case "dim": steps = new[] { 0, 3, 6 }; break;
                default: throw new ArgumentException("わからない和音: " + name);
            }
            tones = new int[steps.Length];
            for (int k = 0; k < steps.Length; k++) tones[k] = (root + steps[k]) % 12;
        }
    }
}
