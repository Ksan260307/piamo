using System;
using System.Collections.Generic;

namespace Piamo
{
    /// <summary>
    /// じゆうに ひく の ろくおん・さいせい（画面や音とは別の、きまりだけ。時間は Update で外からもらう）。
    ///  - おした・はなした 音と 時間を ためる（さいしょの音の すこし前から。ながさと 数には 上限）
    ///  - さいせいは 時間どおりに Down・Up を知らせる。とちゅうで止めたら、鳴っている音を ぜんぶ Up
    ///  - 保存は「ミリ秒・MIDI 番号・おした 1／はなした 0」の 3 つずつの 数のならび
    /// </summary>
    public class Recorder
    {
        public const int MaxEvents = 600;
        public const float MaxSeconds = 60f;
        /// <summary>さいせいの はじめの 間（びょう）。</summary>
        public const float LeadIn = 0.3f;

        public struct Event
        {
            public float time;
            public int midi;
            public bool down;
        }

        public readonly List<Event> Events = new List<Event>();
        public bool Recording { get; private set; }
        public bool Playing { get; private set; }
        /// <summary>ろくおん・さいせいの いまの 時間（びょう）。</summary>
        public float Time { get; private set; }
        public bool HasRecording => Events.Count > 0;
        public float Length => Events.Count > 0 ? Events[Events.Count - 1].time : 0f;
        public bool Full => Events.Count >= MaxEvents || Time >= MaxSeconds;

        public event Action<int> Down;
        public event Action<int> Up;
        /// <summary>ろくおんが いっぱいで とまった・さいせいが おわった。</summary>
        public event Action Stopped;

        float _first = -1f;
        int _next;
        readonly HashSet<int> _held = new HashSet<int>();

        public void StartRecording()
        {
            StopPlayback();
            Events.Clear();
            _held.Clear();
            _first = -1f;
            Time = 0f;
            Recording = true;
        }

        public void StopRecording()
        {
            if (!Recording) return;
            Recording = false;
            // はなしていない音は、ここで はなしたことにする
            foreach (int m in _held) Events.Add(new Event { time = Time, midi = m, down = false });
            _held.Clear();
            // さいしょの音の すこし前から はじめる
            if (Events.Count > 0)
            {
                float shift = Events[0].time - LeadIn;
                for (int i = 0; i < Events.Count; i++)
                {
                    var e = Events[i];
                    e.time = Math.Max(0f, e.time - shift);
                    Events[i] = e;
                }
            }
            Stopped?.Invoke();
        }

        /// <summary>けんばんを おした・はなした（ろくおん中だけ ためる）。</summary>
        public void Add(int midi, bool down)
        {
            if (!Recording) return;
            if (!down && !_held.Contains(midi)) return;
            if (down) _held.Add(midi);
            else _held.Remove(midi);
            if (_first < 0f) _first = Time;
            Events.Add(new Event { time = Time, midi = midi, down = down });
            if (Full) StopRecording();
        }

        public void StartPlayback()
        {
            if (Recording || Events.Count == 0) return;
            StopPlayback();
            Playing = true;
            Time = 0f;
            _next = 0;
        }

        public void StopPlayback()
        {
            if (!Playing) return;
            Playing = false;
            foreach (int m in _held) Up?.Invoke(m);
            _held.Clear();
            Stopped?.Invoke();
        }

        public void Update(float dt)
        {
            if (Recording)
            {
                Time += dt;
                if (Time >= MaxSeconds) StopRecording();
                return;
            }
            if (!Playing) return;
            Time += dt;
            while (_next < Events.Count && Events[_next].time <= Time)
            {
                var e = Events[_next++];
                if (e.down)
                {
                    // まだ鳴っている同じ音は いちど はなす
                    if (_held.Contains(e.midi)) Up?.Invoke(e.midi);
                    _held.Add(e.midi);
                    Down?.Invoke(e.midi);
                }
                else if (_held.Remove(e.midi)) Up?.Invoke(e.midi);
            }
            if (_next >= Events.Count) StopPlayback();
        }

        /// <summary>保存する 数のならび。</summary>
        public List<int> ToList()
        {
            var list = new List<int>(Events.Count * 3);
            foreach (var e in Events)
            {
                list.Add((int)Math.Round(e.time * 1000f));
                list.Add(e.midi);
                list.Add(e.down ? 1 : 0);
            }
            return list;
        }

        /// <summary>保存した 数のならびから（こわれていたら からっぽ）。</summary>
        public void FromList(List<int> list)
        {
            Events.Clear();
            if (list == null || list.Count % 3 != 0 || list.Count > MaxEvents * 3) return;
            int last = 0;
            for (int i = 0; i < list.Count; i += 3)
            {
                int ms = list[i], midi = list[i + 1], down = list[i + 2];
                if (ms < last || midi < 0 || midi > 127 || (down != 0 && down != 1))
                {
                    Events.Clear();
                    return;
                }
                last = ms;
                Events.Add(new Event { time = ms / 1000f, midi = midi, down = down == 1 });
            }
        }
    }
}
