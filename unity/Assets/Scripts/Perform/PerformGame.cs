using System;
using System.Collections.Generic;

namespace Piamo
{
    /// <summary>ばんそうの 1 音。</summary>
    public struct AccNote
    {
        public int midi;
        public float on;
        public float beats;
        public float velocity;
    }

    /// <summary>
    /// ばんそう（ドミソの おと）を 和音から作る。ベースは C2〜B2、和音は C3〜B3 にならべるので、
    /// メロディ（C4 より上）とも 子どもが ひく けんばんとも かさならない。
    /// </summary>
    public static class PerformAccompaniment
    {
        public const int BassLow = 36;    // C2
        public const int ChordLow = 48;   // C3

        enum Part { Bass, Fifth, Chord, Up0, Up1, Up2, Drone }

        public static List<AccNote> Build(PerformSong song)
        {
            var list = new List<AccNote>();
            if (song.style == PerformStyle.None || song.chords.Count == 0) return list;
            var pattern = Pattern(song);
            float full = song.BarBeats;
            for (int bar = 0; bar < song.barStarts.Count; bar++)
            {
                float start = song.barStarts[bar], len = song.BarLength(bar);
                // 弱起の小節は、小節の おわりの ほうだけ（ほんらいの 1 拍目は 曲の前）
                float shift = bar == 0 && len < full - 1e-4f ? full - len : 0f;
                var times = new List<float>();
                foreach (var (at, part, dur) in pattern)
                {
                    float t = at - shift;
                    if (t < -1e-4f || t >= len - 1e-4f) continue;
                    times.Add(t);
                    var c = ChordAt(song, start + t);
                    if (c == null) continue;
                    Emit(list, c.Value, part, start + t, Math.Min(dur, len - t), song.style);
                }
                // 拍の とちゅうで かわる和音は、かわったところで 和音を ならす
                foreach (var c in song.chords)
                {
                    if (c.on < start - 1e-4f || c.on >= start + len - 1e-4f) continue;
                    float t = c.on - start;
                    bool covered = false;
                    foreach (float x in times) if (Math.Abs(x - t) < 1e-4f) covered = true;
                    if (!covered) Emit(list, c, Part.Chord, c.on, Math.Min(0.5f, c.beats), song.style);
                }
            }
            list.Sort((a, b) => a.on.CompareTo(b.on));
            return list;
        }

        static List<(float, Part, float)> Pattern(PerformSong song)
        {
            var p = new List<(float, Part, float)>();
            float bar = song.BarBeats;
            switch (song.style)
            {
                case PerformStyle.Waltz:
                    p.Add((0f, Part.Bass, 0.9f));
                    for (float t = 1f; t < bar - 1e-4f; t += 1f) p.Add((t, Part.Chord, 0.7f));
                    break;
                case PerformStyle.Arp:
                    {
                        var order = new[] { Part.Bass, Part.Up0, Part.Up1, Part.Up2, Part.Up1, Part.Up0 };
                        int k = 0;
                        for (float t = 0f; t < bar - 1e-4f; t += 0.5f) p.Add((t, order[k++ % order.Length], 0.9f));
                    }
                    break;
                case PerformStyle.Elise:
                    p.Add((0f, Part.Bass, 0.7f));
                    p.Add((0.25f, Part.Up1, 0.6f));
                    p.Add((0.5f, Part.Up2, 0.5f));
                    break;
                case PerformStyle.Drone:
                    p.Add((0f, Part.Drone, bar));
                    break;
                default:
                    if (Math.Abs(bar - 3f) < 1e-4f && song.meterBottom == 8)
                    {
                        p.Add((0f, Part.Bass, 1.2f));
                        p.Add((1.5f, Part.Chord, 1f));
                    }
                    else
                    {
                        for (float t = 0f; t < bar - 1e-4f; t += 1f)
                        {
                            int beat = (int)Math.Round(t);
                            if (beat % 2 == 0) p.Add((t, beat % 4 == 0 ? Part.Bass : Part.Fifth, 0.9f));
                            else p.Add((t, Part.Chord, 0.7f));
                        }
                    }
                    break;
            }
            return p;
        }

        static ChordSpan? ChordAt(PerformSong song, float beat)
        {
            foreach (var c in song.chords)
                if (c.on - 1e-4f <= beat && beat < c.Off - 1e-4f) return c;
            return null;
        }

