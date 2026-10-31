using NUnit.Framework;
using UnityEngine;

namespace Piamo.Tests
{
    public class StampBookTests
    {
        const int Day = 20261031;

        static void Play(StampBook b, int day, int times)
        {
            for (int i = 0; i < times; i++) b.RecordPlay(day);
        }

        [Test]
        public void TenPlaysEarnTodaysStamp()
        {
            var s = new PiamoSave();
            var b = new StampBook(s);
            Assert.AreEqual(10, b.PlaysLeft(Day));
            for (int i = 0; i < 9; i++) Assert.IsFalse(b.RecordPlay(Day));
            Assert.AreEqual(1, b.PlaysLeft(Day));
            Assert.IsTrue(b.RecordPlay(Day), "10 回目で もらえる");
            Assert.AreEqual(1, b.PendingCount);
            Assert.AreEqual(0, b.PlaysLeft(Day));
        }

        [Test]
        public void OnlyOneStampPerDay()
        {
            var b = new StampBook(new PiamoSave());
            Play(b, Day, 50);
            Assert.AreEqual(1, b.PendingCount);
            Assert.AreEqual(1, b.Press());
            Play(b, Day, 50);
            Assert.AreEqual(0, b.PendingCount, "おしたあと、同じ日には もうもらえない");
            Assert.AreEqual(1, b.Total);
        }

        [Test]
        public void PressedStampsAreInDayOrder()
        {
            var b = new StampBook(new PiamoSave());
            Play(b, Day, 10);
            Play(b, AppClock.AddDays(Day, 1), 10);
            Play(b, AppClock.AddDays(Day, 3), 10);
            Assert.AreEqual(3, b.PendingCount, "おしわすれても、あそんだ日の分は のこる");
            Assert.AreEqual(1, b.Press());
            Assert.AreEqual(2, b.Press());
            Assert.AreEqual(3, b.Press());
            Assert.AreEqual(0, b.Press());
            Assert.AreEqual(Day, b.DayOf(1));
            Assert.AreEqual(AppClock.AddDays(Day, 3), b.DayOf(3));
        }

        [Test]
        public void ClockGoingBackDoesNotGiveStamps()
        {
            var b = new StampBook(new PiamoSave());
            Play(b, Day, 10);
            b.Press();
            Play(b, AppClock.AddDays(Day, -2), 30);
            Assert.AreEqual(0, b.PendingCount);
        }

        [Test]
        public void EveryFifthIsBig()
        {
            for (int n = 1; n <= 730; n++) Assert.AreEqual(n % 5 == 0, StampBook.IsBig(n), n.ToString());
        }

        [Test]
        public void ThreeHundredSixtyFiveDesignsThenSecondBook()
        {
            Assert.AreEqual(1, StampBook.DesignFor(1));
            Assert.AreEqual(365, StampBook.DesignFor(365));
            Assert.AreEqual(1, StampBook.DesignFor(366));
            Assert.AreEqual(1, StampBook.BookFor(365));
            Assert.AreEqual(2, StampBook.BookFor(366));
        }

        [Test]
        public void PlayCountResetsOnANewDay()
        {
            var s = new PiamoSave();
            var b = new StampBook(s);
            Play(b, Day, 7);
            Assert.AreEqual(3, b.PlaysLeft(Day));
            Assert.AreEqual(10, b.PlaysLeft(AppClock.AddDays(Day, 1)));
            b.RecordPlay(AppClock.AddDays(Day, 1));
            Assert.AreEqual(1, s.playCount);
        }
    }

    public class AppClockTests
    {
        [Test]
        public void Formats()
        {
            Assert.AreEqual("10/31", AppClock.Short(20261031));
            Assert.AreEqual("2026ねん 10がつ 31にち", AppClock.Long(20261031));
            Assert.AreEqual(20261101, AppClock.AddDays(20261031, 1));
            Assert.AreEqual(20270101, AppClock.AddDays(20261231, 1));
        }

