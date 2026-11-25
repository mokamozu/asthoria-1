using UnityEditor;
using UnityEngine;

namespace Athoria.Editor
{
    /// <summary>
    /// Importação dos pacotes PS1/low poly em Assets/_Athoria/ThirdParty:
    /// texturas nítidas (filtro Point, sem compressão borrada) e modelos sem
    /// câmeras, luzes e animações desnecessárias.
    /// </summary>
    public sealed class ThirdPartyImportSettings : AssetPostprocessor
    {
        public const string Root = "Assets/_Athoria/ThirdParty";

        public const string ArmsTextures = "Assets/_Athoria/Art/Personagens/Bracos";

        private bool InThirdParty => assetPath.StartsWith(Root + "/") || assetPath.StartsWith(ArmsTextures + "/");

        private void OnPreprocessTexture()
        {
            var importer = (TextureImporter)assetImporter;
            if (InThirdParty && importer.importSettingsMissing) Configure(importer, assetPath);
        }

        private void OnPreprocessModel()
        {
            var importer = (ModelImporter)assetImporter;
            if (InThirdParty && importer.importSettingsMissing) Configure(importer, assetPath);
        }

        public static void Configure(TextureImporter importer, string path)
        {
            importer.filterMode = FilterMode.Point;
            importer.anisoLevel = 0;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            if (path.EndsWith(".hdr"))
            {
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.mipmapEnabled = false;
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = TextureWrapMode.Clamp;
                return;
            }
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 512;
            importer.alphaIsTransparency = importer.DoesSourceTextureHaveAlpha();
        }

        public static void Configure(ModelImporter importer, string path)
        {
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            var isArms = path.Contains("Arms");
            importer.importAnimation = isArms;
            importer.animationType = isArms ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.meshCompression = ModelImporterMeshCompression.Off;
        }

        /// <summary>Reaplica as regras em tudo que já foi importado antes deste script existir.</summary>
        [MenuItem("Athoria/PS1/Reaplicar importação dos pacotes", priority = 300)]
        public static void ReapplyAll()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D t:Model", new[] { Root });
            try
            {
                AssetDatabase.StartAssetEditing();
                for (var i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    EditorUtility.DisplayProgressBar("Athoria — pacotes PS1", path, i / (float)guids.Length);
                    switch (AssetImporter.GetAtPath(path))
                    {
                        case TextureImporter t: Configure(t, path); t.SaveAndReimport(); break;
                        case ModelImporter m: Configure(m, path); m.SaveAndReimport(); break;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }
            Debug.Log($"[Athoria] Importação PS1 reaplicada em {guids.Length} arquivos.");
        }
    }
}
