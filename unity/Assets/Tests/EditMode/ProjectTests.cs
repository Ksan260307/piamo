using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Piamo.Tests
{
    public class AssetTests
    {
        static PianoAssets Piano => AssetDatabase.LoadAssetAtPath<PianoAssets>("Assets/Settings/PianoAssets.asset");
        static InstrumentBank Bank => AssetDatabase.LoadAssetAtPath<InstrumentBank>("Assets/Settings/InstrumentBank.asset");
        static UiAssets Ui => AssetDatabase.LoadAssetAtPath<UiAssets>("Assets/Settings/UiAssets.asset");

        [Test]
        public void KeyMeshesMatchTheDimensions()
        {
            var p = Piano;
            Assert.IsNotNull(p);
            var w = p.whiteFull.bounds;
            Assert.AreEqual(PianoDimensions.WhiteWidth, w.size.x, 0.01f);
            Assert.AreEqual(0f, w.min.z, 0.01f, "白鍵の前のはしが Z = 0");
            Assert.AreEqual(PianoDimensions.WhiteLength, w.max.z, 0.01f, "白鍵は奥（+Z）へのびる");
            Assert.AreEqual(0f, w.max.y, 0.01f, "白鍵の上の面が Y = 0");
            var b = p.black.bounds;
            Assert.AreEqual(PianoDimensions.BlackHeight, b.max.y, 0.01f);
            Assert.AreEqual(PianoDimensions.WhiteLength, b.max.z, 0.01f);
            Assert.LessOrEqual(b.size.x, PianoDimensions.BlackWidth + 0.01f);
        }

        [Test]
        public void NotchesAreOnTheCorrectSide()
        {
            // 奥（黒鍵のところ）の、いちばん右・左
            float MaxXAtBack(Mesh m, bool right)
            {
                var vs = m.vertices.Where(v => v.z > PianoDimensions.WhiteLength - 1f).Select(v => v.x).ToList();
                return right ? vs.Max() : vs.Min();
            }
            var p = Piano;
            Assert.Less(MaxXAtBack(p.whiteRight, true), 0.3f, "Key_White_R は右が切りかかれている");
            Assert.AreEqual(-PianoDimensions.WhiteWidth / 2, MaxXAtBack(p.whiteRight, false), 0.01f);
            Assert.Greater(MaxXAtBack(p.whiteLeft, false), -0.3f, "Key_White_L は左が切りかかれている");
            Assert.AreEqual(PianoDimensions.WhiteWidth / 2, MaxXAtBack(p.whiteLeft, true), 0.01f);
        }

        [Test]
        public void CaseSegmentsAreUnitLengthAndEndsFitTheKeys()
        {
            var p = Piano;
            foreach (var m in new[] { p.caseBack, p.caseFront, p.caseFelt, p.caseBed })
            {
                Assert.AreEqual(0f, m.bounds.min.x, 0.01f, m.name);
                Assert.AreEqual(1f, m.bounds.max.x, 0.01f, m.name);
            }
            Assert.AreEqual(0f, p.caseEndLeft.bounds.max.x, 0.01f);
            Assert.AreEqual(-PianoDimensions.CaseEndWidth, p.caseEndLeft.bounds.min.x, 0.01f);
            Assert.AreEqual(0f, p.caseEndRight.bounds.min.x, 0.01f);
            Assert.AreEqual(PianoDimensions.CaseBackZ, p.caseBack.bounds.max.z, 0.01f);
            Assert.AreEqual(PianoDimensions.CaseBackTop, p.caseBack.bounds.max.y, 0.01f);
        }

        [Test]
        public void EveryInstrumentHasAllNotes()
        {
            var bank = Bank;
            Assert.IsNotNull(bank);
            foreach (var info in InstrumentCatalog.All)
            {
                var inst = bank.Find(info.id);
                Assert.AreEqual(info.id, inst.id);
                Assert.AreEqual(22, inst.clips.Length, info.id);
                Assert.IsTrue(inst.clips.All(c => c != null), info.id);
                for (int n = KeyboardLayout.LowestNote; n <= KeyboardLayout.HighestNote; n++)
                {
                    int i = InstrumentBank.Nearest(inst.roots, n);
                    Assert.LessOrEqual(Mathf.Abs(inst.roots[i] - n), 2, $"{info.id} {n}");
                }
                var imp = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(inst.clips[0]));
                Assert.AreEqual(info.id == "piano", imp.defaultSampleSettings.preloadAudioData, "ピアノだけ はじめから読む");
            }
        }

        [Test]
        public void PitchMath()
        {
            Assert.AreEqual(1f, InstrumentBank.PitchFor(60, 60), 1e-5f);
            Assert.AreEqual(2f, InstrumentBank.PitchFor(60, 72), 1e-4f);
            Assert.AreEqual(Mathf.Pow(2, 2 / 12f), InstrumentBank.PitchFor(60, 62), 1e-4f);
            Assert.AreEqual(1, InstrumentBank.Nearest(new[] { 56, 60, 64 }, 61));
        }

        [Test]
        public void StampCatalogHas365()
        {
            StampCatalog.Load();
            var e = StampCatalog.Entries;
            Assert.AreEqual(365, e.Count);
            for (int i = 0; i < e.Count; i++)
            {
                Assert.AreEqual(i + 1, e[i].no);
                Assert.AreEqual((i + 1) % 5 == 0, e[i].big);
                Assert.IsFalse(string.IsNullOrEmpty(e[i].name));
                Assert.IsTrue(File.Exists(Path.Combine(Application.streamingAssetsPath, "Stamps", StampCatalog.FileName(i + 1))), e[i].no.ToString());
            }
            Assert.AreEqual(365, e.Select(x => x.name).Distinct().Count());
        }

        [Test]
        public void StampImagesDecodeWithTransparency()
        {
            foreach (int no in new[] { 1, 5, 365 })
            {
                var bytes = File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath, "Stamps", StampCatalog.FileName(no)));
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.IsTrue(t.LoadImage(bytes), $"stamp {no} を読めない");
                    Assert.AreEqual(no % 5 == 0 ? 320 : 256, t.width);
                    Assert.Less(t.GetPixel(0, 0).a, 0.05f, "すみは すきとおる");
                    Assert.Greater(t.GetPixel(t.width / 2, t.height / 2).a, 0.95f);
                }
                finally { Object.DestroyImmediate(t); }
            }
        }

        [Test]
        public void StampUrlIsAValidFileUrlInTheEditor()
        {
            string url = StampCatalog.Url(1);
            StringAssert.StartsWith("file:///", url);
            using (var req = UnityWebRequest.Get(url))
            {
                var op = req.SendWebRequest();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (!op.isDone && sw.ElapsedMilliseconds < 5000) { }
                Assert.AreEqual(UnityWebRequest.Result.Success, req.result, req.error);
                Assert.Greater(req.downloadHandler.data.Length, 1000);
            }
        }

        [Test]
        public void UiPicturesForSongsMenuAndInstruments()
        {
            var ui = Ui;
            Assert.IsNotNull(ui.bodyFont);
            Assert.IsNotNull(ui.titleFont);
            Assert.IsNotNull(ui.logo);
            foreach (var s in Songs.All) Assert.IsNotNull(ui.Character(s.animal), $"{s.title} の先生 {s.animal}");
            foreach (var a in new[] { "cat", "rabbit", "bear", "frog", "panda", "chick", "monkey", "elephant", "bird", "sheep", "dog" })
                Assert.IsNotNull(ui.Character(a), a);
            foreach (var info in InstrumentCatalog.All) Assert.IsNotNull(ui.Icon("inst_" + info.id), info.id);
            foreach (var i in new[] { "back", "gear", "piano", "stamp", "left", "right", "minus", "plus", "star", "note1", "note2", "hint", "ear", "songs",
                         "volume_low", "volume_high", "sec_sound", "sec_keys", "sec_mimic", "sec_screen", "sec_parents" })
                Assert.IsNotNull(ui.Icon(i), i);
        }

        [Test]
        public void FontCoversEveryDisplayedString()
        {
            var font = Ui.bodyFont;
            var text = new HashSet<char>();
            var rx = new Regex("\"((?:[^\"\\\\]|\\\\.)*)\"");
            foreach (var path in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
                foreach (Match m in rx.Matches(File.ReadAllText(path)))
                    foreach (char c in m.Groups[1].Value) text.Add(c);
            foreach (char c in File.ReadAllText("Assets/UI/Main.uxml")) text.Add(c);
            foreach (var e in StampCatalog.Entries) foreach (char c in e.name) text.Add(c);
            var missing = text.Where(c => c >= 0x3000 && !font.HasCharacter(c)).ToList();
            Assert.IsEmpty(missing, "フォントに ない字（tools/make_fonts.py をもう一度）: " + new string(missing.ToArray()));
        }
    }

    public class ProjectConfigTests
    {
        [Test]
        public void MainSceneIsTheOnlyBuildScene()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.AreEqual(1, scenes.Length);
            Assert.AreEqual("Assets/Scenes/Main.unity", scenes[0].path);
            Assert.IsTrue(scenes[0].enabled);
        }

        [Test]
        public void WebGLSettings()
        {
            Assert.AreEqual("PROJECT:Piamo", PlayerSettings.WebGL.template);
            Assert.AreEqual(WebGLCompressionFormat.Gzip, PlayerSettings.WebGL.compressionFormat);
            Assert.IsTrue(PlayerSettings.WebGL.decompressionFallback);
            Assert.AreEqual(ColorSpace.Linear, PlayerSettings.colorSpace);
            Assert.AreEqual("Piamo", PlayerSettings.productName);
            Assert.IsTrue(File.Exists("Assets/WebGLTemplates/Piamo/index.html"));
            Assert.IsTrue(File.Exists("Assets/Plugins/WebGL/PiamoPage.jslib"));
            string html = File.ReadAllText("Assets/WebGLTemplates/Piamo/index.html");
            StringAssert.Contains("viewport-fit=cover", html, "iPhone のノッチ");
            StringAssert.Contains("user-scalable=no", html, "ピンチで拡大しない");
            StringAssert.Contains("manifest.webmanifest", html);
        }

        [Test]
        public void UsesTheNewInputSystemAndURP()
        {
            var ps = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            Assert.AreEqual(1, ps.FindProperty("activeInputHandler").intValue);
            Assert.IsNotNull(GraphicsSettings.defaultRenderPipeline);
            Assert.AreEqual("PiamoURP", GraphicsSettings.defaultRenderPipeline.name);
        }

        [Test]
        public void PanelScalesWithTheScreen()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/PiamoPanel.asset");
            Assert.AreEqual(PanelScaleMode.ScaleWithScreenSize, panel.scaleMode);
            Assert.IsNull(panel.targetTexture);
        }

        [Test]
        public void SceneIsWired()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            // スクリプトの見つからない部品がない（クラス名とファイル名がちがうと、こうなる）
            foreach (var root in scene.GetRootGameObjects())
            foreach (var c in root.GetComponentsInChildren<Component>(true))
                Assert.IsTrue(c != null, $"{root.name} に、スクリプトの見つからない部品がある");
            var app = GameObject.Find("App");
            Assert.IsNotNull(app, "jslib が SendMessage する GameObject「App」");
            var ui = app.GetComponent<AppUI>();
            Assert.IsTrue(ui.document != null);
            Assert.IsTrue(ui.uiAssets != null);
            Assert.IsTrue(ui.play != null);
            Assert.IsTrue(ui.input != null);
            Assert.IsTrue(ui.audioOut != null);
            Assert.IsTrue(ui.stampImages != null, "StampImages");
            Assert.IsTrue(ui.play.view != null);
            Assert.IsTrue(ui.play.view.assets != null);
            Assert.IsTrue(ui.play.cam != null);
            Assert.IsTrue(ui.play.backdrop != null);
            Assert.IsTrue(ui.audioOut.bank != null);
            var sfx = ui.audioOut.sfx.Select(s => s.name).ToList();
            foreach (var n in new[] { "tap", "back", "open", "stamp", "stamp_big", "success", "clear", "hint", "earn", "page", "listen", "oops" })
                CollectionAssert.Contains(sfx, n);
            Assert.IsNotNull(ui.document.visualTreeAsset);
        }

        [Test]
        public void UxmlHasEveryScreenAndBackButton()
        {
            var vta = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Main.uxml");
            var root = vta.Instantiate();
            foreach (var n in new[] { "menu", "play", "stamps", "settings", "btn-play", "btn-stamps", "btn-settings",
                         "play-back", "stamps-back", "settings-back", "play-settings", "tab-free", "tab-mimic", "keyboard-area" })
                Assert.IsNotNull(root.Q(n), n);
        }
    }
}
