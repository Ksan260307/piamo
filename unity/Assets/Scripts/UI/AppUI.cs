using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Piamo
{
    /// <summary>
    /// 画面のまとめ役（UI Toolkit）。メニュー → ピアノで あそぶ ／ スタンプちょう ／ せってい。
    ///  - どの画面にも「もどる」があり、メニューへもどる（せっていは、ひらいた画面へもどる）
    ///  - せっていは、ピアノで あそんでいる とちゅうでも ひらける
    ///  - スマホのたて・よこ、iPad の大きさに合わせて、portrait / landscape / short のクラスをつける
    /// 画面ごとの中身は AppUI.Menu.cs / AppUI.Play.cs / AppUI.Stamps.cs / AppUI.Settings.cs。
    /// </summary>
    public partial class AppUI : MonoBehaviour
    {
        public UIDocument document;
        public UiAssets uiAssets;
        public PlayController play;
        public PianoInput input;
        public PianoAudio audioOut;
        public StampImages stampImages;

        public enum Page { Menu, Play, Stamps }

        public Page Current { get; private set; } = Page.Menu;
        public bool SettingsOpen { get; private set; }
        public bool DialogOpen => _dialog != null && !_dialog.ClassListContains("hidden");
        public bool Ready { get; private set; }
        public StampBook Stamps { get; private set; }
        public VisualElement Root => _root;

        VisualElement _root, _menu, _playPage, _stampsPage, _settings, _dialog, _toast, _instPopup;
        Label _toastText;
        Action _dialogYes;
        float _toastTimer;
        float _safeTimer;
        Vector4 _safe = new Vector4(-1, -1, -1, -1);
        Vector2 _lastPanelSize;

        void Start()
        {
            Application.targetFrameRate = 60;
            Stamps = new StampBook(SaveSystem.Data);
            var doc = document != null ? document : GetComponent<UIDocument>();
            document = doc;
            _root = doc.rootVisualElement.Q("root") ?? doc.rootVisualElement;
            if (uiAssets != null && uiAssets.bodyFont != null)
                doc.rootVisualElement.style.unityFontDefinition = FontDefinition.FromFont(uiAssets.bodyFont);

            _menu = _root.Q("menu");
            _playPage = _root.Q("play");
            _stampsPage = _root.Q("stamps");
            _settings = _root.Q("settings");
            _dialog = _root.Q("dialog");
            _toast = _root.Q("toast");
            _toastText = _root.Q<Label>("toast-text");
            _instPopup = _root.Q("inst-popup");

            _root.Q<Button>("dialog-yes").clicked += () =>
            {
                var a = _dialogYes;
                CloseDialog();
                a?.Invoke();
            };
            _root.Q<Button>("dialog-no").clicked += () =>
            {
                Click();
                CloseDialog();
            };
            // 窓の外をタップしたら とじる
            _dialog.RegisterCallback<PointerDownEvent>(e => { if (e.target == _dialog) CloseDialog(); });

            BuildMenu();
            BuildPlay();
            BuildStamps();
            BuildSettings();

            if (input != null) input.IsOverUI = IsOverUI;
            SaveSystem.SettingsChanged += OnSettingsChanged;
            if (play != null)
            {
                play.NotePlayed += OnNotePlayed;
                play.NoteReleased += OnNoteReleased;
                play.FramingChanged += OnFramingChanged;
                play.ApplySettings(SaveSystem.Settings);
            }
            _root.RegisterCallback<GeometryChangedEvent>(_ => OnLayout());
            WebBridge.RegisterPageEvents();
            Show(Page.Menu);
            Ready = true;
            // 音の高さの けんさ：URL に ?pitchtest をつけると、すべての楽器の すべての音を じゅんに鳴らす
            //（ブラウザで 鳴った音を はかるため。TESTING.md の「WebGL の音の高さ」を参照）
            if (Application.absoluteURL.Contains("pitchtest")) StartCoroutine(PitchSweep());
        }

        System.Collections.IEnumerator PitchSweep()
        {
            yield return new WaitForSecondsRealtime(1f);
            foreach (var info in InstrumentCatalog.All)
            {
                audioOut.SetInstrument(info.id);
                float wait = 0f;
                while (!audioOut.InstrumentLoaded && wait < 20f)
                {
                    wait += Time.unscaledDeltaTime;
                    yield return null;
                }
                Debug.Log($"[PitchTest] {info.id} {KeyboardLayout.LowestNote}-{KeyboardLayout.HighestNote}");
                for (int n = KeyboardLayout.LowestNote; n <= KeyboardLayout.HighestNote; n++)
                {
                    audioOut.NoteOn(n, 0.3f);
                    yield return new WaitForSecondsRealtime(0.06f);
                    audioOut.NoteOff(n);
                    yield return new WaitForSecondsRealtime(0.02f);
                }
                audioOut.AllOff(true);
                yield return new WaitForSecondsRealtime(0.3f);
            }
            audioOut.SetInstrument(SaveSystem.Settings.instrument);
            Debug.Log("[PitchTest] done");
        }

        void OnDestroy()
        {
            SaveSystem.SettingsChanged -= OnSettingsChanged;
        }

        // ------------------------------------------------------------------
        // 画面のきりかえ

        public void Show(Page p)
        {
            if (Current == Page.Play && p != Page.Play) LeavePlay();
            Current = p;
            _menu.EnableInClassList("hidden", p != Page.Menu);
            _playPage.EnableInClassList("hidden", p != Page.Play);
            _stampsPage.EnableInClassList("hidden", p != Page.Stamps);
            if (play != null)
            {
                play.view.gameObject.SetActive(p == Page.Play);
                // ピアノの画面のほかは UI が画面ぜんぶをおおうので、3D は描かない（スマホの電池のため）
                if (play.cam != null) play.cam.enabled = p == Page.Play;
            }
            ApplyBackground();
            switch (p)
            {
                case Page.Menu: RefreshMenu(); break;
                case Page.Play: EnterPlay(); break;
                case Page.Stamps: EnterStamps(); break;
            }
            UpdateInputActive();
            UpdateKeepAwake();
        }

        public void OpenSettings()
        {
            if (SettingsOpen) return;
            SettingsOpen = true;
            RefreshSettings();
            _settings.RemoveFromClassList("hidden");
            ScrollSettingsForContext();
            audioOut?.PlaySfx("open", 0.8f, true);
            UpdateInputActive();
        }

        public void CloseSettings()
        {
            if (!SettingsOpen) return;
            SettingsOpen = false;
            _settings.AddToClassList("hidden");
            audioOut?.PlaySfx("back", 0.8f, true);
            if (Current == Page.Menu) RefreshMenu();
            UpdateInputActive();
        }

        /// <summary>「もどる」。ひらいている窓をとじる → メニューへ。</summary>
        public void Back()
        {
            if (DialogOpen) { CloseDialog(); return; }
            if (!_instPopup.ClassListContains("hidden")) { CloseInstrumentPopup(); return; }
            if (SettingsOpen) { CloseSettings(); return; }
            if (Current == Page.Stamps && CloseStampOverlays()) return;
            if (Current != Page.Menu)
            {
                audioOut?.PlaySfx("back", 0.8f, true);
                Show(Page.Menu);
            }
        }

        void UpdateInputActive()
        {
            if (play == null) return;
            play.InputActive = Current == Page.Play && !SettingsOpen && !DialogOpen && _instPopup.ClassListContains("hidden");
        }

        // ------------------------------------------------------------------
        // ダイアログ・おしらせ

        public void Confirm(string message, string yes, Action onYes, string no = "やめる")
        {
            _root.Q<Label>("dialog-text").text = message;
            _root.Q<Label>("dialog-yes-label").text = yes;
            _root.Q<Label>("dialog-no-label").text = no;
            _dialogYes = onYes;
            _dialog.RemoveFromClassList("hidden");
            UpdateInputActive();
        }

        void CloseDialog()
        {
            _dialogYes = null;
            _dialog.AddToClassList("hidden");
            UpdateInputActive();
        }

        public void Toast(string text, float seconds = 2.2f)
        {
            _toastText.text = text;
            _toast.RemoveFromClassList("hidden");
            _toastTimer = seconds;
        }

        public string ToastText => _toast.ClassListContains("hidden") ? null : _toastText.text;

        void Click() => audioOut?.PlaySfx("tap", 0.7f, true);

        /// <summary>ボタンに「おしたら ぽん」を つける。</summary>
        void OnClick(Button b, Action a)
        {
            if (b == null) return;
            b.clicked += () =>
            {
                Click();
                a();
            };
        }

        Texture2D Icon(string name) => uiAssets != null ? uiAssets.Icon(name) : null;
        Texture2D Character(string name) => uiAssets != null ? uiAssets.Character(name) : null;

        void SetIcon(string elementName, string icon) => UIParts.SetIcon(_root.Q(elementName), Icon(icon));

        // ------------------------------------------------------------------
        // せっていが変わった

        /// <summary>メニューとスタンプちょうの はいけい（せっていの「はいけい」の いろ）。</summary>
        void ApplyBackground()
        {
            var sky = Icon("sky_" + SaveSystem.Settings.theme) ?? Icon("sky_0");
            foreach (var e in new[] { _menu, _stampsPage })
            {
                if (e == null) continue;
                e.style.backgroundImage = sky != null ? new StyleBackground(sky) : new StyleBackground(StyleKeyword.None);
                e.style.backgroundColor = Themes.Bottom[SaveSystem.Settings.theme];
            }
        }

        void OnSettingsChanged()
        {
            ApplyBackground();
            if (play != null) play.ApplySettings(EffectiveSettings());
            RefreshPlayAfterSettings();
            RefreshPerformAfterSettings();
            // まねっこの とちゅうで オクターブを かえたら、その まとまりを あたらしい たかさで
            if (Mimic != null && play != null) Mimic.SetTranspose(play.Transpose);
            UpdateKeepAwake();
            RefreshSettings();
            if (Current == Page.Menu) RefreshMenu();
        }

        // ------------------------------------------------------------------
        // 画面の形

        /// <summary>画面の大きさ（ピクセル）。UI を絵に描き出すとき（PanelSettings.targetTexture）は、その絵の大きさ。</summary>
        public Vector2 ScreenSize
        {
            get
            {
                var rt = document != null && document.panelSettings != null ? document.panelSettings.targetTexture : null;
                return rt != null ? new Vector2(rt.width, rt.height) : new Vector2(Screen.width, Screen.height);
            }
        }

        /// <summary>UI の 1 単位が、画面の何ピクセルか。</summary>
        public float PanelScale
        {
            get
            {
                var vt = _root?.panel?.visualTree;
                if (vt == null || vt.layout.width < 1f) return 1f;
                return ScreenSize.x / vt.layout.width;
            }
        }

        void OnLayout()
        {
            var vt = _root.panel?.visualTree;
            if (vt == null) return;
            var size = new Vector2(vt.layout.width, vt.layout.height);
            if (size == _lastPanelSize) return;
            _lastPanelSize = size;
            bool portrait = size.y > size.x * 1.05f;
            _root.EnableInClassList("portrait", portrait);
            _root.EnableInClassList("landscape", !portrait);
            // よこ長のスマホなど、高さの足りない画面
            _root.EnableInClassList("short", !portrait && size.y < 1150f);
            _root.EnableInClassList("narrow", portrait && size.x < 1150f);
            OnLayoutMenu(size);
            OnLayoutPlay();
            OnLayoutStamps();
        }

        void ApplySafeArea()
        {
            var s = WebBridge.SafeInsets();
            if (s == _safe) return;
            _safe = s;
            var vt = _root.panel?.visualTree;
            if (vt == null) return;
            float w = vt.layout.width, h = vt.layout.height;
            _root.style.paddingLeft = s.x * w;
            _root.style.paddingTop = s.y * h;
            _root.style.paddingRight = s.z * w;
            _root.style.paddingBottom = s.w * h;
        }

        /// <summary>画面の点（ピクセル。左下が原点）が、ボタンなどの UI の上か。</summary>
        public bool IsOverUI(Vector2 screen)
        {
            var panel = _root?.panel;
            if (panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, ScreenSize.y - screen.y));
            var picked = panel.Pick(p);
            return picked != null;
        }

        /// <summary>UI の要素の、画面での四角（ピクセル。左下が原点）。</summary>
        public Rect ScreenRectOf(VisualElement e)
        {
            var wb = e.worldBound;
            float k = PanelScale;
            return new Rect(wb.xMin * k, ScreenSize.y - wb.yMax * k, wb.width * k, wb.height * k);
        }

        /// <summary>画面の点（ピクセル。左下が原点）を、element の中の位置へ。</summary>
        public Vector2 ScreenToLocal(VisualElement e, Vector2 screen)
        {
            float k = PanelScale;
            var panelPos = new Vector2(screen.x / k, (ScreenSize.y - screen.y) / k);
            return e.WorldToLocal(panelPos);
        }

        // ------------------------------------------------------------------

        void Update()
        {
            if (!Ready) return;
            // タブを もどしたとき などに 時間が とんで、おてほんの音が いっせいに 鳴らないように
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Back();

            var vt = _root.panel?.visualTree;
            if (vt != null && new Vector2(vt.layout.width, vt.layout.height) != _lastPanelSize) OnLayout();
            _safeTimer -= dt;
            if (_safeTimer <= 0f)
            {
                _safeTimer = 0.5f;
                ApplySafeArea();
            }
            if (_toastTimer > 0f)
            {
                _toastTimer -= dt;
                if (_toastTimer <= 0f) _toast.AddToClassList("hidden");
            }
            UpdateMenu(dt);
            UpdatePlay(dt);
            UpdateStamps(dt);
            UpdateConfetti(dt);
            UpdateDesktopInput(dt);
            UpdatePlayTime(dt);
        }

        void LateUpdate()
        {
            if (!Ready) return;
            LateUpdatePlay();
        }

        /// <summary>ブラウザのタブを とじる・かくすとき（PiamoPage.jslib から）。</summary>
        public void OnPageHidden()
        {
            _pageHidden = true;
            SaveSystem.Save();
            // まねっこの おてほんの とちゅうなら、もどったら はじめから
            Mimic?.Rewind();
            if (_recorder != null && (_recorder.Recording || _recorder.Playing))
            {
                _recorder.StopRecording();
                _recorder.StopPlayback();
            }
            // えんそうの とちゅうなら 止めておく（もどってきたら ▶ で つづき）
            if (_performSong != null && Perform != null && (Perform.Current == PerformGame.State.CountIn || Perform.Current == PerformGame.State.Playing))
            {
                _performUserPaused = true;
                Perform.SetPaused(true);
                RefreshPerformControls();
            }
            play?.ReleaseAll();
            audioOut?.AllOff(false);
            UpdateKeepAwake();
        }

        public void OnPageVisible()
        {
            _pageHidden = false;
            UpdateKeepAwake();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) OnPageHidden();
        }
    }
}
