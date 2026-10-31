using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Piamo
{
    public partial class AppUI
    {
        public enum PlayMode { Free, Mimic }

        public PlayMode Mode { get; private set; } = PlayMode.Free;
        public MimicGame Mimic { get; private set; }
        /// <summary>テスト用：まねっこの時間をはやめる。</summary>
        public static float MimicTimeScale = 1f;

        VisualElement _kbArea, _labelsLayer, _fxLayer, _freePanel, _mimicPanel, _songPicker, _mimicGame, _nav, _navTrack, _navWindow, _earnBanner, _teacher, _mimicDots, _meterDots, _hintArrow, _freeMascot;
        Button _tabFree, _tabMimic;
        Label _freeBubble, _mimicBubble, _mimicTitle, _meterText, _instChipLabel, _replayLabel;
        ScrollView _songList;
        readonly Dictionary<int, Label> _keyLabels = new Dictionary<int, Label>();
        readonly List<VisualElement> _navKeys = new List<VisualElement>();
        int _labelsBuiltFor = -1;
        LabelMode _labelsMode = (LabelMode)(-1);
        float _bubbleTimer, _teacherBounce, _teacherShake, _freeTimer;
        int _freeMessage;
        bool _navDragging;
        int _navPointer = -1;

        // おんぷ（♪）の とびだす こうか
        class Fx
        {
            public VisualElement e;
            public float t, life;
            public Vector2 pos, vel;
            public float spin, size;
        }
        readonly List<Fx> _fx = new List<Fx>();
        int _fxNext;
        const int FxPool = 26;

        static readonly string[] Cheers = { "じょうず！", "すごい！", "やったね！", "ばっちり！", "いいね！", "かんぺき！" };
        static readonly string[] FreeMessages =
        {
            "すきに ひいてね♪",
            "ゆびで すーっと なぞると、つづけて なるよ",
            "なんぼんの ゆびでも いっしょに おせるよ",
            "「まねっこ」で きょくを ひいてみよう！",
        };

        static readonly Dictionary<string, string> InstrumentMascot = new Dictionary<string, string>
        {
            { "piano", "cat" }, { "toy", "bear" }, { "musicbox", "rabbit" }, { "glock", "chick" }, { "marimba", "monkey" },
            { "organ", "elephant" }, { "flute", "bird" }, { "harp", "sheep" }, { "cat", "cat" }, { "dog", "dog" },
        };

        void BuildPlay()
        {
            _kbArea = _root.Q("keyboard-area");
            _labelsLayer = _root.Q("key-labels");
            _fxLayer = _root.Q("fx-layer");
            _freePanel = _root.Q("free-panel");
            _mimicPanel = _root.Q("mimic-panel");
            _songPicker = _root.Q("song-picker");
            _mimicGame = _root.Q("mimic-game");
            _nav = _root.Q("nav");
            _navTrack = _root.Q("nav-track");
            _navWindow = _root.Q("nav-window");
            _earnBanner = _root.Q("earn-banner");
            _teacher = _root.Q("teacher");
            _mimicDots = _root.Q("mimic-dots");
            _meterDots = _root.Q("stamp-meter-dots");
            _freeMascot = _root.Q("free-mascot");
            _freeBubble = _root.Q<Label>("free-bubble");
            _mimicBubble = _root.Q<Label>("mimic-bubble");
            _mimicTitle = _root.Q<Label>("mimic-title");
            _meterText = _root.Q<Label>("stamp-meter-text");
            _instChipLabel = _root.Q<Label>("inst-chip-label");
            _replayLabel = _root.Q<Label>("mimic-replay-label");
            _songList = _root.Q<ScrollView>("song-list");
            _tabFree = _root.Q<Button>("tab-free");
            _tabMimic = _root.Q<Button>("tab-mimic");

            SetIcon("play-back-icon", "back");
            SetIcon("play-settings-icon", "gear");
            SetIcon("nav-left-icon", "left");
            SetIcon("nav-right-icon", "right");
            SetIcon("mimic-replay-icon", "ear");
            SetIcon("mimic-change-icon", "songs");
            SetIcon("stamp-meter-icon", "stamp");
            SetIcon("earn-icon", "stamp");

            _root.Q<Button>("play-back").clicked += Back;
            _root.Q<Button>("play-settings").clicked += OpenSettings;
            OnClick(_tabFree, () => SetMode(PlayMode.Free));
            OnClick(_tabMimic, () => SetMode(PlayMode.Mimic));
            OnClick(_root.Q<Button>("nav-left"), () => play.Scroll(-7));
            OnClick(_root.Q<Button>("nav-right"), () => play.Scroll(7));
            OnClick(_root.Q<Button>("inst-chip"), OpenInstrumentPopup);
            OnClick(_root.Q<Button>("mimic-replay"), OnReplayPressed);
            OnClick(_root.Q<Button>("mimic-change"), StopSong);
            OnClick(_root.Q<Button>("earn-go"), () =>
            {
                _earnBanner.AddToClassList("hidden");
                Show(Page.Stamps);
            });
            OnClick(_root.Q<Button>("earn-later"), () => _earnBanner.AddToClassList("hidden"));
            OnClick(_root.Q<Button>("inst-popup-close"), CloseInstrumentPopup);
            _instPopup.RegisterCallback<PointerDownEvent>(e => { if (e.target == _instPopup) CloseInstrumentPopup(); });

            // 鍵盤のスクロール（小さな鍵盤の帯をドラッグ）
            _navTrack.RegisterCallback<PointerDownEvent>(e =>
            {
                _navDragging = true;
                _navPointer = e.pointerId;
                _navTrack.CapturePointer(e.pointerId);
                NavTo(e.localPosition.x);
            });
            _navTrack.RegisterCallback<PointerMoveEvent>(e => { if (_navDragging && e.pointerId == _navPointer) NavTo(e.localPosition.x); });
            _navTrack.RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.pointerId != _navPointer) return;
                _navDragging = false;
                _navTrack.ReleasePointer(e.pointerId);
            });
            _kbArea.RegisterCallback<GeometryChangedEvent>(_ => UpdateArea());

            for (int i = 0; i < 10; i++) UIParts.El("meter-dot", _meterDots);
            for (int i = 0; i < FxPool; i++)
            {
                var e = UIParts.El("fx hidden", _fxLayer);
                _fx.Add(new Fx { e = e, life = 0f });
            }
            _hintArrow = UIParts.El("hint-arrow hidden", _labelsLayer);
            UIParts.SetIcon(_hintArrow, Icon("hint"));

            HideScrollers(_songList);
            HideScrollers(_root.Q<ScrollView>("inst-popup-scroll"));
            BuildSongList();
            BuildInstrumentPopup();
            SetMode(PlayMode.Free);
        }

        // ------------------------------------------------------------------

        void EnterPlay()
        {
            _earnBanner.AddToClassList("hidden");
            if (play != null) play.ApplySettings(SaveSystem.Settings);
            RefreshPlayAfterSettings();
            UpdateStampMeter();
            OnLayoutPlay();
        }

        void LeavePlay()
        {
            if (Mimic != null && Mimic.Current != MimicGame.State.Idle) StopSong();
            play?.ReleaseAll();
            input?.ReleaseAll();
            audioOut?.AllOff(false);
            SaveSystem.Save();
        }

        public void SetMode(PlayMode m)
        {
            Mode = m;
            _tabFree.EnableInClassList("tab--on", m == PlayMode.Free);
            _tabMimic.EnableInClassList("tab--on", m == PlayMode.Mimic);
            _freePanel.EnableInClassList("hidden", m != PlayMode.Free);
            _mimicPanel.EnableInClassList("hidden", m != PlayMode.Mimic);
            if (m == PlayMode.Free)
            {
                if (Mimic != null && Mimic.Current != MimicGame.State.Idle) StopSong();
            }
            else ShowPicker();
            OnLayoutPlay();
        }

        void RefreshPlayAfterSettings()
        {
            var s = SaveSystem.Settings;
            var info = InstrumentCatalog.Get(s.instrument);
            _instChipLabel.text = info.label;
            UIParts.SetIcon(_root.Q("inst-chip-icon"), Icon("inst_" + info.id));
            UIParts.SetIcon(_freeMascot, Character(InstrumentMascot.TryGetValue(info.id, out var a) ? a : "cat"));
            BuildKeyLabels(true);
            OnLayoutPlay();
            RefreshInstrumentPopup();
        }

        void OnLayoutPlay()
        {
            if (_kbArea == null || play == null || play.Layout == null) return;
            var vt = _root.panel?.visualTree;
            if (vt == null || vt.layout.width < 1f) return;
            bool portrait = _root.ClassListContains("portrait");
            float frac = Mode == PlayMode.Mimic ? (portrait ? 0.42f : 0.52f) : (portrait ? 0.5f : 0.62f);
            float k = PanelScale;
            float widthPx = Mathf.Max(1f, (_kbArea.layout.width > 1f ? _kbArea.layout.width : vt.layout.width) * k);
            float minKeyPx = SaveSystem.Settings.MinKeyWidthUI * k;
            float h = KeyboardFraming.PreferredHeight(widthPx, ScreenSize.y, play.Layout.WhiteCount, minKeyPx, frac);
            _kbArea.style.height = h / k;
            UpdateArea();
        }

        void UpdateArea()
        {
            if (play == null || _kbArea.layout.width < 1f || Current != Page.Play) return;
            var rect = ScreenRectOf(_kbArea);
            play.SetArea(rect, ScreenSize, SaveSystem.Settings.MinKeyWidthUI * PanelScale);
        }

        void OnFramingChanged()
        {
            var f = play.Framing;
            bool navOn = f.scrolling;
            if (_nav.ClassListContains("hidden") == navOn) _nav.EnableInClassList("hidden", !navOn);
            if (navOn) RefreshNav();
        }

        void RefreshNav()
        {
            var layout = play.Layout;
            if (layout == null) return;
            if (_navKeys.Count != layout.WhiteCount)
            {
                foreach (var e in _navKeys) e.RemoveFromHierarchy();
                _navKeys.Clear();
                for (int i = 0; i < layout.WhiteCount; i++)
                {
                    var e = UIParts.El("nav-key");
                    if (NoteUtil.PitchClass(layout.WhiteMidi(i)) == 0) e.AddToClassList("nav-key--c");
                    _navTrack.Insert(_navTrack.childCount - 1, e);
                    _navKeys.Add(e);
                }
                _navWindow.BringToFront();
            }
            var f = play.Framing;
            float w = Mathf.Max(1f, _navTrack.layout.width - 12f);
            float per = w / layout.WhiteCount;
            _navWindow.style.left = 6f + f.offset * per - 4f;
            _navWindow.style.width = f.visible * per + 8f;
        }

        void NavTo(float localX)
        {
            var layout = play.Layout;
            if (layout == null) return;
            float w = Mathf.Max(1f, _navTrack.layout.width - 12f);
            float per = w / layout.WhiteCount;
            float center = (localX - 6f) / per;
            play.SetOffset(Mathf.Round(center - play.Framing.visible / 2f));
        }

        // ------------------------------------------------------------------
        // 鍵盤の音の名前

        void BuildKeyLabels(bool force)
        {
            var layout = play?.Layout;
            if (layout == null) return;
            var mode = SaveSystem.Settings.labels;
            int sig = layout.Low * 1000 + layout.High;
            if (!force && sig == _labelsBuiltFor && mode == _labelsMode) return;
            _labelsBuiltFor = sig;
            _labelsMode = mode;
            foreach (var l in _keyLabels.Values) l.RemoveFromHierarchy();
            _keyLabels.Clear();
            if (mode == LabelMode.None) return;
            foreach (var key in layout.Keys)
            {
                if (key.black) continue;
                var l = UIParts.Text(NoteUtil.Name(key.midi, mode), "key-label", _labelsLayer);
                l.style.backgroundColor = NoteUtil.NoteColor(key.midi);
                if (mode == LabelMode.ColorOnly) l.text = "";
                _keyLabels[key.midi] = l;
            }
            _hintArrow.BringToFront();
        }

        void LateUpdatePlay()
        {
            if (Current != Page.Play || play == null || play.Layout == null) return;
            float k = PanelScale;
            float keyUnits = play.Framing.keyPx / k;
            float size = Mathf.Clamp(keyUnits * 0.6f, 34f, 112f);
            float w = _labelsLayer.layout.width;
            foreach (var kv in _keyLabels)
            {
                var l = kv.Value;
                var sp = play.WorldToScreen(play.view.LabelPoint(kv.Key));
                var lp = ScreenToLocal(_labelsLayer, sp);
                bool visible = lp.x > -size && lp.x < w + size;
                l.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (!visible) continue;
                l.style.width = size;
                l.style.height = size;
                l.style.left = lp.x - size / 2f;
                l.style.top = lp.y - size / 2f;
                l.style.fontSize = size * (l.text.Length >= 2 ? 0.36f : 0.5f);
            }
            int hint = play.view.HintKey;
            if (hint >= 0)
            {
                var sp = play.WorldToScreen(play.view.TopPoint(hint));
                var lp = ScreenToLocal(_labelsLayer, sp);
                float a = Mathf.Clamp(keyUnits * 0.8f, 60f, 130f);
                _hintArrow.RemoveFromClassList("hidden");
                _hintArrow.style.width = a;
                _hintArrow.style.height = a;
                _hintArrow.style.left = lp.x - a / 2f;
                _hintArrow.style.top = lp.y - a * 1.15f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) * -a * 0.25f;
            }
            else if (!_hintArrow.ClassListContains("hidden")) _hintArrow.AddToClassList("hidden");
        }

        // ------------------------------------------------------------------
        // 鍵盤が おされた

        void OnNotePlayed(int midi)
        {
            int today = AppClock.Today();
            if (Stamps.RecordPlay(today))
            {
                SaveSystem.Save();
                audioOut?.PlaySfx("earn", 0.9f);
                _earnBanner.RemoveFromClassList("hidden");
            }
            UpdateStampMeter();
            if (SaveSystem.Settings.noteEffects) SpawnNote(midi);
            if (Mode == PlayMode.Mimic && Mimic != null && Mimic.Current == MimicGame.State.Listen)
            {
                var r = Mimic.Press(midi);
                if (r == MimicGame.PressResult.Correct)
                {
                    _teacherBounce = 0.3f;
                    SetBubble("そう そう！ その ちょうし♪");
                    RefreshDots();
                }
                else if (r == MimicGame.PressResult.Wrong)
                {
                    _teacherShake = 0.4f;
                    SetBubble(Mimic.HintActive ? "ひかっている けんばんを おしてね" : "おしい！ もういちど きいてみる？");
                }
            }
        }

        void UpdateStampMeter()
        {
            int today = AppClock.Today();
            int left = Stamps.PlaysLeft(today);
            bool got = Stamps.HasStampFor(today);
            if (got) _meterText.text = Stamps.PendingCount > 0 ? "スタンプが おせるよ！" : "きょうの スタンプ もらったよ";
            else _meterText.text = $"スタンプまで あと {left}かい";
            int filled = got ? 10 : StampBook.PlaysForStamp - left;
            for (int i = 0; i < _meterDots.childCount; i++) _meterDots[i].EnableInClassList("meter-dot--on", i < filled);
            _meterDots.EnableInClassList("hidden", got);
        }

        void SpawnNote(int midi)
        {
            if (_fx.Count == 0 || play == null) return;
            var f = _fx[_fxNext];
            _fxNext = (_fxNext + 1) % _fx.Count;
            var sp = play.WorldToScreen(play.view.TopPoint(midi));
            var lp = ScreenToLocal(_fxLayer, sp);
            float keyUnits = play.Framing.keyPx / PanelScale;
            f.size = Mathf.Clamp(keyUnits * 0.75f, 60f, 130f);
            f.pos = lp;
            f.vel = new Vector2(Random.Range(-60f, 60f), -Random.Range(260f, 380f));
            f.spin = Random.Range(-40f, 40f);
            f.t = 0f;
            f.life = 1.3f;
            UIParts.SetIcon(f.e, Icon(Random.value < 0.5f ? "note1" : "note2"));
            f.e.style.unityBackgroundImageTintColor = NoteUtil.NoteColor(midi);
            f.e.style.width = f.size;
            f.e.style.height = f.size;
            f.e.RemoveFromClassList("hidden");
            f.e.BringToFront();
        }

        void UpdateFx(float dt)
        {
            foreach (var f in _fx)
            {
                if (f.life <= 0f) continue;
                f.t += dt;
                if (f.t >= f.life)
                {
                    f.life = 0f;
                    f.e.AddToClassList("hidden");
                    continue;
                }
                f.pos += f.vel * dt;
                f.vel.x += Mathf.Sin(f.t * 7f) * 140f * dt;
                float u = f.t / f.life;
                float pop = u < 0.15f ? Mathf.SmoothStep(0.3f, 1.15f, u / 0.15f) : Mathf.Lerp(1.15f, 0.8f, (u - 0.15f) / 0.85f);
                f.e.style.left = f.pos.x - f.size / 2f;
                f.e.style.top = f.pos.y - f.size / 2f;
                f.e.style.scale = new Scale(new Vector2(pop, pop));
                f.e.style.rotate = new Rotate(f.spin * f.t);
                f.e.style.opacity = 1f - Mathf.SmoothStep(0.55f, 1f, u);
            }
        }

        // ------------------------------------------------------------------
        // がっきを えらぶ（ピアノの画面から すぐに）

        readonly List<(Button b, string id)> _popupCards = new List<(Button, string)>();

        void BuildInstrumentPopup()
        {
            var grid = _root.Q("inst-popup-grid");
            foreach (var info in InstrumentCatalog.All)
            {
                string id = info.id;
                var b = UIParts.Candy(info.label, "white", Icon("inst_" + id), () =>
                {
                    SelectInstrument(id);
                    CloseInstrumentPopup();
                }, grid, "inst-card");
                _popupCards.Add((b, id));
            }
        }

        void RefreshInstrumentPopup()
        {
            string cur = SaveSystem.Settings.instrument;
            foreach (var (b, id) in _popupCards) b.EnableInClassList("inst-card--on", id == cur);
        }

        void OpenInstrumentPopup()
        {
            RefreshInstrumentPopup();
            _instPopup.RemoveFromClassList("hidden");
            UpdateInputActive();
        }

        void CloseInstrumentPopup()
        {
            _instPopup.AddToClassList("hidden");
            UpdateInputActive();
        }

        /// <summary>楽器をかえて、ためしに ドミソ を鳴らす。</summary>
        public void SelectInstrument(string id)
        {
            SaveSystem.Settings.instrument = id;
            SaveSystem.ApplySettings();
            var info = InstrumentCatalog.Get(id);
            _freeBubble.text = $"{info.label}の おとだよ♪";
            _freeTimer = 0f;
            if (audioOut != null)
            {
                int c = NoteUtil.MiddleC + 12 * SaveSystem.Settings.octaveShift;
                StartCoroutine(Preview(new[] { c, c + 4, c + 7 }));
            }
        }

        System.Collections.IEnumerator Preview(int[] notes)
        {
            foreach (int n in notes)
            {
                audioOut.NoteOn(n, 0.8f);
                yield return new WaitForSecondsRealtime(0.16f);
                audioOut.NoteOff(n);
            }
        }

        // ------------------------------------------------------------------
        // まねっこ

        readonly List<(Button b, string id, VisualElement star)> _songCards = new List<(Button, string, VisualElement)>();

        void BuildSongList()
        {
            foreach (var song in Songs.All)
            {
                var s = song;
                string color = s.random ? "purple" : s.exercise ? "green" : "white";
                var b = UIParts.Candy(s.title, color, Character(s.animal), () => StartSong(s), _songList, "song-card");
                var star = UIParts.El("icon song-card__star hidden", b);
                UIParts.SetIcon(star, Icon("star"));
                _songCards.Add((b, s.id, star));
            }
        }

        void ShowPicker()
        {
            _songPicker.RemoveFromClassList("hidden");
            _mimicGame.AddToClassList("hidden");
            foreach (var (b, id, star) in _songCards)
                star.EnableInClassList("hidden", !SaveSystem.Data.clearedSongs.Contains(id));
        }

        public void StartSong(SongDef song)
        {
            var s = SaveSystem.Settings;
            Mimic = new MimicGame(System.Environment.TickCount);
            Mimic.DemoNoteOn += (m, d) =>
            {
                play.EnsureVisible(m, m);
                play.DemoDown(m, SaveSystem.Settings.demoLights);
            };
            Mimic.DemoNoteOff += m => play.DemoUp(m);
            Mimic.StateChanged += OnMimicState;
            Mimic.HintShown += m =>
            {
                audioOut?.PlaySfx("hint", 0.7f);
                SetBubble("ひかっている けんばんを おしてね");
                play.EnsureVisible(m, m);
            };
            Mimic.RoundCleared += () =>
            {
                audioOut?.PlaySfx("success", 0.9f);
                SetBubble(Cheers[Random.Range(0, Cheers.Length)]);
                _teacherBounce = 0.8f;
                RefreshDots();
            };
            Mimic.SongFinished += OnSongFinished;
            float tempo = s.mimicSpeed == 0 ? 0.72f : s.mimicSpeed == 2 ? 1.3f : 1f;
            _songPicker.AddToClassList("hidden");
            _mimicGame.RemoveFromClassList("hidden");
            _mimicTitle.text = song.title;
            UIParts.SetIcon(_teacher, Character(song.animal));
            _replayLabel.text = "もういちど きく";
            Mimic.Start(song, s.mimicLength, tempo, s.hintSeconds, play.Transpose);
            RefreshDots();
        }

        void StopSong()
        {
            Mimic?.Stop();
            Mimic = null;
            play?.ReleaseAll();
            play?.view.SetHint(-1);
            ShowPicker();
        }

        void OnReplayPressed()
        {
            if (Mimic == null) return;
            if (Mimic.Current == MimicGame.State.Finished)
            {
                // おわったあと：「もういちど」で はじめから
                StartSong(Mimic.Song);
                return;
            }
            play.view.SetHint(-1);
            Mimic.ReplayDemo();
        }

        void OnMimicState(MimicGame.State st)
        {
            switch (st)
            {
                case MimicGame.State.Intro:
                    SetBubble("よく きいてね♪");
                    audioOut?.PlaySfx("listen", 0.6f);
                    RefreshDots();
                    var r = Mimic.CurrentRound;
                    if (r != null)
                    {
                        int lo = 999, hi = -1;
                        foreach (var n in r) if (!n.IsRest) { lo = Mathf.Min(lo, n.midi + play.Transpose); hi = Mathf.Max(hi, n.midi + play.Transpose); }
                        if (hi >= 0) play.EnsureVisible(lo, hi);
                    }
                    break;
                case MimicGame.State.Demo:
                    SetBubble("♪ ♪ ♪");
                    break;
                case MimicGame.State.Listen:
                    SetBubble("まねして ひいてね！");
                    RefreshDots();
                    break;
            }
        }

        void OnSongFinished()
        {
            audioOut?.PlaySfx("clear", 1f);
            SetBubble("ぜんぶ ひけたね！ はなまる！");
            _teacherBounce = 1.4f;
            _replayLabel.text = "もういちど";
            var save = SaveSystem.Data;
            if (Mimic != null && !save.clearedSongs.Contains(Mimic.Song.id))
            {
                save.clearedSongs.Add(Mimic.Song.id);
                SaveSystem.Save();
            }
        }

        void SetBubble(string text)
        {
            _mimicBubble.text = text;
            _bubbleTimer = 0.25f;
        }

        void RefreshDots()
        {
            if (Mimic == null) return;
            int n = Mimic.RoundLength;
            while (_mimicDots.childCount < n) UIParts.El("dot", _mimicDots);
            for (int i = 0; i < _mimicDots.childCount; i++)
            {
                var d = _mimicDots[i];
                d.style.display = i < n ? DisplayStyle.Flex : DisplayStyle.None;
                bool on = Mimic.Current == MimicGame.State.Cleared || Mimic.Current == MimicGame.State.Finished || i < Mimic.Progress;
                d.EnableInClassList("dot--on", on);
            }
            _mimicTitle.text = Mimic.Song.random
                ? $"{Mimic.Song.title}  {Mimic.RoundIndex + 1} / {MimicGame.RandomRounds}"
                : $"{Mimic.Song.title}  {Mimic.RoundIndex + 1} / {Mimic.Rounds.Count}";
        }

        void UpdatePlay(float dt)
        {
            if (Current != Page.Play) return;
            UpdateFx(dt);
            bool paused = SettingsOpen || DialogOpen || !_instPopup.ClassListContains("hidden");
            if (Mode == PlayMode.Mimic && Mimic != null && !paused)
            {
                Mimic.Update(dt * MimicTimeScale);
                play.view.SetHint(Mimic.HintKey);
            }
            else if (Mode == PlayMode.Free && play != null && play.view.HintKey >= 0) play.view.SetHint(-1);

            // 先生・マスコットの うごき
            if (_bubbleTimer > 0f)
            {
                _bubbleTimer -= dt;
                float s = 1f + 0.08f * Mathf.Sin(Mathf.Clamp01(_bubbleTimer / 0.25f) * Mathf.PI);
                _mimicBubble.style.scale = new Scale(new Vector2(s, s));
            }
            float bob = Mathf.Sin(Time.unscaledTime * 2.2f) * 6f;
            if (_teacherBounce > 0f)
            {
                _teacherBounce -= dt;
                bob -= Mathf.Abs(Mathf.Sin(_teacherBounce * 12f)) * 26f;
            }
            float shake = 0f;
            if (_teacherShake > 0f)
            {
                _teacherShake -= dt;
                shake = Mathf.Sin(_teacherShake * 50f) * 8f * _teacherShake;
            }
            _teacher.style.translate = new Translate(shake, bob);
            _freeMascot.style.translate = new Translate(0, Mathf.Sin(Time.unscaledTime * 2f + 1f) * 6f);
            _freeMascot.style.rotate = new Rotate(Mathf.Sin(Time.unscaledTime * 1.3f) * 4f);

            if (Mode == PlayMode.Free)
            {
                _freeTimer += dt;
                if (_freeTimer > 9f)
                {
                    _freeTimer = 0f;
                    _freeMessage = (_freeMessage + 1) % FreeMessages.Length;
                    _freeBubble.text = FreeMessages[_freeMessage];
                }
            }
            if (_navDragging || (!_nav.ClassListContains("hidden"))) RefreshNav();
        }
    }
}
