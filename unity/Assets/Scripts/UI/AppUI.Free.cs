using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Piamo
{
    /// <summary>
    /// 「じゆうに ひく」の たのしみ。
    ///  - ろくおん（●）と さいせい（▶）：ひいた 曲を とっておいて、あとで きく（1 きょく ぶん。この たんまつに 保存）
    ///  - ペダル：「おとを のばす」を、ピアノの 画面から すぐに
    ///  - マスコットを タップすると、ぴょんと はねて ひいてくれる
    ///  - 3 つの 音を いっしょに おすと「ドミソ！ あかるい ひびき」、すーっと なぞると「グリッサンド」と ほめる
    ///  - パソコンの キー（A S D F…）を けんばんに かく（せってい）
    /// </summary>
    public partial class AppUI
    {
        Recorder _recorder;
        Button _freeRec, _freePlayback, _freePedal;
        Label _freePedalLabel;
        /// <summary>1 ろくおん中・2 さいせい中（おわったときに どちらか わかるように）。</summary>
        int _recMode;
        int _recShownSecond = -1;
        float _mascotHop, _freeTipCooldown;
        readonly List<int> _held = new List<int>();
        int _glissLast = -1, _glissCount, _glissDir;
        /// <summary>さいごに はなした 音と その フレーム（ゆびが すべったか しらべる）。</summary>
        int _lastReleased = -1, _lastReleaseFrame = -1;
        readonly Dictionary<int, Label> _pcLabels = new Dictionary<int, Label>();
        int _pcSig = int.MinValue;

        /// <summary>テスト用：じゆうに ひく の ろくおん。</summary>
        public Recorder FreeRecorder => _recorder;

        /// <summary>すーっと なぞって、この数 つづけて となりの音を ひいたら「グリッサンド」。</summary>
        public const int GlissandoNotes = 6;

        static readonly string[] MascotLines = { "ぼくも ひいちゃお♪", "いい おとだね！", "もっと きかせて！", "ららら〜♪", "じょうず じょうず！" };

        void BuildFree()
        {
            _freeRec = _root.Q<Button>("free-rec");
            _freePlayback = _root.Q<Button>("free-playback");
            _freePedal = _root.Q<Button>("free-pedal");
            _freePedalLabel = _root.Q<Label>("free-pedal-label");
            SetIcon("free-pedal-icon", "pedal");
            OnClick(_freeRec, ToggleRecording);
            OnClick(_freePlayback, TogglePlayback);
            OnClick(_freePedal, TogglePedal);
            _freeMascot.RegisterCallback<ClickEvent>(_ => TapMascot());

            _recorder = new Recorder();
            _recorder.FromList(SaveSystem.Data.recording);
            _recorder.Down += m =>
            {
                if (play == null) return;
                play.EnsureVisible(m, m);
                play.DemoDown(m, true, 0.85f);
            };
            _recorder.Up += m => play?.DemoUp(m);
            _recorder.Stopped += OnRecorderStopped;
            RefreshFreeControls();
        }

        void SetFreeBubble(string text)
        {
            _freeBubble.text = text;
            _freeTimer = 0f;
        }

        // ------------------------------------------------------------------
        // ろくおん・さいせい

        void ToggleRecording()
        {
            if (_recorder.Recording)
            {
                _recorder.StopRecording();
                return;
            }
            if (_recorder.Playing) _recorder.StopPlayback();
            _recMode = 1;
            _recShownSecond = -1;
            _recorder.StartRecording();
            SetFreeBubble("ろくおん ちゅう！ ひいてね（もういちど ● で おしまい）");
            RefreshFreeControls();
        }

        void TogglePlayback()
        {
            if (_recorder.Playing)
            {
                _recorder.StopPlayback();
                return;
            }
            if (_recorder.Recording || !_recorder.HasRecording) return;
            _recMode = 2;
            _recorder.StartPlayback();
            SetFreeBubble("さっき ひいた きょくだよ♪");
            RefreshFreeControls();
        }

        void StopRecorder()
        {
            if (_recorder == null) return;
            if (_recorder.Recording) _recorder.StopRecording();
            if (_recorder.Playing) _recorder.StopPlayback();
        }

        void OnRecorderStopped()
        {
            int mode = _recMode;
            _recMode = 0;
            if (mode == 1)
            {
                if (_recorder.HasRecording)
                {
                    SaveSystem.Data.recording = _recorder.ToList();
                    SaveSystem.Save();
                    SetFreeBubble(_recorder.Full ? "いっぱい ひいたね！ ▶ で きいてみよう" : "ろくおん できたよ！ ▶ で きいてみよう");
                }
                else
                {
                    // なにも ひかなかったら、まえの ろくおんを のこす
                    _recorder.FromList(SaveSystem.Data.recording);
                    SetFreeBubble("ろくおん やめたよ");
                }
            }
            else if (mode == 2) SetFreeBubble("じょうずに ひけてたね♪");
            RefreshFreeControls();
        }

        void RefreshFreeControls()
        {
            if (_freeRec == null || _recorder == null) return;
            bool rec = _recorder.Recording, playing = _recorder.Playing;
            SetIcon("free-rec-icon", rec ? "stop" : "rec");
            _freeRec.EnableInClassList("free-rec--on", rec);
            if (!rec) _freeRec.style.scale = StyleKeyword.Null;
            _freePlayback.EnableInClassList("hidden", rec || !_recorder.HasRecording);
            SetIcon("free-playback-icon", playing ? "stop" : "play");
            bool sustain = SaveSystem.Settings.sustain;
            _freePedal.EnableInClassList("candy--yellow", sustain);
            _freePedal.EnableInClassList("candy--white", !sustain);
            _freePedalLabel.text = sustain ? "ペダル オン" : "ペダル";
        }

        void TogglePedal()
        {
            var s = SaveSystem.Settings;
            s.sustain = !s.sustain;
            SaveSystem.ApplySettings();
            SetFreeBubble(s.sustain ? "ペダル オン！ おとが ながく のびるよ" : "ペダル オフ。おとが すぐ とまるよ");
        }

        // ------------------------------------------------------------------
        // マスコット・ほめる ことば

        void TapMascot()
        {
            _mascotHop = 0.5f;
            SetFreeBubble(MascotLines[Random.Range(0, MascotLines.Length)]);
            if (audioOut == null) return;
            int c = NoteUtil.MiddleC + 12 * SaveSystem.Settings.octaveShift;
            int[][] tunes =
            {
                new[] { c, c + 4, c + 7, c + 12 },
                new[] { c + 12, c + 7, c + 4, c },
                new[] { c, c + 2, c + 4, c + 7, c + 9 },
            };
            StartCoroutine(Preview(tunes[Random.Range(0, tunes.Length)], 0.13f));
        }

        void FreeNotePlayed(int midi)
        {
            _recorder?.Add(midi, true);
            if (Mode != PlayMode.Free || play == null) return;
            // すーっと なぞった：おなじ フレームで まえの 音を はなして となりを おした（ゆびが すべった）。
            // はやく なぞって 1 けん とばしても よい。むきは おなじ まま
            int d = midi - _glissLast;
            bool slid = _glissLast >= 0 && _lastReleased == _glissLast && _lastReleaseFrame == Time.frameCount;
            bool step = slid && d != 0 && Mathf.Abs(d) <= 4 && (_glissDir == 0 || (d > 0 ? 1 : -1) == _glissDir);
            if (step)
            {
                _glissCount++;
                _glissDir = d > 0 ? 1 : -1;
            }
            else
            {
                _glissCount = 1;
                _glissDir = 0;
            }
            _glissLast = midi;
            if (_freeTipCooldown > 0f || (_recorder != null && (_recorder.Playing || _recorder.Recording))) return;
            if (_glissCount == GlissandoNotes)
            {
                SetFreeBubble(_glissDir > 0 ? "すーっと のぼったね！ グリッサンドって いうよ" : "すーっと おりたね！ グリッサンドって いうよ");
                _freeTipCooldown = 4f;
                _mascotHop = 0.5f;
                return;
            }
            // 3 つ いじょう いっしょに おさえたら、和音の なまえ
            play.HeldNotes(_held);
            if (_held.Count >= 3 && ChordNames.TryName(_held, SaveSystem.Settings.labels, out var notes, out var feel))
            {
                SetFreeBubble($"{notes}！ {feel}");
                _freeTipCooldown = 2.5f;
                _mascotHop = 0.5f;
            }
        }

        void UpdateFree(float dt, bool paused)
        {
            if (_freeTipCooldown > 0f) _freeTipCooldown -= dt;
            if (_recorder != null && !paused)
            {
                _recorder.Update(dt);
                if (_recorder.Recording)
                {
                    // ろくおん中は ● が どきどき。のこりが すくなくなったら おしらせ
                    float s = SaveSystem.Settings.reduceMotion ? 1f : 1f + 0.08f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
                    _freeRec.style.scale = new Scale(new Vector2(s, s));
                    int left = Mathf.CeilToInt(Recorder.MaxSeconds - _recorder.Time);
                    if (left <= 10 && left != _recShownSecond)
                    {
                        _recShownSecond = left;
                        SetFreeBubble($"ろくおん あと {left}びょう");
                    }
                }
            }
            if (Mode != PlayMode.Free) return;
            float hop = 0f;
            if (_mascotHop > 0f)
            {
                _mascotHop -= dt;
                hop = -Mathf.Sin(Mathf.Clamp01(_mascotHop / 0.5f) * Mathf.PI) * 36f;
            }
            bool still = SaveSystem.Settings.reduceMotion;
            _freeMascot.style.translate = new Translate(0, (still ? 0f : Mathf.Sin(Time.unscaledTime * 2f + 1f) * 6f) + hop);
            _freeMascot.style.rotate = new Rotate(still ? 0f : Mathf.Sin(Time.unscaledTime * 1.3f) * 4f);
        }

        // ------------------------------------------------------------------
        // パソコンの キーの もじ

        void UpdatePcLetters()
        {
            var layout = play?.Layout;
            bool on = SaveSystem.Settings.pcKeyLabels && layout != null;
            var range = on ? play.VisibleWhiteRange() : Vector2Int.zero;
            int sig = on ? ((layout.Low * 1000 + layout.High) * 31 + range.x) * 131 + range.y : -1;
            if (sig != _pcSig)
            {
                _pcSig = sig;
                foreach (var l in _pcLabels.Values) l.RemoveFromHierarchy();
                _pcLabels.Clear();
                if (on)
                {
                    foreach (var key in layout.Keys)
                    {
                        string letter = PianoInput.LetterFor(layout, range, key.midi);
                        if (letter == null) continue;
                        _pcLabels[key.midi] = UIParts.Text(letter, key.black ? "pc-letter pc-letter--black" : "pc-letter", _labelsLayer);
                    }
                }
            }
            if (_pcLabels.Count == 0) return;
            float keyUnits = play.Framing.keyPx / PanelScale;
            float size = Mathf.Clamp(keyUnits * 0.36f, 26f, 60f);
            foreach (var kv in _pcLabels)
            {
                bool black = NoteUtil.IsBlack(kv.Key);
                var front = play.view.LabelPoint(kv.Key);
                var world = black ? front : Vector3.Lerp(front, play.view.TopPoint(kv.Key), 0.5f);
                var lp = ScreenToLocal(_labelsLayer, play.WorldToScreen(world));
                var l = kv.Value;
                l.style.width = size;
                l.style.height = size;
                l.style.left = lp.x - size / 2f;
                l.style.top = lp.y - size / 2f;
                l.style.fontSize = size * 0.6f;
            }
        }

        /// <summary>テスト用：けんばんに かいた パソコンの キーの もじ（なければ null）。</summary>
        public string PcLetterOn(int midi) => _pcLabels.TryGetValue(midi, out var l) ? l.text : null;
    }
}
