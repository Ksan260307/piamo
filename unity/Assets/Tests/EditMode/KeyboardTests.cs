using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Piamo.Tests
{
    public class NoteUtilTests
    {
        [Test]
        public void BlackKeysFollowThePianoPattern()
        {
            int[] blacks = { 61, 63, 66, 68, 70 };
            for (int m = 60; m < 72; m++) Assert.AreEqual(blacks.Contains(m), NoteUtil.IsBlack(m), m.ToString());
            Assert.IsFalse(NoteUtil.IsBlack(24));
            Assert.IsTrue(NoteUtil.IsBlack(25));
        }

        [Test]
        public void Names()
        {
            Assert.AreEqual("ド", NoteUtil.Name(60, LabelMode.Katakana));
            Assert.AreEqual("ファ", NoteUtil.Name(65, LabelMode.Katakana));
            Assert.AreEqual("し", NoteUtil.Name(71, LabelMode.Hiragana));
            Assert.AreEqual("A", NoteUtil.Name(69, LabelMode.Letters));
            Assert.AreEqual("", NoteUtil.Name(60, LabelMode.None));
            Assert.AreEqual(4, NoteUtil.Octave(60));
            Assert.AreEqual(3, NoteUtil.Octave(59));
        }

        [Test]
        public void SevenDifferentColorsForDoReMi()
        {
            var cols = new[] { 60, 62, 64, 65, 67, 69, 71 }.Select(NoteUtil.NoteColor).ToList();
            for (int i = 0; i < cols.Count; i++)
            for (int j = i + 1; j < cols.Count; j++)
                Assert.Greater(Vector4.Distance(cols[i], cols[j]), 0.1f, $"{i} と {j} の いろが にている");
            // オクターブがちがっても同じ いろ
            Assert.AreEqual(NoteUtil.NoteColor(60), NoteUtil.NoteColor(72));
        }

        [Test]
        public void WhiteNumberRoundTrip()
        {
            for (int m = 24; m <= 108; m++)
            {
                if (NoteUtil.IsBlack(m)) continue;
                Assert.AreEqual(m, NoteUtil.FromWhiteNumber(NoteUtil.WhiteNumber(m)));
            }
            Assert.AreEqual(NoteUtil.WhiteNumber(60) + 1, NoteUtil.WhiteNumber(62));
            Assert.AreEqual(NoteUtil.WhiteNumber(60), NoteUtil.WhiteNumber(61), "黒鍵は すぐ下の白鍵の番号");
        }

        [Test]
        public void Frequency() => Assert.AreEqual(440f, NoteUtil.Frequency(69), 0.01f);
    }

    public class KeyboardLayoutTests
    {
        [Test]
        public void StartsWithMinimumKeys()
        {
            var s = new PiamoSettings();
            Assert.AreEqual(1, s.octaves, "はじめは いちばん少ない 1 オクターブ");
            var k = KeyboardLayout.Create(s.octaves, s.octaveShift, s.blackKeys);
            Assert.AreEqual(60, k.Low);
            Assert.AreEqual(72, k.High);
            Assert.AreEqual(8, k.WhiteCount, "ド〜ド の 8 けん");
            Assert.AreEqual(13, k.Keys.Count);
        }

        [TestCase(1, 8)]
        [TestCase(2, 15)]
        [TestCase(3, 22)]
        [TestCase(4, 29)]
        [TestCase(5, 36)]
        public void WhiteKeysPerOctaveCount(int octaves, int whites)
        {
            var k = KeyboardLayout.Create(octaves, 0, true);
            Assert.AreEqual(whites, k.WhiteCount);
            Assert.AreEqual(12 * octaves + 1, k.Keys.Count);
            Assert.AreEqual(0, NoteUtil.PitchClass(k.Low), "ドから はじまる");
            Assert.AreEqual(0, NoteUtil.PitchClass(k.High), "ドで おわる");
            Assert.IsTrue(k.Contains(60), "まんなかのド が いつもある");
        }

        [Test]
        public void ShiftStaysInPlayableRange()
        {
            for (int o = 1; o <= 5; o++)
            {
                var lim = KeyboardLayout.ShiftLimits(o);
                Assert.LessOrEqual(lim.x, 0);
                Assert.GreaterOrEqual(lim.y, 0);
                for (int s = lim.x - 2; s <= lim.y + 2; s++)
                {
                    var k = KeyboardLayout.Create(o, s, true);
                    Assert.GreaterOrEqual(k.Low, KeyboardLayout.LowestNote, $"{o} {s}");
                    Assert.LessOrEqual(k.High, KeyboardLayout.HighestNote, $"{o} {s}");
                }
            }
            Assert.AreEqual(new Vector2Int(-3, 3), KeyboardLayout.ShiftLimits(1));
        }

        [Test]
        public void OctaveShiftMovesTheSound()
        {
            var k = KeyboardLayout.Create(1, -1, true);
            Assert.AreEqual(48, k.Low);
            k = KeyboardLayout.Create(1, 2, true);
            Assert.AreEqual(84, k.Low);
        }

        [Test]
        public void WhiteKeyNotchesMatchBlackKeys()
        {
            var k = KeyboardLayout.Create(1, 0, true);
            Notch N(int m) { k.TryGet(m, out var i); return i.notch; }
            Assert.AreEqual(Notch.Right, N(60)); // ド
            Assert.AreEqual(Notch.Both, N(62));  // レ
            Assert.AreEqual(Notch.Left, N(64));  // ミ
            Assert.AreEqual(Notch.Right, N(65)); // ファ
            Assert.AreEqual(Notch.Both, N(67));  // ソ
            Assert.AreEqual(Notch.Both, N(69));  // ラ
            Assert.AreEqual(Notch.Left, N(71));  // シ
            Assert.AreEqual(Notch.Full, N(72));  // いちばん上のド（となりに黒鍵がない）
            var noBlack = KeyboardLayout.Create(2, 0, false);
            Assert.IsTrue(noBlack.Keys.All(x => !x.black && x.notch == Notch.Full), "黒鍵なしのときは、切りかきもない");
            Assert.AreEqual(15, noBlack.Keys.Count);
        }

        [Test]
        public void BlackKeysSitBetweenWhiteKeys()
        {
            var k = KeyboardLayout.Create(1, 0, true);
            k.TryGet(61, out var cs);
            Assert.AreEqual(0.5f, cs.x, 1e-4f);
            k.TryGet(70, out var as_);
            Assert.AreEqual(5.5f, as_.x, 1e-4f);
        }

        [Test]
        public void HitTest()
        {
            var k = KeyboardLayout.Create(1, 0, true);
            Assert.AreEqual(60, k.HitWhite(0.1f, 1f));
            Assert.AreEqual(62, k.HitWhite(1.3f, 1f));
            Assert.AreEqual(72, k.HitWhite(7.2f, 1f));
            Assert.AreEqual(-1, k.HitWhite(8.2f, 1f), "鍵盤の外");
            Assert.AreEqual(-1, k.HitWhite(-0.8f, 1f));
            Assert.AreEqual(61, k.HitBlack(0.5f, 3.5f));
            Assert.AreEqual(-1, k.HitBlack(0.5f, 0.8f), "黒鍵の手前は白鍵");
            Assert.AreEqual(-1, k.HitBlack(2.5f, 3.5f), "ミとファのあいだに黒鍵はない");
        }
    }

    public class FramingTests
    {
        static readonly Vector2 Phone = new Vector2(2532, 1170);

        [Test]
        public void FewKeysAreStretchedToFillWideScreens()
        {
            var area = new Rect(0, 0, Phone.x, Phone.y * 0.6f);
            var f = KeyboardFraming.Compute(area, Phone, 8, 120f, 0f);
            Assert.IsFalse(f.scrolling);
            Assert.AreEqual(8, f.visible);
            Assert.Greater(f.stretch, 1f);
            Assert.LessOrEqual(f.stretch, KeyboardFraming.MaxStretch);
            Assert.AreEqual(area.height, f.contentHeight, 1f, "高さいっぱい");
        }

        [Test]
        public void ManyKeysOnANarrowScreenScroll()
        {
            var portrait = new Vector2(1170, 2532);
            var area = new Rect(0, 0, portrait.x, 900);
            var f = KeyboardFraming.Compute(area, portrait, 36, 130f, 0f);
            Assert.IsTrue(f.scrolling);
            Assert.Less(f.visible, 36);
            Assert.GreaterOrEqual(f.keyPx, 130f - 0.5f, "白鍵が細くなりすぎない");
            Assert.AreEqual(36 - f.visible, f.maxOffset, 1e-3f);
            var g = KeyboardFraming.Compute(area, portrait, 36, 130f, 999f);
            Assert.AreEqual(g.maxOffset, g.offset, 1e-3f, "スクロールは はしで止まる");
        }

        [Test]
        public void KeyboardIsCenteredAndAlignedToTheAreaBottom()
        {
            var area = new Rect(100, 50, 2000, 600);
            var f = KeyboardFraming.Compute(area, Phone, 15, 80f, 0f);
            // カメラの計算どおりに、鍵盤のまんなか・下のはしを画面へ写す
            Vector3 up = f.cameraRotation * Vector3.up;
            float vCam = Vector3.Dot(f.cameraPosition, up);
            float centerX = f.stretch * (15 - 1) / 2f;
            float sx = Phone.x / 2f + (centerX - f.cameraPosition.x) * f.ppu;
            float sy = Phone.y / 2f + (KeyboardFraming.VerticalExtent().x - vCam) * f.ppu;
            Assert.AreEqual(area.center.x, sx, 1f);
            Assert.AreEqual(area.yMin, sy, 1f);
            Assert.LessOrEqual(f.contentHeight, area.height + 1f);
        }

        [Test]
        public void PreferredHeightIsCapped()
        {
            float h = KeyboardFraming.PreferredHeight(2532, 1170, 8, 120, 0.6f);
            Assert.AreEqual(1170 * 0.6f, h, 1f);
            float p = KeyboardFraming.PreferredHeight(1170, 2532, 8, 120, 0.5f);
            Assert.Less(p, 2532 * 0.5f, "たて長では、よこはばに合わせた高さ");
        }
    }
}
