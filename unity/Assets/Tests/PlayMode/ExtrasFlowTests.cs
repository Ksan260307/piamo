using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static Piamo.Tests.AppHarness;

namespace Piamo.Tests
{
    /// <summary>まねっこ：おてほんの とちゅうでも うけつける・ほし・つぎの きょく など。</summary>
    public class MimicExtrasFlowTests
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

        static IEnumerator StartSong(string id)
        {
            Click("mimic-song-" + id);
            yield return Frames(2);
            Assert.IsNotNull(UI.Mimic);
            Assert.AreEqual(id, UI.Mimic.Song.id);
        }

        /// <summary>つぎの ひとまとまりの はじめの音（なければ -1）。</summary>
        static int NextRoundFirstNote()
        {
            var g = UI.Mimic;
            if (g.RoundIndex + 1 >= g.Rounds.Count) return -1;
            foreach (var n in g.Rounds[g.RoundIndex + 1])
                if (!n.IsRest) return n.midi + g.Transpose;
            return -1;
        }

        [UnityTest]
        public IEnumerator TheRightFirstNoteDuringTheDemoCountsRightAway()
        {
            yield return StartSong("twinkle");
            yield return WaitUntil(() => UI.Mimic.Current == MimicGame.State.Demo, 8f, "おてほん");
            yield return Tap(UI.Mimic.RoundNote(0));
            Assert.AreEqual(MimicGame.State.Listen, UI.Mimic.Current, "おてほんを とめて まねする番");
            Assert.AreEqual(1, UI.Mimic.Progress, "おした 音も かぞえる");
            Assert.Less(UI.Mimic.DemoPlayed, UI.Mimic.RoundLength, "のこりの おてほんは 鳴らさない");
            yield return Frames(2);
            Assert.AreEqual("on", UI.MimicDotState(0));
            Assert.AreEqual("now", UI.MimicDotState(1), "つぎに ひく ドット");
        }

        [UnityTest]
        public IEnumerator WrongNotesDuringTheDemoAreIgnored()
        {
            yield return StartSong("twinkle");
            yield return WaitUntil(() => UI.Mimic.Current == MimicGame.State.Demo, 8f, "おてほん");
            yield return Tap(71);
            Assert.AreEqual(MimicGame.State.Demo, UI.Mimic.Current, "おてほんは つづく");
            Assert.AreEqual(0, UI.Mimic.Mistakes, "まちがいに しない");
        }

        [UnityTest]
        public IEnumerator PlayingStraightThroughNeedsNoWaitingAndEarnsThreeStars()
        {
            yield return StartSong("frog");
            float t = 0f;
            while (UI.Mimic.Current != MimicGame.State.Finished)
            {
                var g = UI.Mimic;
                switch (g.Current)
                {
                    case MimicGame.State.Intro:
                    case MimicGame.State.Demo:
                        yield return Tap(g.RoundNote(0));
                        break;
                    case MimicGame.State.Listen:
                        yield return Tap(g.Expected);
                        break;
                    case MimicGame.State.Cleared:
                        int next = NextRoundFirstNote();
                        if (next >= 0) yield return Tap(next);
                        else yield return null;
                        break;
                    default:
                        yield return null;
                        break;
                }
                t += UnityEngine.Time.unscaledDeltaTime;
                Assert.Less(t, 60f, "おわらない");
            }
            Assert.AreEqual(0, UI.Mimic.Mistakes);
            Assert.AreEqual(3, UI.Mimic.Stars);
            Assert.AreEqual(3, SaveSystem.Data.MimicStarsFor("frog"), "ほしが のこる");
            yield return Frames(2);
            Assert.IsTrue(Visible("mimic-next"), "つぎの きょく");
            Assert.IsFalse(Visible("mimic-dots"));
            Assert.AreEqual("clear", Audio.LastSfx);
            Click("mimic-next");
            yield return Frames(2);
            Assert.AreEqual("sheep", UI.Mimic.Song.id);
            Assert.IsFalse(Visible("mimic-next"));
            Click("mimic-change");
            yield return Frames(2);
            var card = Button("mimic-song-frog");
            Assert.IsFalse(card.Q(className: "song-card__stars").ClassListContains("hidden"), "カードに ほし");
            Assert.IsTrue(Button("mimic-song-sheep").ClassListContains("song-card--last"), "さいごに えらんだ 曲");
        }

