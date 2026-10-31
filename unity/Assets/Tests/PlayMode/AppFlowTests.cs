using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static Piamo.Tests.AppHarness;

namespace Piamo.Tests
{
    /// <summary>画面のながれ：メニュー・もどる・せってい。</summary>
    public class NavigationTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => Boot();

        [TearDown]
        public void TearDown() => AppHarness.TearDown();

        [UnityTest]
        public IEnumerator StartsOnTheMenu()
        {
            Assert.AreEqual(AppUI.Page.Menu, UI.Current);
            Assert.IsTrue(Visible("menu"));
            Assert.IsFalse(Visible("play"));
            Assert.IsFalse(Visible("stamps"));
            Assert.IsFalse(Visible("settings"));
            Assert.IsFalse(Play.cam.enabled, "メニューでは 3D を描かない");
            yield break;
        }

        [UnityTest]
        public IEnumerator EveryScreenHasABackButtonToTheMenu()
        {
            Click("btn-play");
            yield return Frames(2);
            Assert.AreEqual(AppUI.Page.Play, UI.Current);
            Assert.IsTrue(Play.cam.enabled);
            Click("play-back");
            yield return Frames(2);
            Assert.AreEqual(AppUI.Page.Menu, UI.Current);

            Click("btn-stamps");
            yield return Frames(2);
            Assert.AreEqual(AppUI.Page.Stamps, UI.Current);
            Assert.IsTrue(Visible("stamps"));
            Click("stamps-back");
            yield return Frames(2);
            Assert.AreEqual(AppUI.Page.Menu, UI.Current);

            Click("btn-settings");
            yield return Frames(2);
            Assert.IsTrue(UI.SettingsOpen);
            Assert.IsTrue(Visible("settings"));
            Click("settings-back");
            yield return Frames(2);
            Assert.IsFalse(UI.SettingsOpen);
            Assert.AreEqual(AppUI.Page.Menu, UI.Current);
        }

        [UnityTest]
        public IEnumerator SettingsCanBeOpenedWhilePlayingAndGoBackToThePiano()
        {
            yield return OpenPlay();
            Assert.IsTrue(Play.InputActive);
            Click("play-settings");
            yield return Frames(2);
            Assert.IsTrue(UI.SettingsOpen);
            Assert.AreEqual(AppUI.Page.Play, UI.Current, "うしろは ピアノの画面のまま");
            Assert.IsFalse(Play.InputActive, "せっていの あいだは 鍵盤が ならない");
            Click("settings-back");
            yield return Frames(2);
            Assert.IsFalse(UI.SettingsOpen);
            Assert.AreEqual(AppUI.Page.Play, UI.Current, "せっていの「もどる」は ピアノへ");
            Assert.IsTrue(Play.InputActive);
        }

