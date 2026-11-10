using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Piamo
{
    public partial class AppUI
    {
        /// <summary>1 ページの スタンプの数（5 こ × 5 だん。どのだんも 5 こめが でっかいスタンプ）。</summary>
        public const int PerPage = 25;
        public const int PagesPerBook = (StampBook.Designs + PerPage - 1) / PerPage;   // 15

        VisualElement _pages, _detail, _celebrate, _stampFx;
        Label _stampCount, _pageLabel, _stampMessage, _celebrateText;
        Button _pagePrev, _pageNext;
        /// <summary>いま ひらいている ページ（0 から。2 さつめ以降もつづけて数える）。</summary>
        public int PageIndex { get; private set; }
        int _pagesShown = 1;
        float _stampTime, _celebrateTimer;
        // ページを ゆびで めくる
        int _swipePointer = -1;
        Vector2 _swipeStart;
        float _swipeTime = -10f;
        /// <summary>くわしい画面で 見ている スタンプ（なんこめ）。</summary>
        int _detailCount;
        float _messageHold;
        readonly Dictionary<int, Slot> _slots = new Dictionary<int, Slot>();
        readonly List<Confetto> _confetti = new List<Confetto>();
        Press _press;

        class Slot
        {
            public int count;       // なんこめ（1 から。2 さつめは 366 から）
            public VisualElement cell, circle, image;
            public Label number, date, tap;
            public bool big;
        }

        class Press
        {
            public Slot slot;
            public float t;
            public bool landed;
        }

        class Confetto
        {
            public VisualElement e;
            public Vector2 pos, vel;
            public float t, life, spin;
        }

        static readonly Color[] ConfettiColors =
        {
            new Color(1f, 0.55f, 0.68f), new Color(1f, 0.82f, 0.3f), new Color(0.48f, 0.8f, 0.98f),
            new Color(0.55f, 0.86f, 0.6f), new Color(0.75f, 0.62f, 0.96f), new Color(1f, 0.65f, 0.4f),
        };

        void BuildStamps()
        {
            _pages = _root.Q("pages");
            _detail = _root.Q("detail");
            _celebrate = _root.Q("celebrate");
            _celebrateText = _root.Q<Label>("celebrate-text");
            _stampFx = _root.Q("stamp-fx");
            _stampCount = _root.Q<Label>("stamp-count");
            _pageLabel = _root.Q<Label>("page-label");
            _stampMessage = _root.Q<Label>("stamp-message");
            _pagePrev = _root.Q<Button>("page-prev");
            _pageNext = _root.Q<Button>("page-next");
            SetIcon("stamps-back-icon", "back");
            SetIcon("page-prev-icon", "left");
            SetIcon("page-next-icon", "right");
            _root.Q<Button>("stamps-back").clicked += Back;
            _pagePrev.clicked += () => TurnPage(-1);
            _pageNext.clicked += () => TurnPage(1);
            OnClick(_root.Q<Button>("detail-close"), () => _detail.AddToClassList("hidden"));
            SetIcon("detail-prev-icon", "left");
            SetIcon("detail-next-icon", "right");
            OnClick(_root.Q<Button>("detail-prev"), () => ShowDetailAt(_detailCount - 1));
            OnClick(_root.Q<Button>("detail-next"), () => ShowDetailAt(_detailCount + 1));
            // ゆびで よこに すーっと うごかすと ページを めくる
            _pages.RegisterCallback<PointerDownEvent>(e =>
            {
                _swipePointer = e.pointerId;
                _swipeStart = e.position;
            }, TrickleDown.TrickleDown);
            _pages.RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.pointerId != _swipePointer) return;
                _swipePointer = -1;
                Vector2 d = (Vector2)e.position - _swipeStart;
                if (Mathf.Abs(d.x) < 90f || Mathf.Abs(d.x) < Mathf.Abs(d.y) * 1.4f) return;
                _swipeTime = Time.unscaledTime;
                TurnPage(d.x < 0f ? 1 : -1);
            }, TrickleDown.TrickleDown);
            _detail.RegisterCallback<PointerDownEvent>(e => { if (e.target == _detail) _detail.AddToClassList("hidden"); });
            _pages.RegisterCallback<GeometryChangedEvent>(_ => LayoutPages());
            if (uiAssets != null && uiAssets.titleFont != null) _celebrateText.style.unityFontDefinition = FontDefinition.FromFont(uiAssets.titleFont);
        }

        /// <summary>いちばん新しい あいている ところ（つぎに おす ところ）のスタンプの番号。</summary>
        int NextCount => Stamps.Total + 1;

        static int PageOf(int count) => (count - 1) / StampBook.Designs * PagesPerBook + ((count - 1) % StampBook.Designs) / PerPage;

        void EnterStamps()
        {
            _detail.AddToClassList("hidden");
            _celebrate.AddToClassList("hidden");
            _press = null;
            PageIndex = SpreadStartOf(PageOf(NextCount));
            RebuildPages();
            // つぎの スタンプの絵を先に読んでおく
            if (Stamps.PendingCount > 0) stampImages?.Request(StampBook.DesignFor(NextCount), null);
        }

        bool CloseStampOverlays()
        {
            if (!_detail.ClassListContains("hidden"))
            {
                _detail.AddToClassList("hidden");
                return true;
            }
            return false;
        }

        void OnLayoutStamps()
        {
            int want = _root.ClassListContains("portrait") ? 1 : 2;
            if (want != _pagesShown)
            {
                _pagesShown = want;
                PageIndex = SpreadStartOf(PageIndex);
                if (Current == Page.Stamps) RebuildPages();
            }
        }

        // ページは さつごとに 15 まい。よこ画面では 2 まいずつ（見開き）。見開きは さつを またがない

        int SpreadStartOf(int page)
        {
            int book = page / PagesPerBook, local = page % PagesPerBook;
            return book * PagesPerBook + local - local % _pagesShown;
        }

        int NextSpread(int page)
        {
            int book = page / PagesPerBook, local = page % PagesPerBook + _pagesShown;
            if (local >= PagesPerBook) { book++; local = 0; }
            return book * PagesPerBook + local;
        }

        int PrevSpread(int page)
        {
            int book = page / PagesPerBook, local = page % PagesPerBook - _pagesShown;
            if (local < 0)
            {
                book--;
                local = (PagesPerBook - 1) - (PagesPerBook - 1) % _pagesShown;
            }
            return book < 0 ? -1 : book * PagesPerBook + local;
        }

        /// <summary>見られるのは、つぎに おす ところの ある 見開きまで。</summary>
        int LastSpread => SpreadStartOf(PageOf(NextCount));

        public void TurnPage(int dir)
        {
            int next = dir > 0 ? NextSpread(PageIndex) : PrevSpread(PageIndex);
            if (next < 0 || next > LastSpread) return;
            PageIndex = next;
            audioOut?.PlaySfx("page", 0.8f, true);
            RebuildPages();
        }

        void RebuildPages()
        {
            _pages.Clear();
            _slots.Clear();
            int shown = 0;
            for (int p = 0; p < _pagesShown; p++)
            {
                int page = PageIndex + p;
                if (p > 0 && page / PagesPerBook != PageIndex / PagesPerBook) break;
                shown++;
                var pe = UIParts.El("page", _pages);
                int book = page / PagesPerBook;
                int local = page % PagesPerBook;
                int first = local * PerPage + 1;
                int lastInBook = Mathf.Min(StampBook.Designs, first + PerPage - 1);
                for (int row = 0; row < 5; row++)
                {
                    var re = UIParts.El("page-row", pe);
                    for (int c = 0; c < 5; c++)
                    {
                        int n = first + row * 5 + c;
                        if (n > lastInBook) break;
                        int count = book * StampBook.Designs + n;
                        _slots[count] = MakeSlot(re, count, n);
                    }
                }
            }
            int bookNo = PageIndex / PagesPerBook + 1;
            int localPage = PageIndex % PagesPerBook + 1;
            string bookText = bookNo > 1 ? $"{bookNo}さつめ  " : "";
            _pageLabel.text = shown == 2
                ? $"{bookText}{localPage}〜{localPage + 1} ページ / {PagesPerBook}"
                : $"{bookText}{localPage} ページ / {PagesPerBook}";
            _pagePrev.SetEnabled(PageIndex > 0);
            _pagePrev.style.opacity = PageIndex > 0 ? 1f : 0.3f;
            bool canNext = NextSpread(PageIndex) <= LastSpread;
            _pageNext.SetEnabled(canNext);
            _pageNext.style.opacity = canNext ? 1f : 0.3f;
            RefreshStampTexts();
            LayoutPages();
        }

        Slot MakeSlot(VisualElement row, int count, int numberInBook)
        {
            var s = new Slot { count = count, big = StampBook.IsBig(count) };
            s.cell = UIParts.El("cell", row, true);
            s.circle = UIParts.El("slot", s.cell);
            if (s.big) s.circle.AddToClassList("slot--big");
            s.number = UIParts.Text(numberInBook.ToString(), "slot__number", s.circle);
            s.image = UIParts.El("slot__image hidden", s.circle);
            s.date = UIParts.Text("", "slot__date", s.cell);
            s.tap = UIParts.Text("ここを タップ！", "slot__tap hidden", s.cell);
            s.cell.RegisterCallback<ClickEvent>(_ => OnSlotClicked(s));
            FillSlot(s);
            return s;
        }

        void FillSlot(Slot s)
        {
            int day = Stamps.DayOf(s.count);
            bool filled = day > 0;
            s.circle.EnableInClassList("slot--filled", filled);
            s.number.EnableInClassList("hidden", filled);
            s.image.EnableInClassList("hidden", !filled);
            s.date.text = filled ? AppClock.Short(day) : "";
            bool next = !filled && s.count == NextCount && Stamps.PendingCount > 0;
            s.circle.EnableInClassList("slot--next", next);
            s.tap.EnableInClassList("hidden", !next);
            if (filled)
            {
                // スタンプは、おしたときの かたむきで のこる（番号できまる）
                float tilt = ((s.count * 37) % 17 - 8) * 1.1f;
                s.image.style.rotate = new Rotate(tilt);
                int design = StampBook.DesignFor(s.count);
                if (stampImages != null)
                {
                    var img = s.image;
                    stampImages.Request(design, tex => { if (tex != null) img.style.backgroundImage = new StyleBackground(tex); });
                }
            }
        }

        void LayoutPages()
        {
            if (_pages.layout.width < 10f) return;
            float pw = _pages.layout.width / _pagesShown - 24f;
            float ph = _pages.layout.height - 20f;
            // 4 この ふつうの スタンプ ＋ 1.3 ばいの でっかい スタンプ ＋ すきま
            float c = Mathf.Min(pw / 6.3f, ph / 7.4f);
            c = Mathf.Max(30f, c);
            foreach (var s in _slots.Values)
            {
                float d = s.big ? c * 1.3f : c;
                float cellW = s.big ? c * 1.5f : c * 1.18f;
                s.cell.style.width = cellW;
                s.cell.style.height = c * 1.48f;
                s.circle.style.width = d;
                s.circle.style.height = d;
                s.number.style.fontSize = d * 0.34f;
                s.date.style.fontSize = Mathf.Max(18f, c * 0.2f);
                s.tap.style.fontSize = Mathf.Max(20f, c * 0.22f);
                s.tap.style.top = -c * 0.12f;
            }
        }

        void RefreshStampTexts()
        {
            int total = Stamps.Total;
            // でっかい スタンプまで あと いくつ
            int toBig = StampBook.BigEvery - total % StampBook.BigEvery;
            _stampCount.text = toBig == 1
                ? $"あつめた スタンプ {total}こ ・ つぎは でっかい！"
                : $"あつめた スタンプ {total}こ ・ でっかいまで あと {toBig}こ";
            int today = AppClock.Today();
            if (Stamps.PendingCount > 0)
            {
                bool big = StampBook.IsBig(NextCount);
                _stampMessage.text = big ? "つぎは でっかい スタンプ！ ひかっている ところを タップしてね" : "ひかっている ところを タップして、スタンプを おそう！";
            }
            else if (Stamps.HasStampFor(today)) _stampMessage.text = "きょうの スタンプは おしたよ。また あした あそぼうね♪";
            else _stampMessage.text = $"ピアノを {Stamps.PlaysLeft(today)}かい ひくと、きょうの スタンプが もらえるよ";
        }

        void OnSlotClicked(Slot s)
        {
            if (_press != null) return;
            if (Time.unscaledTime - _swipeTime < 0.3f) return;   // めくった ゆびの タップは かぞえない
            int day = Stamps.DayOf(s.count);
            if (day > 0)
            {
                Click();
                ShowDetail(s.count, day);
                return;
            }
            if (s.count == NextCount && Stamps.PendingCount > 0)
            {
                PressStamp(s);
                return;
            }
            // まだの ところ：ぷるぷる。あと いくつで ここに おせるか
            audioOut?.PlaySfx("oops", 0.5f, true);
            int more = s.count - Stamps.Total - Stamps.PendingCount;
            if (more > 0)
            {
                _stampMessage.text = $"あと {more}こ スタンプを あつめると、ここに おせるよ";
                _messageHold = 2.5f;
            }
            s.circle.style.rotate = new Rotate(8f);
            s.circle.schedule.Execute(() => s.circle.style.rotate = new Rotate(-6f)).StartingIn(80);
            s.circle.schedule.Execute(() => s.circle.style.rotate = new Rotate(0f)).StartingIn(160);
        }

        /// <summary>スタンプを おす（つぎの ところを タップした）。</summary>
        void PressStamp(Slot s)
        {
            int count = Stamps.Press();
            if (count <= 0) return;
            SaveSystem.Save();
            s.circle.RemoveFromClassList("slot--next");
            s.tap.AddToClassList("hidden");
            s.number.AddToClassList("hidden");
            s.circle.AddToClassList("slot--filled");
            s.image.RemoveFromClassList("hidden");
            s.image.style.opacity = 0f;
            s.date.text = AppClock.Short(Stamps.DayOf(count));
            float tilt = ((count * 37) % 17 - 8) * 1.1f;
            s.image.style.rotate = new Rotate(tilt);
            var img = s.image;
            stampImages?.Request(StampBook.DesignFor(count), tex => { if (tex != null) img.style.backgroundImage = new StyleBackground(tex); });
            _press = new Press { slot = s, t = 0f };
            LastPressedCount = count;
        }

        /// <summary>テスト用：さいごに おしたスタンプが なんこめか。</summary>
        public int LastPressedCount { get; private set; }

        /// <summary>テスト用：つぎの ところを タップする。</summary>
        public bool TapNextSlot()
        {
            if (!_slots.TryGetValue(NextCount, out var s)) return false;
            OnSlotClicked(s);
            return true;
        }

        public bool PressAnimating => _press != null;
        public bool CelebrateVisible => !_celebrate.ClassListContains("hidden");
        public bool DetailVisible => !_detail.ClassListContains("hidden");

        public void TapSlot(int count)
        {
            if (_slots.TryGetValue(count, out var s)) OnSlotClicked(s);
        }

        /// <summary>くわしい画面で となりの スタンプへ（おした ものだけ）。</summary>
        void ShowDetailAt(int count)
        {
            int day = Stamps.DayOf(count);
            if (day <= 0) return;
            ShowDetail(count, day);
        }

        void ShowDetail(int count, int day)
        {
            _detailCount = count;
            var prev = _root.Q<Button>("detail-prev");
            var next = _root.Q<Button>("detail-next");
            prev.SetEnabled(count > 1);
            prev.style.opacity = count > 1 ? 1f : 0.3f;
            next.SetEnabled(count < Stamps.Total);
            next.style.opacity = count < Stamps.Total ? 1f : 0.3f;
            int design = StampBook.DesignFor(count);
            var entry = StampCatalog.Get(design);
            _root.Q<Label>("detail-name").text = entry != null ? entry.name : $"No.{design}";
            int book = StampBook.BookFor(count);
            string bookText = book > 1 ? $"{book}さつめ の " : "";
            string big = StampBook.IsBig(count) ? "でっかい スタンプ ・ " : "";
            _root.Q<Label>("detail-info").text = $"{bookText}{(count - 1) % StampBook.Designs + 1}こめ ・ {big}No.{design}\n{AppClock.Long(day)}（{AppClock.Weekday(day)}）";
            var imgEl = _root.Q("detail-image");
            imgEl.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            stampImages?.Request(design, tex => { if (tex != null) imgEl.style.backgroundImage = new StyleBackground(tex); });
            _detail.RemoveFromClassList("hidden");
        }

        void UpdateStamps(float dt)
        {
            if (Current != Page.Stamps) return;
            _stampTime += dt;
            if (_messageHold > 0f)
            {
                _messageHold -= dt;
                if (_messageHold <= 0f && _press == null) RefreshStampTexts();
            }
            // つぎの ところを ぴかぴか
            foreach (var s in _slots.Values)
            {
                if (!s.circle.ClassListContains("slot--next")) continue;
                float k = 1f + 0.08f * Mathf.Sin(_stampTime * 5f);
                s.circle.style.scale = new Scale(new Vector2(k, k));
                s.tap.style.translate = new Translate(0, Mathf.Sin(_stampTime * 5f) * 5f);
            }
            if (_press != null) UpdatePress(dt);
            if (_celebrateTimer > 0f)
            {
                _celebrateTimer -= dt;
                float u = Mathf.Clamp01(1f - _celebrateTimer / 2.4f);
                float sc = u < 0.12f ? Mathf.Lerp(0.3f, 1.2f, u / 0.12f) : Mathf.Lerp(1.2f, 1f, Mathf.Clamp01((u - 0.12f) / 0.1f));
                _celebrateText.style.scale = new Scale(new Vector2(sc, sc));
                _celebrateText.style.rotate = new Rotate(Mathf.Sin(_stampTime * 8f) * 3f);
                _celebrate.style.opacity = _celebrateTimer < 0.4f ? _celebrateTimer / 0.4f : 1f;
                if (_celebrateTimer <= 0f) _celebrate.AddToClassList("hidden");
            }
        }

        void UpdatePress(float dt)
        {
            var p = _press;
            p.t += dt;
            var img = p.slot.image;
            bool big = p.slot.big;
            const float drop = 0.32f;
            if (p.t < drop)
            {
                // 上から ぐっと おりてくる
                float u = p.t / drop;
                float sc = Mathf.Lerp(big ? 3.2f : 2.4f, 1f, u * u);
                img.style.scale = new Scale(new Vector2(sc, sc));
                img.style.opacity = Mathf.Clamp01(u * 2f);
            }
            else if (!p.landed)
            {
                p.landed = true;
                img.style.opacity = 1f;
                img.style.scale = new Scale(Vector2.one);
                audioOut?.PlaySfx(big ? "stamp_big" : "stamp", 1f);
                WebBridge.Vibrate(big ? 60 : 30);
                var center = p.slot.circle.worldBound.center;
                if (!SaveSystem.Settings.reduceMotion) Burst(_stampFx, _stampFx.WorldToLocal(center), big ? 46 : 18, big ? 1.6f : 1f);
                if (big)
                {
                    _celebrateText.text = $"でっかい スタンプ！\n{(p.slot.count - 1) % StampBook.Designs + 1}こめ おめでとう！";
                    _celebrate.RemoveFromClassList("hidden");
                    _celebrate.style.opacity = 1f;
                    _celebrateTimer = 2.4f;
                }
            }
            else
            {
                // ぽよん
                float u = (p.t - drop) / 0.35f;
                float sc = 1f + 0.12f * Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI) * (1f - Mathf.Clamp01(u));
                p.slot.circle.style.scale = new Scale(new Vector2(sc, 2f - sc));
                if (u >= 1f)
                {
                    p.slot.circle.style.scale = new Scale(Vector2.one);
                    _press = null;
                    // まだ おせるスタンプがあれば、つぎの ところを ひからせる
                    if (_slots.TryGetValue(NextCount, out var nextSlot)) FillSlot(nextSlot);
                    else if (Stamps.PendingCount > 0) EnterStamps();
                    RefreshStampTexts();
                    // おした スタンプの なまえ
                    var entry = StampCatalog.Get(StampBook.DesignFor(p.slot.count));
                    if (entry != null)
                    {
                        _stampMessage.text = Stamps.PendingCount > 0
                            ? $"「{entry.name}」を おしたよ！ つぎも おせるよ"
                            : $"「{entry.name}」を おしたよ！";
                        _messageHold = 3f;
                    }
                }
            }
        }

        void Burst(VisualElement layer, Vector2 at, int n, float power)
        {
            for (int i = 0; i < n; i++)
            {
                var e = UIParts.El("confetti", layer);
                e.style.backgroundColor = ConfettiColors[i % ConfettiColors.Length];
                float a = Random.Range(0f, Mathf.PI * 2f);
                float sp = Random.Range(380f, 900f) * power;
                var c = new Confetto
                {
                    e = e,
                    pos = at,
                    vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a) - 0.6f) * sp,
                    life = Random.Range(0.9f, 1.6f) * (power > 1f ? 1.3f : 1f),
                    spin = Random.Range(-720f, 720f),
                };
                _confetti.Add(c);
            }
        }

        void UpdateConfetti(float dt)
        {
            for (int i = _confetti.Count - 1; i >= 0; i--)
            {
                var c = _confetti[i];
                c.t += dt;
                if (c.t >= c.life)
                {
                    c.e.RemoveFromHierarchy();
                    _confetti.RemoveAt(i);
                    continue;
                }
                c.vel.y += 1600f * dt;
                c.vel *= 1f - 1.2f * dt;
                c.pos += c.vel * dt;
                c.e.style.left = c.pos.x;
                c.e.style.top = c.pos.y;
                c.e.style.rotate = new Rotate(c.spin * c.t);
                // Mathf.SmoothStep は「from から to へ」。はじめは こく、さいごの 4 わりで きえる
                float u = c.t / c.life;
                c.e.style.opacity = u < 0.6f ? 1f : Mathf.SmoothStep(1f, 0f, (u - 0.6f) / 0.4f);
            }
        }
    }
}
