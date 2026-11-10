using System;
using System.Collections.Generic;

namespace Piamo
{
    /// <summary>
    /// まねっこモードの すすみかた（画面や音とは別の、きまりだけ。テストしやすいように時間は Update で外からもらう）。
    ///  1. おてほん（Demo）: 曲のひとまとまりを鳴らす（けんばんも ひからせる）
    ///  2. まねする番（Listen）: 子どもが同じ音を、同じ順に おす。ちがう音を おしても やりなおしにはならない
    ///     正解の音を おさないまま HintSeconds たつか、まちがいが HintAfterMistakes 回 つづくと、正解の けんばんが ひかる（ヒント）
    ///  3. できた（Cleared）→ つぎの ひとまとまりへ。ぜんぶできたら おしまい（Finished）
    /// まつ時間を へらすため、
    ///  - おてほんの さいごの音が 鳴りはじめたら、もう まねする番（音が のこっていても おした音を 数える）
    ///  - おてほんの前・とちゅうでも、正しい はじめの音を おせば おてほんを止めて まねする番へ（ちがう音は 数えない）
    ///  - 「できた！」の あいだに つぎの ひとまとまりの はじめの音を おせば、おてほんを とばして つぎへ
    /// </summary>
    public class MimicGame
    {
        public enum State { Idle, Intro, Demo, Listen, Cleared, Finished }
        public enum PressResult { Ignored, Correct, Wrong, RoundComplete }

        public const float IntroSeconds = 0.6f;
        /// <summary>おてほんの さいごの音が 鳴りはじめてから、まねする番に なるまで。</summary>
        public const float ListenAfterLastNote = 0.05f;
        public const float ClearedSeconds = 1.1f;
        public const int RandomRounds = 6;
        /// <summary>まちがいが この回数 つづいたら、まよった時間に かかわらず ヒント。</summary>
        public const int HintAfterMistakes = 3;

        public State Current { get; private set; } = State.Idle;
        public SongDef Song { get; private set; }
        public readonly List<List<SongNote>> Rounds = new List<List<SongNote>>();
        public int RoundIndex { get; private set; }
        /// <summary>いまのひとまとまりで、もう ひけた音の数。</summary>
        public int Progress { get; private set; }
        /// <summary>まよっている時間（正解の音を おしてからの秒数）。</summary>
        public float WaitTime { get; private set; }
        public float HintSeconds { get; set; } = 10f;
        public float TempoScale { get; private set; } = 1f;
        public int Mistakes { get; private set; }
        /// <summary>いま つづいている まちがいの回数（正解で 0 に もどる）。</summary>
        public int WrongStreak { get; private set; }
        public bool HintShownThisNote { get; private set; }
        /// <summary>いまの おてほんで、もう鳴らした音の数（ドットを じゅんに ひからせる）。</summary>
        public int DemoPlayed { get; private set; }
        /// <summary>ずらしている半音の数（オクターブのせってい）。</summary>
        public int Transpose => _transpose;

        public event Action<int, float> DemoNoteOn;   // MIDI 番号・鳴らす秒数
        public event Action<int> DemoNoteOff;
        public event Action<State> StateChanged;
        public event Action<int> HintShown;           // ひかる けんばん
        public event Action RoundCleared;
        public event Action SongFinished;

        readonly Random _rng;
        int _transpose;
        int _lengthMode;
        float _stateTime;
        // おてほんの再生（まねする番に なっても、のこりの音は 鳴らしおえる）
        readonly List<(float on, float off, int midi)> _timeline = new List<(float, float, int)>();
        int _nextOn;
        float _demoTime;
        readonly List<int> _sounding = new List<int>();

        public MimicGame(int seed = 12345)
        {
            _rng = new Random(seed);
        }

        public List<SongNote> CurrentRound => RoundIndex < Rounds.Count ? Rounds[RoundIndex] : null;

        /// <summary>いま おすべき音（MIDI 番号。まねする番でなければ -1）。</summary>
        public int Expected => Current == State.Listen ? NoteAt(CurrentRound, Progress) : -1;