        [UnityTest]
        public IEnumerator BackKeyClosesOverlaysFirst()
        {
            yield return OpenPlay();
            Click("play-settings");
            yield return Frames(1);
            UI.Back();
            yield return Frames(1);
            Assert.IsFalse(UI.SettingsOpen);
            Assert.AreEqual(AppUI.Page.Play, UI.Current);
            UI.Back();
            yield return Frames(1);
            Assert.AreEqual(AppUI.Page.Menu, UI.Current);
        }
    }

    /// <summary>ピアノとして ひく。</summary>
    public class PianoPlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return Boot();
            yield return OpenPlay();
            Audio.ResetCounters();
        }

        [TearDown]
        public void TearDown() => AppHarness.TearDown();

        [UnityTest]
        public IEnumerator TappingAKeyPlaysItsNote()
        {
            Touch(1, 64);
            yield return Frames(3);
            Assert.AreEqual(64, Audio.LastNote);
            Assert.IsTrue(Play.view.IsPressed(64));
            Assert.IsTrue(Audio.IsSounding(64));
            yield return Seconds(0.1f);
            Assert.Greater(Play.view.PressAmount(64), 0.9f, "鍵盤が しずむ");
            Release(1);
            yield return Frames(3);
            Assert.IsFalse(Play.view.IsPressed(64));
            yield return Seconds(0.6f);
            Assert.IsFalse(Audio.IsSounding(64), "はなすと音が消える（ペダルなし）");
        }

        [UnityTest]
        public IEnumerator EveryKeyCanBeTapped()
        {
            foreach (var k in Play.Layout.Keys)
            {
                yield return Tap(k.midi);
                Assert.AreEqual(k.midi, Audio.LastNote, $"{NoteUtil.Name(k.midi, LabelMode.Katakana)} ({k.midi})");
            }
            Assert.AreEqual(Play.Layout.Keys.Count, Audio.NotesPlayed);
        }

        [UnityTest]
        public IEnumerator ChordWithManyFingers()
        {
            Touch(1, 60);
            Touch(2, 64);
            Touch(3, 67);
            Touch(4, 70);
            yield return Frames(3);
            foreach (int m in new[] { 60, 64, 67, 70 }) Assert.IsTrue(Play.view.IsPressed(m), m.ToString());
            Assert.GreaterOrEqual(Audio.ActiveVoices, 4);
            Release(2);
            yield return Frames(2);
            Assert.IsFalse(Play.view.IsPressed(64));
            Assert.IsTrue(Play.view.IsPressed(60), "ほかの指は おしたまま");
        }

        [UnityTest]
        public IEnumerator SlidingAFingerPlaysEachKey()
        {
            Vector2 a = Play.KeyCenterScreen(60), b = Play.KeyCenterScreen(72);
            for (int i = 0; i <= 40; i++)
            {
                TouchAt(1, Vector2.Lerp(a, b, i / 40f));
                yield return null;
            }
            Assert.AreEqual(72, Audio.LastNote);
            Assert.GreaterOrEqual(Audio.NotesPlayed, 8, "グリッサンドで 白鍵が つぎつぎ なる");
            int pressed = Play.Layout.Keys.Count(k => Play.view.IsPressed(k.midi));
            Assert.AreEqual(1, pressed, "ゆびの下の鍵盤だけが しずむ");
            Release(1);
            yield return Frames(2);
        }

        [UnityTest]
        public IEnumerator AVeryQuickTapStillPlays()
        {
            // おして はなすのが 1 フレームのうちに おわっても（重い端末・はやいタップ）、音は なる
            PianoInput.VirtualQuickTaps.Add(Play.KeyCenterScreen(65));
            yield return null;
            Assert.AreEqual(65, Audio.LastNote);
            Assert.IsTrue(Play.view.IsPressed(65));
            yield return Frames(2);
            Assert.IsFalse(Play.view.IsPressed(65), "つぎの フレームで はなす");
            Assert.IsFalse(Play.IsHeld(65));
            var r = UI.ScreenRectOf(Root.Q("play-settings"));
            PianoInput.VirtualQuickTaps.Add(r.center);
            yield return Frames(2);
            Assert.AreEqual(1, Audio.NotesPlayed, "ボタンの上の はやいタップでは ならない");
        }

        [UnityTest]
        public IEnumerator TouchesThatStartOnButtonsDoNotPlay()
        {
            var r = UI.ScreenRectOf(Root.Q("play-settings"));
            TouchAt(1, r.center);
            yield return Frames(3);
            Assert.AreEqual(0, Audio.NotesPlayed);
            Release(1);
            yield return Frames(2);
        }

        [UnityTest]
        public IEnumerator SustainKeepsTheSoundAfterRelease()
        {
            ((Switch)UI.SettingControl("sustain")).Set(true, true);
            yield return Frames(2);
            Touch(1, 60);
            yield return Frames(3);
            Release(1);
            yield return Seconds(0.5f);
            Assert.IsTrue(Audio.IsSounding(60), "おとを のばす：はなしても なっている");
        }

        [UnityTest]
        public IEnumerator SettingsChangeTheKeyboard()
        {
            Assert.AreEqual(8, Play.Layout.WhiteCount, "はじめは ド〜ド の 8 けん");
            ((Segmented)UI.SettingControl("octaves")).Select(2, true);
            yield return Frames(2);
            Assert.AreEqual(22, Play.Layout.WhiteCount, "3 オクターブ");
            Assert.AreEqual(48, Play.Layout.Low);

            ((Switch)UI.SettingControl("blackKeys")).Set(false, true);
            yield return Frames(2);
            Assert.IsTrue(Play.Layout.Keys.All(k => !k.black), "くろい けんばん なし");

            SaveSystem.Settings.octaveShift = 1;
            SaveSystem.ApplySettings();
            yield return Frames(2);
            Assert.AreEqual(60, Play.Layout.Low, "オクターブを あげる");

            UI.SelectInstrument("harp");
            yield return Frames(2);
            Assert.AreEqual("harp", Audio.CurrentInstrument);

            ((Segmented)UI.SettingControl("labels")).Select((int)LabelMode.None, true);
            yield return Frames(2);
            Assert.AreEqual(0, Root.Query(className: "key-label").ToList().Count, "おんめい なし");
            ((Segmented)UI.SettingControl("labels")).Select((int)LabelMode.Letters, true);
            yield return Frames(2);
            Assert.IsTrue(Root.Query<Label>(className: "key-label").ToList().Any(l => l.text == "C"));

            ((Segmented)UI.SettingControl("octaves")).Select(0, true);
            yield return Frames(2);
            Assert.AreEqual(8, Play.Layout.WhiteCount);
            Assert.AreEqual(72, Play.Layout.Low, "1 オクターブに もどしても、オクターブの せっていは のこる");

            // 保存されている
            SaveSystem.Load();
            Assert.AreEqual("harp", SaveSystem.Settings.instrument);
            Assert.AreEqual(LabelMode.Letters, SaveSystem.Settings.labels);
        }

        [UnityTest]
        public IEnumerator ManyKeysScrollWithTheNavigator()
        {
            SaveSystem.Settings.octaves = 5;
            SaveSystem.Settings.keySize = 2;
            SaveSystem.ApplySettings();
            yield return Frames(4);
            Assert.IsTrue(Play.Framing.scrolling, "5 オクターブ・大きい鍵盤は スクロール");
            Assert.IsTrue(Visible("nav"));
            Assert.Greater(Play.Framing.keyPx, 0f);
            float before = Play.Offset;
            Click("nav-right");
            yield return Frames(2);
            Assert.Greater(Play.Offset, before);
            Click("nav-left");
            Click("nav-left");
            Click("nav-left");
            Click("nav-left");
            Click("nav-left");
            Click("nav-left");
            yield return Frames(2);
            Assert.AreEqual(0f, Play.Offset, 1e-3f);
            // 見えていない鍵盤は、画面のエリアの外
            var range = Play.VisibleWhiteRange();
            int hidden = Play.Layout.WhiteMidi(range.y + 2);
            Assert.IsFalse(Play.Area.Contains(Play.KeyCenterScreen(hidden)));
        }
    }

    /// <summary>スタンプちょう。</summary>
    public class StampFlowTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => Boot();

        [TearDown]
        public void TearDown() => AppHarness.TearDown();

        [UnityTest]
        public IEnumerator TenKeysEarnTodaysStampAndYouPressIt()
        {
            yield return OpenPlay();
            for (int i = 0; i < 9; i++) yield return Tap(60 + (i % 5) * 2);
            Assert.AreEqual(0, UI.Stamps.PendingCount);
            Assert.IsFalse(Visible("earn-banner"));
            yield return Tap(72);
            Assert.AreEqual(1, UI.Stamps.PendingCount, "10 かい で きょうの スタンプ");
            Assert.IsTrue(Visible("earn-banner"));
            Assert.AreEqual("earn", Audio.LastSfx);
            for (int i = 0; i < 20; i++) yield return Tap(64);
            Assert.AreEqual(1, UI.Stamps.PendingCount, "1 日に 1 こ");

            Click("earn-go");
            yield return Frames(3);
            Assert.AreEqual(AppUI.Page.Stamps, UI.Current);
            Assert.IsTrue(UI.TapNextSlot());
            Assert.IsTrue(UI.PressAnimating);
            yield return WaitUntil(() => !UI.PressAnimating, 5f, "スタンプを おす");
            Assert.AreEqual(1, UI.Stamps.Total);
            Assert.AreEqual(1, UI.LastPressedCount);
            Assert.AreEqual("stamp", Audio.LastSfx);
            Assert.IsFalse(UI.CelebrateVisible, "1 こめは ふつうの スタンプ");

            // 保存されている
            SaveSystem.Load();
            CollectionAssert.AreEqual(new[] { Today }, SaveSystem.Data.stampDays);
        }

        [UnityTest]
        public IEnumerator FifthStampIsBig()
        {
            var book = UI.Stamps;
            for (int i = 0; i < 4; i++)
            {
                Today = AppClock.AddDays(Day0, i - 4);
                for (int k = 0; k < StampBook.PlaysForStamp; k++) book.RecordPlay(Today);
                book.Press();
            }
            Today = Day0;
            yield return OpenPlay();
            for (int i = 0; i < StampBook.PlaysForStamp; i++) yield return Tap(67);
            Click("play-back");
            yield return Frames(2);
            Click("btn-stamps");
            yield return Frames(3);
            UI.TapNextSlot();
            yield return WaitUntil(() => !UI.PressAnimating, 5f, "でっかい スタンプ");
            Assert.AreEqual(5, UI.LastPressedCount);
            Assert.AreEqual("stamp_big", Audio.LastSfx);
            Assert.IsTrue(UI.CelebrateVisible, "でっかい スタンプの おいわい");
        }

        [UnityTest]
        public IEnumerator TappingAPressedStampShowsItsDetails()
        {
            var book = UI.Stamps;
            for (int k = 0; k < StampBook.PlaysForStamp; k++) book.RecordPlay(Today);
            book.Press();
            Click("btn-stamps");
            yield return Frames(3);
            UI.TapSlot(1);
            yield return Frames(2);
            Assert.IsTrue(UI.DetailVisible);
            StringAssert.Contains(StampCatalog.Get(1).name, Root.Q<Label>("detail-name").text);
            StringAssert.Contains("10がつ 31にち", Root.Q<Label>("detail-info").text);
            yield return WaitUntil(() => UI.stampImages.TryGet(1, out _), 5f, "スタンプの絵");
            Click("detail-close");
            yield return Frames(2);
            Assert.IsFalse(UI.DetailVisible);
        }

        [UnityTest]
        public IEnumerator CannotPressWithoutPlaying()
        {
            Click("btn-stamps");
            yield return Frames(3);
            UI.TapSlot(1);
            yield return Frames(2);
            Assert.AreEqual(0, UI.Stamps.Total);
            Assert.IsFalse(UI.PressAnimating);
        }
    }

    /// <summary>まねっこモード。</summary>
    public class MimicFlowTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return Boot();
            yield return OpenPlay();
            Click("tab-mimic");
            yield return Frames(2);
        }

        [TearDown]
        public void TearDown() => AppHarness.TearDown();

        IEnumerator StartFirstSong()
        {
            var list = Root.Q<ScrollView>("song-list");
            var card = list.contentContainer.Children().OfType<Button>().First();
            Click(card);
            yield return Frames(2);
            Assert.IsNotNull(UI.Mimic);
            Assert.AreEqual("twinkle", UI.Mimic.Song.id);
        }

        [UnityTest]
        public IEnumerator ListenThenCopyTheSample()
        {
            yield return StartFirstSong();
            Audio.ResetCounters();
            AppUI.MimicTimeScale = 3f;
            yield return WaitUntil(() => UI.Mimic.Current == MimicGame.State.Listen, 15f, "まねする番");
            Assert.GreaterOrEqual(Audio.NotesPlayed, 3, "おてほんが なった");
            int round = UI.Mimic.RoundIndex;
            while (UI.Mimic.Current == MimicGame.State.Listen)
                yield return Tap(UI.Mimic.Expected);
            Assert.AreEqual(MimicGame.State.Cleared, UI.Mimic.Current);
            Assert.AreEqual("success", Audio.LastSfx);
            yield return WaitUntil(() => UI.Mimic.RoundIndex == round + 1, 10f, "つぎの ひとまとまり");
        }

        [UnityTest]
        public IEnumerator HintLightsTheRightKeyAfterTenSeconds()
        {
            yield return StartFirstSong();
            AppUI.MimicTimeScale = 5f;
            yield return WaitUntil(() => UI.Mimic.Current == MimicGame.State.Listen, 15f, "まねする番");
            Assert.AreEqual(-1, Play.view.HintKey, "すぐには ひからない");
            // まねっこの中の時間（はやめてある）で はかる
            yield return WaitUntil(() => UI.Mimic.WaitTime >= 8f, 10f, "8 びょう まよう");
            Assert.AreEqual(-1, Play.view.HintKey, "8 びょうでは まだ ひからない");
            yield return WaitUntil(() => Play.view.HintKey >= 0, 10f, "ヒント");
            Assert.GreaterOrEqual(UI.Mimic.WaitTime, 10f, "ひかるのは 10 びょう まよってから");
            Assert.AreEqual(UI.Mimic.Expected, Play.view.HintKey, "正解の鍵盤が ひかる");
            Assert.AreEqual(60, Play.view.HintKey);
            yield return Frames(2);
            Assert.AreEqual("hint", Audio.LastSfx);
            yield return Tap(60);
            yield return Frames(2);
            Assert.AreEqual(-1, Play.view.HintKey, "おしたら きえる");
        }

        [UnityTest]
        public IEnumerator WrongKeysJustKeepWaiting()
        {
            yield return StartFirstSong();
            AppUI.MimicTimeScale = 3f;
            yield return WaitUntil(() => UI.Mimic.Current == MimicGame.State.Listen, 15f, "まねする番");
            yield return Tap(71);
            Assert.AreEqual(0, UI.Mimic.Progress);
            Assert.AreEqual(MimicGame.State.Listen, UI.Mimic.Current);
            Assert.AreEqual(71, Audio.LastNote, "まちがえても、ピアノの音は なる");
        }

        [UnityTest]
        public IEnumerator ReplayAndChangeSong()
        {
            yield return StartFirstSong();
            AppUI.MimicTimeScale = 3f;
            yield return WaitUntil(() => UI.Mimic.Current == MimicGame.State.Listen, 15f, "まねする番");
            Click("mimic-replay");
            yield return Frames(1);
            Assert.AreEqual(MimicGame.State.Demo, UI.Mimic.Current, "もういちど きく");
            Click("mimic-change");
            yield return Frames(1);
            Assert.IsNull(UI.Mimic);
            Assert.IsTrue(Visible("song-picker"));
        }

        [UnityTest]
        public IEnumerator LeavingThePianoStopsTheSong()
        {
            yield return StartFirstSong();
            Click("play-back");
            yield return Frames(2);
            Assert.IsNull(UI.Mimic);
            Assert.AreEqual(AppUI.Page.Menu, UI.Current);
        }
    }
}
