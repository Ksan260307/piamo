using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Piamo.Tests
{
    public class PerformSongsTests
    {
        [Test]
        public void ThirtySongsInFourGroups()
        {
            Assert.AreEqual(30, PerformSongs.All.Count);
            Assert.AreEqual(30, PerformSongs.All.Select(s => s.id).Distinct().Count(), "id が かぶっている");
            Assert.AreEqual(30, PerformSongs.All.Select(s => s.title).Distinct().Count(), "なまえが かぶっている");
            foreach (PerformCategory c in System.Enum.GetValues(typeof(PerformCategory)))
                Assert.GreaterOrEqual(PerformSongs.All.Count(s => s.category == c), 3, c.ToString());
            Assert.AreEqual(PerformSongs.CategoryLabels.Length, System.Enum.GetValues(typeof(PerformCategory)).Length);
            // 曲えらびは なかまごとに ならぶ
            var cats = PerformSongs.All.Select(s => (int)s.category).ToList();
            for (int i = 1; i < cats.Count; i++) Assert.GreaterOrEqual(cats[i], cats[i - 1], PerformSongs.All[i].id);
        }

        [Test]
        public void EverySongTellsWhereItComesFrom()
        {
            foreach (var s in PerformSongs.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(s.source), s.id);
                Assert.IsFalse(string.IsNullOrEmpty(s.animal), s.id);
                Assert.That(s.stars, Is.InRange(1, 3), s.id);
                Assert.That(s.bpm, Is.InRange(50f, 160f), s.id);
            }
        }

        [Test]
        public void EveryBarHasTheRightLength()
        {
            foreach (var s in PerformSongs.All)
            {
                int bars = s.barStarts.Count;
                Assert.Greater(bars, 4, s.id);
                for (int i = 0; i < bars; i++)
                {
                    float len = s.BarLength(i);
                    if (i == 0 || i == bars - 1) Assert.LessOrEqual(len, s.BarBeats + 1e-4f, $"{s.title} {i + 1} 小節め");
                    else Assert.AreEqual(s.BarBeats, len, 1e-4f, $"{s.title} {i + 1} 小節め");
                }
                foreach (var n in s.notes) Assert.Greater(n.beats, 0f, s.id);
                for (int i = 1; i < s.notes.Count; i++)
                    Assert.GreaterOrEqual(s.notes[i].on, s.notes[i - 1].Off - 1e-4f, $"{s.title}: 音が かさなっている（{i}）");
            }
        }

        [Test]
        public void MelodiesStayBetweenMiddleCAndTwoOctavesUp()
        {
            // ばんそう（C2〜B3）と かさならず、いちばん多くても 2 オクターブの けんばんで ひける
            foreach (var s in PerformSongs.All)
            {
                Assert.GreaterOrEqual(s.Low, 60, s.title);
                Assert.LessOrEqual(s.High, 84, s.title);
            }
        }

        [Test]
        public void ChordsCoverEveryBarAndFitTheMelody()
        {
            foreach (var s in PerformSongs.All)
            {
                if (s.style == PerformStyle.None || s.style == PerformStyle.Drone) continue;
                // 和音の かわりめ・小節の はじめに鳴る メロディは、その和音の音
                foreach (var n in s.notes)
                {
                    foreach (var c in s.chords)
                    {
                        if (System.Math.Abs(c.on - n.on) > 1e-4f) continue;
                        Assert.Contains(NoteUtil.PitchClass(n.midi), c.tones, $"{s.title}: {n.bar + 1} 小節めの {n.midi} が {c.name} に あわない");
                    }
                }
                // 和音は かさならずに ならび、曲の おわりまで つづく（「R」の小節は ばんそうを 休む）
                for (int i = 1; i < s.chords.Count; i++)
                    Assert.GreaterOrEqual(s.chords[i].on, s.chords[i - 1].Off - 1e-4f, $"{s.title}: 和音が かさなる");
                Assert.AreEqual(s.Length, s.chords[s.chords.Count - 1].Off, 1e-4f, s.title);
            }
        }

        [Test]
        public void FamousOpenings()
        {
            int[] First(string id, int n) => PerformSongs.Find(id).notes.Take(n).Select(x => x.midi).ToArray();
            CollectionAssert.AreEqual(new[] { 60, 60, 67, 67, 69, 69, 67 }, First("twinkle", 7), "きらきらぼし");
            CollectionAssert.AreEqual(new[] { 60, 62, 64, 65, 64, 62, 60 }, First("kaeru", 7), "かえるのうた");
            CollectionAssert.AreEqual(new[] { 60, 60, 62, 60, 65, 64 }, First("birthday", 6), "ハッピーバースデー（ヘ長調）");
            CollectionAssert.AreEqual(new[] { 71, 71, 72, 74, 74, 72, 71, 69 }, First("ode", 8), "よろこびのうた（ト長調）");
            CollectionAssert.AreEqual(new[] { 76, 75, 76, 75, 76, 71, 74, 72, 69 }, First("elise", 9), "エリーゼのために");
            CollectionAssert.AreEqual(new[] { 64, 64, 64, 64, 64, 64, 64, 67, 60, 62, 64 }, First("jingle", 11), "ジングルベル");
            CollectionAssert.AreEqual(new[] { 67, 64, 64, 65, 62, 62 }, First("chocho", 6), "ちょうちょう");
        }

        [Test]
        public void PickupBarsAndTies()
        {
            var b = PerformSongs.Find("birthday");
            Assert.AreEqual(1f, b.BarLength(0), 1e-4f, "ハッピーバースデーは 弱起（1 拍）");
            Assert.AreEqual(1f, b.chords[0].on, 1e-4f, "弱起の小節には 和音が ない");
            // よろこびのうた の 4 つめの まとまりは、シを 小節を こえて のばす（2 拍）
            var ode = PerformSongs.Find("ode");
            var tied = ode.notes.First(n => n.bar == 11 && n.midi == 71);
            Assert.AreEqual(2f, tied.beats, 1e-4f);

            var t = PerformSongs.Make("t", "てすと", PerformCategory.World, "cat", "-", 1, 100, "4/4", PerformStyle.Pop, "C4 D4 E4 F4~ | F4 G4:3", "C | F:1 C:3");
            Assert.AreEqual(5, t.notes.Count);
            Assert.AreEqual(2f, t.notes[3].beats, 1e-4f, "F は つながって 2 拍");
            Assert.AreEqual(3, t.chords.Count);
        }

        [Test]
        public void ParseChord()
        {
            PerformSongs.ParseChord("G7", out int root, out int[] tones);
            Assert.AreEqual(7, root);
            CollectionAssert.AreEqual(new[] { 7, 11, 2, 5 }, tones);
            PerformSongs.ParseChord("Bb", out root, out tones);
            Assert.AreEqual(10, root);
            CollectionAssert.AreEqual(new[] { 10, 2, 5 }, tones);
            PerformSongs.ParseChord("Am", out root, out tones);
            CollectionAssert.AreEqual(new[] { 9, 0, 4 }, tones);
            PerformSongs.ParseChord("A5", out root, out tones);
            CollectionAssert.AreEqual(new[] { 9, 4 }, tones);
            Assert.Throws<System.ArgumentException>(() => PerformSongs.ParseChord("Cxyz", out root, out tones));
        }

        [Test]
        public void WrongNumberOfChordBarsIsRejected()
        {
            Assert.Throws<System.ArgumentException>(() =>
                PerformSongs.Make("t", "てすと", PerformCategory.World, "cat", "-", 1, 100, "4/4", PerformStyle.Pop, "C4:4 | D4:4", "C"));
        }
    }

    public class PerformAccompanimentTests
    {
        [Test]
        public void AccompanimentStaysBelowTheKeyboard()
        {
            foreach (var s in PerformSongs.All)
            {
                var acc = PerformAccompaniment.Build(s);
                foreach (var a in acc)
                {
                    Assert.That(a.midi, Is.InRange(36, 59), s.title);
                    Assert.Greater(a.beats, 0f, s.title);
                    Assert.That(a.velocity, Is.InRange(0.1f, 0.6f), s.title);
                    Assert.LessOrEqual(a.on + a.beats, s.Length + 1e-3f, s.title);
                }
                if (s.style == PerformStyle.None) Assert.IsEmpty(acc, s.title);
                else Assert.IsNotEmpty(acc, s.title);
            }
        }

        [Test]
        public void PopPatternIsBassChordFifthChord()
        {
            var acc = PerformAccompaniment.Build(PerformSongs.Find("twinkle"));
            int[] At(float beat) => acc.Where(a => System.Math.Abs(a.on - beat) < 1e-4f).Select(a => a.midi).OrderBy(m => m).ToArray();
            CollectionAssert.AreEqual(new[] { 36 }, At(0f), "1 拍め：ド（C2）");
            CollectionAssert.AreEqual(new[] { 48, 52, 55 }, At(1f), "2 拍め：ドミソ（C3）");
            CollectionAssert.AreEqual(new[] { 43 }, At(2f), "3 拍め：ソ（G2）");
            CollectionAssert.AreEqual(new[] { 48, 52, 55 }, At(3f));
            CollectionAssert.AreEqual(new[] { 41 }, At(4f), "2 小節めは ファの和音（F2）");
        }

        [Test]
        public void WaltzArpeggioAndElise()
        {
            var waltz = PerformAccompaniment.Build(PerformSongs.Find("minuet"));
            Assert.AreEqual(43, waltz.First(a => a.on == 0f).midi, "メヌエットは ソ（G2）から");
            Assert.AreEqual(3, waltz.Count(a => System.Math.Abs(a.on - 1f) < 1e-4f), "2 拍めは 和音");

            var arp = PerformAccompaniment.Build(PerformSongs.Find("silentnight"));
            var firstBar = arp.Where(a => a.on < 3f - 1e-4f).OrderBy(a => a.on).ToList();
            Assert.AreEqual(6, firstBar.Count, "8 分音符で 6 こ");
            Assert.AreEqual(36, firstBar[0].midi);

            var elise = PerformSongs.Find("elise");
            var lh = PerformAccompaniment.Build(elise);
            float bar2 = elise.barStarts[2];
            var notes = lh.Where(a => a.on >= bar2 - 1e-4f && a.on < bar2 + 1.5f - 1e-4f).OrderBy(a => a.on).Select(a => a.midi).ToArray();
            CollectionAssert.AreEqual(new[] { 45, 52, 57 }, notes, "ラ・ミ・ラ（A2 E3 A3）");
        }

        [Test]
        public void PickupHasNoAccompanimentBeforeTheFirstBar()
        {
            var acc = PerformAccompaniment.Build(PerformSongs.Find("birthday"));
            Assert.IsFalse(acc.Any(a => a.on < 1f - 1e-4f));
            Assert.AreEqual(41, acc.First().midi, "1 小節めは ファ（F2）");
        }
    }

    public class PerformLayoutTests
    {
        [Test]
        public void KeepsTheKeyboardWhenTheSongFits()
        {
            var s = new PiamoSettings();
            Assert.IsNull(PerformLayout.For(PerformSongs.Find("twinkle"), s));
            s.octaves = 5;
            s.octaveShift = 0;
            foreach (var song in PerformSongs.All) Assert.IsNull(PerformLayout.For(song, s), song.id);
        }

        [Test]
        public void AddsAnOctaveForBiggerSongs()
        {
            var r = PerformLayout.For(PerformSongs.Find("furusato"), new PiamoSettings());
            Assert.IsNotNull(r);
            Assert.AreEqual(2, r.Value.octaves);
            Assert.AreEqual(0, r.Value.shift);
        }

        [Test]
        public void ShowsBlackKeysWhenTheSongNeedsThem()
        {
            var s = new PiamoSettings { blackKeys = false };
            var r = PerformLayout.For(PerformSongs.Find("birthday"), s);
            Assert.IsNotNull(r);
            Assert.IsTrue(r.Value.blackKeys);
            Assert.AreEqual(1, r.Value.octaves);
            Assert.IsNull(PerformLayout.For(PerformSongs.Find("twinkle"), s), "白鍵だけの曲は そのまま");
        }

        [Test]
        public void MovesAShiftedKeyboardBack()
        {
            var s = new PiamoSettings { octaveShift = 1 };
            var r = PerformLayout.For(PerformSongs.Find("twinkle"), s);
            Assert.IsNotNull(r);
            Assert.AreEqual(1, r.Value.octaves);
            Assert.AreEqual(0, r.Value.shift);
        }

        [Test]
        public void EverySongFitsItsKeyboard()
        {
            foreach (var hide in new[] { false, true })
            {
                var s = new PiamoSettings { blackKeys = !hide };
                foreach (var song in PerformSongs.All)
                {
                    var r = PerformLayout.For(song, s);
                    var k = r == null ? KeyboardLayout.Create(s.octaves, s.octaveShift, s.blackKeys) : KeyboardLayout.Create(r.Value.octaves, r.Value.shift, r.Value.blackKeys);
                    foreach (var n in song.notes) Assert.IsTrue(k.Contains(n.midi), $"{song.title}: {n.midi}");
                    Assert.LessOrEqual(k.WhiteCount, 15, $"{song.title}: 2 オクターブまで");
                }
            }
        }
    }

    public class PerformGameTests
    {
        static PerformSong Simple => PerformSongs.Make("t", "てすと", PerformCategory.World, "cat", "-", 1, 120, "4/4", PerformStyle.Pop,
            "C4 D4 E4 F4 | G4:2 E4:2", "C | C");

        static void Run(PerformGame g, float seconds, float step = 0.01f)
        {
            for (float t = 0; t < seconds; t += step) g.Update(step);
        }

        [Test]
        public void CountsInThenPlaysEveryNoteOnTime()
        {
            var g = new PerformGame();
            var ticks = new List<int>();
            var ons = new List<(int, float)>();
            float clock = 0f;
            g.CountTick += n => ticks.Add(n);
            g.MelodyOn += (i, sec) => ons.Add((i, clock));
            g.Start(Simple, PerformGame.Mode.Listen, 1f, false, true);
            Assert.AreEqual(PerformGame.State.CountIn, g.Current);
            for (int k = 0; k < 800; k++)
            {
                g.Update(0.01f);
                clock += 0.01f;
            }
            CollectionAssert.AreEqual(new[] { 4, 3, 2, 1 }, ticks);
            Assert.AreEqual(6, ons.Count);
            float spb = 0.5f, count = 4 * spb;
            var song = Simple;
            for (int i = 0; i < ons.Count; i++)
            {
                Assert.AreEqual(i, ons[i].Item1);
                Assert.AreEqual(count + song.notes[i].on * spb, ons[i].Item2, 0.02f, $"{i} ばんめの音の時間");
            }
            Assert.AreEqual(PerformGame.State.Finished, g.Current);
            Assert.AreEqual(0, g.Stars, "きくは ほしなし");
        }

        [Test]
        public void SlowSpeedTakesLonger()
        {
            var g = new PerformGame();
            g.Start(Simple, PerformGame.Mode.Listen, 0.75f, false, true);
            Assert.AreEqual(60f / (120f * 0.75f), g.SecondsPerBeat, 1e-4f);
            g.SetTempo(1f);
            Assert.AreEqual(0.5f, g.SecondsPerBeat, 1e-4f);
        }

        [Test]
        public void AlongJudgesTimingAndMissesWithoutStopping()
        {
            var g = new PerformGame();
            var grades = new List<PerformGame.Grade>();
            g.Judged += (i, gr) => grades.Add(gr);
            g.Start(Simple, PerformGame.Mode.Along, 1f, false, true);
            Assert.AreEqual(PerformGame.PressResult.Ignored, g.Press(60), "カウント中は かぞえない");
            Run(g, 2f);                          // カウントが おわって ちょうど 1 つめ（ド）
            Assert.AreEqual(PerformGame.PressResult.Correct, g.Press(60));
            Assert.AreEqual(PerformGame.Grade.Perfect, g.Grades[0]);
            Run(g, 0.5f + 0.2f);                 // レ を 0.2 びょう おくれて
            Assert.AreEqual(PerformGame.PressResult.Wrong, g.Press(71), "ちがう音は あたりにならない");
            Assert.AreEqual(PerformGame.PressResult.Correct, g.Press(62));
            Assert.AreEqual(PerformGame.Grade.Good, g.Grades[1]);
            Run(g, 4.5f);                        // のこりは ひかない → ミス（曲は とまらない）
            Assert.AreEqual(PerformGame.State.Finished, g.Current);
            Assert.AreEqual(1, g.Perfect);
            Assert.AreEqual(1, g.Good);
            Assert.AreEqual(4, g.Missed);
            Assert.AreEqual(1, g.Stars, "6 この うち 2 こ");
            CollectionAssert.AreEqual(new[] { PerformGame.Grade.Perfect, PerformGame.Grade.Good }, grades.Take(2));
        }

        [Test]
        public void AlongAllHitsGivesThreeStars()
        {
            var g = new PerformGame();
            var song = Simple;
            g.Start(song, PerformGame.Mode.Along, 1f, true, true);
            int next = 0;
            for (int k = 0; k < 800 && g.Current != PerformGame.State.Finished; k++)
            {
                g.Update(0.01f);
                if (next < song.notes.Count && g.Beat >= song.notes[next].on) g.Press(song.notes[next++].midi);
            }
            Assert.AreEqual(PerformGame.State.Finished, g.Current);
            Assert.AreEqual(6, g.Perfect);
            Assert.AreEqual(3, g.Stars);
        }

        [Test]
        public void StepWaitsForTheLitKey()
        {
            var g = new PerformGame();
            var song = Simple;
            g.Start(song, PerformGame.Mode.Step, 1f, true, true);
            Assert.AreEqual(PerformGame.State.Playing, g.Current, "ひとつずつは カウントなし");
            Run(g, 5f);
            Assert.AreEqual(0, g.NextIndex, "おすまで まつ");
            Assert.AreEqual(song.notes[0].on, g.Beat, 1e-4f);
            Assert.AreEqual(PerformGame.PressResult.Wrong, g.Press(65));
            Assert.AreEqual(1, g.Mistakes);
            Assert.AreEqual(0, g.NextIndex);
            foreach (var n in song.notes)
            {
                Assert.AreEqual(PerformGame.PressResult.Correct, g.Press(n.midi));
                Run(g, 0.4f);
            }
            Run(g, 2f);
            Assert.AreEqual(PerformGame.State.Finished, g.Current);
            Assert.AreEqual(2, g.Stars, "6 この うち 1 かい まちがい");
        }

        [Test]
        public void PauseStopsTimeAndSound()
        {
            var g = new PerformGame();
            int sounding = 0;
            g.MelodyOn += (i, s) => sounding++;
            g.MelodyOff += i => sounding--;
            g.Start(Simple, PerformGame.Mode.Listen, 1f, false, true);
            Run(g, 2.1f);
            Assert.AreEqual(1, sounding);
            g.SetPaused(true);
            Assert.AreEqual(0, sounding, "とめると 音も とまる");
            float beat = g.Beat;
            Run(g, 3f);
            Assert.AreEqual(beat, g.Beat, 1e-5f);
            Assert.AreEqual(PerformGame.PressResult.Ignored, g.Press(60));
            g.SetPaused(false);
            Run(g, 0.5f);
            Assert.Greater(g.Beat, beat);
        }

        [Test]
        public void AccompanimentNotesAllEnd()
        {
            var g = new PerformGame();
            var on = new List<int>();
            int off = 0;
            g.AccOn += (a, s) => on.Add(a.midi);
            g.AccOff += a => off++;
            g.Start(PerformSongs.Find("twinkle"), PerformGame.Mode.Listen, 2f, true, true);
            Run(g, 40f, 0.02f);
            Assert.AreEqual(PerformGame.State.Finished, g.Current);
            Assert.AreEqual(g.AccNotes.Count, on.Count);
            Assert.AreEqual(on.Count, off, "鳴らした ばんそうは ぜんぶ 止まる");
        }

        [Test]
        public void AccompanimentCanBeTurnedOnMidSong()
        {
            var g = new PerformGame();
            var on = new List<AccNote>();
            g.AccOn += (a, s) => on.Add(a);
            g.Start(PerformSongs.Find("twinkle"), PerformGame.Mode.Listen, 1f, false, true);
            Run(g, 4f);
            Assert.IsEmpty(on);
            float beat = g.Beat;
            g.SetAccompaniment(true);
            Run(g, 1f);
            Assert.IsNotEmpty(on);
            Assert.IsTrue(on.All(a => a.on >= beat - 1e-3f), "とちゅうから。まえの音は ならさない");
        }

        [Test]
        public void RestartGoesBackToTheCount()
        {
            var g = new PerformGame();
            g.Start(Simple, PerformGame.Mode.Along, 1f, false, true);
            Run(g, 2.6f);
            g.Press(62);
            g.Restart();
            Assert.AreEqual(PerformGame.State.CountIn, g.Current);
            Assert.AreEqual(0, g.Perfect + g.Good + g.Missed);
            Assert.Less(g.Beat, 0f);
        }

        [Test]
        public void EverySongCanBePlayedThroughInEachMode()
        {
            foreach (var song in PerformSongs.All)
            {
                foreach (PerformGame.Mode mode in System.Enum.GetValues(typeof(PerformGame.Mode)))
                {
                    var g = new PerformGame();
                    g.Start(song, mode, 1f, true, true);
                    int next = 0;
                    for (int k = 0; k < 20000 && g.Current != PerformGame.State.Finished; k++)
                    {
                        g.Update(0.02f);
                        if (mode == PerformGame.Mode.Step && next < song.notes.Count) g.Press(song.notes[next++].midi);
                        else if (mode == PerformGame.Mode.Along && next < song.notes.Count && g.Beat >= song.notes[next].on) g.Press(song.notes[next++].midi);
                    }
                    Assert.AreEqual(PerformGame.State.Finished, g.Current, $"{song.title} {mode}");
                    if (mode != PerformGame.Mode.Listen) Assert.AreEqual(3, g.Stars, $"{song.title} {mode}");
                }
            }
        }
    }

    public class PerformSaveTests
    {
        [Test]
        public void StarsKeepTheBest()
        {
            var save = new PiamoSave();
            Assert.AreEqual(0, save.StarsFor("twinkle"));
            Assert.IsTrue(save.RecordStars("twinkle", 2));
            Assert.IsFalse(save.RecordStars("twinkle", 1), "わるくなったら のこさない");
            Assert.IsTrue(save.RecordStars("twinkle", 3));
            Assert.AreEqual(3, save.StarsFor("twinkle"));
            Assert.IsFalse(save.RecordStars("ode", 0));
            Assert.AreEqual(0, save.StarsFor("ode"));
        }

        [Test]
        public void BrokenStarsAreRepaired()
        {
            var save = new PiamoSave();
            save.performStars = new List<SongStars>
            {
                new SongStars { id = "twinkle", stars = 2 },
                new SongStars { id = "twinkle", stars = 3 },
                new SongStars { id = "", stars = 3 },
                null,
                new SongStars { id = "ode", stars = 9 },
            };
            save.Repair();
            Assert.AreEqual(2, save.performStars.Count);
            Assert.AreEqual(3, save.StarsFor("twinkle"));
            Assert.AreEqual(3, save.StarsFor("ode"));
            save.performStars = null;
            save.Repair();
            Assert.IsNotNull(save.performStars);
        }

        [Test]
        public void PerformSettingsAreClamped()
        {
            var s = new PiamoSettings { performMode = 7, performSpeed = -3 };
            s.Clamp();
            Assert.AreEqual(2, s.performMode);
            Assert.AreEqual(0, s.performSpeed);
            Assert.AreEqual(0.75f, s.PerformTempo, 1e-4f);
            var d = new PiamoSettings();
            Assert.AreEqual(1, d.performMode, "はじめは いっしょに");
            Assert.IsTrue(d.performMelody);
            Assert.IsTrue(d.performAccomp);
        }
    }
}