        /// <summary>ひとまとまりの k ばんめの音（休みはのぞく。ずらしたあと）。なければ -1。</summary>
        int NoteAt(List<SongNote> round, int k)
        {
            if (round == null) return -1;
            int i = 0;
            foreach (var n in round)
            {
                if (n.IsRest) continue;
                if (i == k) return n.midi + _transpose;
                i++;
            }
            return -1;
        }

        /// <summary>いまの ひとまとまりの k ばんめの音（ずらしたあと。ドットの いろ・ヒントの なまえ用）。なければ -1。</summary>
        public int RoundNote(int k) => NoteAt(CurrentRound, k);

        /// <summary>いまのひとまとまりの音の数（休みはのぞく）。</summary>
        public int RoundLength
        {
            get
            {
                var r = CurrentRound;
                if (r == null) return 0;
                int k = 0;
                foreach (var n in r) if (!n.IsRest) k++;
                return k;
            }
        }

        /// <summary>曲ぜんぶの音の数（おまかせは、いまある ひとまとまりの ぶん）。</summary>
        public int TotalNotes
        {
            get
            {
                int k = 0;
                foreach (var r in Rounds)
                foreach (var n in r)
                    if (!n.IsRest) k++;
                return k;
            }
        }

        public bool HintActive => Current == State.Listen && HintSeconds > 0f && (WaitTime >= HintSeconds || WrongStreak >= HintAfterMistakes);
        public int HintKey => HintActive ? Expected : -1;

        /// <summary>おわったときの ほし（まちがいの 少なさ。1〜3）。おわっていなければ 0。</summary>
        public int Stars
        {
            get
            {
                if (Current != State.Finished) return 0;
                int total = Math.Max(1, TotalNotes);
                return Mistakes <= total / 10 ? 3 : Mistakes <= total / 3 ? 2 : 1;
            }
        }

        /// <summary>
        /// はじめる。lengthMode: 0 みじかい（4 音まで）・1 ふつう（フレーズ）・2 ながい（2 フレーズ）。
        /// transpose: 曲の音を ずらす半音の数（オクターブのせってい）。
        /// </summary>
        public void Start(SongDef song, int lengthMode, float tempoScale, float hintSeconds, int transpose)
        {
            StopDemo();
            Song = song;
            _lengthMode = lengthMode;
            TempoScale = Math.Max(0.3f, tempoScale);
            HintSeconds = hintSeconds;
            _transpose = transpose;
            Rounds.Clear();
            if (song.random) Rounds.Add(RandomRound(0));
            else Rounds.AddRange(BuildRounds(song, lengthMode));
            RoundIndex = 0;
            Mistakes = 0;
            BeginIntro();
        }

        public void Stop()
        {
            StopDemo();
            SetState(State.Idle);
        }

        /// <summary>もういちど きく（いまのひとまとまりを、はじめから）。</summary>
        public void ReplayDemo()
        {
            if (Current == State.Idle || Current == State.Finished || Current == State.Cleared) return;
            StopDemo();
            BeginDemo();
        }

        /// <summary>おてほんの とちゅうなら 止めて、つぎは「よく きいてね」から（タブを かくした とき）。</summary>
        public void Rewind()
        {
            if (Current != State.Demo) return;
            StopDemo();
            BeginIntro();
        }

        /// <summary>はやさを かえる（つぎの おてほんから）。</summary>
        public void SetTempo(float tempoScale) => TempoScale = Math.Max(0.3f, tempoScale);

        /// <summary>オクターブを かえたとき：いまの ひとまとまりを、あたらしい たかさで はじめから。</summary>
        public void SetTranspose(int transpose)
        {
            if (transpose == _transpose) return;
            _transpose = transpose;
            if (Current == State.Intro || Current == State.Demo || Current == State.Listen)
            {
                StopDemo();
                BeginIntro();
            }
        }

