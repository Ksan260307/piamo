using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Piamo
{
    public partial class AppUI
    {
        Label _menuToday, _stampBadge, _menuStampCount;
        VisualElement _menuMeter, _menuRecent;
        Button _btnStamps;
        readonly List<VisualElement> _friends = new List<VisualElement>();
        readonly List<Vector2> _friendBase = new List<Vector2>();
        readonly List<float> _friendHop = new List<float>();
        float _menuTime, _menuIdle;
        bool _idleShown;
        // どうぶつを タップしたときの 音（ペンタトニック。どれを ならしても きれい）
        static readonly int[] FriendNotes = { 60, 62, 64, 67, 69, 72 };

        // メニューのまわりで ゆれる どうぶつ（どこに・どのくらいの大きさで）
        // よこ画面では 四すみ（ボタンに かさならない）、たて画面では 上と下の すきま
        static readonly (string animal, float x, float y, float size)[] FriendSpots =
        {
            ("cat", 0.04f, 0.82f, 0.15f),
            ("rabbit", 0.90f, 0.80f, 0.15f),
            ("bear", 0.90f, 0.06f, 0.12f),
            ("frog", 0.03f, 0.08f, 0.11f),
            ("panda", 0.74f, 0.88f, 0.10f),
            ("chick", 0.24f, 0.90f, 0.09f),
        };

        void BuildMenu()
        {
            _menuToday = _root.Q<Label>("menu-today");
            _stampBadge = _root.Q<Label>("stamp-badge");
            _menuStampCount = _root.Q<Label>("menu-stamp-count");
            _menuMeter = _root.Q("menu-meter");
            _menuRecent = _root.Q("menu-recent");
            _btnStamps = _root.Q<Button>("btn-stamps");
            for (int i = 0; i < StampBook.PlaysForStamp; i++) UIParts.El("meter-dot", _menuMeter);
            var logo = _root.Q("menu-logo");
            UIParts.SetIcon(logo, uiAssets != null ? uiAssets.logo : null);
            // ロゴの けんばんを タップすると ドレミファソラシド
            logo.RegisterCallback<ClickEvent>(_ =>
            {
                if (audioOut == null) return;
                StartCoroutine(Preview(new[] { 60, 62, 64, 65, 67, 69, 71, 72 }, 0.09f));
            });
            // メニューの ことばを タップすると、そこへ（スタンプが おせる → スタンプちょう、ほかは ピアノ）
            _menuToday.RegisterCallback<ClickEvent>(_ =>
            {
                Click();
                Show(Stamps.PendingCount > 0 ? Page.Stamps : Page.Play);
            });
            _menu.RegisterCallback<PointerDownEvent>(_ => _menuIdle = 0f, TrickleDown.TrickleDown);
            if (uiAssets != null && uiAssets.titleFont != null)
            {
                foreach (var l in _root.Query<Label>(className: "candy__label").ToList())
                {
                    if (l.parent != null && l.parent.ClassListContains("menu-button"))
                        l.style.unityFontDefinition = FontDefinition.FromFont(uiAssets.titleFont);
                }
            }
            SetIcon("btn-play-icon", "piano");
            SetIcon("btn-stamps-icon", "stamp");
            SetIcon("btn-settings-icon", "gear");
            OnClick(_root.Q<Button>("btn-play"), () => Show(Page.Play));
            OnClick(_root.Q<Button>("btn-stamps"), () => Show(Page.Stamps));
            _root.Q<Button>("btn-settings").clicked += OpenSettings;

            var friends = _root.Q("menu-friends");
            foreach (var f in FriendSpots)
            {
                var e = UIParts.El("menu-friend", friends, true);
                UIParts.SetIcon(e, Character(f.animal));
                int k = _friends.Count;
                // タップすると ぴょんと はねて 音が なる
                e.RegisterCallback<ClickEvent>(_ => HopFriend(k));
                _friends.Add(e);
                _friendBase.Add(new Vector2(f.x, f.y));
                _friendHop.Add(0f);
            }
        }

        void HopFriend(int k)
        {
            _friendHop[k] = 0.45f;
            if (audioOut == null) return;
            int note = FriendNotes[(k * 7 + (int)(_menuTime * 3f)) % FriendNotes.Length];
            StartCoroutine(Preview(new[] { note }, 0.25f));
        }

        /// <summary>じかんの あいさつ（その土地の 時で）。</summary>
        static string Greeting()
        {
            int h = WebBridge.LocalHour();
            return h >= 5 && h < 11 ? "おはよう！" : h >= 11 && h < 17 ? "こんにちは！" : "こんばんは！";
        }

        void OnLayoutMenu(Vector2 panel)
        {
            bool portrait = panel.y > panel.x;
            for (int i = 0; i < _friends.Count; i++)
            {
                var f = FriendSpots[i];
                float size = Mathf.Min(panel.x, panel.y) * f.size * (portrait ? 1.25f : 1.5f);
                var e = _friends[i];
                e.style.width = size;
                e.style.height = size;
                float x = f.x, y = f.y;
                if (portrait)
                {
                    // たて長では、上と下のすきまに ならべる
                    y = f.y < 0.5f ? 0.04f + (i % 2) * 0.03f : 0.86f - (i % 2) * 0.03f;
                }
                e.style.left = x * panel.x - (x > 0.5f ? size * 0.4f : 0f);
                e.style.top = y * panel.y - (y > 0.5f ? size * 0.5f : 0f);
            }
        }

        public void RefreshMenu()
        {
            int today = AppClock.Today();
            int pending = Stamps.PendingCount;
            _stampBadge.EnableInClassList("hidden", pending == 0);
            _stampBadge.text = pending > 1 ? pending.ToString() : "!";
            string hello = Greeting() + " ";
            if (pending > 0) _menuToday.text = hello + "スタンプが おせるよ！ スタンプちょうを ひらいてね";
            else if (Stamps.HasStampFor(today)) _menuToday.text = hello + "きょうの スタンプは もう おしたよ。また あしたね♪";
            else _menuToday.text = hello + $"ピアノを {Stamps.PlaysLeft(today)}かい ひくと、きょうの スタンプが もらえるよ";
            _idleShown = false;
            _menuIdle = 0f;
            // きょうの すすみ（ドット）
            bool got = Stamps.HasStampFor(today);
            int filled = got ? StampBook.PlaysForStamp : StampBook.PlaysForStamp - Stamps.PlaysLeft(today);
            _menuMeter.EnableInClassList("hidden", got || filled <= 0);
            for (int i = 0; i < _menuMeter.childCount; i++) _menuMeter[i].EnableInClassList("meter-dot--on", i < filled);
            // あつめた 数・さいきん おした スタンプ・つぎが でっかい スタンプなら 金色
            int total = Stamps.Total;
            _menuStampCount.EnableInClassList("hidden", total == 0);
            _menuStampCount.text = $"{total}こ";
            _menuRecent.EnableInClassList("hidden", total == 0);
            if (total > 0 && stampImages != null)
            {
                var img = _menuRecent;
                stampImages.Request(StampBook.DesignFor(total), tex => { if (tex != null) img.style.backgroundImage = new StyleBackground(tex); });
            }
            _btnStamps.EnableInClassList("menu-button--gold", pending > 0 && StampBook.IsBig(total + 1));
        }

        void UpdateMenu(float dt)
        {
            if (Current != Page.Menu) return;
            _menuTime += dt;
            bool still = SaveSystem.Settings.reduceMotion;
            for (int i = 0; i < _friends.Count; i++)
            {
                float t = _menuTime * 1.6f + i * 1.3f;
                float hop = 0f;
                if (_friendHop[i] > 0f)
                {
                    _friendHop[i] -= dt;
                    hop = -Mathf.Sin(Mathf.Clamp01(_friendHop[i] / 0.45f) * Mathf.PI) * 40f;
                }
                _friends[i].style.translate = new Translate(0, (still ? 0f : Mathf.Sin(t) * 10f) + hop);
                _friends[i].style.rotate = new Rotate(still ? 0f : Mathf.Sin(t * 0.7f) * 5f);
            }
            // しばらく さわらないと、どうぶつが さそう
            if (!SettingsOpen && !DialogOpen)
            {
                _menuIdle += dt;
                if (_menuIdle > 15f && !_idleShown)
                {
                    _idleShown = true;
                    _menuToday.text = Stamps.PendingCount > 0 ? "スタンプちょうで スタンプを おそう！" : "ピアノで いっしょに あそぼう！ ♪";
                    HopFriend(0);
                    HopFriend(1);
                }
            }
            if (_btnStamps.ClassListContains("menu-button--gold"))
            {
                float g = 1f + 0.04f * Mathf.Sin(_menuTime * 4f);
                _btnStamps.style.scale = new Scale(new Vector2(g, g));
            }
            var badge = _stampBadge;
            if (!badge.ClassListContains("hidden"))
            {
                float s = 1f + 0.12f * Mathf.Abs(Mathf.Sin(_menuTime * 3f));
                badge.style.scale = new Scale(new Vector2(s, s));
            }
        }
    }
}