        /// <summary>和音の音を C3〜B3 に、ひくい順に。</summary>
        public static List<int> Upper(ChordSpan c)
        {
            var u = new List<int>();
            foreach (int pc in c.tones)
            {
                int m = ChordLow + pc;
                if (!u.Contains(m)) u.Add(m);
            }
            u.Sort();
            return u;
        }

        static void Emit(List<AccNote> list, ChordSpan c, Part part, float on, float beats, PerformStyle style)
        {
            var up = Upper(c);
            int bass = BassLow + c.root;
            int fifth = BassLow + (c.root + 7) % 12;
            void Add(int m, float v) => list.Add(new AccNote { midi = m, on = on, beats = beats, velocity = v });
            switch (part)
            {
                case Part.Bass: Add(bass, 0.5f); break;
                case Part.Fifth: Add(fifth, 0.42f); break;
                case Part.Chord: foreach (int m in up) Add(m, 0.3f); break;
                case Part.Up0: Add(up[0], 0.34f); break;
                case Part.Up1: Add(up[Math.Min(1, up.Count - 1)], 0.34f); break;
                case Part.Up2: Add(up[up.Count - 1], 0.34f); break;
                case Part.Drone:
                    Add(bass, 0.36f);
                    Add(fifth + 12, 0.26f);
                    break;
            }
        }
    }

    /// <summary>えんそうの曲を ひくのに つかう けんばん（いまの せっていで たりなければ ふやす）。</summary>
    public static class PerformLayout
    {
        public struct Result
        {
            public int octaves;
            public int shift;
            public bool blackKeys;
        }

        /// <summary>いまの けんばんで ひけるなら null。ひけなければ、いちばん少ない オクターブで ひける けんばん。</summary>
        public static Result? For(PerformSong song, PiamoSettings s)
        {
            bool needBlack = song.UsesBlackKeys;
            var cur = KeyboardLayout.Create(s.octaves, s.octaveShift, s.blackKeys);
            if (cur.Low <= song.Low && song.High <= cur.High && (!needBlack || s.blackKeys)) return null;
            bool black = s.blackKeys || needBlack;
            for (int o = KeyboardLayout.MinOctaves; o <= KeyboardLayout.MaxOctaves; o++)
            {
                var lim = KeyboardLayout.ShiftLimits(o);
                foreach (int shift in new[] { 0, -1, 1, -2, 2 })
                {
                    if (shift < lim.x || shift > lim.y) continue;
                    var k = KeyboardLayout.Create(o, shift, black);
                    if (k.Low <= song.Low && song.High <= k.High) return new Result { octaves = o, shift = shift, blackKeys = black };
                }
            }
            return new Result { octaves = KeyboardLayout.MaxOctaves, shift = 0, blackKeys = black };
        }
    }

    /// <summary>
    /// えんそうモードの すすみかた（画面や音とは別の、きまりだけ。時間は Update で外からもらう）。
    ///  - きく（Listen）: 曲を ぜんぶ アプリが ひく（けんばんも しずんで ひかる）
    ///  - いっしょに（Along）: ばんそうと うすい おてほんが ながれ、ひく けんばんが その時に ひかる。
    ///    子どもが その音を ちょうどよく おすと「ぴったり」「いいね」。まちがえても 曲は とまらない
    ///  - ひとつずつ（Step）: つぎの音の けんばんが ひかって、おすまで まつ。おすと つぎへ すすむ
    /// きく・いっしょには、はじめに 1 小節ぶん カウントする。
    /// </summary>
    public class PerformGame
    {
        public enum Mode { Listen, Along, Step }
        public enum State { Idle, CountIn, Playing, Finished }
        public enum Grade { None, Perfect, Good, Miss }
        public enum PressResult { Ignored, Correct, Wrong }

        /// <summary>「ぴったり」の はば（秒）。</summary>
        public const float PerfectSeconds = 0.12f;
        /// <summary>はやく おしても あたりにする 時間（秒）。</summary>
        public const float EarlySeconds = 0.25f;
        /// <summary>おそく おしても あたりにする 時間（秒）。長い音は 音の長さの半分まで（LateSecondsMax まで）まつ。</summary>
        public const float LateSeconds = 0.3f;
        public const float LateSecondsMax = 0.6f;
        /// <summary>さいごの音のあと、ばんそうが きえるまで まつ（拍）。</summary>
        public const float TailBeats = 1f;
        /// <summary>ひとつずつ：おしたあと つぎの音まで すすむ はやさ（曲の はやさの 何ばいか）。</summary>
        public const float StepCatchUp = 2.5f;
        /// <summary>メロディを はなす まで（音の長さの わりあい。おなじ音が つづいても きこえるように）。</summary>
        public const float Gate = 0.9f;
        /// <summary>とめたあと つづきから ひくときの カウント（3・2・1）。</summary>
        public const int ResumeTicks = 3;

