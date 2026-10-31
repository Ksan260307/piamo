using System;
using System.Collections.Generic;

namespace Piamo
{
    /// <summary>
    /// まねっこモードの すすみかた（画面や音とは別の、きまりだけ。テストしやすいように時間は Update で外からもらう）。
    ///  1. おてほん（Demo）: 曲のひとまとまりを鳴らす（けんばんも ひからせる）
    ///  2. まねする番（Listen）: 子どもが同じ音を、同じ順に おす。ちがう音を おしても やりなおしにはならない
    ///     正解の音を おさないまま HintSeconds たつと、正解の けんばんが ひかる（ヒント）
    ///  3. できた（Cleared）→ つぎの ひとまとまりへ。ぜんぶできたら おしまい（Finished）
    /// </summary>
    public class MimicGame
    {
        public enum State { Idle, Intro, Demo, Listen, Cleared, Finished }
        public enum PressResult { Ignored, Correct, Wrong, RoundComplete }

        public const float IntroSeconds = 0.9f;
        public const float AfterDemoSeconds = 0.35f;
        public const float ClearedSeconds = 1.7f;
        public const int RandomRounds = 6;

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
        public bool HintShownThisNote { get; private set; }

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
        // おてほんの再生
        readonly List<(float on, float off, int midi)> _timeline = new List<(float, float, int)>();
        int _nextOn;
        readonly List<int> _sounding = new List<int>();

        public MimicGame(int seed = 12345)
        {
            _rng = new Random(seed);
        }

        public List<SongNote> CurrentRound => RoundIndex < Rounds.Count ? Rounds[RoundIndex] : null;

        /// <summary>いま おすべき音（MIDI 番号。まねする番でなければ -1）。</summary>
        public int Expected
        {
            get
            {
                var r = CurrentRound;
                if (Current != State.Listen || r == null) return -1;
                int k = 0;
                foreach (var n in r)
                {
                    if (n.IsRest) continue;
                    if (k == Progress) return n.midi + _transpose;
                    k++;
                }
                return -1;
            }
        }

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

        public bool HintActive => Current == State.Listen && HintSeconds > 0f && WaitTime >= HintSeconds;
        public int HintKey => HintActive ? Expected : -1;

        /// <summary>
        /// はじめる。lengthMode: 0 みじかい（4 音まで）・1 ふつう（フレーズ）・2 ながい（2 フレーズ）。
        /// transpose: 曲の音を ずらす半音の数（オクターブのせってい）。
        /// </summary>
        public void Start(SongDef song, int lengthMode, float tempoScale, float hintSeconds, int transpose)
        {
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
            StopDemoNotes();
            SetState(State.Idle);
        }

        /// <summary>もういちど きく（いまのひとまとまりを、はじめから）。</summary>
        public void ReplayDemo()
        {
            if (Current == State.Idle || Current == State.Finished || Current == State.Cleared) return;
            StopDemoNotes();
            BeginDemo();
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
            HintShownThisNote = false;
            SetState(State.Intro);
        }

        void BeginDemo()
        {
            Progress = 0;
            WaitTime = 0f;
            HintShownThisNote = false;
            _timeline.Clear();
            _nextOn = 0;
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

        void SetState(State s)
        {
            Current = s;
            _stateTime = 0f;
            StateChanged?.Invoke(s);
        }

        void StopDemoNotes()
        {
            foreach (int m in _sounding) DemoNoteOff?.Invoke(m);
            _sounding.Clear();
        }

        public void Update(float dt)
        {
            _stateTime += dt;
            switch (Current)
            {
                case State.Intro:
                    if (_stateTime >= IntroSeconds) BeginDemo();
                    break;
                case State.Demo:
                    while (_nextOn < _timeline.Count && _timeline[_nextOn].on <= _stateTime)
                    {
                        var e = _timeline[_nextOn++];
                        _sounding.Add(e.midi);
                        DemoNoteOn?.Invoke(e.midi, e.off - e.on);
                    }
                    for (int i = _sounding.Count - 1; i >= 0; i--)
                    {
                        // 鳴りおわった音を止める（同じ音がつづくときのために、時間の順に見る）
                        int m = _sounding[i];
                        bool stillOn = false;
                        foreach (var e in _timeline)
                        {
                            if (e.midi == m && e.on <= _stateTime && e.off > _stateTime) { stillOn = true; break; }
                        }
                        if (!stillOn)
                        {
                            _sounding.RemoveAt(i);
                            DemoNoteOff?.Invoke(m);
                        }
                    }
                    if (_nextOn >= _timeline.Count && _sounding.Count == 0 && _stateTime >= DemoLength + AfterDemoSeconds)
                    {
                        WaitTime = 0f;
                        SetState(State.Listen);
                    }
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

        void NextRound()
        {
            RoundIndex++;
            if (Song.random && RoundIndex < RandomRounds) Rounds.Add(RandomRound(RoundIndex));
            if (RoundIndex >= Rounds.Count)
            {
                RoundIndex = Rounds.Count - 1;
                SetState(State.Finished);
                SongFinished?.Invoke();
                return;
            }
            BeginIntro();
        }

        /// <summary>子どもが けんばんを おした。</summary>
        public PressResult Press(int midi)
        {
            if (Current != State.Listen) return PressResult.Ignored;
            int expected = Expected;
            if (midi != expected)
            {
                Mistakes++;
                return PressResult.Wrong;
            }
            Progress++;
            WaitTime = 0f;
            HintShownThisNote = false;
            if (Progress >= RoundLength)
            {
                SetState(State.Cleared);
                RoundCleared?.Invoke();
                return PressResult.RoundComplete;
            }
            return PressResult.Correct;
        }
    }
}