        [UnityTest]
        public IEnumerator ThreeWrongNotesInARowShowTheHintWithItsName()
        {
            yield return StartSong("twinkle");
            AppUI.MimicTimeScale = 3f;
            yield return WaitUntil(() => UI.Mimic.Current == MimicGame.State.Listen, 15f, "まねする番");
            Assert.AreEqual(-1, Play.view.HintKey);
            for (int i = 0; i < MimicGame.HintAfterMistakes; i++) yield return Tap(71);
            yield return Frames(2);
            Assert.AreEqual(60, Play.view.HintKey, "3 かい まちがえたら すぐ ヒント");
            StringAssert.Contains("「ド」", UI.MimicBubbleText);
        }

        [UnityTest]
        public IEnumerator SpeedChipChangesTheTempo()
        {
            yield return StartSong("twinkle");
            Click("mimic-speed");
            yield return Frames(1);
            Assert.AreEqual(2, SaveSystem.Settings.mimicSpeed);
            Assert.AreEqual(1.3f, UI.Mimic.TempoScale, 1e-4f);
            Assert.AreEqual("はやい", Root.Q<Label>("mimic-speed-label").text);
            Click("mimic-speed");
            yield return Frames(1);
            Assert.AreEqual(0, SaveSystem.Settings.mimicSpeed, "ゆっくり に もどる");
            Assert.Less(UI.Mimic.TempoScale, 1f);
        }