        public static List<List<SongNote>> BuildRounds(SongDef song, int lengthMode)
        {
            var rounds = new List<List<SongNote>>();
            if (lengthMode >= 2)
            {
                for (int i = 0; i < song.phrases.Count; i += 2)
                {
                    var r = new List<SongNote>(song.phrases[i]);
                    if (i + 1 < song.phrases.Count) r.AddRange(song.phrases[i + 1]);
                    rounds.Add(r);
                }
            }
            else if (lengthMode == 1)
            {
                foreach (var p in song.phrases) rounds.Add(new List<SongNote>(p));
            }
            else
            {
                foreach (var p in song.phrases)
                {
                    // 4 音までに分ける（7 音なら 4 + 3、8 音なら 4 + 4）
                    int notes = 0;
                    foreach (var n in p) if (!n.IsRest) notes++;
                    int parts = Math.Max(1, (notes + 3) / 4);
                    int per = (notes + parts - 1) / parts;
                    var cur = new List<SongNote>();
                    int k = 0;
                    foreach (var n in p)
                    {
                        if (n.IsRest)
                        {
                            if (cur.Count > 0) cur.Add(n);
                            continue;
                        }
                        cur.Add(n);
                        k++;
                        if (k == per)
                        {
                            rounds.Add(TrimRests(cur));
                            cur = new List<SongNote>();
                            k = 0;
                        }
                    }
                    if (cur.Count > 0 && k > 0) rounds.Add(TrimRests(cur));
                }
            }
            return rounds;
        }

        static List<SongNote> TrimRests(List<SongNote> r)
        {
            while (r.Count > 0 && r[r.Count - 1].IsRest) r.RemoveAt(r.Count - 1);
            return r;
        }

        List<SongNote> RandomRound(int index)
        {
            int start = _lengthMode == 0 ? 2 : _lengthMode == 1 ? 3 : 4;
            int max = _lengthMode == 0 ? 4 : _lengthMode == 1 ? 6 : 8;
            int len = Math.Min(max, start + index);
            int[] whites = { 60, 62, 64, 65, 67, 69, 71, 72 };
            var r = new List<SongNote>();
            int prev = -1;
            for (int i = 0; i < len; i++)
            {
                int m;
                do { m = whites[_rng.Next(whites.Length)]; } while (m == prev && _rng.Next(3) > 0);
                prev = m;
                r.Add(new SongNote { midi = m, beats = 1f });
            }
            return r;
        }

        void BeginIntro()
        {
            Progress = 0;
            WaitTime = 0f;
            WrongStreak = 0;
            HintShownThisNote = false;
            DemoPlayed = 0;
            SetState(State.Intro);
        }

        void BeginDemo()
        {
            Progress = 0;
            WaitTime = 0f;
            WrongStreak = 0;
            HintShownThisNote = false;
            DemoPlayed = 0;
            _timeline.Clear();
            _nextOn = 0;
            _demoTime = 0f;
            float beat = 60f / (Song.bpm * TempoScale);
            float t = 0f;
            foreach (var n in CurrentRound)
            {
                float d = n.beats * beat;
                if (!n.IsRest) _timeline.Add((t, t + Math.Max(0.12f, d * 0.88f), n.midi + _transpose));
                t += d;
            }
            SetState(State.Demo);
        }

        void BeginListen()
        {
            WaitTime = 0f;
            WrongStreak = 0;
            HintShownThisNote = false;
            SetState(State.Listen);
        }

        /// <summary>おてほんが ぜんぶ鳴りおわる時間（秒）。</summary>
        public float DemoLength
        {
            get
            {
                float end = 0f;
                foreach (var e in _timeline) end = Math.Max(end, e.off);
                return end;
            }
        }

        /// <summary>おてほんの さいごの音が 鳴りはじめる時間（秒）。</summary>
        float LastOnset => _timeline.Count > 0 ? _timeline[_timeline.Count - 1].on : 0f;

        /// <summary>おてほんを止める（鳴っている音も止めて、のこりは鳴らさない）。</summary>
        void StopDemo()
        {
            foreach (int m in _sounding) DemoNoteOff?.Invoke(m);
            _sounding.Clear();
            _nextOn = _timeline.Count;
        }

        void SetState(State s)
        {
            Current = s;
            _stateTime = 0f;
            StateChanged?.Invoke(s);
        }

