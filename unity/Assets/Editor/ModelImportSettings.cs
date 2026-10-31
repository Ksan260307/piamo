using UnityEditor;
using UnityEngine;

namespace Piamo.EditorTools
{
    /// <summary>
    /// Blender から出力した FBX と、音・UI の絵の取り込み設定。
    /// </summary>
    public class ModelImportSettings : AssetPostprocessor
    {
        const string ModelFolder = "Assets/Art/Models";

        // 取り込み設定を変えたら、数字を上げる（取り込みなおされる）
        public override uint GetVersion() => 4;

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelFolder)) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.isReadable = false;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.None;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.importVisibility = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.generateSecondaryUV = false;
            mi.weldVertices = true;
        }

        void OnPreprocessAudio()
        {
            var ai = (AudioImporter)assetImporter;
            var s = ai.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.preloadAudioData = true;
            if (assetPath.Contains("/Instruments/"))
            {
                // 楽器の音は、えらんだ楽器だけを読みこむ（PianoAudio.SetInstrument）。
                // すべての半音の音（610 こ）があるので、ダウンロードが大きくならないように すこし つよく圧縮する
                s.quality = 0.3f;
                s.preloadAudioData = assetPath.Contains("/piano/");
            }
            else s.quality = 0.6f;
            ai.defaultSampleSettings = s;
            ai.forceToMono = true;
            ai.loadInBackground = false;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/UI/") && !assetPath.StartsWith("Assets/WebGLTemplates")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.sRGBTexture = true;
        }
    }
}
