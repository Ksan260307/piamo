using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static Piamo.Tests.AppHarness;

namespace Piamo.Tests
{
    /// <summary>えんそうモード（と、まねっこ との かかわり）。</summary>
    public class PerformFlowTests
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

        /// <summary>しかた（0 きく・1 いっしょに・2 ひとつずつ）を えらんで、曲の カードを おす。</summary>
        static IEnumerator Begin(string id, int mode)
        {
            SaveSystem.Settings.performMode = mode;
            SaveSystem.ApplySettings();
            Click("perform-song-" + id);
            yield return Frames(3);
            Assert.IsNotNull(UI.PerformSong);
            Assert.AreEqual(id, UI.PerformSong.id);
            Assert.IsTrue(Visible("perform-game"));
        }

        [UnityTest]
        public IEnumerator PickerShowsThirtySongsInGroups()
        {
            Assert.AreEqual(AppUI.PlayMode.Perform, UI.Mode);
            Assert.IsTrue(Visible("perform-picker"));
            Assert.IsFalse(Visible("perform-game"));
            var list = Root.Q<ScrollView>("perform-list");
            Assert.AreEqual(30, list.contentContainer.Children().OfType<Button>().Count());
            Assert.AreEqual(4, list.contentContainer.Children().OfType<Label>().Count(l => l.ClassListContains("perform-section")));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ListenPlaysTheSongAndPressesTheKeys()
        {
            yield return Begin("twinkle", 0);
            Assert.IsTrue(Visible("perform-count"), "はじめに カウント");
            AppUI.PerformTimeScale = 2f;
            Audio.ResetCounters();
            yield return WaitUntil(() => UI.Perform.Beat >= 0.3f, 10f, "カウントのあと");
            Assert.IsTrue(Play.view.IsPressed(60), "ドが しずんで ひかる");
            Assert.GreaterOrEqual(Audio.NotesPlayed, 2, "メロディと ばんそう");
            Assert.Greater(UI.LaneNotesVisible, 0, "おちてくる音");
            Assert.IsFalse(Visible("perform-count"));
            yield return WaitUntil(() => UI.Perform.Current == PerformGame.State.Finished, 40f, "おわり");
            yield return Frames(2);   // 見た目（resolvedStyle）が かわるのは つぎの フレーム
            Assert.IsTrue(Visible("perform-result"));
            Assert.AreEqual(0, SaveSystem.Data.StarsFor("twinkle"), "きくは ほしなし");
        }

        [UnityTest]
        public IEnumerator PlayingAlongOnTimeEarnsStars()
        {
            yield return Begin("mary", 1);
            AppUI.PerformTimeScale = 1.5f;
            var g = UI.Perform;
            var notes = g.Song.notes;
            yield return WaitUntil(() => g.Current == PerformGame.State.Playing, 10f, "カウント");
            for (int i = 0; i < notes.Count; i++)
            {
                int k = i;
                yield return WaitUntil(() => g.Beat >= notes[k].on - 0.05f, 20f, $"{k} ばんめの音");
                Assert.GreaterOrEqual(Play.view.GuideLevel(notes[k].midi), 0.3f, "ひく けんばんが ひかっている");
                yield return Tap(notes[k].midi);
            }
            yield return WaitUntil(() => g.Current == PerformGame.State.Finished, 20f, "おわり");
            yield return Frames(2);
            Assert.GreaterOrEqual(g.Perfect + g.Good, notes.Count - 1);
            Assert.GreaterOrEqual(g.Stars, 2);
            Assert.GreaterOrEqual(SaveSystem.Data.StarsFor("mary"), 2, "ほしが のこる");
            Assert.IsTrue(Visible("perform-result"));
            Click("perform-other");
            yield return Frames(2);
            Assert.IsTrue(Visible("perform-picker"));
            Assert.IsFalse(Root.Q("perform-song-mary").Q(className: "song-card__stars").ClassListContains("hidden"), "カードに ほし");
        }

        [UnityTest]
        public IEnumerator OneAtATimeWaitsForTheLitKey()
        {
            yield return Begin("twinkle", 2);
            var g = UI.Perform;
            yield return Frames(2);
            Assert.AreEqual(PerformGame.State.Playing, g.Current, "カウントなしで はじまる");
            Assert.AreEqual(1f, Play.view.GuideLevel(60), 0.01f, "さいしょの ドが ひかる");
            yield return Tap(71);
            Assert.AreEqual(0, g.NextIndex, "ちがう けんばんでは すすまない");
            Assert.AreEqual(71, Audio.LastNote, "まちがえても 音は なる");
            foreach (var n in g.Song.notes.ToList()) yield return Tap(n.midi);
            yield return WaitUntil(() => g.Current == PerformGame.State.Finished, 10f, "おわり");
            Assert.AreEqual(3, g.Stars);
            Assert.AreEqual(3, SaveSystem.Data.StarsFor("twinkle"));
        }

        [UnityTest]
        public IEnumerator BigSongsAddKeysOnlyWhileTheyPlay()
        {
            Assert.AreEqual(60, Play.Layout.Low);
            Assert.AreEqual(72, Play.Layout.High);
            yield return Begin("furusato", 1);
            Assert.AreEqual(60, Play.Layout.Low);
            Assert.AreEqual(84, Play.Layout.High, "2 オクターブに ふえる");
            Assert.AreEqual(1, SaveSystem.Settings.octaves, "せっていは かわらない");
            Assert.IsFalse(Visible("nav"), "えんそう中は スクロールの帯を出さない");
            Click("perform-change");
            yield return Frames(3);
            Assert.IsNull(UI.PerformSong);
            Assert.IsTrue(Visible("perform-picker"));
            Assert.AreEqual(72, Play.Layout.High, "もとの けんばんに もどる");
        }

        [UnityTest]
        public IEnumerator MimicAndPerformDoNotGetInEachOthersWay()
        {
            // えんそう（2 オクターブの曲）の とちゅうで まねっこへ
            yield return Begin("furusato", 1);
            AppUI.PerformTimeScale = 2f;
            yield return WaitUntil(() => UI.Perform.Beat > 1f, 15f, "えんそうの とちゅう");
            Assert.AreEqual(84, Play.Layout.High);
            Click("tab-mimic");
            yield return Frames(3);
            Assert.IsNull(UI.PerformSong, "えんそうは 止まる");
            Assert.AreEqual(PerformGame.State.Idle, UI.Perform.Current);
            Assert.AreEqual(72, Play.Layout.High, "けんばんは せっていの 1 オクターブに もどる");
            foreach (var k in Play.Layout.Keys)
            {
                Assert.IsFalse(Play.view.IsPressed(k.midi), $"{k.midi} が しずんだまま");
                Assert.AreEqual(0f, Play.view.GuideLevel(k.midi), $"{k.midi} が ひかったまま");
            }
            yield return Seconds(0.4f);
            Assert.AreEqual(0, Audio.ActiveVoices, "ばんそうも きえる");

            // まねっこは いつもどおり（こたえは まねっこが うけとり、えんそうは うごかない）
            var list = Root.Q<ScrollView>("song-list");
            Click(list.contentContainer.Children().OfType<Button>().First());
            yield return Frames(2);
            AppUI.MimicTimeScale = 3f;
            yield return WaitUntil(() => UI.Mimic.Current == MimicGame.State.Listen, 15f, "まねする番");
            Assert.IsTrue(Play.Layout.Contains(UI.Mimic.Expected));
            yield return Tap(UI.Mimic.Expected);
            Assert.AreEqual(1, UI.Mimic.Progress);
            Assert.AreEqual(PerformGame.State.Idle, UI.Perform.Current);
            Assert.AreEqual(0, Play.view.GuideLevel(60), "えんそうの ひかりは 出ない");

            // まねっこの とちゅうで えんそうへ
            Click("tab-perform");
            yield return Frames(2);
            Assert.IsNull(UI.Mimic, "まねっこは 止まる");
            Assert.AreEqual(-1, Play.view.HintKey, "まねっこの ヒントは きえる");
            yield return Begin("twinkle", 2);
            yield return Tap(60);
            Assert.AreEqual(1, UI.Perform.NextIndex, "えんそうの こたえは えんそうが うけとる");

            // せっていと きろくは べつべつ
            Assert.AreEqual(1, SaveSystem.Settings.octaves);
            Assert.AreEqual(0, SaveSystem.Settings.octaveShift);
            Assert.IsTrue(SaveSystem.Settings.blackKeys);
            Assert.IsEmpty(SaveSystem.Data.clearedSongs);
            Assert.IsEmpty(SaveSystem.Data.performStars);
        }

        [UnityTest]
        public IEnumerator ControlsSettingsAndLeaving()
        {
            yield return Begin("twinkle", 1);
            AppUI.PerformTimeScale = 2f;
            yield return WaitUntil(() => UI.Perform.Beat > 0.5f, 10f, "ひいている");

            UI.OpenSettings();
            yield return Frames(3);
            Assert.IsTrue(UI.Perform.Paused, "せっていの あいだは とまる");
            float beat = UI.Perform.Beat;
            yield return Seconds(0.3f);
            Assert.AreEqual(beat, UI.Perform.Beat, 1e-4f);
            UI.CloseSettings();
            yield return Frames(3);
            Assert.IsFalse(UI.Perform.Paused);

            Click("perform-play");
            yield return Frames(2);
            Assert.IsTrue(UI.Perform.Paused, "とめる");
            Click("perform-play");
            yield return Frames(2);
            Assert.IsFalse(UI.Perform.Paused, "つづきから");

            Click("perform-speed");
            yield return Frames(1);
            Assert.AreEqual(2, SaveSystem.Settings.performSpeed);
            Assert.AreEqual(1.2f, UI.Perform.TempoScale, 1e-4f, "とちゅうでも はやさが かわる");

            Click("perform-mode-step");
            yield return Frames(2);
            Assert.AreEqual(2, SaveSystem.Settings.performMode);
            Assert.AreEqual(PerformGame.Mode.Step, UI.Perform.CurrentMode, "しかたを かえると はじめから");
            Assert.AreEqual(0, UI.Perform.NextIndex);

            Click("play-back");
            yield return Frames(3);
            Assert.AreEqual(AppUI.Page.Menu, UI.Current);
            Assert.AreEqual(PerformGame.State.Idle, UI.Perform.Current);
            Assert.IsNull(UI.PerformSong);
        }
    }
}
