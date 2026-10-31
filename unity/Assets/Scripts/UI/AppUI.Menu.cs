using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Piamo
{
    public partial class AppUI
    {
        Label _menuToday, _stampBadge;
        readonly List<VisualElement> _friends = new List<VisualElement>();
        readonly List<Vector2> _friendBase = new List<Vector2>();
        float _menuTime;

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
            UIParts.SetIcon(_root.Q("menu-logo"), uiAssets != null ? uiAssets.logo : null);
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
                var e = UIParts.El("menu-friend", friends);
                UIParts.SetIcon(e, Character(f.animal));
                _friends.Add(e);
                _friendBase.Add(new Vector2(f.x, f.y));
            }
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
            if (pending > 0) _menuToday.text = "スタンプが おせるよ！ スタンプちょうを ひらいてね";
            else if (Stamps.HasStampFor(today)) _menuToday.text = "きょうの スタンプは もう おしたよ。また あしたね♪";
            else _menuToday.text = $"ピアノを {Stamps.PlaysLeft(today)}かい ひくと、きょうの スタンプが もらえるよ";
        }

        void UpdateMenu(float dt)
        {
            if (Current != Page.Menu) return;
            _menuTime += dt;
            for (int i = 0; i < _friends.Count; i++)
            {
                float t = _menuTime * 1.6f + i * 1.3f;
                _friends[i].style.translate = new Translate(0, Mathf.Sin(t) * 10f);
                _friends[i].style.rotate = new Rotate(Mathf.Sin(t * 0.7f) * 5f);
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
