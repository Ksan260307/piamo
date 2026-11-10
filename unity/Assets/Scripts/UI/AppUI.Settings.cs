using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Piamo
{
    public partial class AppUI
    {
        ScrollView _settingsScroll;
        /// <summary>せっていが かわったときに、ボタンの えらばれかたを なおす。</summary>
        readonly List<Action> _settingRefresh = new List<Action>();
        readonly Dictionary<string, VisualElement> _settingControls = new Dictionary<string, VisualElement>();
        /// <summary>セクションの 見出し（上の ボタンで そこへ とぶ）。</summary>
        readonly Dictionary<string, VisualElement> _sectionTitles = new Dictionary<string, VisualElement>();
        VisualElement _sectionChips;

        static readonly string[] OctaveCountLabels = { "8", "15", "22", "29", "36" };

        public VisualElement SettingControl(string key) => _settingControls.TryGetValue(key, out var e) ? e : null;

        /// <summary>せっていの セクション（見出しの ボタンの じゅん）。</summary>
        static readonly (string key, string label, string icon)[] SettingSections =
        {
            ("sound", "おと", "sec_sound"),
            ("keys", "けんばん", "sec_keys"),
            ("mimic", "まねっこ", "sec_mimic"),
            ("perform", "えんそう", "sec_perform"),
            ("screen", "がめん", "sec_screen"),
            ("parents", "おうちの かたへ", "sec_parents"),
        };

        /// <summary>テスト用：いま いちばん上に 見えている セクション。</summary>
        public string SettingsSectionInView
        {
            get
            {
                if (_settingsScroll == null) return null;
                float y = _settingsScroll.scrollOffset.y + 20f;
                string best = SettingSections[0].key;
                foreach (var sec in SettingSections)
                    if (_sectionTitles.TryGetValue(sec.key, out var t) && t.layout.height > 0f && t.layout.y <= y) best = sec.key;
                return best;
            }
        }

        void BuildSettings()
        {
            _settingsScroll = _root.Q<ScrollView>("settings-scroll");
            HideScrollers(_settingsScroll);
            SetIcon("settings-back-icon", "back");
            _root.Q<Button>("settings-back").clicked += CloseSettings;
            var c = _settingsScroll.contentContainer;
            var s = SaveSystem.Settings;

            // 見出しへ とぶ ボタン（ながい せっていを スクロール しなくても いいように）
            _sectionChips = UIParts.El("section-chips", c);
            foreach (var sec in SettingSections)
            {
                string k = sec.key;
                var chip = UIParts.Candy(sec.label, "white", Icon(sec.icon), () =>
                {
                    Click();
                    ScrollToSection(k);
                }, _sectionChips, "section-chip");
                chip.name = "section-chip-" + k;
            }

            // ---------------- おと ----------------
            Section(c, "おと", "sec_sound", "sound");
            {
                var row = Row(c, "がっき", "けんばんを おしたときの おと");
                var grid = UIParts.El("grid", row.control);
                row.control.style.flexGrow = 1;
                var cards = new List<(Button, string)>();
                foreach (var info in InstrumentCatalog.All)
                {
                    string id = info.id;
                    var b = UIParts.Candy(info.label, "white", Icon("inst_" + id), () => SelectInstrument(id), grid, "inst-card");
                    cards.Add((b, id));
                }
                _settingControls["instrument"] = grid;
                _settingRefresh.Add(() => { foreach (var (b, id) in cards) b.EnableInClassList("inst-card--on", id == SaveSystem.Settings.instrument); });
            }
            {
                var row = Row(c, "おとの おおきさ", null);
                var slider = new SoftSlider(s.volume);
                slider.Changed += v =>
                {
                    SaveSystem.Settings.volume = v;
                    if (audioOut != null) audioOut.Volume = v;
                };
                slider.Released += () =>
                {
                    SaveSystem.ApplySettings();
                    audioOut?.NoteOn(NoteUtil.MiddleC + 7 + 12 * SaveSystem.Settings.octaveShift, 0.9f);
                };
                var lo = UIParts.El("icon", row.control);
                lo.style.width = 56; lo.style.height = 56; lo.style.marginRight = 10;
                UIParts.SetIcon(lo, Icon("volume_low"));
                row.control.Add(slider);
                var hi = UIParts.El("icon", row.control);
                hi.style.width = 56; hi.style.height = 56; hi.style.marginLeft = 10;
                UIParts.SetIcon(hi, Icon("volume_high"));
                _settingControls["volume"] = slider;
                _settingRefresh.Add(() => slider.Set(SaveSystem.Settings.volume, false));
            }
            Toggle(c, "sustain", "おとを のばす", "はなしても、おとが のこります（ペダル）", () => SaveSystem.Settings.sustain, v => SaveSystem.Settings.sustain = v);

            // ---------------- けんばん ----------------
            Section(c, "けんばん", "sec_keys", "keys");
            {
                var row = Row(c, "けんばんの かず", "しろい けんばんの かず（1〜5 オクターブ）");
                var seg = new Segmented(OctaveCountLabels, s.octaves - 1);
                seg.Changed += i =>
                {
                    Click();
                    SaveSystem.Settings.octaves = i + 1;
                    SaveSystem.ApplySettings();
                };
                row.control.Add(seg);
                _settingControls["octaves"] = seg;
                _settingRefresh.Add(() => seg.Select(SaveSystem.Settings.octaves - 1, false));
            }
            {
                var row = Row(c, "オクターブ", "おとの たかさ（ひくい ← → たかい）");
                Stepper st = null;
                st = new Stepper(Icon("minus"), Icon("plus"),
                    () => { Click(); ShiftOctave(-1); },
                    () => { Click(); ShiftOctave(1); });
                row.control.Add(st);
                _settingControls["octaveShift"] = st;
                _settingRefresh.Add(() =>
                {
                    var set = SaveSystem.Settings;
                    var lim = KeyboardLayout.ShiftLimits(set.octaves);
                    var lay = KeyboardLayout.Create(set.octaves, set.octaveShift, true);
                    string sign = set.octaveShift > 0 ? "+" : "";
                    st.Set($"{sign}{set.octaveShift}  ({OctaveName(lay.Low)}〜{OctaveName(lay.High)})", set.octaveShift > lim.x, set.octaveShift < lim.y);
                });
            }
            Choice(c, "keySize", "けんばんの おおきさ", "ほそくなりすぎると、よこに スクロールします", new[] { "ちいさい", "ふつう", "おおきい" },
                () => SaveSystem.Settings.keySize, v => SaveSystem.Settings.keySize = v);
            Toggle(c, "blackKeys", "くろい けんばん", "なしにすると、しろい けんばんだけ（ドレミファソラシ）", () => SaveSystem.Settings.blackKeys, v => SaveSystem.Settings.blackKeys = v, "あり", "なし");
            Choice(c, "labels", "おんめい", "けんばんに かく おとの なまえ", new[] { "なし", "ドレミ", "どれみ", "CDE", "いろ" },
                () => (int)SaveSystem.Settings.labels, v => SaveSystem.Settings.labels = (LabelMode)v);
            Toggle(c, "rainbowKeys", "にじいろの けんばん", "しろい けんばんを、ドレミの いろに ぬります", () => SaveSystem.Settings.rainbowKeys, v => SaveSystem.Settings.rainbowKeys = v);
            {
                var row = Row(c, "ピアノの いろ", null);
                var wrap = UIParts.El("grid", row.control);
                var sw = new List<Button>();
                for (int i = 0; i < PianoColors.Count; i++)
                {
                    int k = i;
                    var b = new Button(() =>
                    {
                        Click();
                        SaveSystem.Settings.pianoColor = k;
                        SaveSystem.ApplySettings();
                    });
                    b.AddToClassList("swatch");
                    b.style.backgroundColor = PianoColors.Colors[i];
                    b.tooltip = PianoColors.Names[i];
                    wrap.Add(b);
                    sw.Add(b);
                }
                _settingControls["pianoColor"] = wrap;
                _settingRefresh.Add(() => { for (int i = 0; i < sw.Count; i++) sw[i].EnableInClassList("swatch--on", i == SaveSystem.Settings.pianoColor); });
            }

            // ---------------- まねっこ ----------------
            Section(c, "まねっこ", "sec_mimic", "mimic");
            Choice(c, "mimicSpeed", "おてほんの はやさ", null, new[] { "ゆっくり", "ふつう", "はやい" },
                () => SaveSystem.Settings.mimicSpeed, v => SaveSystem.Settings.mimicSpeed = v);
            Choice(c, "mimicLength", "まねする ながさ", "1かいに まねする おとの かず", new[] { "みじかい", "ふつう", "ながい" },
                () => SaveSystem.Settings.mimicLength, v => SaveSystem.Settings.mimicLength = v);
            {
                var labels = new List<string>();
                foreach (int sec in PiamoSettings.HintChoices) labels.Add(sec == 0 ? "なし" : $"{sec}びょう");
                Choice(c, "hintSeconds", "ヒント", "まよったとき、こたえの けんばんが ひかるまでの じかん", labels.ToArray(),
                    () => Mathf.Max(0, Array.IndexOf(PiamoSettings.HintChoices, SaveSystem.Settings.hintSeconds)),
                    v => SaveSystem.Settings.hintSeconds = PiamoSettings.HintChoices[v]);
            }
            Toggle(c, "demoLights", "おてほんで ひかる", "おてほんの とき、けんばんも ひからせます", () => SaveSystem.Settings.demoLights, v => SaveSystem.Settings.demoLights = v);

            // ---------------- えんそう ----------------
            Section(c, "えんそう", "sec_perform", "perform");
            Choice(c, "performSpeed", "えんそうの はやさ", null, new[] { "ゆっくり", "ふつう", "はやい" },
                () => SaveSystem.Settings.performSpeed, v => SaveSystem.Settings.performSpeed = v);
            Toggle(c, "performMelody", "おてほんの メロディ", "「いっしょに」で、ひく メロディも アプリが ちいさく ならします", () => SaveSystem.Settings.performMelody, v => SaveSystem.Settings.performMelody = v);
            Toggle(c, "performAccomp", "ばんそう", "ドミソの おとで、いっしょに ひいてくれます", () => SaveSystem.Settings.performAccomp, v => SaveSystem.Settings.performAccomp = v);
            Choice(c, "performWindow", "あたりの はば", "「いっしょに」で、すこし ずれても あたりに します", new[] { "ひろい", "ふつう" },
                () => SaveSystem.Settings.performWindow, v => SaveSystem.Settings.performWindow = v);

            // ---------------- がめん ----------------
            Section(c, "がめん", "sec_screen", "screen");
            Toggle(c, "noteEffects", "おんぷが とぶ", "けんばんを おすと ♪ が とびだします", () => SaveSystem.Settings.noteEffects, v => SaveSystem.Settings.noteEffects = v);
            Toggle(c, "uiSounds", "ボタンの おと", null, () => SaveSystem.Settings.uiSounds, v => SaveSystem.Settings.uiSounds = v);
            Toggle(c, "reduceMotion", "うごきを すくなく", "どうぶつが ゆれない・かみふぶきが でない", () => SaveSystem.Settings.reduceMotion, v => SaveSystem.Settings.reduceMotion = v);
            Toggle(c, "pcKeyLabels", "パソコンの キー", "けんばんに A S D F… の もじを かきます（パソコンの キーボードで ひけます）", () => SaveSystem.Settings.pcKeyLabels, v => SaveSystem.Settings.pcKeyLabels = v);
            {
                var row = Row(c, "はいけい", null);
                var wrap = UIParts.El("grid", row.control);
                var sw = new List<Button>();
                for (int i = 0; i < Themes.Count; i++)
                {
                    int k = i;
                    var b = new Button(() =>
                    {
                        Click();
                        SaveSystem.Settings.theme = k;
                        SaveSystem.ApplySettings();
                    }) { text = Themes.Names[i] };
                    b.AddToClassList("theme-swatch");
                    b.style.backgroundColor = Color.Lerp(Themes.Top[i], Themes.Bottom[i], 0.35f);
                    wrap.Add(b);
                    sw.Add(b);
                }
                _settingControls["theme"] = wrap;
                _settingRefresh.Add(() => { for (int i = 0; i < sw.Count; i++) sw[i].EnableInClassList("theme-swatch--on", i == SaveSystem.Settings.theme); });
            }

            // ---------------- おうちの かたへ ----------------
            Section(c, "おうちの かたへ", "sec_parents", "parents");
            UIParts.Text("スタンプは、ピアノの けんばんを 1にちに 10かい おすと 1こ もらえます（1にち 1こまで）。" +
                         "5こめ・10こめ… は でっかい スタンプです。スタンプは 365しゅるい あります。\n" +
                         "「まねっこ」は おてほんを きいて まねする あそび、「えんそう」は ひかる けんばんを いっしょに ひく あそびです。" +
                         "どちらも まちがいの すくなさで ほしが 1〜3こ つきます。「じゆうに ひく」では ひいた きょくを ろくおん できます。\n" +
                         "せってい・スタンプ・ほし・ろくおんは、この たんまつ（ブラウザ）に ほぞんされます。", "parents-text", c);
            Toggle(c, "keepAwake", "がめんを くらく しない", "ピアノで あそんでいる あいだ、スリープ しません（たいおう している ブラウザだけ）", () => SaveSystem.Settings.keepAwake, v => SaveSystem.Settings.keepAwake = v);
            Toggle(c, "vibrate", "ぶるっと ふるえる", "スタンプや ほしを もらったとき（たいおう している スマホだけ）", () => SaveSystem.Settings.vibrate, v => SaveSystem.Settings.vibrate = v);
            {
                var labels = new List<string>();
                foreach (int m in PiamoSettings.PlayLimitChoices) labels.Add(m == 0 ? "なし" : MinutesText(m));
                Choice(c, "playLimit", "あそぶ じかんの めやす", "この じかん あそぶと「すこし きゅうけい しようね」と おしらせします", labels.ToArray(),
                    () => Mathf.Max(0, Array.IndexOf(PiamoSettings.PlayLimitChoices, SaveSystem.Settings.playLimit)),
                    v => SaveSystem.Settings.playLimit = PiamoSettings.PlayLimitChoices[v]);
            }
            {
                var row = Row(c, "せっていを もとに もどす", "スタンプ・ほし・ろくおんは そのままです");
                UIParts.Candy("もとに もどす", "white", null, () =>
                {
                    Click();
                    Confirm("せっていを はじめの じょうたいに もどしますか？", "もどす", () =>
                    {
                        SaveSystem.ResetSettings();
                        RefreshSettings();
                        Toast("せっていを もとに もどしました");
                    });
                }, row.control);
            }
            {
                var row = Row(c, "スタンプと ほしを ぜんぶ けす", "2びょう ながおし すると けせます（もとに もどせません）");
                var hold = new HoldButton("ながおしで けす", "pink", 2f);
                hold.Completed += () => Confirm("スタンプと ほしを ぜんぶ けしますか？\nもとに もどせません。", "けす", () =>
                {
                    SaveSystem.ResetStamps();
                    Stamps = new StampBook(SaveSystem.Data);
                    RefreshMenu();
                    Toast("スタンプを けしました");
                });
                row.control.Add(hold);
                _settingControls["resetStamps"] = hold;
            }
            UIParts.Text($"Piamo（ぴあも） バージョン {Application.version}{BuildDateText()}\n" +
                         "おと・スタンプの え・けんばんの かたちは、すべて この アプリの ために つくりました（Blender / Python）。\n" +
                         "フォント：M PLUS Rounded 1c（SIL Open Font License 1.1）\n" +
                         "まねっこ・えんそうの きょくは、ちょさくけんの きれた むかしからの メロディです。", "credits", c);

            RefreshSettings();
        }

        /// <summary>スクロールバーは出さない（指や ホイールで うごかす）。たてだけに うごく。</summary>
        static void HideScrollers(ScrollView sv)
        {
            if (sv == null) return;
            sv.mode = ScrollViewMode.Vertical;
            sv.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            sv.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        }

        static string OctaveName(int midi) => $"{NoteUtil.Name(midi, LabelMode.Katakana)}{NoteUtil.Octave(midi)}";

        void ShiftOctave(int d)
        {
            var s = SaveSystem.Settings;
            s.octaveShift = KeyboardLayout.ClampShift(s.octaves, s.octaveShift + d);
            SaveSystem.ApplySettings();
        }

        public void RefreshSettings()
        {
            foreach (var a in _settingRefresh) a();
        }

        (VisualElement row, VisualElement control) Row(VisualElement parent, string label, string sub)
        {
            var row = UIParts.El("row", parent);
            var text = UIParts.El("row__text", row);
            UIParts.Text(label, "row__label", text);
            if (!string.IsNullOrEmpty(sub)) UIParts.Text(sub, "row__sub", text);
            var control = UIParts.El("row__control", row);
            return (row, control);
        }

        void Section(VisualElement parent, string title, string icon, string key)
        {
            var t = UIParts.El("section-title", parent);
            t.name = "section-" + key;
            var i = UIParts.El("icon", t);
            UIParts.SetIcon(i, Icon(icon));
            UIParts.Text(title, null, t);
            _sectionTitles[key] = t;
        }

        /// <summary>その セクションの 見出しが いちばん上に くるように スクロール。</summary>
        public void ScrollToSection(string key)
        {
            if (!_sectionTitles.TryGetValue(key, out var t)) return;
            // ならびおわる まえ（ひらいた すぐ あと）なら、すこし まってから
            if (t.layout.height < 1f)
            {
                _settingsScroll.schedule.Execute(() => ScrollToSection(key)).ExecuteLater(30);
                return;
            }
            _settingsScroll.scrollOffset = new Vector2(0f, Mathf.Max(0f, t.layout.y - 10f));
        }

        /// <summary>せっていを ひらいたときの 見出し：ピアノの まねっこ・えんそう からなら その セクション。</summary>
        void ScrollSettingsForContext()
        {
            if (Current == Page.Play && Mode == PlayMode.Mimic) ScrollToSection("mimic");
            else if (Current == Page.Play && Mode == PlayMode.Perform) ScrollToSection("perform");
            else _settingsScroll.scrollOffset = Vector2.zero;
        }

        /// <summary>「15ふん」「30ぷん」（よみかたで ふん・ぷん が かわる）。</summary>
        public static string MinutesText(int minutes)
        {
            int d = minutes % 10;
            bool pun = d == 0 || d == 1 || d == 3 || d == 4 || d == 6 || d == 8;
            return $"{minutes}{(pun ? "ぷん" : "ふん")}";
        }

        /// <summary>ビルドした日（Resources/build_info.txt）。なければ から。</summary>
        static string BuildDateText()
        {
            var t = Resources.Load<TextAsset>("build_info");
            string d = t != null ? t.text.Trim() : "";
            return string.IsNullOrEmpty(d) ? "" : $"（{d}）";
        }

        void Toggle(VisualElement parent, string key, string label, string sub, Func<bool> get, Action<bool> set, string on = "オン", string off = "オフ")
        {
            var row = Row(parent, label, sub);
            var sw = new Switch(get(), on, off);
            sw.Changed += v =>
            {
                Click();
                set(v);
                SaveSystem.ApplySettings();
            };
            row.control.Add(sw.WithText());
            _settingControls[key] = sw;
            _settingRefresh.Add(() => sw.Set(get(), false));
        }

        void Choice(VisualElement parent, string key, string label, string sub, string[] labels, Func<int> get, Action<int> set)
        {
            var row = Row(parent, label, sub);
            var seg = new Segmented(labels, get());
            seg.Changed += v =>
            {
                Click();
                set(v);
                SaveSystem.ApplySettings();
            };
            row.control.Add(seg);
            _settingControls[key] = seg;
            _settingRefresh.Add(() => seg.Select(get(), false));
        }
    }
}
