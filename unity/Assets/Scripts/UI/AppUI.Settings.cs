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

        static readonly string[] OctaveCountLabels = { "8", "15", "22", "29", "36" };

        public VisualElement SettingControl(string key) => _settingControls.TryGetValue(key, out var e) ? e : null;

        void BuildSettings()
        {
            _settingsScroll = _root.Q<ScrollView>("settings-scroll");
            HideScrollers(_settingsScroll);
            SetIcon("settings-back-icon", "back");
            _root.Q<Button>("settings-back").clicked += CloseSettings;
            var c = _settingsScroll.contentContainer;
            var s = SaveSystem.Settings;

            // ---------------- おと ----------------
            Section(c, "おと", "sec_sound");
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
            Section(c, "けんばん", "sec_keys");
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
            Section(c, "まねっこ", "sec_mimic");
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

            // ---------------- がめん ----------------
            Section(c, "がめん", "sec_screen");
            Toggle(c, "noteEffects", "おんぷが とぶ", "けんばんを おすと ♪ が とびだします", () => SaveSystem.Settings.noteEffects, v => SaveSystem.Settings.noteEffects = v);
            Toggle(c, "uiSounds", "ボタンの おと", null, () => SaveSystem.Settings.uiSounds, v => SaveSystem.Settings.uiSounds = v);
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
            Section(c, "おうちの かたへ", "sec_parents");
            UIParts.Text("スタンプは、ピアノの けんばんを 1にちに 10かい おすと 1こ もらえます（1にち 1こまで）。" +
                         "5こめ・10こめ… は でっかい スタンプです。スタンプは 365しゅるい あります。" +
                         "せっていと スタンプは、この たんまつ（ブラウザ）に ほぞんされます。", "parents-text", c);
            {
                var row = Row(c, "せっていを もとに もどす", "スタンプは そのままです");
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
                var row = Row(c, "スタンプを ぜんぶ けす", "2びょう ながおし すると けせます（もとに もどせません）");
                var hold = new HoldButton("ながおしで けす", "pink", 2f);
                hold.Completed += () => Confirm("スタンプを ぜんぶ けしますか？\nもとに もどせません。", "けす", () =>
                {
                    SaveSystem.ResetStamps();
                    Stamps = new StampBook(SaveSystem.Data);
                    RefreshMenu();
                    Toast("スタンプを けしました");
                });
                row.control.Add(hold);
                _settingControls["resetStamps"] = hold;
            }
            UIParts.Text($"Piamo（ぴあも） バージョン {Application.version}\n" +
                         "おと・スタンプの え・けんばんの かたちは、すべて この アプリの ために つくりました（Blender / Python）。\n" +
                         "フォント：M PLUS Rounded 1c（SIL Open Font License 1.1）\n" +
                         "まねっこの きょくは、ちょさくけんの きれた むかしからの メロディです。", "credits", c);

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

        void Section(VisualElement parent, string title, string icon)
        {
            var t = UIParts.El("section-title", parent);
            var i = UIParts.El("icon", t);
            UIParts.SetIcon(i, Icon(icon));
            UIParts.Text(title, null, t);
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
