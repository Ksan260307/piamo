using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Piamo
{
    /// <summary>
    /// どの画面にも かかわる こまかい きのう。
    ///  - ピアノの画面の 紙ふぶき（ほし 3 つ のとき）・スタンプまでの 小さな メーター
    ///  - 画面を くらく しない（ひいている あいだ）・あそぶ じかんの めやす
    ///  - パソコン：スペースキー（▶❚❚ ／ もういちど きく）・マウスホイールで けんばんを スクロール
    ///  - スマホの「もどる」・ほかの まどに うつったら 鍵盤を はなす
    /// </summary>
    public partial class AppUI
    {
        /// <summary>まちがえた けんばんの 光る いろ（はいいろ）。</summary>
        static readonly Color WrongGlow = new Color(0.62f, 0.62f, 0.66f);

        float _playTime;
        bool _pageHidden;
        float _wheel;

        /// <summary>あそんだ 時間（びょう。ピアノの 画面に いる あいだ）。テストでは すすめられる。</summary>
        public float PlayTime
        {
            get => _playTime;
            set => _playTime = Mathf.Max(0f, value);
        }

        // ------------------------------------------------------------------
        // 紙ふぶき（スタンプちょうと おなじ しくみ。ピアノの画面では おんぷの層に）

        void PlayConfetti()
        {
            if (SaveSystem.Settings.reduceMotion || _fxLayer == null) return;
            float w = _fxLayer.layout.width, h = _fxLayer.layout.height;
            if (w < 2f) return;
            Burst(_fxLayer, new Vector2(w * 0.3f, h * 0.15f), 26, 1.2f);
            Burst(_fxLayer, new Vector2(w * 0.7f, h * 0.15f), 26, 1.2f);
        }

        // ------------------------------------------------------------------
        // スタンプまでの 小さな メーター（まねっこ・えんそう）

        void BuildMiniMeter(VisualElement box)
        {
            if (box == null) return;
            UIParts.SetIcon(UIParts.El("icon mini-meter__icon", box), Icon("stamp"));
            UIParts.Text("", "mini-meter__text", box);
            var dots = UIParts.El("mini-meter__dots", box);
            for (int i = 0; i < StampBook.PlaysForStamp; i++) UIParts.El("mini-dot", dots);
        }

        void RefreshMiniMeter(VisualElement box, int left, bool got)
        {
            if (box == null || box.childCount < 3) return;
            box.EnableInClassList("hidden", got);
            if (got) return;
            ((Label)box[1]).text = $"スタンプまで あと {left}";
            var dots = box[2];
            int filled = StampBook.PlaysForStamp - left;
            for (int i = 0; i < dots.childCount; i++) dots[i].EnableInClassList("mini-dot--on", i < filled);
        }

        // ------------------------------------------------------------------
        // 画面を くらく しない・あそぶ じかん

        void UpdateKeepAwake()
        {
            WebBridge.KeepAwake(Current == Page.Play && SaveSystem.Settings.keepAwake && !_pageHidden);
        }

        void UpdatePlayTime(float dt)
        {
            int limit = SaveSystem.Settings.playLimit;
            if (Current != Page.Play || SettingsOpen || DialogOpen) return;
            _playTime += dt;
            if (limit <= 0 || _playTime < limit * 60f) return;
            _playTime = 0f;
            if (_performSong != null && Perform != null && Perform.Current != PerformGame.State.Finished)
            {
                _performUserPaused = true;
                RefreshPerformControls();
            }
            Confirm($"{MinutesText(limit)} あそんだよ。\nすこし きゅうけい しようね", "メニューへ", () => Show(Page.Menu), "もうすこし");
        }

        // ------------------------------------------------------------------
        // パソコンの キー・マウスホイール

        void UpdateDesktopInput(float dt)
        {
            if (Current != Page.Play || SettingsOpen || DialogOpen) return;
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
            {
                if (Mode == PlayMode.Perform && _performSong != null) TogglePerformPause();
                else if (Mode == PlayMode.Mimic && Mimic != null) OnReplayPressed();
            }
            // マウスホイール：けんばんが スクロールするときだけ、白鍵 1 本ずつ
            var mouse = Mouse.current;
            if (mouse == null || play == null || !play.Framing.scrolling) return;
            float sy = mouse.scroll.ReadValue().y + mouse.scroll.ReadValue().x;
            if (Mathf.Abs(sy) < 0.01f) return;
            var pos = mouse.position.ReadValue();
            if (!play.Area.Contains(pos) && !ScreenRectOf(_nav).Contains(pos)) return;
            _wheel += sy > 0f ? -1f : 1f;
            play.Scroll(_wheel);
            _wheel = 0f;
        }

        // ------------------------------------------------------------------
        // ブラウザ（PiamoPage.jslib から）

        /// <summary>スマホ・ブラウザの「もどる」：ページから 出ないで、アプリの「もどる」。</summary>
        public void OnBrowserBack() => Back();

        void OnApplicationFocus(bool focus)
        {
            // ほかの まどに うつったら、おしっぱなしの けんばん（キーボード）を はなす
            if (focus) return;
            input?.ReleaseAll();
            play?.ReleaseAll();
        }
    }
}