        public PerformSong Song { get; private set; }
        public Mode CurrentMode { get; private set; }
        public State Current { get; private set; } = State.Idle;
        public bool Paused { get; private set; }
        public float TempoScale { get; private set; } = 1f;
        public bool Accompaniment { get; private set; }
        /// <summary>いっしょに：おてほんの メロディも ならす。</summary>
        public bool GuideSound { get; set; } = true;
        /// <summary>いまの位置（拍）。カウントの あいだは マイナス。</summary>
        public float Beat { get; private set; }
        public float SecondsPerBeat => Song == null ? 0.5f : 60f / (Song.bpm * TempoScale);
        /// <summary>ひとつずつ：つぎに おす音の番号。きく・いっしょに：つぎに鳴る音の番号。</summary>
        public int NextIndex { get; private set; }
        /// <summary>いま鳴っている メロディの番号（なければ -1）。</summary>
        public int SoundingIndex => _soundingMelody.Count > 0 ? _soundingMelody[_soundingMelody.Count - 1] : -1;
        public Grade[] Grades { get; private set; } = Array.Empty<Grade>();
        public int Perfect { get; private set; }
        public int Good { get; private set; }
        public int Missed { get; private set; }
        /// <summary>ひとつずつ：ちがう けんばんを おした回数。</summary>
        public int Mistakes { get; private set; }
        public int Total => Song == null ? 0 : Song.notes.Count;
        public List<AccNote> AccNotes { get; private set; } = new List<AccNote>();
        /// <summary>カウントの のこり（3, 2, 1）。カウント中でなければ 0。</summary>
        public int CountLeft { get; private set; }
        /// <summary>つづけて あたった 数（ミス・まちがいで 0）。</summary>
        public int Combo { get; private set; }
        public int BestCombo { get; private set; }
        /// <summary>ひとつずつ：いまの音を まっている 時間（秒）。</summary>
        public float StepWait { get; private set; }
        /// <summary>あたりの はばの かけ算（1 = ふつう。大きいほど はやくても おそくても あたり）。</summary>
        public float WindowScale { get; set; } = 1f;
        /// <summary>とめたあと、つづきの カウント中。</summary>
        public bool Resuming => _resumeTimer > 0f;

        public event Action<int> CountTick;              // のこりの数（4, 3, 2, 1）
        public event Action<int, float> MelodyOn;        // 番号・鳴らす秒数
        public event Action<int> MelodyOff;
        public event Action<AccNote, float> AccOn;       // 音・鳴らす秒数
        public event Action<AccNote> AccOff;
        public event Action<int, Grade> Judged;          // 番号・できばえ
        public event Action Finished;

        int _nextOn, _nextAcc, _judgeFrom;
        float _stepTarget, _resumeTimer;
        readonly List<int> _soundingMelody = new List<int>();
        readonly List<int> _soundingAcc = new List<int>();

        public static int CountInTicks(PerformSong song)
        {
            int n = (int)Math.Round(song.BarBeats / song.Pulse);
            return n >= 3 ? n : n * 2;
        }

        public void Start(PerformSong song, Mode mode, float tempoScale, bool accompaniment, bool guideSound)
        {
            Silence();
            Song = song;
            CurrentMode = mode;
            TempoScale = Math.Max(0.3f, tempoScale);
            Accompaniment = accompaniment;
            GuideSound = guideSound;
            AccNotes = accompaniment ? PerformAccompaniment.Build(song) : new List<AccNote>();
            Grades = new Grade[song.notes.Count];
            Perfect = Good = Missed = Mistakes = 0;
            Combo = BestCombo = 0;
            StepWait = 0f;
            _resumeTimer = 0f;
            _nextOn = _nextAcc = _judgeFrom = 0;
            NextIndex = 0;
            Paused = false;
            if (mode == Mode.Step)
            {
                Beat = song.notes.Count > 0 ? song.notes[0].on : 0f;
                _stepTarget = Beat;
                CountLeft = 0;
                Current = State.Playing;
            }
            else
            {
                CountLeft = CountInTicks(song);
                Beat = -CountLeft * song.Pulse;
                Current = State.CountIn;
            }
        }

        public void Restart()
        {
            if (Song != null) Start(Song, CurrentMode, TempoScale, Accompaniment, GuideSound);
        }