        [UnityTest]
        public IEnumerator SettingsOpenAtTheMimicSection()
        {
            yield return StartSong("twinkle");
            UI.OpenSettings();
            yield return Seconds(0.3f);
            Assert.AreEqual("mimic", UI.SettingsSectionInView);
            UI.CloseSettings();
            yield return Frames(2);
        }
    }

    /// <summary>じゆうに ひく：ろくおん・和音・グリッサンド・ペダル・パソコンの キー。</summary>
    public class FreeExtrasFlowTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return Boot();
            yield return OpenPlay();
        }

        [TearDown]
        public void TearDown() => AppHarness.TearDown();

        [UnityTest]
        public IEnumerator RecordAndPlayBack()
        {
            Assert.AreEqual(AppUI.PlayMode.Free, UI.Mode);
            Assert.IsFalse(Visible("free-playback"), "ろくおんが ないうちは ▶ は ない");
            Click("free-rec");
            yield return Frames(1);
            Assert.IsTrue(UI.FreeRecorder.Recording);
            yield return Tap(60);
            yield return Tap(64);
            Click("free-rec");
            yield return Frames(1);
            Assert.IsFalse(UI.FreeRecorder.Recording);
            Assert.AreEqual(4, UI.FreeRecorder.Events.Count, "おした・はなした");
            Assert.AreEqual(12, SaveSystem.Data.recording.Count, "ほぞん");
            Assert.IsTrue(Visible("free-playback"));

            Audio.ResetCounters();
            Click("free-playback");
            yield return Frames(1);
            Assert.IsTrue(UI.FreeRecorder.Playing);
            yield return WaitUntil(() => !UI.FreeRecorder.Playing, 10f, "さいせい");
            Assert.GreaterOrEqual(Audio.NotesPlayed, 2, "ひいた 音が なる");
            StringAssert.Contains("じょうずに", UI.FreeBubbleText);
            Assert.IsFalse(Play.view.IsPressed(60), "さいごは はなす");
        }

        [UnityTest]
        public IEnumerator ThreeKeysTogetherNameTheChord()
        {
            Touch(1, 60);
            Touch(2, 64);
            Touch(3, 67);
            yield return Frames(3);
            StringAssert.Contains("ドミソ", UI.FreeBubbleText);
            StringAssert.Contains("あかるい", UI.FreeBubbleText);
            Release(1);
            Release(2);
            Release(3);
            yield return Frames(2);
        }

        [UnityTest]
        public IEnumerator SlidingAcrossTheKeysIsAGlissando()
        {
            foreach (int m in new[] { 60, 62, 64, 65, 67, 69 })
            {
                Touch(1, m);
                yield return Frames(2);
            }
            Release(1);
            yield return Frames(2);
            StringAssert.Contains("グリッサンド", UI.FreeBubbleText);
        }

        [UnityTest]
        public IEnumerator TappingAScaleIsNotAGlissando()
        {
            // ひとつずつ おして はなす（ゆびが すべっていない）
            foreach (int m in new[] { 60, 62, 64, 65, 67, 69 }) yield return Tap(m);
            yield return Frames(2);
            StringAssert.DoesNotContain("グリッサンド", UI.FreeBubbleText);
        }

        [UnityTest]
        public IEnumerator PedalChipTurnsSustainOnAndOff()
        {
            Assert.IsFalse(SaveSystem.Settings.sustain);
            Click("free-pedal");
            yield return Frames(1);
            Assert.IsTrue(SaveSystem.Settings.sustain);
            Assert.AreEqual("ペダル オン", Root.Q<Label>("free-pedal-label").text);
            Click("free-pedal");
            yield return Frames(1);
            Assert.IsFalse(SaveSystem.Settings.sustain);
        }

        [UnityTest]
        public IEnumerator PcKeyLettersCanBeShownOnTheKeys()
        {
            Assert.IsNull(UI.PcLetterOn(60));
            SaveSystem.Settings.pcKeyLabels = true;
            SaveSystem.ApplySettings();
            yield return Frames(2);
            Assert.AreEqual("A", UI.PcLetterOn(60));
            Assert.AreEqual("W", UI.PcLetterOn(61));
        }
    }

    /// <summary>えんそう：とめたら つづきから（3・2・1）・れんぞく・ヒント・けっか。</summary>
    public class PerformExtrasFlowTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return Boot();
            yield return OpenPlay();
            Click("tab-perform");
            yield return Frames(2);
        }

        [TearDown]
        public void TearDown() => AppHarness.TearDown();

        static IEnumerator Begin(string id, int mode)
        {
            SaveSystem.Settings.performMode = mode;
            SaveSystem.ApplySettings();
            Click("perform-song-" + id);
            yield return Frames(3);
            Assert.AreEqual(id, UI.PerformSong.id);
        }

        [UnityTest]
        public IEnumerator PausingShowsResumeAndCountsBackIn()
        {
            yield return Begin("twinkle", 1);
            AppUI.PerformTimeScale = 2f;
            yield return WaitUntil(() => UI.Perform.Beat > 0.5f, 10f, "ひいている");
            Click("perform-play");
            yield return Frames(2);
            Assert.IsTrue(UI.Perform.Paused);
            Assert.IsTrue(Visible("perform-paused"), "「つづきから」");
            Click("perform-resume");
            yield return Frames(2);
            Assert.IsFalse(Visible("perform-paused"));
            Assert.IsTrue(UI.Perform.Resuming, "3・2・1 と かぞえてから");
            Assert.IsTrue(Visible("perform-count"));
            Assert.AreEqual("3", Root.Q<Label>("perform-count").text);
            float beat = UI.Perform.Beat;
            yield return WaitUntil(() => !UI.Perform.Resuming, 10f, "つづき");
            yield return Seconds(0.2f);
            Assert.Greater(UI.Perform.Beat, beat, "また すすむ");
        }

        [UnityTest]
        public IEnumerator StepModeCountsTheComboAndPointsAfterWaiting()
        {
            yield return Begin("twinkle", 2);
            var notes = UI.Perform.Song.notes;
            for (int i = 0; i < AppUI.ComboShowFrom; i++) yield return Tap(notes[i].midi);
            yield return Frames(2);
            Assert.AreEqual($"{AppUI.ComboShowFrom} れんぞく！", UI.PerformComboText);
            AppUI.PerformTimeScale = 4f;
            int next = notes[UI.Perform.NextIndex].midi;
            yield return WaitUntil(() => Play.view.HintKey == next, 10f, "しばらく まつと 矢じるし");
            yield return Tap(next);
            yield return Frames(2);
            Assert.AreEqual(-1, Play.view.HintKey, "おしたら きえる");
        }

        [UnityTest]
        public IEnumerator FinishingShowsTheRecordAndTheNextSong()
        {
            yield return Begin("twinkle", 2);
            foreach (var n in UI.Perform.Song.notes.ToList()) yield return Tap(n.midi);
            yield return WaitUntil(() => UI.Perform.Current == PerformGame.State.Finished, 10f, "おわり");
            yield return Frames(2);
            Assert.IsTrue(Visible("perform-result-record"));
            Assert.AreEqual("はじめての ほし！", Root.Q<Label>("perform-result-record").text);
            Assert.IsFalse(Visible("perform-slow"), "ほし 3 つなら ゆっくりは すすめない");
            Assert.IsTrue(Visible("perform-next"));
            int i = PerformSongs.All.FindIndex(s => s.id == "twinkle");
            string nextId = PerformSongs.All[(i + 1) % PerformSongs.All.Count].id;
            Click("perform-next");
            yield return Frames(3);
            Assert.AreEqual(nextId, UI.PerformSong.id);
            Click("perform-change");
            yield return Frames(2);
            Assert.IsTrue(Button("perform-song-" + nextId).ClassListContains("song-card--last"));
            Assert.IsTrue(Visible("perform-total"));
            StringAssert.Contains("3こ", Root.Q<Label>("perform-total").text);
        }

        [UnityTest]
        public IEnumerator BarLinesFallWithTheNotes()
        {
            yield return Begin("twinkle", 0);
            AppUI.PerformTimeScale = 2f;
            yield return WaitUntil(() => UI.Perform.Beat > 1f, 10f, "ひいている");
            yield return Frames(2);
            Assert.Greater(UI.BarLinesVisible, 0, "小節の 線");
        }
    }

    /// <summary>メニュー・せってい・ブラウザ：あいさつ・きょうの すすみ・とぶ ボタン・きゅうけい・もどる。</summary>
    public class AppExtrasFlowTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => Boot();

        [TearDown]
        public void TearDown() => AppHarness.TearDown();

        [UnityTest]
        public IEnumerator MenuGreetsAndShowsTodaysProgress()
        {
            string text = Root.Q<Label>("menu-today").text;
            Assert.IsTrue(text.StartsWith("おはよう！") || text.StartsWith("こんにちは！") || text.StartsWith("こんばんは！"), text);
            Assert.IsFalse(Visible("menu-meter"), "まだ ひいていない");
            yield return OpenPlay();
            for (int i = 0; i < 3; i++) yield return Tap(60 + i * 2);
            Click("play-back");
            yield return Frames(2);
            Assert.IsTrue(Visible("menu-meter"));
            Assert.AreEqual(3, Root.Q("menu-meter").Children().Count(d => d.ClassListContains("meter-dot--on")));
        }

        [UnityTest]
        public IEnumerator ScreenStaysOnOnlyWhilePlaying()
        {
            Assert.IsFalse(WebBridge.KeepAwakeRequested);
            yield return OpenPlay();
            Assert.IsTrue(WebBridge.KeepAwakeRequested, "ピアノの あいだは スリープしない");
            SaveSystem.Settings.keepAwake = false;
            SaveSystem.ApplySettings();
            Assert.IsFalse(WebBridge.KeepAwakeRequested, "せっていで やめられる");
            SaveSystem.Settings.keepAwake = true;
            SaveSystem.ApplySettings();
            Click("play-back");
            yield return Frames(2);
            Assert.IsFalse(WebBridge.KeepAwakeRequested);
        }

        [UnityTest]
        public IEnumerator BrowserBackActsLikeTheAppsBack()
        {
            yield return OpenPlay();
            UI.OpenSettings();
            yield return Frames(2);
            UI.OnBrowserBack();
            yield return Frames(2);
            Assert.IsFalse(UI.SettingsOpen);
            Assert.AreEqual(AppUI.Page.Play, UI.Current);
            UI.OnBrowserBack();
            yield return Frames(2);
            Assert.AreEqual(AppUI.Page.Menu, UI.Current);
        }

        [UnityTest]
        public IEnumerator SectionChipsJumpToTheirSection()
        {
            UI.OpenSettings();
            yield return Seconds(0.3f);
            Assert.AreEqual("sound", UI.SettingsSectionInView, "メニューからは いちばん上");
            Click("section-chip-perform");
            yield return Seconds(0.2f);
            Assert.AreEqual("perform", UI.SettingsSectionInView);
            foreach (var key in new[] { "performWindow", "reduceMotion", "pcKeyLabels", "keepAwake", "vibrate", "playLimit" })
                Assert.IsNotNull(UI.SettingControl(key), key);
            ((Segmented)UI.SettingControl("playLimit")).Select(1, true);
            Assert.AreEqual(15, SaveSystem.Settings.playLimit);
        }

        [UnityTest]
        public IEnumerator PlayTimeReminderAsksForABreak()
        {
            SaveSystem.Settings.playLimit = 15;
            SaveSystem.ApplySettings();
            yield return OpenPlay();
            UI.PlayTime = 15 * 60f - 0.05f;
            yield return Frames(5);
            Assert.IsTrue(UI.DialogOpen, "きゅうけいの おしらせ");
            StringAssert.Contains("15ふん", Root.Q<Label>("dialog-text").text);
            Click("dialog-no");
            yield return Frames(2);
            Assert.IsFalse(UI.DialogOpen);
            Assert.AreEqual(AppUI.Page.Play, UI.Current, "「もうすこし」で つづけられる");
            Assert.Less(UI.PlayTime, 60f, "また はじめから かぞえる");
        }

        [UnityTest]
        public IEnumerator ThePianoReopensInTheLastMode()
        {
            yield return OpenPlay();
            Click("tab-perform");
            yield return Frames(2);
            Click("play-back");
            yield return Frames(2);
            yield return Boot(keepSave: true);
            yield return OpenPlay();
            Assert.AreEqual(AppUI.PlayMode.Perform, UI.Mode, "さいごに あそんだ モード");
            Assert.IsTrue(Visible("perform-picker"));
        }

        [UnityTest]
        public IEnumerator StampDetailStepsToTheNeighbours()
        {
            var book = UI.Stamps;
            for (int i = 0; i < 2; i++)
            {
                Today = AppClock.AddDays(Day0, i - 2);
                for (int k = 0; k < StampBook.PlaysForStamp; k++) book.RecordPlay(Today);
                book.Press();
            }
            Today = Day0;
            Click("btn-stamps");
            yield return Frames(3);
            UI.TapSlot(1);
            yield return Frames(2);
            Assert.IsTrue(UI.DetailVisible);
            Assert.IsFalse(Button("detail-prev").enabledSelf, "1 こめの まえは ない");
            Click("detail-next");
            yield return Frames(2);
            StringAssert.Contains(StampCatalog.Get(StampBook.DesignFor(2)).name, Root.Q<Label>("detail-name").text);
            StringAssert.Contains("ようび", Root.Q<Label>("detail-info").text);
            Assert.IsFalse(Button("detail-next").enabledSelf, "さいごの スタンプ");
            Click("detail-prev");
            yield return Frames(2);
            StringAssert.Contains(StampCatalog.Get(StampBook.DesignFor(1)).name, Root.Q<Label>("detail-name").text);
        }
    }
}
