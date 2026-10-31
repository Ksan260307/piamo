using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Piamo.Tests
{
    /// <summary>
    /// 総合テスト用の操作ハーネス。実際のシーン（Main）を読みこみ、ボタンや指（PianoInput の仮想の指）で操作する。
    /// 保存先は テスト用のキーにして、ふだんの記録をこわさない。
    /// </summary>
    public static class AppHarness
    {
        public const int Day0 = 20261031;
        public static int Today = Day0;

        public static AppUI UI { get; private set; }
        public static PlayController Play => UI.play;
        public static PianoAudio Audio => UI.audioOut;
        public static VisualElement Root => UI.Root;

        public static IEnumerator Boot(bool keepSave = false)
        {
            SaveSystem.KeyOverride = "piamo.test";
            if (!keepSave)
            {
                SaveSystem.ResetAll();
                Today = Day0;
            }
            AppClock.TodayOverride = () => Today;
            AppUI.MimicTimeScale = 1f;
            AppUI.PerformTimeScale = 1f;
            PianoInput.VirtualPointers.Clear();
            PianoInput.VirtualQuickTaps.Clear();
            Time.timeScale = 1f;
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;
            float t = 0f;
            while (true)
            {
                UI = UnityEngine.Object.FindAnyObjectByType<AppUI>();
                if (UI != null && UI.Ready) break;
                t += Time.unscaledDeltaTime;
                if (t > 30f) Assert.Fail("アプリの起動がタイムアウトしました");
                yield return null;
            }
            yield return Frames(3);
        }

        public static void TearDown()
        {
            PianoInput.VirtualPointers.Clear();
            AppClock.TodayOverride = null;
            AppUI.MimicTimeScale = 1f;
            AppUI.PerformTimeScale = 1f;
            SaveSystem.ResetAll();
            SaveSystem.KeyOverride = null;
            SaveSystem.Load();
        }

        public static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        public static IEnumerator Seconds(float s)
        {
            float t = 0f;
            while (t < s)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        public static IEnumerator WaitUntil(Func<bool> cond, float timeout, string what)
        {
            float t = 0f;
            while (!cond())
            {
                t += Time.unscaledDeltaTime;
                if (t > timeout) Assert.Fail($"待ち時間切れ: {what}");
                yield return null;
            }
        }

        public static Button Button(string name)
        {
            var b = Root.Q<Button>(name);
            Assert.IsNotNull(b, $"ボタン {name} がありません");
            return b;
        }

        /// <summary>ボタンを おす（UI Toolkit の Clickable を、ほんとうに おしたのと同じように呼ぶ）。</summary>
        public static void Click(Button b)
        {
            Assert.IsNotNull(b);
            Assert.IsTrue(b.enabledInHierarchy, $"{b.name} が おせない状態です");
            var m = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(m, "Clickable.Invoke が見つかりません");
            using (var e = NavigationSubmitEvent.GetPooled())
            {
                e.target = b;
                m.Invoke(b.clickable, new object[] { e });
            }
        }

        public static void Click(string name) => Click(Button(name));

        public static bool Visible(string name)
        {
            var e = Root.Q(name);
            Assert.IsNotNull(e, $"{name} がありません");
            for (var p = e; p != null; p = p.parent)
                if (p.resolvedStyle.display == DisplayStyle.None || p.ClassListContains("hidden")) return false;
            return true;
        }

        /// <summary>指で鍵盤をおす（id ごとに 1 本の指）。</summary>
        public static void Touch(int id, int midi) => PianoInput.VirtualPointers[id] = Play.KeyCenterScreen(midi);
        public static void TouchAt(int id, Vector2 screen) => PianoInput.VirtualPointers[id] = screen;
        public static void Release(int id) => PianoInput.VirtualPointers.Remove(id);

        /// <summary>鍵盤を 1 回タップする（おして、はなす）。</summary>
        public static IEnumerator Tap(int midi, int id = 1)
        {
            Touch(id, midi);
            yield return Frames(2);
            Release(id);
            yield return Frames(2);
        }

        public static IEnumerator OpenPlay()
        {
            Click("btn-play");
            yield return Frames(4);
            Assert.AreEqual(AppUI.Page.Play, UI.Current);
            yield return WaitUntil(() => Play.Framing.ppu > 0f && Play.Area.width > 10f, 5f, "鍵盤のカメラ");
        }
    }
}