        [Test]
        public void TodayCanBeOverridden()
        {
            AppClock.TodayOverride = () => 20300101;
            try { Assert.AreEqual(20300101, AppClock.Today()); }
            finally { AppClock.TodayOverride = null; }
            int t = AppClock.Today();
            Assert.Greater(t, 20200101);
        }
    }

    public class SaveTests
    {
        [SetUp]
        public void SetUp()
        {
            SaveSystem.KeyOverride = "piamo.test.edit";
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
        public void RoundTrip()
        {
            var d = SaveSystem.Data;
            d.settings.instrument = "harp";
            d.settings.octaves = 3;
            d.settings.labels = LabelMode.Letters;
            d.stampDays.Add(20261030);
            d.pendingDays.Add(20261031);
            d.clearedSongs.Add("twinkle");
            SaveSystem.Save();
            SaveSystem.Load();
            d = SaveSystem.Data;
            Assert.AreEqual("harp", d.settings.instrument);
            Assert.AreEqual(3, d.settings.octaves);
            Assert.AreEqual(LabelMode.Letters, d.settings.labels);
            CollectionAssert.AreEqual(new[] { 20261030 }, d.stampDays);
            CollectionAssert.AreEqual(new[] { 20261031 }, d.pendingDays);
            CollectionAssert.Contains(d.clearedSongs, "twinkle");
        }

        [Test]
        public void BrokenDataIsRepaired()
        {
            PlayerPrefs.SetString("piamo.test.edit", "{\"settings\":{\"octaves\":99,\"octaveShift\":9,\"volume\":5,\"keySize\":-3},\"stampDays\":[20261001,0],\"pendingDays\":[20261001,20261002,20261002]}");
            SaveSystem.Load();
            var d = SaveSystem.Data;
            Assert.AreEqual(5, d.settings.octaves);
            Assert.AreEqual(KeyboardLayout.ShiftLimits(5).y, d.settings.octaveShift);
            Assert.AreEqual(1f, d.settings.volume);
            Assert.AreEqual(0, d.settings.keySize);
            CollectionAssert.AreEqual(new[] { 20261001 }, d.stampDays);
            CollectionAssert.AreEqual(new[] { 20261002 }, d.pendingDays, "おした日・同じ日の だぶりを のぞく");
        }

        [Test]
        public void GarbageStartsFresh()
        {
            PlayerPrefs.SetString("piamo.test.edit", "not json at all {{{");
            SaveSystem.Load();
            Assert.AreEqual(1, SaveSystem.Data.settings.octaves);
        }

        [Test]
        public void ResetStampsKeepsSettings()
        {
            SaveSystem.Settings.instrument = "organ";
            SaveSystem.Data.stampDays.Add(20261001);
            SaveSystem.ResetStamps();
            Assert.AreEqual(0, SaveSystem.Data.stampDays.Count);
            Assert.AreEqual("organ", SaveSystem.Settings.instrument);
        }

        [Test]
        public void ResetSettingsKeepsStamps()
        {
            SaveSystem.Settings.instrument = "organ";
            SaveSystem.Data.stampDays.Add(20261001);
            SaveSystem.ResetSettings();
            Assert.AreEqual("piano", SaveSystem.Settings.instrument);
            Assert.AreEqual(1, SaveSystem.Data.stampDays.Count);
        }

        [Test]
        public void SettingsChangedIsRaised()
        {
            int calls = 0;
            System.Action a = () => calls++;
            SaveSystem.SettingsChanged += a;
            try
            {
                SaveSystem.ApplySettings();
                Assert.AreEqual(1, calls);
            }
            finally { SaveSystem.SettingsChanged -= a; }
        }

        [Test]
        public void DefaultsAreKidFriendly()
        {
            var s = new PiamoSettings();
            Assert.AreEqual(10, s.hintSeconds, "10 びょう まよったら ヒント");
            Assert.AreEqual(1, s.octaves);
            Assert.IsTrue(s.blackKeys);
            Assert.AreEqual("piano", s.instrument);
            Assert.IsTrue(s.demoLights);
            CollectionAssert.Contains(PiamoSettings.HintChoices, 10);
        }
    }
}