        public void Stop()
        {
            Silence();
            Current = State.Idle;
        }

        public void SetPaused(bool paused)
        {
            if (Paused == paused) return;
            Paused = paused;
            if (paused)
            {
                Silence();
                _resumeTimer = 0f;
                if (Current == State.Playing) CountLeft = 0;
            }
            else if (Current == State.Playing && CurrentMode != Mode.Step && Song != null)
            {
                // いきなり つづかないように 3・2・1 と かぞえてから
                _resumeTimer = ResumeTicks * Song.Pulse * SecondsPerBeat;
                CountLeft = ResumeTicks;
                CountTick?.Invoke(CountLeft);
            }
        }

        public void SetTempo(float scale) => TempoScale = Math.Max(0.3f, scale);

        /// <summary>ばんそうを とちゅうで かえる（いまより前の音は ならさない）。</summary>
        public void SetAccompaniment(bool on)
        {
            if (Song == null || on == Accompaniment) return;
            foreach (int i in _soundingAcc) AccOff?.Invoke(AccNotes[i]);
            _soundingAcc.Clear();
            Accompaniment = on;
            AccNotes = on ? PerformAccompaniment.Build(Song) : new List<AccNote>();
            _nextAcc = 0;
            while (_nextAcc < AccNotes.Count && AccNotes[_nextAcc].on < Beat - 1e-4f) _nextAcc++;
        }

        /// <summary>鳴っている音を ぜんぶ止める。</summary>
        void Silence()
        {
            foreach (int i in _soundingMelody) MelodyOff?.Invoke(i);
            _soundingMelody.Clear();
            foreach (int i in _soundingAcc)
                if (i < AccNotes.Count) AccOff?.Invoke(AccNotes[i]);
            _soundingAcc.Clear();
        }

        float LateBeats(PerformNote n)
        {
            float spb = SecondsPerBeat;
            float sec = Math.Min(LateSecondsMax, Math.Max(LateSeconds, n.beats * spb * 0.5f));
            return sec * WindowScale / spb;
        }

        void Hit()
        {
            Combo++;
            BestCombo = Math.Max(BestCombo, Combo);
        }

        public void Update(float dt)
        {
            if (Song == null || Current == State.Idle || Current == State.Finished || Paused) return;
            float spb = SecondsPerBeat;
            if (_resumeTimer > 0f)
            {
                _resumeTimer -= dt;
                int left = (int)Math.Ceiling(_resumeTimer / (Song.Pulse * spb) - 1e-4f);
                while (CountLeft > 1 && CountLeft > left)
                {
                    CountLeft--;
                    CountTick?.Invoke(CountLeft);
                }
                if (_resumeTimer > 0f) return;
                _resumeTimer = 0f;
                CountLeft = 0;
            }
            if (CurrentMode == Mode.Step)
            {
                if (Beat < _stepTarget) Beat = Math.Min(_stepTarget, Beat + dt / spb * StepCatchUp);
            }
            else Beat += dt / spb;

            while (CountLeft > 0 && Beat >= -CountLeft * Song.Pulse - 1e-4f)
            {
                CountTick?.Invoke(CountLeft);
                CountLeft--;
            }
            if (Current == State.CountIn)
            {
                if (Beat < -1e-4f) return;
                CountLeft = 0;
                Current = State.Playing;
            }

            // ばんそう
            while (_nextAcc < AccNotes.Count && AccNotes[_nextAcc].on <= Beat + 1e-4f)
            {
                var a = AccNotes[_nextAcc];
                _soundingAcc.Add(_nextAcc);
                AccOn?.Invoke(a, a.beats * spb);
                _nextAcc++;
            }
            for (int i = _soundingAcc.Count - 1; i >= 0; i--)
            {
                var a = AccNotes[_soundingAcc[i]];
                if (Beat >= a.on + a.beats)
                {
                    _soundingAcc.RemoveAt(i);
                    AccOff?.Invoke(a);
                }
            }

            // メロディ（きく・いっしょに）
            if (CurrentMode != Mode.Step)
            {
                for (int i = _soundingMelody.Count - 1; i >= 0; i--)
                {
                    var n = Song.notes[_soundingMelody[i]];
                    if (Beat >= n.on + n.beats * Gate)
                    {
                        int idx = _soundingMelody[i];
                        _soundingMelody.RemoveAt(i);
                        MelodyOff?.Invoke(idx);
                    }
                }
                while (_nextOn < Song.notes.Count && Song.notes[_nextOn].on <= Beat + 1e-4f)
                {
                    var n = Song.notes[_nextOn];
                    _soundingMelody.Add(_nextOn);
                    MelodyOn?.Invoke(_nextOn, n.beats * Gate * spb);
                    _nextOn++;
                    NextIndex = _nextOn;
                }
            }

            // いっしょに：おそすぎた音は「ミス」（きく では かぞえない）
            if (CurrentMode != Mode.Step)
            {
                while (_judgeFrom < Total)
                {
                    var n = Song.notes[_judgeFrom];
                    if (Grades[_judgeFrom] == Grade.None)
                    {
                        if (Beat <= n.on + LateBeats(n)) break;
                        if (CurrentMode == Mode.Along)
                        {
                            Grades[_judgeFrom] = Grade.Miss;
                            Missed++;
                            Combo = 0;
                            Judged?.Invoke(_judgeFrom, Grade.Miss);
                        }
                    }
                    _judgeFrom++;
                }
            }

            // ひとつずつ：つぎの音に とどいてから まっている 時間
            if (CurrentMode == Mode.Step && NextIndex < Total && Beat >= Song.notes[NextIndex].on - 1e-4f) StepWait += dt;

            bool done = CurrentMode == Mode.Step
                ? NextIndex >= Total && Beat >= Song.Length + TailBeats - 1e-4f
                : Beat >= Song.Length + TailBeats && _soundingMelody.Count == 0;
            if (done) Finish();
        }

