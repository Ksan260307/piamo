using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Piamo
{
    public partial class AppUI
    {
        public enum PlayMode { Free, Mimic, Perform }

        public PlayMode Mode { get; private set; } = PlayMode.Free;
        public MimicGame Mimic { get; private set; }
        /// <summary>テスト用：まねっこの時間をはやめる。</summary>
        public static float MimicTimeScale = 1f;

        VisualElement _kbArea, _labelsLayer, _fxLayer, _freePanel, _mimicPanel, _songPicker, _mimicGame, _nav, _navTrack, _navWindow, _earnBanner, _teacher, _mimicDots, _meterDots, _hintArrow, _freeMascot;
        VisualElement _mimicStars, _mimicMeter, _nowDot;
        Button _tabFree, _tabMimic, _mimicNext;
        Label _freeBubble, _mimicBubble, _mimicTitle, _meterText, _instChipLabel, _replayLabel, _mimicSpeedLabel;
        ScrollView _songList;
        readonly Dictionary<int, Label> _keyLabels = new Dictionary<int, Label>();
        /// <summary>けんばんを おした 時（ラベルを ぽよんと はねさせる）。</summary>
        readonly Dictionary<int, float> _labelPressedAt = new Dictionary<int, float>();
        readonly List<VisualElement> _navKeys = new List<VisualElement>();
        int _labelsBuiltFor = -1;
        LabelMode _labelsMode = (LabelMode)(-1);
        float _bubbleTimer, _teacherBounce, _teacherShake, _freeTimer, _earnTimer, _mimicLoadWait;
        int _freeMessage;
        bool _navDragging;
        /// <summary>さいごに あそんだ モード（アプリを ひらいて はじめて ピアノの画面に きたとき もどす。-1 = もう もどした）。</summary>
        int _restoreMode = -1;
        int _navPointer = -1;

        /// <summary>テスト用：まねっこの ドットの ようす（"on" / "demo" / "now" / ""）。</summary>
        public string MimicDotState(int i)
        {
            if (_mimicDots == null || i >= _mimicDots.childCount) return null;
            var d = _mimicDots[i];
            return d.ClassListContains("dot--on") ? "on" : d.ClassListContains("dot--demo") ? "demo" : d.ClassListContains("dot--now") ? "now" : "";
        }

        public string MimicBubbleText => _mimicBubble?.text;
        public string FreeBubbleText => _freeBubble?.text;

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
            "● を おすと、ひいた きょくを ろくおん できるよ",
            "「ペダル」を おすと、おとが ながく のびるよ",
            "3つの けんばんを いっしょに おすと…？",
            "どうぶつを タップしてみてね",
            "「まねっこ」で きょくを ひいてみよう！",
            "「えんそう」で、ひかる けんばんを いっしょに ひこう！",
        };
        static readonly string[] SpeedLabels = { "ゆっくり", "ふつう", "はやい" };

        static readonly Dictionary<string, string> InstrumentMascot = new Dictionary<string, string>
        {
            { "piano", "cat" }, { "toy", "bear" }, { "musicbox", "rabbit" }, { "glock", "chick" }, { "marimba", "monkey" },
            { "organ", "elephant" }, { "flute", "bird" }, { "harp", "sheep" }, { "cat", "cat" }, { "dog", "dog" },
        };

        void BuildPlay()
        {
            _restoreMode = SaveSystem.Data.lastPlayMode;
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
            _mimicMeter = _root.Q("mimic-meter");
            _meterDots = _root.Q("stamp-meter-dots");
            _freeMascot = _root.Q("free-mascot");
            _freeBubble = _root.Q<Label>("free-bubble");
            _mimicBubble = _root.Q<Label>("mimic-bubble");
            _mimicTitle = _root.Q<Label>("mimic-title");
            _meterText = _root.Q<Label>("stamp-meter-text");
            _instChipLabel = _root.Q<Label>("inst-chip-label");
            _replayLabel = _root.Q<Label>("mimic-replay-label");
            _mimicSpeedLabel = _root.Q<Label>("mimic-speed-label");
            _mimicNext = _root.Q<Button>("mimic-next");
            _songList = _root.Q<ScrollView>("song-list");
            _tabFree = _root.Q<Button>("tab-free");
            _tabMimic = _root.Q<Button>("tab-mimic");

            SetIcon("play-back-icon", "back");
            SetIcon("play-settings-icon", "gear");
            SetIcon("nav-left-icon", "left");
            SetIcon("nav-right-icon", "right");
            SetIcon("mimic-replay-icon", "ear");
            SetIcon("mimic-change-icon", "songs");
            SetIcon("mimic-next-icon", "next");
            SetIcon("mimic-speed-icon", "speed");
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
            OnClick(_mimicNext, StartNextMimicSong);
            OnClick(_root.Q<Button>("mimic-speed"), CycleMimicSpeed);
            OnClick(_root.Q<Button>("mimic-inst"), OpenInstrumentPopup);
            OnClick(_root.Q<Button>("earn-go"), () =>
            {
                _earnBanner.AddToClassList("hidden");
                Show(Page.Stamps);
            });
            OnClick(_root.Q<Button>("earn-later"), () => _earnBanner.AddToClassList("hidden"));
            OnClick(_root.Q<Button>("inst-popup-close"), CloseInstrumentPopup);
            _instPopup.RegisterCallback<PointerDownEvent>(e => { if (e.target == _instPopup) CloseInstrumentPopup(); });
            // 先生を タップすると、もういちど きける
            _teacher.RegisterCallback<ClickEvent>(_ => TapTeacher());

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

            for (int i = 0; i < StampBook.PlaysForStamp; i++) UIParts.El("meter-dot", _meterDots);
            for (int i = 0; i < FxPool; i++)
            {
                var e = UIParts.El("fx hidden", _fxLayer);
                _fx.Add(new Fx { e = e, life = 0f });
            }
            _hintArrow = UIParts.El("hint-arrow hidden", _labelsLayer);
            UIParts.SetIcon(_hintArrow, Icon("hint"));

            // まねっこが おわったときの ほし（ドットの かわりに 出す）
            _mimicStars = UIParts.El("mimic-stars hidden", _mimicDots.parent);
            for (int i = 0; i < 3; i++) UIParts.SetIcon(UIParts.El("result-star mimic-star", _mimicStars), Icon("star"));
            BuildMiniMeter(_mimicMeter);

            HideScrollers(_songList);
            HideScrollers(_root.Q<ScrollView>("inst-popup-scroll"));
            BuildSongList();
            BuildInstrumentPopup();
            BuildFree();
            BuildPerform();
            SetMode(PlayMode.Free);
        }

        // ------------------------------------------------------------------

        void EnterPlay()
        {
            _earnBanner.AddToClassList("hidden");
            if (play != null) play.ApplySettings(EffectiveSettings());
            RefreshPlayAfterSettings();
            UpdateStampMeter();
            // さいごに あそんだ モードから（アプリを ひらいて はじめの 1 かいだけ）
            if (_restoreMode >= 0)
            {
                var last = (PlayMode)Mathf.Clamp(_restoreMode, 0, 2);
                _restoreMode = -1;
                if (last != Mode) SetMode(last);
            }
            OnLayoutPlay();
        }

        void LeavePlay()
        {
            if (Mimic != null && Mimic.Current != MimicGame.State.Idle) StopSong();
            if (_performSong != null) StopPerform();
            StopRecorder();
            play?.ReleaseAll();
            input?.ReleaseAll();
            audioOut?.AllOff(false);
            SaveSystem.Save();
        }

        public void SetMode(PlayMode m)
        {
            // まねっこ と えんそう は いっしょに うごかない（おてほんの音・ひかる けんばん・けんばんの かずが ぶつからないように、ほかの モードは止める）
            if (m != PlayMode.Mimic && Mimic != null && Mimic.Current != MimicGame.State.Idle) StopSong();
            if (m != PlayMode.Perform && _performSong != null) StopPerform();
            if (m != PlayMode.Free) StopRecorder();
            Mode = m;
            // おぼえるのは、まえの モードに もどしたあと（はじめの くみたての「じゆう」で うわがき しない）
            if (_restoreMode < 0) SaveSystem.Data.lastPlayMode = (int)m;
            _tabFree.EnableInClassList("tab--on", m == PlayMode.Free);
            _tabMimic.EnableInClassList("tab--on", m == PlayMode.Mimic);
            _tabPerform.EnableInClassList("tab--on", m == PlayMode.Perform);
            _freePanel.EnableInClassList("hidden", m != PlayMode.Free);
            _mimicPanel.EnableInClassList("hidden", m != PlayMode.Mimic);
            _performPanel.EnableInClassList("hidden", m != PlayMode.Perform);
            if (m == PlayMode.Mimic) ShowPicker();
            else if (m == PlayMode.Perform) ShowPerformPicker();
            play?.view.SetHint(-1);
            OnLayoutPlay();
            if (play != null && play.Layout != null) OnFramingChanged();
        }

        void RefreshPlayAfterSettings()
        {
            var s = SaveSystem.Settings;
            var info = InstrumentCatalog.Get(s.instrument);
            _instChipLabel.text = info.label;
            var instIcon = Icon("inst_" + info.id);
            UIParts.SetIcon(_root.Q("inst-chip-icon"), instIcon);
            UIParts.SetIcon(_root.Q("mimic-inst-icon"), instIcon);
            UIParts.SetIcon(_root.Q("perform-inst-icon"), instIcon);
            UIParts.SetIcon(_freeMascot, Character(InstrumentMascot.TryGetValue(info.id, out var a) ? a : "cat"));
            _mimicSpeedLabel.text = SpeedLabels[s.mimicSpeed];
            Mimic?.SetTempo(s.MimicTempo);
            BuildKeyLabels(true);
            OnLayoutPlay();
            RefreshInstrumentPopup();
            RefreshFreeControls();
        }

        void OnLayoutPlay()
        {
            if (_kbArea == null || play == null || play.Layout == null) return;
            var vt = _root.panel?.visualTree;
            if (vt == null || vt.layout.width < 1f) return;
            bool portrait = _root.ClassListContains("portrait");
            float frac = Mode == PlayMode.Mimic ? (portrait ? 0.42f : 0.52f)
                : Mode == PlayMode.Perform ? (portrait ? 0.4f : 0.46f)
                : (portrait ? 0.5f : 0.62f);
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
            // えんそうの曲の あいだは、つぎの音に あわせて うごかすので、スクロールの帯は出さない（おちてくる音が 鍵盤に とどくように）
            bool navOn = f.scrolling && !(Mode == PlayMode.Perform && _performSong != null);
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
            _pcSig = int.MinValue;   // パソコンの キーの もじも かきなおす
            if (mode == LabelMode.None) return;
            foreach (var key in layout.Keys)
            {
                if (key.black) continue;
                var l = UIParts.Text(NoteUtil.Name(key.midi, mode), "key-label", _labelsLayer);
                l.style.backgroundColor = NoteUtil.NoteColor(key.midi);
                if (mode == LabelMode.ColorOnly) l.text = "";
                // まんなかの ド（ここから はじめる めじるし）
                if (key.midi == NoteUtil.MiddleC) l.AddToClassList("key-label--c4");
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
            float now = Time.unscaledTime;
            bool still = SaveSystem.Settings.reduceMotion;
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
                // おした けんばんの ラベルは ぽよんと はねる
                float pop = 1f;
                if (!still && _labelPressedAt.TryGetValue(kv.Key, out float at) && now - at < 0.25f)
                    pop = 1f + 0.28f * Mathf.Sin((now - at) / 0.25f * Mathf.PI);
                l.style.scale = new Scale(new Vector2(pop, pop));
            }
            UpdatePcLetters();
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
            if (Mode == PlayMode.Perform) UpdateLane();
        }

        // ------------------------------------------------------------------
        // 鍵盤が おされた・はなされた

        void OnNotePlayed(int midi)
        {
            int today = AppClock.Today();
            if (Stamps.RecordPlay(today))
            {
                SaveSystem.Save();
                audioOut?.PlaySfx("earn", 0.9f);
                _earnBanner.RemoveFromClassList("hidden");
                _earnTimer = 8f;
                WebBridge.Vibrate(40);
            }
            UpdateStampMeter();
            if (SaveSystem.Settings.noteEffects) SpawnNote(midi);
            _labelPressedAt[midi] = Time.unscaledTime;
            FreeNotePlayed(midi);
            if (Mode == PlayMode.Perform && Perform != null && _performSong != null) PerformNotePlayed(midi);
            if (Mode == PlayMode.Mimic && Mimic != null) MimicNotePlayed(midi);
        }

        void OnNoteReleased(int midi)
        {
            _recorder?.Add(midi, false);
            _lastReleased = midi;
            _lastReleaseFrame = Time.frameCount;
        }

        void UpdateStampMeter()
        {
            int today = AppClock.Today();
            int left = Stamps.PlaysLeft(today);
            bool got = Stamps.HasStampFor(today);
            if (got) _meterText.text = Stamps.PendingCount > 0 ? "スタンプが おせるよ！" : "きょうの スタンプ もらったよ";
            else _meterText.text = $"スタンプまで あと {left}かい";
            int filled = got ? StampBook.PlaysForStamp : StampBook.PlaysForStamp - left;
            for (int i = 0; i < _meterDots.childCount; i++) _meterDots[i].EnableInClassList("meter-dot--on", i < filled);
            _meterDots.EnableInClassList("hidden", got);
            // まねっこ・えんそうの 小さな メーター
            RefreshMiniMeter(_mimicMeter, left, got);
            RefreshMiniMeter(_performMeter, left, got);
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
                // Mathf.SmoothStep は「from から to へ」。さいごの 4 わり ちょっとで きえる
                f.e.style.opacity = u < 0.55f ? 1f : Mathf.SmoothStep(1f, 0f, (u - 0.55f) / 0.45f);
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
            SetFreeBubble($"{info.label}の おとだよ♪");
            if (Mode == PlayMode.Mimic && Mimic != null) SetBubble($"{info.label}の おとで ひこう！");
            if (audioOut != null)
            {
                int c = NoteUtil.MiddleC + 12 * SaveSystem.Settings.octaveShift;
                StartCoroutine(Preview(new[] { c, c + 4, c + 7 }));
            }
        }

        System.Collections.IEnumerator Preview(int[] notes) => Preview(notes, 0.16f);

        System.Collections.IEnumerator Preview(int[] notes, float step)
        {
            // WebGL では、えらんだ楽器の音を ブラウザが ほどきおわるまで 鳴らないので、まってから（3 びょうまで）
            float wait = 0f;
            while (!audioOut.InstrumentLoaded && wait < 3f)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }
            foreach (int n in notes)
            {
                audioOut.NoteOn(n, 0.8f);
                yield return new WaitForSecondsRealtime(step);
                audioOut.NoteOff(n);
            }
        }

        // ------------------------------------------------------------------
        // まねっこ

        readonly List<(Button b, SongDef song, VisualElement stars)> _songCards = new List<(Button, SongDef, VisualElement)>();

        void BuildSongList()
        {
            foreach (var song in Songs.All)
            {
                var s = song;
                string color = s.random ? "purple" : s.exercise ? "green" : "white";
                var b = UIParts.Candy(s.title, color, Character(s.animal), () => StartSong(s), _songList, "song-card");
                b.name = "mimic-song-" + s.id;
                TitleSize(b, s.title);
                // まちがいの 少なさの ほし（1〜3）
                var stars = UIParts.El("song-card__stars hidden", b);
                for (int i = 0; i < 3; i++) UIParts.SetIcon(UIParts.El("mini-star", stars), Icon("star"));
                _songCards.Add((b, s, stars));
            }
        }

        /// <summary>ながい なまえの 曲は、カードの字を小さく（2 行に わかれないように）。</summary>
        static void TitleSize(Button card, string title)
        {
            if (title.Length >= 10) card.AddToClassList("song-card--xlong");
            else if (title.Length >= 7) card.AddToClassList("song-card--long");
        }

        void ShowPicker()
        {
            _songPicker.RemoveFromClassList("hidden");
            _mimicGame.AddToClassList("hidden");
            string last = SaveSystem.Data.lastMimicSong;
            Button lastCard = null;
            foreach (var (b, song, stars) in _songCards)
            {
                int n = SaveSystem.Data.MimicStarsFor(song.id);
                stars.EnableInClassList("hidden", n <= 0);
                for (int i = 0; i < stars.childCount; i++) stars[i].style.display = i < n ? DisplayStyle.Flex : DisplayStyle.None;
                bool isLast = song.id == last;
                b.EnableInClassList("song-card--last", isLast);
                if (isLast) lastCard = b;
            }
            ScrollIntoView(_songList, lastCard);
        }

        /// <summary>さいごに えらんだ 曲が 見えるように（ならべおわってから）。</summary>
        static void ScrollIntoView(ScrollView list, VisualElement card)
        {
            if (list == null || card == null) return;
            list.schedule.Execute(() =>
            {
                if (card.layout.height > 1f) list.ScrollTo(card);
            }).ExecuteLater(60);
        }

        public void StartSong(SongDef song)
        {
            var s = SaveSystem.Settings;
            Mimic?.Stop();
            play?.ReleaseAll();
            Mimic = new MimicGame(System.Environment.TickCount);
            Mimic.DemoNoteOn += (m, d) =>
            {
                play.EnsureVisible(m, m);
                play.DemoDown(m, SaveSystem.Settings.demoLights);
                RefreshDots();
            };
            Mimic.DemoNoteOff += m => play.DemoUp(m);
            Mimic.StateChanged += OnMimicState;
            Mimic.HintShown += m =>
            {
                audioOut?.PlaySfx("hint", 0.7f);
                SetBubble(HintText(m));
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
            _songPicker.AddToClassList("hidden");
            _mimicGame.RemoveFromClassList("hidden");
            _mimicNext.AddToClassList("hidden");
            _mimicStars.AddToClassList("hidden");
            _mimicDots.RemoveFromClassList("hidden");
            _mimicTitle.text = song.title;
            UIParts.SetIcon(_teacher, Character(song.animal));
            _replayLabel.text = "もういちど きく";
            _mimicLoadWait = 0f;
            SaveSystem.Data.lastMimicSong = song.id;
            Mimic.Start(song, s.mimicLength, s.MimicTempo, s.hintSeconds, play.Transpose);
            RefreshDots();
        }

        void StopSong()
        {
            Mimic?.Stop();
            Mimic = null;
            _nowDot = null;
            play?.ReleaseAll();
            play?.view.SetHint(-1);
            ShowPicker();
        }

        void StartNextMimicSong()
        {
            if (Mimic == null) return;
            int i = Songs.All.IndexOf(Mimic.Song);
            StartSong(Songs.All[(i + 1) % Songs.All.Count]);
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

        void TapTeacher()
        {
            if (Mimic == null) return;
            _teacherBounce = 0.45f;
            if (Mimic.Current == MimicGame.State.Listen || Mimic.Current == MimicGame.State.Demo)
            {
                Click();
                play.view.SetHint(-1);
                Mimic.ReplayDemo();
            }
        }

        void CycleMimicSpeed()
        {
            var s = SaveSystem.Settings;
            s.mimicSpeed = (s.mimicSpeed + 1) % 3;
            SaveSystem.ApplySettings();
            if (Mimic != null) SetBubble($"はやさ：{SpeedLabels[s.mimicSpeed]}（つぎの おてほんから）");
        }

        /// <summary>ヒントの ことば（おんめいを かく せっていなら、おとの なまえも）。</summary>
        static string HintText(int midi)
        {
            var mode = SaveSystem.Settings.labels;
            bool named = mode == LabelMode.Katakana || mode == LabelMode.Hiragana || mode == LabelMode.Letters;
            return named && midi >= 0 ? $"ひかっている「{NoteUtil.Name(midi, mode)}」を おしてね" : "ひかっている けんばんを おしてね";
        }

        void MimicNotePlayed(int midi)
        {
            // おてほんの とちゅうでも、正しい はじめの音なら すぐに まねする番（MimicGame が きめる）
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
                // まちがえた けんばんは はいいろに ひかる（せめない いろ）
                play.view.SetPressed(midi, true, WrongGlow);
                SetBubble(Mimic.HintActive ? HintText(Mimic.Expected) : Mimic.WrongStreak >= 2 ? "おしい！ もういちど きいてみる？" : "おしい！");
                RefreshDots();
            }
        }

        void OnMimicState(MimicGame.State st)
        {
            switch (st)
            {
                case MimicGame.State.Intro:
                    SetBubble("よく きいてね♪");
                    audioOut?.PlaySfx("listen", 0.6f);
                    RefreshDots();
                    ShowMimicRound();
                    break;
                case MimicGame.State.Demo:
                    SetBubble("♪ ♪ ♪");
                    RefreshDots();
                    break;
                case MimicGame.State.Listen:
                    SetBubble("まねして ひいてね！");
                    RefreshDots();
                    ShowMimicRound();
                    break;
            }
        }

        /// <summary>いまの ひとまとまりの 音が ぜんぶ 見えるように けんばんを うごかす。</summary>
        void ShowMimicRound()
        {
            var r = Mimic?.CurrentRound;
            if (r == null || play == null) return;
            int lo = 999, hi = -1;
            foreach (var n in r) if (!n.IsRest) { lo = Mathf.Min(lo, n.midi + Mimic.Transpose); hi = Mathf.Max(hi, n.midi + Mimic.Transpose); }
            if (hi >= 0) play.EnsureVisible(lo, hi);
        }

        void OnSongFinished()
        {
            int stars = Mimic != null ? Mimic.Stars : 0;
            audioOut?.PlaySfx("clear", 1f);
            SetBubble(stars >= 3 ? "ぜんぶ ひけたね！ はなまる！" : stars == 2 ? "ぜんぶ ひけたね！ じょうず！" : "さいごまで ひけたね！ よく がんばったね");
            _teacherBounce = 1.4f;
            _replayLabel.text = "もういちど";
            _mimicNext.RemoveFromClassList("hidden");
            _mimicDots.AddToClassList("hidden");
            _mimicStars.RemoveFromClassList("hidden");
            for (int i = 0; i < _mimicStars.childCount; i++) _mimicStars[i].EnableInClassList("result-star--off", i >= stars);
            _nowDot = null;
            var save = SaveSystem.Data;
            bool changed = false;
            if (Mimic != null)
            {
                if (!save.clearedSongs.Contains(Mimic.Song.id))
                {
                    save.clearedSongs.Add(Mimic.Song.id);
                    changed = true;
                }
                if (save.RecordMimicStars(Mimic.Song.id, stars)) changed = true;
            }
            if (changed) SaveSystem.Save();
            if (stars >= 3) PlayConfetti();
            WebBridge.Vibrate(40);
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
            var st = Mimic.Current;
            bool all = st == MimicGame.State.Cleared || st == MimicGame.State.Finished;
            VisualElement now = null;
            for (int i = 0; i < _mimicDots.childCount; i++)
            {
                var d = _mimicDots[i];
                d.style.display = i < n ? DisplayStyle.Flex : DisplayStyle.None;
                if (i >= n) continue;
                // ひけた音は その音の いろ、おてほんで 鳴った音は うすく、つぎに ひく音は ぴょこぴょこ
                bool done = all || (st == MimicGame.State.Listen && i < Mimic.Progress);
                bool demo = !done && st == MimicGame.State.Demo && i < Mimic.DemoPlayed;
                bool isNow = st == MimicGame.State.Listen && i == Mimic.Progress;
                d.EnableInClassList("dot--on", done);
                d.EnableInClassList("dot--demo", demo);
                d.EnableInClassList("dot--now", isNow);
                if (done || demo) d.style.backgroundColor = NoteUtil.NoteColor(Mimic.RoundNote(i));
                else d.style.backgroundColor = StyleKeyword.Null;
                if (isNow) now = d;
                else d.style.scale = StyleKeyword.Null;
            }
            _nowDot = now;
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
                // えらんだ 楽器の 音が よみこめるまで、おてほんを まつ（3 びょうまで）
                bool loading = Mimic.Current == MimicGame.State.Intro && audioOut != null && !audioOut.InstrumentLoaded && _mimicLoadWait < 3f;
                if (loading) _mimicLoadWait += dt;
                else Mimic.Update(dt * MimicTimeScale);
                if (Mimic != null) play.view.SetHint(Mimic.HintKey);
            }
            else if (Mode == PlayMode.Free && play != null && play.view.HintKey >= 0) play.view.SetHint(-1);
            UpdatePerform(dt, paused);
            UpdateFree(dt, paused);

            // スタンプが もらえた おしらせは、しばらく したら しまう
            if (_earnTimer > 0f)
            {
                _earnTimer -= dt;
                if (_earnTimer <= 0f) _earnBanner.AddToClassList("hidden");
            }

            // 先生の うごき・つぎに ひく ドット
            if (_bubbleTimer > 0f)
            {
                _bubbleTimer -= dt;
                float s = 1f + 0.08f * Mathf.Sin(Mathf.Clamp01(_bubbleTimer / 0.25f) * Mathf.PI);
                _mimicBubble.style.scale = new Scale(new Vector2(s, s));
            }
            bool still = SaveSystem.Settings.reduceMotion;
            float bob = still ? 0f : Mathf.Sin(Time.unscaledTime * 2.2f) * 6f;
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
            if (_nowDot != null)
            {
                float p = still ? 1.1f : 1f + 0.14f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f));
                _nowDot.style.scale = new Scale(new Vector2(p, p));
            }

            if (Mode == PlayMode.Free && (_recorder == null || (!_recorder.Recording && !_recorder.Playing)))
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
