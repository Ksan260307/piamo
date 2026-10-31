using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Piamo
{
    /// <summary>
    /// 「ピアノで あそぶ」の えんそうモード。
    ///  - 曲えらび（なかまごとの 見出し・むずかしさ・もらった ほし）
    ///  - きく／いっしょに／ひとつずつ、はやさ、はじめから、とめる
    ///  - ひく音が 上から おちてきて、けんばんに とどくと その けんばんが ひかる
    ///  - 曲が いまの けんばんに おさまらなければ、その曲の あいだだけ けんばんを ふやす（せっていは かえない）
    /// まねっこ とは べつの ゲーム（PerformGame）で、モードを かえると もう一方は 止める。
    /// </summary>
    public partial class AppUI
    {
        public PerformGame Perform { get; private set; }
        /// <summary>テスト用：えんそうの時間をはやめる。</summary>
        public static float PerformTimeScale = 1f;
        /// <summary>いま えんそうしている曲（曲えらびの ときは null）。</summary>
        public PerformSong PerformSong => _performSong;
        public bool PerformUserPaused => _performUserPaused;

        PerformSong _performSong;
        VisualElement _performPanel, _performPicker, _performGame, _performLane, _performNotes, _performResult, _performResultStars, _performProgress, _performAnimal, _performPlayIcon;
        Label _performTitle, _performCount, _performBubble, _performResultText, _performResultDetail, _performSpeedLabel;
        Button _tabPerform;
        readonly Button[] _performModeTabs = new Button[3];
        ScrollView _performList;
        bool _performUserPaused;
        float _performBubbleTimer, _performCountPop;
        int _laneFrom;
        Dictionary<int, float> _guides = new Dictionary<int, float>();
        Dictionary<int, float> _guidesNext = new Dictionary<int, float>();
        readonly List<(Button card, PerformSong song, VisualElement stars)> _performCards = new List<(Button, PerformSong, VisualElement)>();

        class LaneNote
        {
            public VisualElement e;
            public Label label;
        }
        readonly List<LaneNote> _lane = new List<LaneNote>();
        const int LanePool = 40;

        class JudgeText
        {
            public Label l;
            public float t, life;
            public Vector2 pos;
        }
        readonly List<JudgeText> _judgeTexts = new List<JudgeText>();
        int _judgeNext;

        static readonly string[] PerformSpeedLabels = { "ゆっくり", "ふつう", "はやい" };
        static readonly string[] PerformIntro =
        {
            "よく きいてね♪ いっしょに ひいても いいよ",
            "ひかった けんばんを いっしょに ひこう！",
            "ひかった けんばんを おしてね。まってるよ",
        };

        void BuildPerform()
        {
            _performPanel = _root.Q("perform-panel");
            _performPicker = _root.Q("perform-picker");
            _performGame = _root.Q("perform-game");
            _performLane = _root.Q("perform-lane");
            _performNotes = _root.Q("perform-notes");
            _performResult = _root.Q("perform-result");
            _performResultStars = _root.Q("perform-result-stars");
            _performProgress = _root.Q("perform-progress-fill");
            _performAnimal = _root.Q("perform-animal");
            _performPlayIcon = _root.Q("perform-play-icon");
            _performTitle = _root.Q<Label>("perform-title");
            _performCount = _root.Q<Label>("perform-count");
            _performBubble = _root.Q<Label>("perform-bubble");
            _performResultText = _root.Q<Label>("perform-result-text");
            _performResultDetail = _root.Q<Label>("perform-result-detail");
            _performSpeedLabel = _root.Q<Label>("perform-speed-label");
            _performList = _root.Q<ScrollView>("perform-list");
            _tabPerform = _root.Q<Button>("tab-perform");
            _performModeTabs[0] = _root.Q<Button>("perform-mode-listen");
            _performModeTabs[1] = _root.Q<Button>("perform-mode-along");
            _performModeTabs[2] = _root.Q<Button>("perform-mode-step");

            SetIcon("perform-restart-icon", "again");
            SetIcon("perform-change-icon", "songs");
            SetIcon("perform-speed-icon", "speed");
            SetIcon("perform-again-icon", "again");
            SetIcon("perform-other-icon", "songs");

            OnClick(_tabPerform, () => SetMode(PlayMode.Perform));
            for (int i = 0; i < _performModeTabs.Length; i++)
            {
                int k = i;
                OnClick(_performModeTabs[i], () => SelectPerformMode(k));
            }
            OnClick(_root.Q<Button>("perform-play"), TogglePerformPause);
            OnClick(_root.Q<Button>("perform-restart"), RestartPerform);
            OnClick(_root.Q<Button>("perform-speed"), CyclePerformSpeed);
            OnClick(_root.Q<Button>("perform-change"), StopPerform);
            OnClick(_root.Q<Button>("perform-again"), RestartPerform);
            OnClick(_root.Q<Button>("perform-other"), StopPerform);

            for (int i = 0; i < LanePool; i++)
            {
                var e = UIParts.El("lane-note hidden", _performNotes);
                _lane.Add(new LaneNote { e = e, label = UIParts.Text("", "lane-note__label", e) });
            }
            for (int i = 0; i < 6; i++) _judgeTexts.Add(new JudgeText { l = UIParts.Text("", "judge hidden", _performLane) });
            for (int i = 0; i < 3; i++) UIParts.SetIcon(UIParts.El("result-star", _performResultStars), Icon("star"));

            Perform = new PerformGame();
            Perform.CountTick += n =>
            {
                _performCount.text = n.ToString();
                _performCount.RemoveFromClassList("hidden");
                _performCountPop = 0.25f;
                audioOut?.PlaySfx("tap", 0.8f);
            };
            Perform.MelodyOn += OnPerformMelodyOn;
            Perform.MelodyOff += OnPerformMelodyOff;
            Perform.AccOn += (a, sec) => audioOut?.NoteOn(a.midi, a.velocity);
            Perform.AccOff += a => audioOut?.NoteOff(a.midi);
            Perform.Judged += OnPerformJudged;
            Perform.Finished += OnPerformFinished;

            HideScrollers(_performList);
            BuildPerformList();
            RefreshPerformControls();
        }

        // ------------------------------------------------------------------
        // 曲えらび

        void BuildPerformList()
        {
            var c = _performList.contentContainer;
            int cat = -1;
            foreach (var song in PerformSongs.All)
            {
                if ((int)song.category != cat)
                {
                    cat = (int)song.category;
                    UIParts.Text(PerformSongs.CategoryLabels[cat], "perform-section", c);
                }
                var s = song;
                var b = UIParts.Candy(s.title, "white", Character(s.animal), () => StartPerform(s), c, "song-card perform-card");
                b.name = "perform-song-" + s.id;
                TitleSize(b, s.title);
                var level = UIParts.El("song-card__level", b);
                for (int i = 0; i < s.stars; i++) UIParts.SetIcon(UIParts.El("mini-note", level), Icon("note1"));
                var stars = UIParts.El("song-card__stars", b);
                for (int i = 0; i < 3; i++) UIParts.SetIcon(UIParts.El("mini-star", stars), Icon("star"));
                _performCards.Add((b, s, stars));
            }
        }

        void RefreshPerformCards()
        {
            foreach (var (card, song, stars) in _performCards)
            {
                int n = SaveSystem.Data.StarsFor(song.id);
                stars.EnableInClassList("hidden", n <= 0);
                for (int i = 0; i < stars.childCount; i++) stars[i].style.display = i < n ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        void ShowPerformPicker()
        {
            _performPicker.RemoveFromClassList("hidden");
            _performGame.AddToClassList("hidden");
            RefreshPerformCards();
        }

        // ------------------------------------------------------------------
        // はじめる・とめる

        /// <summary>えんそうの曲のあいだは、曲に あわせた けんばん。そのほかは せっていの けんばん。</summary>
        PiamoSettings EffectiveSettings()
        {
            var s = SaveSystem.Settings;
            if (Mode != PlayMode.Perform || _performSong == null) return s;
            var r = PerformLayout.For(_performSong, s);
            if (r == null) return s;
            var c = s.Clone();
            c.octaves = r.Value.octaves;
            c.octaveShift = r.Value.shift;
            c.blackKeys = r.Value.blackKeys;
            return c;
        }

        /// <summary>けんばんを いまの モード・曲に あわせて 作りなおす（かわらなければ 何もしない）。</summary>
        void ApplyPlayLayout()
        {
            if (play == null) return;
            play.ApplySettings(EffectiveSettings());
            BuildKeyLabels(false);
            OnLayoutPlay();
            OnFramingChanged();
        }

        public void StartPerform(PerformSong song)
        {
            if (song == null) return;
            if (Mode != PlayMode.Perform) SetMode(PlayMode.Perform);
            Perform.Stop();
            ClearPerformGuides();
            play?.ReleaseAll();
            _performSong = song;
            ApplyPlayLayout();
            var s = SaveSystem.Settings;
            _performPicker.AddToClassList("hidden");
            _performGame.RemoveFromClassList("hidden");
            _performResult.AddToClassList("hidden");
            _performCount.AddToClassList("hidden");
            _performTitle.text = song.title;
            UIParts.SetIcon(_performAnimal, Character(song.animal));
            _performUserPaused = false;
            _laneFrom = 0;
            Perform.Start(song, (PerformGame.Mode)s.performMode, s.PerformTempo, s.performAccomp, s.performMelody);
            SetPerformBubble(PerformIntro[s.performMode]);
            RefreshPerformControls();
        }

        void RestartPerform()
        {
            if (_performSong != null) StartPerform(_performSong);
        }

        /// <summary>曲えらびに もどる（けんばんも せっていの けんばんに もどす）。</summary>
        void StopPerform()
        {
            Perform?.Stop();
            play?.ReleaseAll();
            audioOut?.AllOff(false);
            ClearPerformGuides();
            bool had = _performSong != null;
            _performSong = null;
            _performUserPaused = false;
            if (had) ApplyPlayLayout();
            foreach (var n in _lane) n.e.AddToClassList("hidden");
            foreach (var j in _judgeTexts)
            {
                j.life = 0f;
                j.l.AddToClassList("hidden");
            }
            _performCount.AddToClassList("hidden");
            _performBubble.AddToClassList("hidden");
            ShowPerformPicker();
            RefreshPerformControls();
        }

        void SelectPerformMode(int mode)
        {
            SaveSystem.Settings.performMode = mode;
            SaveSystem.ApplySettings();
            if (_performSong != null) StartPerform(_performSong);
        }

        void CyclePerformSpeed()
        {
            SaveSystem.Settings.performSpeed = (SaveSystem.Settings.performSpeed + 1) % 3;
            SaveSystem.ApplySettings();
            SetPerformBubble($"はやさ：{PerformSpeedLabels[SaveSystem.Settings.performSpeed]}");
        }

        void TogglePerformPause()
        {
            if (Perform == null || _performSong == null) return;
            if (Perform.Current == PerformGame.State.Finished || Perform.Current == PerformGame.State.Idle)
            {
                RestartPerform();
                return;
            }
            _performUserPaused = !_performUserPaused;
            SetPerformBubble(_performUserPaused ? "ちょっと おやすみ" : "つづきから ひこう！");
            RefreshPerformControls();
        }

        /// <summary>せっていが かわったとき（はやさ・おてほん・ばんそう は、ひいている とちゅうでも かわる）。</summary>
        void RefreshPerformAfterSettings()
        {
            var s = SaveSystem.Settings;
            if (Perform != null && _performSong != null)
            {
                Perform.SetTempo(s.PerformTempo);
                Perform.GuideSound = s.performMelody;
                Perform.SetAccompaniment(s.performAccomp);
            }
            RefreshPerformControls();
        }

        void RefreshPerformControls()
        {
            if (_performSpeedLabel == null) return;
            var s = SaveSystem.Settings;
            _performSpeedLabel.text = PerformSpeedLabels[s.performSpeed];
            for (int i = 0; i < _performModeTabs.Length; i++) _performModeTabs[i].EnableInClassList("tab--on", i == s.performMode);
            bool running = Perform != null && _performSong != null && !_performUserPaused
                && (Perform.Current == PerformGame.State.CountIn || Perform.Current == PerformGame.State.Playing);
            UIParts.SetIcon(_performPlayIcon, Icon(running ? "pause" : "play"));
        }

        void SetPerformBubble(string text, float seconds = 2.6f)
        {
            _performBubble.text = text;
            _performBubble.RemoveFromClassList("hidden");
            _performBubbleTimer = seconds;
        }

        // ------------------------------------------------------------------
        // 音と けんばん

        void OnPerformMelodyOn(int index, float seconds)
        {
            var n = Perform.Song.notes[index];
            if (Perform.CurrentMode == PerformGame.Mode.Listen) play.DemoDown(n.midi, true, 0.9f);
            else if (Perform.CurrentMode == PerformGame.Mode.Along && Perform.GuideSound) play.DemoDown(n.midi, false, 0.5f);
        }

        void OnPerformMelodyOff(int index)
        {
            if (Perform?.Song == null || index >= Perform.Song.notes.Count) return;
            play.DemoUp(Perform.Song.notes[index].midi);
        }

        void OnPerformJudged(int index, PerformGame.Grade grade)
        {
            if (Perform.CurrentMode != PerformGame.Mode.Along) return;
            if (grade == PerformGame.Grade.Perfect || grade == PerformGame.Grade.Good)
                ShowJudge(Perform.Song.notes[index].midi, grade == PerformGame.Grade.Perfect);
        }

        void OnPerformFinished()
        {
            ClearPerformGuides();
            var g = Perform;
            _performResult.RemoveFromClassList("hidden");
            _performBubble.AddToClassList("hidden");
            int stars = g.Stars;
            if (g.CurrentMode == PerformGame.Mode.Listen)
            {
                audioOut?.PlaySfx("success", 0.9f);
                _performResultText.text = "おしまい♪";
                _performResultDetail.text = "つぎは「いっしょに」で ひいてみよう！";
                _performResultStars.AddToClassList("hidden");
            }
            else
            {
                audioOut?.PlaySfx(stars >= 3 ? "clear" : "success", 1f);
                _performResultText.text = stars >= 3 ? "すごい！ さいごまで ひけたね！" : stars == 2 ? "じょうず！ さいごまで ひけたね！" : "さいごまで ひけたね！";
                _performResultDetail.text = g.CurrentMode == PerformGame.Mode.Along
                    ? $"ぴったり {g.Perfect}・いいね {g.Good}（ぜんぶで {g.Total}）"
                    : "ひとつずつ ぜんぶ ひけたよ";
                _performResultStars.RemoveFromClassList("hidden");
                for (int i = 0; i < _performResultStars.childCount; i++) _performResultStars[i].EnableInClassList("result-star--off", i >= stars);
                if (SaveSystem.Data.RecordStars(_performSong.id, stars)) SaveSystem.Save();
            }
            RefreshPerformControls();
        }

        void ClearPerformGuides()
        {
            play?.view.ClearGuides();
            _guides.Clear();
        }

        /// <summary>ひく けんばんを ひからせる（いま ひく音は つよく、つぎの音は うすく）。</summary>
        void UpdatePerformGuides()
        {
            _guidesNext.Clear();
            var g = Perform;
            var notes = g.Song.notes;
            if (!g.Paused && (g.Current == PerformGame.State.Playing || g.Current == PerformGame.State.CountIn))
            {
                switch (g.CurrentMode)
                {
                    case PerformGame.Mode.Step:
                        if (g.NextIndex < notes.Count)
                        {
                            Want(notes[g.NextIndex].midi, 1f);
                            if (g.NextIndex + 1 < notes.Count) Want(notes[g.NextIndex + 1].midi, 0.3f);
                        }
                        break;
                    case PerformGame.Mode.Along:
                        {
                            int si = g.SoundingIndex;
                            if (si >= 0 && g.Grades[si] == PerformGame.Grade.None) Want(notes[si].midi, 1f);
                            int ni = g.NextIndex;
                            if (ni < notes.Count && notes[ni].on - g.Beat <= 1.5f) Want(notes[ni].midi, 0.35f);
                        }
                        break;
                    case PerformGame.Mode.Listen:
                        if (g.NextIndex < notes.Count && notes[g.NextIndex].on - g.Beat <= 1f) Want(notes[g.NextIndex].midi, 0.3f);
                        break;
                }
            }
            foreach (var kv in _guides)
                if (!_guidesNext.ContainsKey(kv.Key)) play.view.SetGuide(kv.Key, 0f, Color.white);
            foreach (var kv in _guidesNext) play.view.SetGuide(kv.Key, kv.Value, NoteUtil.NoteColor(kv.Key));
            (_guides, _guidesNext) = (_guidesNext, _guides);
        }

        void Want(int midi, float level)
        {
            if (!_guidesNext.TryGetValue(midi, out float cur) || level > cur) _guidesNext[midi] = level;
        }

        /// <summary>けんばんが スクロールするときは、つぎに ひく音が 見えるように うごかす。</summary>
        void FollowPerform()
        {
            if (!play.Framing.scrolling) return;
            var g = Perform;
            var notes = g.Song.notes;
            int focus = g.CurrentMode == PerformGame.Mode.Step ? g.NextIndex : g.SoundingIndex >= 0 ? g.SoundingIndex : g.NextIndex;
            if (focus < 0 || focus >= notes.Count) return;
            int lo = notes[focus].midi, hi = lo;
            play.EnsureVisible(lo, hi);
            for (int j = focus + 1; j < Mathf.Min(notes.Count, focus + 4); j++)
            {
                lo = Mathf.Min(lo, notes[j].midi);
                hi = Mathf.Max(hi, notes[j].midi);
            }
            if (NoteUtil.WhiteNumber(hi) - NoteUtil.WhiteNumber(lo) + 1 <= play.Framing.visible) play.EnsureVisible(lo, hi);
        }

        // ------------------------------------------------------------------
        // まいフレーム

        void UpdatePerform(float dt, bool blocked)
        {
            if (_performBubbleTimer > 0f)
            {
                _performBubbleTimer -= dt;
                if (_performBubbleTimer <= 0f) _performBubble.AddToClassList("hidden");
            }
            if (_performCountPop > 0f)
            {
                _performCountPop -= dt;
                float s = 1f + 0.35f * Mathf.Clamp01(_performCountPop / 0.25f);
                _performCount.style.scale = new Scale(new Vector2(s, s));
            }
            UpdateJudges(dt);
            if (Mode != PlayMode.Perform || Perform == null || _performSong == null) return;
            bool wasRunning = !Perform.Paused;
            Perform.SetPaused(blocked || _performUserPaused);
            if (wasRunning == Perform.Paused) RefreshPerformControls();
            Perform.Update(dt * PerformTimeScale);
            if (Perform.CountLeft == 0 && Perform.Current != PerformGame.State.CountIn && !_performCount.ClassListContains("hidden"))
                _performCount.AddToClassList("hidden");
            _performProgress.style.width = Length.Percent(Perform.Progress * 100f);
            UpdatePerformGuides();
            FollowPerform();
        }

        /// <summary>おちてくる音（鍵盤の位置に あわせるので、カメラが うごいたあと LateUpdate で）。</summary>
        void UpdateLane()
        {
            var g = Perform;
            if (g == null || _performSong == null || Mode != PlayMode.Perform || play?.Layout == null) return;
            float h = _performNotes.layout.height, w = _performNotes.layout.width;
            if (h < 4f) return;
            float look = Mathf.Clamp(2.2f / g.SecondsPerBeat, 2f, 5f);   // 2 びょうちょっと先まで 見せる
            float ppb = h / look;
            float beat = g.Beat;
            float keyUnits = play.Framing.keyPx / PanelScale;
            var notes = g.Song.notes;
            var labels = SaveSystem.Settings.labels;
            if (_laneFrom > notes.Count) _laneFrom = 0;
            while (_laneFrom < notes.Count && notes[_laneFrom].Off < beat - 0.5f) _laneFrom++;
            int used = 0;
            for (int i = _laneFrom; i < notes.Count && used < _lane.Count; i++)
            {
                var n = notes[i];
                if (n.on - beat > look) break;
                float bottom = h - (n.on - beat) * ppb;
                float top = h - (n.Off - beat) * ppb;
                if (top >= h - 2f || !play.Layout.Contains(n.midi)) continue;
                var lp = ScreenToLocal(_performNotes, play.WorldToScreen(play.view.TopPoint(n.midi)));
                bool black = NoteUtil.IsBlack(n.midi);
                float nw = Mathf.Max(24f, keyUnits * (black ? 0.5f : 0.74f));
                if (lp.x + nw / 2f < 0f || lp.x - nw / 2f > w) continue;
                var ln = _lane[used++];
                float gap = Mathf.Min(6f, (bottom - top) * 0.15f);
                float y = top + gap;
                float height = Mathf.Max(8f, Mathf.Min(bottom, h) - y);
                ln.e.style.left = lp.x - nw / 2f;
                ln.e.style.top = y;
                ln.e.style.width = nw;
                ln.e.style.height = height;
                ln.e.style.backgroundColor = NoteUtil.NoteColor(n.midi);
                var grade = g.Grades[i];
                ln.e.EnableInClassList("lane-note--black", black);
                ln.e.EnableInClassList("lane-note--hit", grade == PerformGame.Grade.Perfect || grade == PerformGame.Grade.Good);
                ln.e.EnableInClassList("lane-note--miss", grade == PerformGame.Grade.Miss);
                ln.label.text = height > 34f ? NoteUtil.Name(n.midi, labels) : "";
                ln.label.style.fontSize = Mathf.Clamp(nw * 0.4f, 18f, 34f);
                if (ln.e.ClassListContains("hidden")) ln.e.RemoveFromClassList("hidden");
            }
            for (int i = used; i < _lane.Count; i++)
                if (!_lane[i].e.ClassListContains("hidden")) _lane[i].e.AddToClassList("hidden");
        }

        /// <summary>見えている おちてくる音の数（テスト用）。</summary>
        public int LaneNotesVisible
        {
            get
            {
                int n = 0;
                foreach (var l in _lane) if (!l.e.ClassListContains("hidden")) n++;
                return n;
            }
        }

        void ShowJudge(int midi, bool perfect)
        {
            if (_judgeTexts.Count == 0 || play == null) return;
            var j = _judgeTexts[_judgeNext];
            _judgeNext = (_judgeNext + 1) % _judgeTexts.Count;
            var lp = ScreenToLocal(_performLane, play.WorldToScreen(play.view.TopPoint(midi)));
            j.pos = new Vector2(lp.x, _performLane.layout.height - 64f);
            j.t = 0f;
            j.life = 0.8f;
            j.l.text = perfect ? "ぴったり！" : "いいね！";
            j.l.EnableInClassList("judge--good", !perfect);
            j.l.RemoveFromClassList("hidden");
            j.l.BringToFront();
        }

        void UpdateJudges(float dt)
        {
            foreach (var j in _judgeTexts)
            {
                if (j.life <= 0f) continue;
                j.t += dt;
                if (j.t >= j.life)
                {
                    j.life = 0f;
                    j.l.AddToClassList("hidden");
                    continue;
                }
                float u = j.t / j.life;
                float pop = u < 0.2f ? Mathf.SmoothStep(0.6f, 1.15f, u / 0.2f) : Mathf.Lerp(1.15f, 1f, (u - 0.2f) / 0.8f);
                float hw = j.l.layout.width > 1f ? j.l.layout.width / 2f : 120f;
                j.l.style.left = j.pos.x - hw;
                j.l.style.top = j.pos.y - 28f - 70f * u;
                j.l.style.scale = new Scale(new Vector2(pop, pop));
                // Mathf.SmoothStep は「from から to へ」なので、さいごの 4 わりで 1 → 0
                j.l.style.opacity = u < 0.6f ? 1f : Mathf.SmoothStep(1f, 0f, (u - 0.6f) / 0.4f);
            }
        }
    }
}