        void Finish()
        {
            Silence();
            Current = State.Finished;
            Finished?.Invoke();
        }

        /// <summary>子どもが けんばんを おした。</summary>
        public PressResult Press(int midi)
        {
            if (Song == null || Paused || Current != State.Playing) return PressResult.Ignored;
            if (CurrentMode == Mode.Step)
            {
                if (NextIndex >= Total) return PressResult.Ignored;
                var n = Song.notes[NextIndex];
                if (midi != n.midi)
                {
                    Mistakes++;
                    Combo = 0;
                    return PressResult.Wrong;
                }
                Grades[NextIndex] = Grade.Perfect;
                Perfect++;
                Hit();
                StepWait = 0f;
                if (Beat < n.on) Beat = n.on;
                Judged?.Invoke(NextIndex, Grade.Perfect);
                NextIndex++;
                // さいごの音のあとは、ばんそうが なりおわるまで すすめる
                _stepTarget = NextIndex < Total ? Song.notes[NextIndex].on : Song.Length + TailBeats;
                return PressResult.Correct;
            }
            // いっしょに（と きく）：ちかくの 同じ音に あてる。きく では ほめるだけ（ほしには しない）
            if (_resumeTimer > 0f) return PressResult.Ignored;
            float spb = SecondsPerBeat;
            int best = -1;
            float bestAbs = float.MaxValue;
            for (int i = _judgeFrom; i < Total; i++)
            {
                var n = Song.notes[i];
                float late = (Beat - n.on) * spb;   // + は おそい
                if (late < -EarlySeconds * WindowScale) break;
                if (Grades[i] != Grade.None || n.midi != midi) continue;
                if (late > LateBeats(n) * spb) continue;
                if (Math.Abs(late) < bestAbs)
                {
                    best = i;
                    bestAbs = Math.Abs(late);
                }
            }
            if (best < 0) return PressResult.Wrong;
            var g = bestAbs <= PerfectSeconds * (0.5f + 0.5f * WindowScale) ? Grade.Perfect : Grade.Good;
            Grades[best] = g;
            if (g == Grade.Perfect) Perfect++;
            else Good++;
            Hit();
            Judged?.Invoke(best, g);
            return PressResult.Correct;
        }

        /// <summary>できばえの ほし（1〜3）。きくは 0。</summary>
        public int Stars
        {
            get
            {
                if (Current != State.Finished || Total == 0) return 0;
                if (CurrentMode == Mode.Along)
                {
                    float r = (Perfect + Good) / (float)Total;
                    return r >= 0.85f ? 3 : r >= 0.5f ? 2 : 1;
                }
                if (CurrentMode == Mode.Step) return Mistakes <= Total / 10 ? 3 : Mistakes <= Total / 3 ? 2 : 1;
                return 0;
            }
        }

        /// <summary>曲の どこまで すすんだか（0〜1）。</summary>
        public float Progress => Song == null || Song.Length <= 0f ? 0f : Math.Max(0f, Math.Min(1f, Beat / Song.Length));
    }
}
