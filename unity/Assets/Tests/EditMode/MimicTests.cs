using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Piamo.Tests
{
    public class SongsTests
    {
        [Test]
        public void AllSongsFitTheSmallestKeyboard()
        {
            var k = KeyboardLayout.Create(1, 0, false);   // ド〜ド・白鍵だけ
            foreach (var s in Songs.All)
            {
                if (s.random) continue;
                Assert.Greater(s.phrases.Count, 0, s.id);
                foreach (var n in s.AllNotes())
                {
                    if (n.IsRest) continue;
                    Assert.IsTrue(k.Contains(n.midi), $"{s.title}: {n.midi} が 1 オクターブの白鍵にない");
                    Assert.IsFalse(NoteUtil.IsBlack(n.midi), s.title);
                    Assert.Greater(n.beats, 0f);
                }
            }
        }

        [Test]
        public void SongsHaveUniqueIdsAndTitles()
        {
            Assert.AreEqual(Songs.All.Count, Songs.All.Select(s => s.id).Distinct().Count());
            Assert.AreEqual(Songs.All.Count, Songs.All.Select(s => s.title).Distinct().Count());
            Assert.GreaterOrEqual(Songs.All.Count(s => !s.exercise), 8);
        }

        [Test]
        public void Twinkle()
        {
            var s = Songs.Find("twinkle");
            var first = s.phrases[0].Select(n => n.midi).ToArray();
            CollectionAssert.AreEqual(new[] { 60, 60, 67, 67, 69, 69, 67 }, first);
        }

        [Test]
        public void ParsePitch()
        {
            Assert.AreEqual(60, Songs.ParsePitch("C4"));
            Assert.AreEqual(61, Songs.ParsePitch("C#4"));
            Assert.AreEqual(70, Songs.ParsePitch("Bb4"));
            Assert.AreEqual(72, Songs.ParsePitch("C5"));
            var n = Songs.ParseNote("E4:0.5");
            Assert.AreEqual(64, n.midi);
            Assert.AreEqual(0.5f, n.beats);
            Assert.IsTrue(Songs.ParseNote("R:2").IsRest);
        }
    }

    public class MimicGameTests
    {
        static SongDef Simple => new SongDef
        {
            id = "t", title = "てすと", animal = "cat", bpm = 120,
            phrases = new List<List<SongNote>>
            {
                new List<SongNote> { N(60), N(62), N(64) },
                new List<SongNote> { N(64), N(62), N(60) },
            },
        };

        static SongNote N(int m, float b = 1f) => new SongNote { midi = m, beats = b };

        static void Run(MimicGame g, float seconds, float step = 0.02f)
        {
            for (float t = 0; t < seconds; t += step) g.Update(step);
        }

        static void ToListen(MimicGame g)
        {
            for (int i = 0; i < 2000 && g.Current != MimicGame.State.Listen; i++) g.Update(0.02f);
            Assert.AreEqual(MimicGame.State.Listen, g.Current);
        }

        [Test]
        public void DemoPlaysEveryNoteThenWaitsForTheChild()
        {
            var g = new MimicGame();
            var on = new List<int>();
            var off = new List<int>();
            g.DemoNoteOn += (m, d) => on.Add(m);
            g.DemoNoteOff += m => off.Add(m);
            g.Start(Simple, 1, 1f, 10f, 0);
            Assert.AreEqual(MimicGame.State.Intro, g.Current);
            Assert.AreEqual(MimicGame.PressResult.Ignored, g.Press(65), "おてほんの前の ちがう音は 数えない");
            Assert.AreEqual(0, g.Mistakes);
            ToListen(g);
            CollectionAssert.AreEqual(new[] { 60, 62, 64 }, on);
            Assert.AreEqual(60, g.Expected);
            Run(g, 1f);
            CollectionAssert.AreEquivalent(new[] { 60, 62, 64 }, off, "まねする番に なっても、のこりの おてほんの音は 鳴らしおえて 止める");
        }

        [Test]
        public void ListeningStartsAsSoonAsTheLastDemoNoteBegins()
        {
            var g = new MimicGame();
            int lastOn = -1;
            bool lastOff = false;
            g.DemoNoteOn += (m, d) => lastOn = m;
            g.DemoNoteOff += m => { if (m == 64) lastOff = true; };
            g.Start(Simple, 1, 1f, 10f, 0);
            ToListen(g);
            Assert.AreEqual(64, lastOn, "さいごの音（ミ）が 鳴りはじめている");
            Assert.IsFalse(lastOff, "まだ 鳴っている うちに まねする番");
            Assert.AreEqual(3, g.DemoPlayed);
            Assert.AreEqual(MimicGame.PressResult.Correct, g.Press(60), "鳴っている とちゅうでも 数える");
        }

        [Test]
        public void TheRightFirstNoteDuringTheDemoStopsItAndCounts()
        {
            var g = new MimicGame();
            var on = new List<int>();
            var off = new List<int>();
            g.DemoNoteOn += (m, d) => on.Add(m);
            g.DemoNoteOff += m => off.Add(m);
            g.Start(Simple, 1, 1f, 10f, 0);
            Run(g, MimicGame.IntroSeconds + 0.1f);
            Assert.AreEqual(MimicGame.State.Demo, g.Current);
            Assert.AreEqual(MimicGame.PressResult.Ignored, g.Press(71), "おてほん中の ちがう音は 数えない");
            Assert.AreEqual(MimicGame.State.Demo, g.Current, "おてほんも 止めない");
            Assert.AreEqual(0, g.Mistakes);
            Assert.AreEqual(MimicGame.PressResult.Correct, g.Press(60), "はじめの音（ド）なら まねする番へ");
            Assert.AreEqual(MimicGame.State.Listen, g.Current);
            Assert.AreEqual(1, g.Progress);
            CollectionAssert.Contains(off, 60, "鳴っていた おてほんは 止める");
            int before = on.Count;
            Run(g, 3f);
            Assert.AreEqual(before, on.Count, "のこりの おてほんは 鳴らさない");
            Assert.AreEqual(62, g.Expected);
        }

        [Test]
        public void TheFirstNoteDuringTheIntroSkipsTheDemo()
        {
            var g = new MimicGame();
            int demo = 0;
            g.DemoNoteOn += (m, d) => demo++;
            g.Start(Simple, 1, 1f, 10f, 0);
            Assert.AreEqual(MimicGame.PressResult.Correct, g.Press(60));
            Assert.AreEqual(MimicGame.State.Listen, g.Current);
            Run(g, 2f);
            Assert.AreEqual(0, demo, "おてほんは とばす");
        }

        [Test]
        public void TheNextFirstNoteDuringCelebrationGoesStraightOn()
        {
            var g = new MimicGame();
            g.Start(Simple, 1, 1f, 10f, 0);
            ToListen(g);
            foreach (int m in new[] { 60, 62, 64 }) g.Press(m);
            Assert.AreEqual(MimicGame.State.Cleared, g.Current);
            Assert.AreEqual(MimicGame.PressResult.Ignored, g.Press(60), "つぎの はじめの音で なければ まつ");
            Assert.AreEqual(MimicGame.PressResult.Correct, g.Press(64), "つぎの まとまりの はじめの音（ミ）");
            Assert.AreEqual(1, g.RoundIndex);
            Assert.AreEqual(1, g.Progress);
            Assert.AreEqual(MimicGame.State.Listen, g.Current);
            g.Press(62);
            Assert.AreEqual(MimicGame.PressResult.RoundComplete, g.Press(60));
            Run(g, MimicGame.ClearedSeconds + 0.1f);
            Assert.AreEqual(MimicGame.State.Finished, g.Current);
            Assert.AreEqual(3, g.Stars, "まちがい なし");
        }

        [Test]
        public void ThreeWrongNotesInARowShowTheHint()
        {
            var g = new MimicGame();
            g.Start(Simple, 1, 1f, 10f, 0);
            ToListen(g);
            g.Press(71);
            g.Press(71);
            Assert.IsFalse(g.HintActive);
            g.Press(71);
            Assert.IsTrue(g.HintActive, "3 かい つづけて まちがえたら ヒント");
            Assert.AreEqual(60, g.HintKey);
            g.Press(60);
            Assert.IsFalse(g.HintActive);
            var off = new MimicGame();
            off.Start(Simple, 1, 1f, 0f, 0);
            ToListen(off);
            for (int i = 0; i < 5; i++) off.Press(71);
            Assert.IsFalse(off.HintActive, "ヒント なし の せってい");
        }

        [Test]
        public void StarsCountMistakes()
        {
            var g = new MimicGame();
            g.Start(Simple, 1, 1f, 10f, 0);
            Assert.AreEqual(0, g.Stars, "おわるまでは 0");
            foreach (var round in new[] { new[] { 60, 62, 64 }, new[] { 64, 62, 60 } })
            {
                ToListen(g);
                for (int i = 0; i < 4; i++) g.Press(71);
                foreach (int m in round) g.Press(m);
                Run(g, MimicGame.ClearedSeconds + 0.1f);
            }
            Assert.AreEqual(MimicGame.State.Finished, g.Current);
            Assert.AreEqual(8, g.Mistakes);
            Assert.AreEqual(1, g.Stars);
        }

        [Test]
        public void ChangingTheOctaveRestartsTheRoundHigher()
        {
            var g = new MimicGame();
            var on = new List<int>();
            g.DemoNoteOn += (m, d) => on.Add(m);
            g.Start(Simple, 1, 1f, 10f, 0);
            ToListen(g);
            g.Press(60);
            g.SetTranspose(12);
            Assert.AreEqual(MimicGame.State.Intro, g.Current);
            Assert.AreEqual(0, g.Progress);
            on.Clear();
            ToListen(g);
            CollectionAssert.AreEqual(new[] { 72, 74, 76 }, on);
            Assert.AreEqual(72, g.Expected);
        }

        [Test]
        public void CorrectNotesAdvanceAndWrongNotesDoNot()
        {
            var g = new MimicGame();
            g.Start(Simple, 1, 1f, 10f, 0);
            ToListen(g);
            Assert.AreEqual(MimicGame.PressResult.Correct, g.Press(60));
            Assert.AreEqual(MimicGame.PressResult.Wrong, g.Press(65));
            Assert.AreEqual(1, g.Progress, "まちがえても、はじめから に もどらない");
            Assert.AreEqual(62, g.Expected);
            Assert.AreEqual(MimicGame.PressResult.Correct, g.Press(62));
            bool cleared = false;
            g.RoundCleared += () => cleared = true;
            Assert.AreEqual(MimicGame.PressResult.RoundComplete, g.Press(64));
            Assert.IsTrue(cleared);
            Assert.AreEqual(1, g.Mistakes);
        }

        [Test]
        public void HintAfterTenSecondsOfHesitation()
        {
            var g = new MimicGame();
            int hinted = -1;
            g.HintShown += m => hinted = m;
            g.Start(Simple, 1, 1f, 10f, 0);
            ToListen(g);
            Run(g, 9.5f);
            Assert.IsFalse(g.HintActive, "10 びょう までは ひからない");
            Assert.AreEqual(-1, g.HintKey);
            Run(g, 0.6f);
            Assert.IsTrue(g.HintActive);
            Assert.AreEqual(60, g.HintKey, "正解の鍵盤が ひかる");
            Assert.AreEqual(60, hinted);
            g.Press(60);
            Assert.IsFalse(g.HintActive, "おしたら ヒントは きえる");
            Assert.AreEqual(62, g.Expected);
            Run(g, 10.1f);
            Assert.AreEqual(62, g.HintKey, "つぎの音でも、また 10 びょう で ひかる");
        }

        [Test]
        public void WrongNotesDoNotDelayTheHint()
        {
            var g = new MimicGame();
            g.Start(Simple, 1, 1f, 10f, 0);
            ToListen(g);
            for (int i = 0; i < 10; i++)
            {
                Run(g, 1.05f);
                g.Press(71);
            }
            Assert.IsTrue(g.HintActive, "まちがえてばかりでも、10 びょう で ヒント");
        }

        [Test]
        public void HintCanBeTurnedOff()
        {
            var g = new MimicGame();
            g.Start(Simple, 1, 1f, 0f, 0);
            ToListen(g);
            Run(g, 60f, 0.1f);
            Assert.IsFalse(g.HintActive);
        }

        [Test]
        public void ReplayRestartsTheRound()
        {
            var g = new MimicGame();
            g.Start(Simple, 1, 1f, 10f, 0);
            ToListen(g);
            g.Press(60);
            Run(g, 8f);
            g.ReplayDemo();
            Assert.AreEqual(MimicGame.State.Demo, g.Current);
            Assert.AreEqual(0, g.Progress);
            ToListen(g);
            Assert.AreEqual(0f, g.WaitTime, 0.05f, "もういちど きいたら、まよった時間も はじめから");
        }

        [Test]
        public void FinishesAfterTheLastRound()
        {
            var g = new MimicGame();
            bool done = false;
            g.SongFinished += () => done = true;
            g.Start(Simple, 1, 1f, 10f, 0);
            foreach (var round in new[] { new[] { 60, 62, 64 }, new[] { 64, 62, 60 } })
            {
                ToListen(g);
                foreach (int m in round) g.Press(m);
                Run(g, MimicGame.ClearedSeconds + 0.1f);
            }
            Assert.IsTrue(done);
            Assert.AreEqual(MimicGame.State.Finished, g.Current);
        }

        [Test]
        public void TransposeFollowsTheOctaveSetting()
        {
            var g = new MimicGame();
            var on = new List<int>();
            g.DemoNoteOn += (m, d) => on.Add(m);
            g.Start(Simple, 1, 1f, 10f, 12);
            ToListen(g);
            CollectionAssert.AreEqual(new[] { 72, 74, 76 }, on);
            Assert.AreEqual(72, g.Expected);
        }

        [Test]
        public void LengthModesSplitOrJoinPhrases()
        {
            var song = Songs.Find("twinkle");
            var shortRounds = MimicGame.BuildRounds(song, 0);
            Assert.AreEqual(12, shortRounds.Count, "7 音のフレーズは 4 + 3");
            Assert.IsTrue(shortRounds.All(r => r.Count(n => !n.IsRest) <= 4));
            Assert.AreEqual(6, MimicGame.BuildRounds(song, 1).Count);
            Assert.AreEqual(3, MimicGame.BuildRounds(song, 2).Count);
            int total = song.AllNotes().Count(n => !n.IsRest);
            foreach (int mode in new[] { 0, 1, 2 })
                Assert.AreEqual(total, MimicGame.BuildRounds(song, mode).Sum(r => r.Count(n => !n.IsRest)), "音が へったり ふえたり しない");
        }

        [Test]
        public void RandomRoundsGetLonger()
        {
            var g = new MimicGame(7);
            g.Start(Songs.Find("random"), 1, 1f, 10f, 0);
            var lengths = new List<int>();
            for (int r = 0; r < MimicGame.RandomRounds; r++)
            {
                ToListen(g);
                lengths.Add(g.RoundLength);
                foreach (var n in g.CurrentRound) g.Press(n.midi);
                Run(g, MimicGame.ClearedSeconds + 0.1f);
            }
            Assert.AreEqual(MimicGame.State.Finished, g.Current);
            Assert.AreEqual(3, lengths[0]);
            for (int i = 1; i < lengths.Count; i++) Assert.GreaterOrEqual(lengths[i], lengths[i - 1]);
        }

        [Test]
        public void SlowerTempoMakesALongerDemo()
        {
            var a = new MimicGame();
            a.Start(Simple, 1, 1f, 10f, 0);
            Run(a, MimicGame.IntroSeconds + 0.05f);
            var b = new MimicGame();
            b.Start(Simple, 1, 0.7f, 10f, 0);
            Run(b, MimicGame.IntroSeconds + 0.05f);
            Assert.Greater(b.DemoLength, a.DemoLength * 1.3f);
        }
    }
}
