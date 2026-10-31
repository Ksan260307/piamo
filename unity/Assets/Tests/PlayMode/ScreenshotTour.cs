using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Piamo.Tests
{
    /// <summary>
    /// 画面の絵を描き出す（確認と README 用。指定したときだけ動く［Explicit］）。
    /// スマホのよこ・たて、iPad のよこ・たての大きさで、メニュー・ピアノ・まねっこ・えんそう・せってい・スタンプちょうを描く。
    ///   Unity.exe -batchmode -projectPath unity -runTests -testPlatform PlayMode -testFilter Piamo.Tests.ScreenshotTour
    /// 出力: build-tmp/screens/*.png（PIAMO_SCREENS で場所をかえられる）
    /// </summary>
    [Explicit]
    public class ScreenshotTour
    {
        public static readonly (string name, int w, int h)[] Sizes =
        {
            ("phone_land", 1266, 585),
            ("phone_port", 585, 1266),
            ("ipad_land", 1366, 1024),
            ("ipad_port", 1024, 1366),
        };

        [UnityTest]
        public IEnumerator Tour()
        {
            string dir = System.Environment.GetEnvironmentVariable("PIAMO_SCREENS");
            if (string.IsNullOrEmpty(dir)) dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "build-tmp", "screens"));
            Directory.CreateDirectory(dir);
            foreach (var size in Sizes)
            {
                yield return AppHarness.Boot();
                var cap = new Capture(size.w, size.h);
                yield return AppHarness.Frames(4);

                yield return cap.Shot(dir, size.name + "_1_menu");

                // スタンプを 12 こ おした ことにして、きょうの 1 こ（13 こめ）が おせる状態
                var book = AppHarness.UI.Stamps;
                for (int i = 0; i < 12; i++)
                {
                    AppHarness.Today = AppClock.AddDays(AppHarness.Day0, -12 + i);
                    for (int k = 0; k < StampBook.PlaysForStamp; k++) book.RecordPlay(AppHarness.Today);
                    book.Press();
                }
                AppHarness.Today = AppHarness.Day0;

                yield return AppHarness.OpenPlay();
                yield return AppHarness.Frames(4);
                AppHarness.Play.KeyDown(60, 1);
                AppHarness.Play.KeyDown(64, 2);
                AppHarness.Play.KeyDown(67, 3);
                yield return AppHarness.Frames(6);
                yield return cap.Shot(dir, size.name + "_2_play");
                AppHarness.Play.ReleaseAll();

                // まねっこ（ヒントが出ているところ）
                AppHarness.Click("tab-mimic");
                yield return AppHarness.Frames(3);
                yield return cap.Shot(dir, size.name + "_3_songs");
                AppHarness.UI.StartSong(Songs.Find("twinkle"));
                AppHarness.UI.Mimic.HintSeconds = 0.5f;
                AppUI.MimicTimeScale = 3f;
                yield return AppHarness.WaitUntil(() => AppHarness.UI.Mimic.HintActive, 20f, "ヒント");
                int expected = AppHarness.UI.Mimic.Expected;
                AppHarness.Play.KeyDown(expected, 5);
                yield return AppHarness.Frames(2);
                AppHarness.Play.KeyUp(expected, 5);
                yield return AppHarness.Seconds(0.2f);
                yield return cap.Shot(dir, size.name + "_4_mimic");
                AppUI.MimicTimeScale = 1f;

                // えんそう（曲えらび・いっしょに ひいている ところ・おわり）
                AppHarness.Click("tab-perform");
                yield return AppHarness.Frames(3);
                yield return cap.Shot(dir, size.name + "_4b_perform_songs");
                SaveSystem.Settings.performMode = 1;
                AppHarness.UI.StartPerform(PerformSongs.Find("furusato"));
                var g = AppHarness.UI.Perform;
                AppUI.PerformTimeScale = 2f;
                yield return AppHarness.WaitUntil(() => g.Beat >= 3.05f, 20f, "えんそう");
                AppUI.PerformTimeScale = 0f;
                yield return AppHarness.Frames(2);
                yield return cap.Shot(dir, size.name + "_4c_perform");
                int now = g.SoundingIndex >= 0 ? g.Song.notes[g.SoundingIndex].midi : g.Song.notes[g.NextIndex].midi;
                AppHarness.Play.KeyDown(now, 6);
                yield return AppHarness.Frames(3);
                yield return cap.Shot(dir, size.name + "_4c_perform_hit");
                AppHarness.Play.KeyUp(now, 6);
                AppUI.PerformTimeScale = 40f;
                yield return AppHarness.WaitUntil(() => g.Current == PerformGame.State.Finished, 20f, "えんそうの おわり");
                AppUI.PerformTimeScale = 1f;
                yield return AppHarness.Frames(3);
                yield return cap.Shot(dir, size.name + "_4d_perform_result");
                AppHarness.Click("perform-other");
                yield return AppHarness.Frames(2);

                // せってい（ピアノの とちゅうから）
                AppHarness.Click("play-settings");
                yield return AppHarness.Frames(4);
                yield return cap.Shot(dir, size.name + "_5_settings");
                AppHarness.Click("settings-back");
                yield return AppHarness.Frames(2);

                // きょうの スタンプを もらって、スタンプちょうへ
                for (int k = 0; k < StampBook.PlaysForStamp; k++) yield return AppHarness.Tap(62);
                AppHarness.Click("play-back");
                yield return AppHarness.Frames(2);
                AppHarness.Click("btn-stamps");
                yield return AppHarness.Seconds(1.2f);
                yield return cap.Shot(dir, size.name + "_6_stamps");
                AppHarness.UI.TapNextSlot();
                yield return AppHarness.Seconds(0.45f);
                yield return cap.Shot(dir, size.name + "_7_pressed");
                yield return AppHarness.WaitUntil(() => !AppHarness.UI.PressAnimating, 5f, "スタンプ");
                AppHarness.UI.TapSlot(10);
                yield return AppHarness.Seconds(0.6f);
                yield return cap.Shot(dir, size.name + "_8_detail");

                cap.Dispose();
                AppHarness.TearDown();
            }
            Debug.Log("[Screens] " + dir);
        }

        /// <summary>カメラと UI を、それぞれ絵に描いて かさねる。</summary>
        class Capture
        {
            readonly RenderTexture _cam, _ui;
            readonly PanelSettings _panel;

            public Capture(int w, int h)
            {
                _cam = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                _ui = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                var doc = AppHarness.UI.document;
                _panel = Object.Instantiate(doc.panelSettings);
                _panel.targetTexture = _ui;
                _panel.clearColor = true;
                _panel.colorClearValue = new Color(0, 0, 0, 0);
                doc.panelSettings = _panel;
                AppHarness.Play.cam.targetTexture = _cam;
            }

            public IEnumerator Shot(string dir, string name)
            {
                // バッチモードでは フレームの おわり（WaitForEndOfFrame）が来ないので、カメラは自分で描く。
                // UI（targetTexture のあるパネル）は、毎フレームの更新で描かれる
                yield return null;
                yield return null;
                AppHarness.Play.cam.Render();
                yield return null;
                var cam = Read(_cam);
                var ui = Read(_ui);
                var a = cam.GetPixels32();
                var b = ui.GetPixels32();
                for (int i = 0; i < a.Length; i++)
                {
                    float k = b[i].a / 255f;
                    a[i] = new Color32(
                        (byte)Mathf.Clamp(b[i].r + a[i].r * (1 - k), 0, 255),
                        (byte)Mathf.Clamp(b[i].g + a[i].g * (1 - k), 0, 255),
                        (byte)Mathf.Clamp(b[i].b + a[i].b * (1 - k), 0, 255), 255);
                }
                cam.SetPixels32(a);
                cam.Apply();
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), cam.EncodeToPNG());
                Object.Destroy(cam);
                Object.Destroy(ui);
            }

            static Texture2D Read(RenderTexture rt)
            {
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                t.Apply();
                RenderTexture.active = prev;
                return t;
            }

            public void Dispose()
            {
                if (AppHarness.Play != null && AppHarness.Play.cam != null) AppHarness.Play.cam.targetTexture = null;
                _cam.Release();
                _ui.Release();
            }
        }
    }
}