        /// <summary>おてほんの音を すすめる（まねする番に なってからも、鳴っている音を 止めるまで）。</summary>
        void UpdateDemo(float dt)
        {
            _demoTime += dt;
            while (_nextOn < _timeline.Count && _timeline[_nextOn].on <= _demoTime)
            {
                var e = _timeline[_nextOn++];
                _sounding.Add(e.midi);
                DemoPlayed++;
                DemoNoteOn?.Invoke(e.midi, e.off - e.on);
            }
            for (int i = _sounding.Count - 1; i >= 0; i--)
            {
                // 鳴りおわった音を止める（同じ音がつづくときのために、時間の順に見る）
                int m = _sounding[i];
                bool stillOn = false;
                foreach (var e in _timeline)
                {
                    if (e.midi == m && e.on <= _demoTime && e.off > _demoTime) { stillOn = true; break; }
                }
                if (!stillOn)
                {
                    _sounding.RemoveAt(i);
                    DemoNoteOff?.Invoke(m);
                }
            }
        }

        public void Update(float dt)
        {
            _stateTime += dt;
            if (Current == State.Demo || (Current == State.Listen && _sounding.Count > 0)) UpdateDemo(dt);
            switch (Current)
            {
                case State.Intro:
                    if (_stateTime >= IntroSeconds) BeginDemo();
                    break;
                case State.Demo:
                    // さいごの音が 鳴りはじめたら、もう まねする番（鳴りおわるのを またない）
                    if (_nextOn >= _timeline.Count && _demoTime >= LastOnset + ListenAfterLastNote) BeginListen();
                    break;
                case State.Listen:
                    WaitTime += dt;
                    if (HintActive && !HintShownThisNote)
                    {
                        HintShownThisNote = true;
                        HintShown?.Invoke(Expected);
                    }
                    break;
                case State.Cleared:
                    if (_stateTime >= ClearedSeconds) NextRound();
                    break;
            }
        }

        /// <summary>つぎの ひとまとまりへ（おわりなら Finished）。つづけられたら true。</summary>
        bool AdvanceRound()
        {
            RoundIndex++;
            if (Song.random && RoundIndex < RandomRounds && RoundIndex >= Rounds.Count) Rounds.Add(RandomRound(RoundIndex));
            if (RoundIndex >= Rounds.Count)
            {
                RoundIndex = Rounds.Count - 1;
                SetState(State.Finished);
                SongFinished?.Invoke();
                return false;
            }
            return true;
        }

        void NextRound()
        {
            if (AdvanceRound()) BeginIntro();
        }

        /// <summary>子どもが けんばんを おした。</summary>
        public PressResult Press(int midi)
        {
            switch (Current)
            {
                case State.Intro:
                case State.Demo:
                    // おてほんの前・とちゅうでも、正しい はじめの音なら まねする番へ（ちがう音は 数えない・おてほんも 止めない）
                    if (midi != NoteAt(CurrentRound, 0)) return PressResult.Ignored;
                    StopDemo();
                    BeginListen();
                    break;
                case State.Cleared:
                    // 「できた！」の あいだに つぎの ひとまとまりの はじめの音 → おてほんを とばして つぎへ
                    if (RoundIndex + 1 >= Rounds.Count || midi != NoteAt(Rounds[RoundIndex + 1], 0)) return PressResult.Ignored;
                    if (!AdvanceRound()) return PressResult.Ignored;
                    Progress = 0;
                    DemoPlayed = 0;
                    BeginListen();
                    break;
                case State.Listen:
                    break;
                default:
                    return PressResult.Ignored;
            }
            if (midi != Expected)
            {
                Mistakes++;
                WrongStreak++;
                return PressResult.Wrong;
            }
            Progress++;
            WaitTime = 0f;
            WrongStreak = 0;
            HintShownThisNote = false;
            if (Progress >= RoundLength)
            {
                StopDemo();
                SetState(State.Cleared);
                RoundCleared?.Invoke();
                return PressResult.RoundComplete;
            }
            return PressResult.Correct;
        }
    }
}
