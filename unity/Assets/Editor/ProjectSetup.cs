using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Piamo.EditorTools
{
    /// <summary>
    /// プロジェクトを一発で組み立てるセットアップ。
    /// URP の設定・マテリアル・音と絵の取り込み・UI・シーン（Main.unity）を作る。
    ///   メニュー: Piamo/Setup Project
    ///   バッチ  : Unity -batchmode -executeMethod Piamo.EditorTools.ProjectSetup.RunBatch
    /// 何度実行しても同じ結果になる（既存のアセットは GUID を保ったまま更新）。
    /// </summary>
    public static class ProjectSetup
    {
        const string Settings = "Assets/Settings";
        const string Materials = "Assets/Art/Materials";
        const string Models = "Assets/Art/Models";
        public const string ScenePath = BuildScript.ScenePath;

        [MenuItem("Piamo/Setup Project", priority = 0)]
        public static void Run()
        {
            EnsureFolder(Settings);
            EnsureFolder(Materials);
            EnsureFolder("Assets/Scenes");
            SetupPlayerSettings();
            var urp = SetupURP();
            ReimportFolder("Assets/Art/Models");
            ReimportFolder("Assets/Audio");
            ReimportFolder("Assets/UI");
            SetupFonts();
            var piano = SetupPianoAssets();
            var bank = SetupInstruments();
            var ui = SetupUiAssets();
            var panel = SetupPanelSettings();
            BuildScene(piano, bank, ui, panel);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Setup] 完了しました: " + ScenePath);
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static T LoadOrCreate<T>(string path, Func<T> create) where T : UnityEngine.Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = create();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        static void ReimportFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;
            AssetDatabase.ImportAsset(folder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        }

        // ------------------------------------------------------------------
        static void SetupPlayerSettings()
        {
            PlayerSettings.companyName = "Ksan";
            PlayerSettings.productName = "Piamo";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = "PROJECT:Piamo";
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithoutStacktrace;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.showDiagnostics = false;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Minimal);
            // スマホ・タブレット（ネイティブでビルドするとき）は、どの向きでも
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.ksan.piamo");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.ksan.piamo");

            // 新しい Input System のみを使う
            var ps = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input = ps.FindProperty("activeInputHandler");
            if (input != null) input.intValue = 1;
            ps.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------
        static UniversalRenderPipelineAsset SetupURP()
        {
            var rd = LoadOrCreate($"{Settings}/PiamoRenderer.asset", () => ScriptableObject.CreateInstance<UniversalRendererData>());
            rd.renderingMode = RenderingMode.Forward;
            rd.postProcessData = null;
            EditorUtility.SetDirty(rd);

            var urp = LoadOrCreate($"{Settings}/PiamoURP.asset", () => UniversalRenderPipelineAsset.Create(rd));
            var so = new SerializedObject(urp);
            void B(string n, bool v) { var p = so.FindProperty(n); if (p != null) p.boolValue = v; else Debug.LogWarning("URP field not found: " + n); }
            void I(string n, int v) { var p = so.FindProperty(n); if (p != null) { if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = v; else p.intValue = v; } else Debug.LogWarning("URP field not found: " + n); }
            void F(string n, float v) { var p = so.FindProperty(n); if (p != null) p.floatValue = v; else Debug.LogWarning("URP field not found: " + n); }
            B("m_RequireDepthTexture", false);
            B("m_RequireOpaqueTexture", false);
            B("m_SupportsHDR", false);
            I("m_MSAA", 4);
            F("m_RenderScale", 1f);
            B("m_MainLightShadowsSupported", false);
            B("m_AdditionalLightShadowsSupported", false);
            B("m_SoftShadowsSupported", false);
            B("m_UseSRPBatcher", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);

            GraphicsSettings.defaultRenderPipeline = urp;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(current, false);
            return urp;
        }

        static void SetupFonts()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Font", new[] { "Assets/UI/Fonts" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(p) is TrueTypeFontImporter imp)
                {
                    imp.fontTextureCase = FontTextureCase.Dynamic;
                    imp.includeFontData = true;
                    imp.SaveAndReimport();
                }
            }
        }

        // ------------------------------------------------------------------
        static Material Mat(string name, string shader, Action<Material> setup)
        {
            string path = $"{Materials}/{name}.mat";
            var sh = Shader.Find(shader);
            if (sh == null) throw new Exception("Shader not found: " + shader);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = sh;
            setup(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Mesh LoadMesh(string name)
        {
            string path = $"{Models}/{name}.fbx";
            var mesh = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault();
            if (mesh == null) throw new Exception("Mesh not found: " + path + "（blender/scripts/build_piano_kit.py を実行してください）");
            var b = mesh.bounds;
            Debug.Log($"[Setup] mesh {name} verts={mesh.vertexCount} min={b.min} max={b.max}");
            return mesh;
        }

        static PianoAssets SetupPianoAssets()
        {
            var a = LoadOrCreate($"{Settings}/PianoAssets.asset", () => ScriptableObject.CreateInstance<PianoAssets>());
            a.whiteFull = LoadMesh("Key_White_Full");
            a.whiteLeft = LoadMesh("Key_White_L");
            a.whiteRight = LoadMesh("Key_White_R");
            a.whiteBoth = LoadMesh("Key_White_LR");
            a.black = LoadMesh("Key_Black");
            a.caseEndLeft = LoadMesh("Case_End_L");
            a.caseEndRight = LoadMesh("Case_End_R");
            a.caseBack = LoadMesh("Case_Back");
            a.caseFront = LoadMesh("Case_Front");
            a.caseFelt = LoadMesh("Case_Felt");
            a.caseBed = LoadMesh("Case_Bed");

            a.keyMaterial = Mat("M_Key", "Piamo/ToyLit", m =>
            {
                m.SetColor("_BaseColor", Color.white);
                m.SetColor("_ShadowColor", new Color(0.78f, 0.76f, 0.88f));
                m.SetFloat("_Gloss", 0.8f);
                m.SetFloat("_SpecStrength", 0.55f);
                m.SetFloat("_RimStrength", 0.18f);
            });
            a.caseMaterial = Mat("M_Case", "Piamo/ToyLit", m =>
            {
                m.SetColor("_BaseColor", PianoColors.Colors[0]);
                m.SetColor("_ShadowColor", new Color(0.74f, 0.7f, 0.84f));
                m.SetFloat("_Gloss", 0.7f);
                m.SetFloat("_SpecStrength", 0.45f);
                m.SetFloat("_RimStrength", 0.22f);
            });
            a.feltMaterial = Mat("M_Felt", "Piamo/ToyLit", m =>
            {
                m.SetColor("_BaseColor", new Color(0.82f, 0.18f, 0.25f));
                m.SetColor("_ShadowColor", new Color(0.7f, 0.6f, 0.7f));
                m.SetFloat("_Gloss", 0.05f);
                m.SetFloat("_SpecStrength", 0f);
                m.SetFloat("_RimStrength", 0.05f);
            });
            a.bedMaterial = Mat("M_Bed", "Piamo/ToyLit", m =>
            {
                m.SetColor("_BaseColor", new Color(0.32f, 0.24f, 0.3f));
                m.SetColor("_ShadowColor", new Color(0.8f, 0.8f, 0.8f));
                m.SetFloat("_Gloss", 0.1f);
                m.SetFloat("_SpecStrength", 0f);
                m.SetFloat("_RimStrength", 0f);
            });
            a.backdropMaterial = Mat("M_Backdrop", "Piamo/Backdrop", m => { });
            EditorUtility.SetDirty(a);
            return a;
        }

        static InstrumentBank SetupInstruments()
        {
            var bank = LoadOrCreate($"{Settings}/InstrumentBank.asset", () => ScriptableObject.CreateInstance<InstrumentBank>());
            bank.instruments.Clear();
            foreach (var info in InstrumentCatalog.All)
            {
                string folder = $"Assets/Audio/Instruments/{info.id}";
                var clips = new List<(int note, AudioClip clip)>();
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    string file = Path.GetFileNameWithoutExtension(p);
                    int us = file.LastIndexOf('_');
                    if (us < 0 || !int.TryParse(file.Substring(us + 1), out int note)) continue;
                    clips.Add((note, AssetDatabase.LoadAssetAtPath<AudioClip>(p)));
                }
                clips.Sort((x, y) => x.note.CompareTo(y.note));
                if (clips.Count == 0) Debug.LogWarning($"[Setup] 楽器 {info.id} の音がありません（tools/make_audio.py）");
                bank.instruments.Add(new InstrumentBank.Instrument
                {
                    id = info.id,
                    roots = clips.Select(c => c.note).ToArray(),
                    clips = clips.Select(c => c.clip).ToArray(),
                });
            }
            EditorUtility.SetDirty(bank);
            return bank;
        }

        static UiAssets SetupUiAssets()
        {
            var ui = LoadOrCreate($"{Settings}/UiAssets.asset", () => ScriptableObject.CreateInstance<UiAssets>());
            ui.bodyFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/PiamoRounded-ExtraBold.ttf");
            ui.titleFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/PiamoRounded-Black.ttf");
            ui.logo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/Images/logo.png");
            ui.icons = Textures("Assets/UI/Icons");
            ui.characters = Textures("Assets/UI/Characters");
            EditorUtility.SetDirty(ui);
            return ui;
        }

        static List<UiAssets.NamedTexture> Textures(string folder)
        {
            var list = new List<UiAssets.NamedTexture>();
            if (!AssetDatabase.IsValidFolder(folder)) return list;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                list.Add(new UiAssets.NamedTexture { name = Path.GetFileNameWithoutExtension(p), texture = AssetDatabase.LoadAssetAtPath<Texture2D>(p) });
            }
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list;
        }

        static PanelSettings SetupPanelSettings()
        {
            string tssPath = "Assets/UI/PiamoTheme.tss";
            if (!File.Exists(tssPath))
            {
                File.WriteAllText(tssPath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(tssPath);
            }
            var panel = LoadOrCreate("Assets/UI/PiamoPanel.asset", () => ScriptableObject.CreateInstance<PanelSettings>());
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(tssPath);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.sortingOrder = 0;
            panel.clearColor = false;
            EditorUtility.SetDirty(panel);
            return panel;
        }

        // ------------------------------------------------------------------
        static void BuildScene(PianoAssets piano, InstrumentBank bank, UiAssets ui, PanelSettings panel)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.8f, 0.8f, 0.85f);
            RenderSettings.fog = false;

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.97f, 0.92f);
            sun.intensity = 1.05f;
            sun.shadows = LightShadows.None;
            sunGo.transform.rotation = Quaternion.Euler(58f, -28f, 0f);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Themes.Bottom[0];
            cam.allowMSAA = true;
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = false;
            camData.antialiasing = AntialiasingMode.None;
            camData.renderShadows = false;
            camGo.AddComponent<AudioListener>();
            camGo.transform.SetPositionAndRotation(new Vector3(4f, 10f, -30f), Quaternion.Euler(KeyboardFraming.TiltDegrees, 0, 0));

            var backdropGo = new GameObject("Backdrop");
            backdropGo.transform.SetParent(camGo.transform, false);
            backdropGo.transform.localPosition = new Vector3(0, 0, KeyboardFraming.CameraDistance * 2f);
            backdropGo.AddComponent<MeshFilter>();
            var backdrop = backdropGo.AddComponent<MeshRenderer>();
            backdrop.sharedMaterial = piano.backdropMaterial;
            backdrop.shadowCastingMode = ShadowCastingMode.Off;
            backdrop.receiveShadows = false;

            var pianoGo = new GameObject("Piano");
            var view = pianoGo.AddComponent<KeyboardView>();
            view.assets = piano;

            var app = new GameObject("App");
            var doc = app.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Main.uxml");
            var audio = app.AddComponent<PianoAudio>();
            audio.bank = bank;
            audio.sfx = new List<PianoAudio.NamedClip>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio/Sfx" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                audio.sfx.Add(new PianoAudio.NamedClip { name = Path.GetFileNameWithoutExtension(p), clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p) });
            }
            audio.sfx.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            var play = app.AddComponent<PlayController>();
            play.view = view;
            play.audioOut = audio;
            play.cam = cam;
            play.backdrop = backdrop;
            var input = app.AddComponent<PianoInput>();
            input.play = play;
            var images = app.AddComponent<StampImages>();
            var appUi = app.AddComponent<AppUI>();
            appUi.document = doc;
            appUi.uiAssets = ui;
            appUi.play = play;
            appUi.input = input;
            appUi.audioOut = audio;
            appUi.stampImages = images;

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
