using System;
using System.Collections.Generic;
using System.Globalization;

namespace Piamo
{
    public struct SongNote
    {
        /// <summary>MIDI 番号（まんなかのド C4 = 60）。休みは -1。</summary>
        public int midi;
        /// <summary>長さ（4 分音符 = 1）。</summary>
        public float beats;
        public bool IsRest => midi < 0;
    }

    public class SongDef
    {
        public string id;
        public string title;
        /// <summary>先生になる どうぶつ（UI/Characters の絵の名前）。</summary>
        public string animal;
        public float bpm;
        /// <summary>フレーズ（まねする ひとまとまり）。音は C4〜C5 の白鍵だけ。</summary>
        public List<List<SongNote>> phrases = new List<List<SongNote>>();
        /// <summary>れんしゅう（ドレミ など）。</summary>
        public bool exercise;
        /// <summary>おまかせ（毎回ちがう音をつくる）。</summary>
        public bool random;

        public IEnumerable<SongNote> AllNotes()
        {
            foreach (var p in phrases)
            foreach (var n in p)
                yield return n;
        }
    }

    /// <summary>
    /// まねっこの曲。どれも作られてから長い年月がたった、だれでも使えるメロディ（著作権の切れた民謡・童謡・クラシック）。
    /// 書き方：「C4」「E4:2」（:のあとは長さ。なければ 1）、「R」は休み、「|」でフレーズを区切る。
    /// </summary>
    public static class Songs
    {
        public static readonly List<SongDef> All = new List<SongDef>
        {
            Make("twinkle", "きらきらぼし", "owl", 100,
                "C4 C4 G4 G4 A4 A4 G4:2 | F4 F4 E4 E4 D4 D4 C4:2 | G4 G4 F4 F4 E4 E4 D4:2 | " +
                "G4 G4 F4 F4 E4 E4 D4:2 | C4 C4 G4 G4 A4 A4 G4:2 | F4 F4 E4 E4 D4 D4 C4:2"),
            Make("frog", "かえるのうた", "frog", 100,
                "C4 D4 E4 F4 E4 D4 C4:2 | E4 F4 G4 A4 G4 F4 E4:2 | C4 R C4 R C4 R C4 R | " +
                "C4:0.5 C4:0.5 D4:0.5 D4:0.5 E4:0.5 E4:0.5 F4:0.5 F4:0.5 E4 D4 C4:2"),
            Make("sheep", "メリーさんのひつじ", "sheep", 108,
                "E4 D4 C4 D4 E4 E4 E4:2 | D4 D4 D4:2 E4 G4 G4:2 | E4 D4 C4 D4 E4 E4 E4 E4 | D4 D4 E4 D4 C4:4"),
            Make("moon", "つきのひかり", "rabbit", 100,
                "C4 C4 C4 D4 E4:2 D4:2 | C4 E4 D4 D4 C4:4 | C4 C4 C4 D4 E4:2 D4:2 | C4 E4 D4 D4 C4:4"),
            Make("london", "ロンドンばし", "dog", 108,
                "G4:1.5 A4:0.5 G4 F4 E4 F4 G4:2 | D4 E4 F4:2 E4 F4 G4:2 | G4:1.5 A4:0.5 G4 F4 E4 F4 G4:2 | D4:2 G4:2 E4 C4:3"),
            Make("ode", "よろこびのうた", "lion", 100,
                "E4 E4 F4 G4 G4 F4 E4 D4 | C4 C4 D4 E4 E4:1.5 D4:0.5 D4:2 | E4 E4 F4 G4 G4 F4 E4 D4 | C4 C4 D4 E4 D4:1.5 C4:0.5 C4:2"),
            Make("boat", "こげこげボート", "duck", 104,
                "C4:1.5 C4:1.5 C4 D4:0.5 E4:1.5 | E4 D4:0.5 E4 F4:0.5 G4:3 | " +
                "C5:0.5 C5:0.5 C5:0.5 G4:0.5 G4:0.5 G4:0.5 E4:0.5 E4:0.5 E4:0.5 C4:0.5 C4:0.5 C4:0.5 | G4 F4:0.5 E4 D4:0.5 C4:3"),
            Make("jingle", "ジングルベル", "penguin", 116,
                "E4 E4 E4:2 E4 E4 E4:2 | E4 G4 C4:1.5 D4:0.5 E4:4 | F4 F4 F4:1.5 F4:0.5 F4 E4 E4 E4:0.5 E4:0.5 | " +
                "E4 D4 D4 E4 D4:2 G4:2 | E4 E4 E4:2 E4 E4 E4:2 | E4 G4 C4:1.5 D4:0.5 E4:4 | " +
                "F4 F4 F4:1.5 F4:0.5 F4 E4 E4 E4:0.5 E4:0.5 | G4 G4 F4 D4 C4:4"),
            Make("scale", "ドレミファソ", "cat", 96,
                "C4 D4 E4 F4 G4:2 | G4 F4 E4 D4 C4:2 | C4 D4 E4 F4 G4 A4 B4 C5:2 | C5 B4 A4 G4 F4 E4 D4 C4:2", exercise: true),
            Make("chord", "ドミソ", "bear", 96,
                "C4 E4 G4:2 | G4 E4 C4:2 | C4 E4 G4 C5:2 | C5 G4 E4 C4:2", exercise: true),
            new SongDef { id = "random", title = "おまかせ", animal = "panda", bpm = 96, random = true, exercise = true },
        };

        public static SongDef Find(string id)
        {
            foreach (var s in All) if (s.id == id) return s;
            return null;
        }

        static SongDef Make(string id, string title, string animal, float bpm, string notes, bool exercise = false)
        {
            var s = new SongDef { id = id, title = title, animal = animal, bpm = bpm, exercise = exercise };
            foreach (string phrase in notes.Split('|'))
            {
                var list = new List<SongNote>();
                foreach (string tok in phrase.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    list.Add(ParseNote(tok));
                if (list.Count > 0) s.phrases.Add(list);
            }
            return s;
        }

        /// <summary>「C4」「F#4:0.5」「R:2」を音にする。</summary>
        public static SongNote ParseNote(string tok)
        {
            string name = tok;
            float beats = 1f;
            int colon = tok.IndexOf(':');
            if (colon >= 0)
            {
                name = tok.Substring(0, colon);
                beats = float.Parse(tok.Substring(colon + 1), CultureInfo.InvariantCulture);
            }
            if (name == "R") return new SongNote { midi = -1, beats = beats };
            return new SongNote { midi = ParsePitch(name), beats = beats };
        }

        public static int ParsePitch(string name)
        {
            int[] pcs = { 9, 11, 0, 2, 4, 5, 7 };   // A B C D E F G
            char letter = char.ToUpperInvariant(name[0]);
            int pc = pcs[letter - 'A'];
            int i = 1;
            if (i < name.Length && (name[i] == '#' || name[i] == '♯')) { pc++; i++; }
            else if (i < name.Length && (name[i] == 'b' || name[i] == '♭')) { pc--; i++; }
            int octave = int.Parse(name.Substring(i), CultureInfo.InvariantCulture);
            return (octave + 1) * 12 + pc;
        }
    }
}
