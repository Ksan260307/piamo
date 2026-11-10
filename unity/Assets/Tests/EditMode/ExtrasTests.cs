using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Piamo.Tests
{
    /// <summary>じゆうに ひく の ろくおん・さいせい（Recorder）。</summary>
    public class RecorderTests
    {
        [Test]
        public void RecordsWhatWasPlayedAndPlaysItBackOnTime()
        {
            var r = new Recorder();
            r.StartRecording();
            r.Update(1f);            // なにも ひいていない はじめの 1 びょうは のぞく
            r.Add(60, true);
            r.Update(0.5f);
            r.Add(60, false);
            r.Add(64, true);
            r.Update(0.5f);
            r.StopRecording();       // 64 は はなしていないので、ここで はなしたことに
            Assert.AreEqual(4, r.Events.Count);
            Assert.AreEqual(Recorder.LeadIn, r.Events[0].time, 1e-4f, "さいしょの 音の すこし前から");
            Assert.AreEqual(Recorder.LeadIn + 1f, r.Length, 1e-4f);

            var down = new List<int>();
            var up = new List<int>();
            int stopped = 0;
            r.Down += m => down.Add(m);
            r.Up += m => up.Add(m);
            r.Stopped += () => stopped++;
            r.StartPlayback();
            r.Update(Recorder.LeadIn - 0.01f);
            Assert.IsEmpty(down);
            r.Update(0.02f);
            CollectionAssert.AreEqual(new[] { 60 }, down);
            r.Update(0.5f);
            CollectionAssert.AreEqual(new[] { 60, 64 }, down);
            CollectionAssert.AreEqual(new[] { 60 }, up);
            r.Update(1f);
            CollectionAssert.AreEqual(new[] { 60, 64 }, up);
            Assert.IsFalse(r.Playing, "おわったら とまる");
            Assert.AreEqual(1, stopped);
        }

        [Test]
        public void StopsWhenFullOrTooLong()
        {
            var r = new Recorder();
            int stopped = 0;
            r.Stopped += () => stopped++;
            r.StartRecording();
            for (int i = 0; i < Recorder.MaxEvents; i++) r.Add(60, i % 2 == 0);
            Assert.IsFalse(r.Recording, "かずの 上限で とまる");
            Assert.AreEqual(1, stopped);
            Assert.AreEqual(Recorder.MaxEvents, r.Events.Count);

            r.StartRecording();
            r.Add(62, true);
            r.Update(Recorder.MaxSeconds + 1f);
            Assert.IsFalse(r.Recording, "ながさの 上限で とまる");
            var last = r.Events[r.Events.Count - 1];
            Assert.AreEqual(62, last.midi);
            Assert.IsFalse(last.down, "おしっぱなしの 音は はなす");
        }

        [Test]
        public void SavesAsNumbersAndRejectsBrokenData()
        {
            var r = new Recorder();
            r.StartRecording();
            r.Add(60, true);
            r.Update(0.25f);
            r.Add(60, false);
            r.StopRecording();
            var list = r.ToList();
            CollectionAssert.AreEqual(new[] { 300, 60, 1, 550, 60, 0 }, list);

            var r2 = new Recorder();
            r2.FromList(list);
            Assert.AreEqual(2, r2.Events.Count);
            Assert.AreEqual(0.55f, r2.Length, 1e-4f);
            r2.FromList(new List<int> { 300, 60 });
            Assert.IsFalse(r2.HasRecording, "3 つずつ でない");
            r2.FromList(new List<int> { 500, 60, 1, 100, 60, 0 });
            Assert.IsFalse(r2.HasRecording, "時間が もどる");
            r2.FromList(new List<int> { 0, 200, 1 });
            Assert.IsFalse(r2.HasRecording, "MIDI の はんいの そと");
            r2.FromList(null);
            Assert.IsFalse(r2.HasRecording);
        }

        [Test]
        public void StoppingPlaybackReleasesSoundingNotes()
        {
            var r = new Recorder();
            r.FromList(new List<int> { 300, 60, 1, 2000, 60, 0 });
            var up = new List<int>();
            r.Up += m => up.Add(m);
            r.StartPlayback();
            r.Update(0.5f);
            Assert.IsTrue(r.Playing);
            r.StopPlayback();
            CollectionAssert.AreEqual(new[] { 60 }, up, "鳴っている 音を とめる");
            Assert.IsFalse(r.Playing);
        }

        [Test]
        public void IgnoresKeysHeldFromBeforeAndPlaybackWhileRecording()
        {
            var r = new Recorder();
            r.StartRecording();
            r.Add(60, false);   // ろくおんの 前に おした けんばんを はなした
            Assert.IsFalse(r.HasRecording);
            r.Add(62, true);
            r.StartPlayback();
            Assert.IsFalse(r.Playing, "ろくおん中は さいせい しない");
        }
    }

    /// <summary>和音の なまえ（じゆうに ひく で ほめる）。</summary>
    public class ChordNameTests
    {
        [Test]
        public void NamesBrightSadAndJazzyChords()
        {
            Assert.IsTrue(ChordNames.TryName(new[] { 60, 64, 67 }, LabelMode.Katakana, out var n, out var f));
            Assert.AreEqual("ドミソ", n);
            StringAssert.Contains("あかるい", f);
            Assert.IsTrue(ChordNames.TryName(new[] { 57, 60, 64 }, LabelMode.Katakana, out n, out f));
            Assert.AreEqual("ラドミ", n);
            StringAssert.Contains("さみしい", f);
            Assert.IsTrue(ChordNames.TryName(new[] { 67, 71, 74, 77 }, LabelMode.Letters, out n, out f));
            Assert.AreEqual("GBDF", n);
            StringAssert.Contains("おしゃれ", f);
            // 転回形・オクターブちがいの おなじ音も わかる（なまえは ひくい順・1 回ずつ）
            Assert.IsTrue(ChordNames.TryName(new[] { 64, 67, 72, 76 }, LabelMode.Hiragana, out n, out f));
            Assert.AreEqual("みそど", n);
            // おんめい なし・いろ だけ でも、なまえは カタカナで
            Assert.IsTrue(ChordNames.TryName(new[] { 65, 69, 72 }, LabelMode.None, out n, out _));
            Assert.AreEqual("ファラド", n);
        }

        [Test]
        public void OtherClustersAreNotChords()
        {
            Assert.IsFalse(ChordNames.TryName(new[] { 60, 64 }, LabelMode.Katakana, out _, out _));
            Assert.IsFalse(ChordNames.TryName(new[] { 60, 62, 64 }, LabelMode.Katakana, out _, out _));
            Assert.IsFalse(ChordNames.TryName(new[] { 60, 61, 62, 63, 64 }, LabelMode.Katakana, out _, out _));
            Assert.IsFalse(ChordNames.TryName(null, LabelMode.Katakana, out _, out _));
        }
    }

    /// <summary>1.1 で ふえた こまかい きまり。</summary>
    public class SmallRuleTests
    {
        [Test]
        public void WeekdayNames()
        {
            Assert.AreEqual("どようび", AppClock.Weekday(20261031));
            Assert.AreEqual("にちようび", AppClock.Weekday(20261101));
            Assert.AreEqual("", AppClock.Weekday(0));
            Assert.AreEqual("", AppClock.Weekday(20261340), "ない日");
        }

        [Test]
        public void MinutesAreReadTheJapaneseWay()
        {
            Assert.AreEqual("15ふん", AppUI.MinutesText(15));
            Assert.AreEqual("30ぷん", AppUI.MinutesText(30));
            Assert.AreEqual("60ぷん", AppUI.MinutesText(60));
            Assert.AreEqual("5ふん", AppUI.MinutesText(5));
        }

        [Test]
        public void FlatNamesForFlatKeys()
        {
            Assert.AreEqual("ラ♯", NoteUtil.Name(70, LabelMode.Katakana));
            Assert.AreEqual("シ♭", NoteUtil.Name(70, LabelMode.Katakana, true));
            Assert.AreEqual("し♭", NoteUtil.Name(70, LabelMode.Hiragana, true));
            Assert.AreEqual("B♭", NoteUtil.Name(70, LabelMode.Letters, true));
            Assert.AreEqual("ド", NoteUtil.Name(72, LabelMode.Katakana, true), "白鍵は かわらない");
            Assert.IsTrue(PerformSongs.All.Find(s => s.id == "birthday").flats, "ヘ長調");
        }

        [Test]
        public void PcKeyLettersStartAtTheFirstVisibleC()
        {
            var layout = KeyboardLayout.Create(1, 0, true);
            var range = new Vector2Int(0, layout.WhiteCount - 1);
            Assert.AreEqual("A", PianoInput.LetterFor(layout, range, 60));
            Assert.AreEqual("S", PianoInput.LetterFor(layout, range, 62));
            Assert.AreEqual("W", PianoInput.LetterFor(layout, range, 61));
            Assert.AreEqual("T", PianoInput.LetterFor(layout, range, 66));
            Assert.AreEqual("K", PianoInput.LetterFor(layout, range, 72));
            var whiteOnly = KeyboardLayout.Create(1, 0, false);
            Assert.IsNull(PianoInput.LetterFor(whiteOnly, range, 61), "くろい けんばん なし");
            Assert.IsNull(PianoInput.LetterFor(null, range, 60));
        }

        [Test]
        public void NewSettingsAreClampedAndKidFriendly()
        {
            var s = new PiamoSettings { performWindow = 5, playLimit = 7 };
            s.Clamp();
            Assert.AreEqual(1, s.performWindow);
            Assert.AreEqual(0, s.playLimit, "えらべない 時間は なし に");
            s.playLimit = 30;
            s.Clamp();
            Assert.AreEqual(30, s.playLimit);
            var d = new PiamoSettings();
            Assert.IsTrue(d.keepAwake);
            Assert.IsTrue(d.vibrate);
            Assert.IsFalse(d.reduceMotion);
            Assert.IsFalse(d.pcKeyLabels);
            Assert.AreEqual(0, d.playLimit);
            Assert.AreEqual(1.4f, d.PerformWindowScale, 1e-4f, "はじめは あたりが ひろい");
            Assert.AreEqual(1f, d.MimicTempo, 1e-4f);
            Assert.Less(new PiamoSettings { mimicSpeed = 0 }.MimicTempo, 1f);
            Assert.Greater(new PiamoSettings { mimicSpeed = 2 }.MimicTempo, 1f);
        }

        [Test]
        public void MimicStarsKeepTheBestAndOldClearsCountAsOne()
        {
            var s = new PiamoSave();
            s.clearedSongs.Add("twinkle");
            Assert.AreEqual(1, s.MimicStarsFor("twinkle"), "まえの バージョンで ひけた曲は ほし 1");
            Assert.IsTrue(s.RecordMimicStars("twinkle", 2));
            Assert.IsFalse(s.RecordMimicStars("twinkle", 1), "わるい きろくでは かわらない");
            Assert.AreEqual(2, s.MimicStarsFor("twinkle"));
            Assert.AreEqual(0, s.MimicStarsFor("tulip"));
            Assert.IsTrue(s.RecordStars("a", 2));
            Assert.IsTrue(s.RecordStars("b", 3));
            Assert.AreEqual(5, s.TotalPerformStars);
        }

        [Test]
        public void RepairFixesTheNewFields()
        {
            var s = new PiamoSave { lastPlayMode = 9, lastMimicSong = null, lastPerformSong = null, recording = new List<int> { 1, 2 } };
            s.mimicStars = new List<SongStars>
            {
                new SongStars { id = "a", stars = 9 }, new SongStars { id = "a", stars = 1 }, null, new SongStars { id = "", stars = 2 },
            };
            s.Repair();
            Assert.AreEqual(2, s.lastPlayMode);
            Assert.AreEqual("", s.lastMimicSong);
            Assert.AreEqual("", s.lastPerformSong);
            Assert.IsEmpty(s.recording, "こわれた ろくおんは けす");
            Assert.AreEqual(1, s.mimicStars.Count);
            Assert.AreEqual(3, s.mimicStars[0].stars);
        }
    }

    /// <summary>保存（1.1 で ふえた もの）。</summary>
    public class SaveExtrasTests
    {
        [SetUp]
        public void SetUp()
        {
            SaveSystem.KeyOverride = "piamo.test.extras";
            SaveSystem.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.ResetAll();
            SaveSystem.KeyOverride = null;
            SaveSystem.Load();
        }

        [Test]
        public void NewFieldsSurviveSaveAndLoad()
        {
            var d = SaveSystem.Data;
            d.lastPlayMode = 2;
            d.lastMimicSong = "kaeru";
            d.lastPerformSong = "tulip";
            d.recording.AddRange(new[] { 300, 60, 1, 600, 60, 0 });
            d.RecordMimicStars("kaeru", 3);
            d.settings.playLimit = 30;
            d.settings.pcKeyLabels = true;
            SaveSystem.Save();
            SaveSystem.Load();
            var e = SaveSystem.Data;
            Assert.AreEqual(2, e.lastPlayMode);
            Assert.AreEqual("kaeru", e.lastMimicSong);
            Assert.AreEqual("tulip", e.lastPerformSong);
            CollectionAssert.AreEqual(new[] { 300, 60, 1, 600, 60, 0 }, e.recording);
            Assert.AreEqual(3, e.MimicStarsFor("kaeru"));
            Assert.AreEqual(30, e.settings.playLimit);
            Assert.IsTrue(e.settings.pcKeyLabels);
        }

        [Test]
        public void ResetStampsClearsStarsButKeepsTheRecording()
        {
            SaveSystem.Data.RecordMimicStars("x", 2);
            SaveSystem.Data.RecordStars("y", 3);
            SaveSystem.Data.recording.AddRange(new[] { 300, 60, 1 });
            SaveSystem.ResetStamps();
            Assert.IsEmpty(SaveSystem.Data.mimicStars);
            Assert.IsEmpty(SaveSystem.Data.performStars);
            Assert.AreEqual(3, SaveSystem.Data.recording.Count, "ろくおんは のこす");
        }

        [Test]
        public void ResetSettingsKeepsStarsAndRecording()
        {
            SaveSystem.Data.RecordMimicStars("x", 2);
            SaveSystem.Data.recording.AddRange(new[] { 300, 60, 1 });
            SaveSystem.Settings.reduceMotion = true;
            SaveSystem.ResetSettings();
            Assert.IsFalse(SaveSystem.Settings.reduceMotion);
            Assert.AreEqual(2, SaveSystem.Data.MimicStarsFor("x"));
            Assert.AreEqual(3, SaveSystem.Data.recording.Count);
        }
    }
}
