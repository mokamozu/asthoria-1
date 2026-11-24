using UnityEditor;
using UnityEngine;

namespace Athoria.Editor
{
    /// <summary>
    /// Configura automaticamente a importação das artes vindas do Asthoria antigo
    /// (GameMaker): Sprite, filtro Point (pixel art nítida), sem mipmaps.
    /// Só age na primeira importação, para respeitar ajustes manuais depois.
    /// Use o menu "Athoria/Arte/Reaplicar importação da arte Legacy" para forçar.
    /// </summary>
    public sealed class LegacyArtImportSettings : AssetPostprocessor
    {
        private const string LegacyRoot = "Assets/_Athoria/Art/Legacy";
        private static bool s_Force;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(LegacyRoot + "/")) return;

            var importer = (TextureImporter)assetImporter;
            if (!importer.importSettingsMissing && !s_Force) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;

            importer.GetSourceTextureWidthAndHeight(out var width, out var height);
            var largest = Mathf.Max(width, height);

            if (largest <= 1024)
            {
                // Ícones, retratos da HUD e sprites pequenos: sem compressão, pixel perfeito.
                importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
            }
            else
            {
                // Retratos e artes de corpo inteiro (4096 px): reduz para 2048 e
                // usa compressão de alta qualidade para não pesar no projeto.
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
            }
        }

        [MenuItem("Athoria/Arte/Reaplicar importação da arte Legacy")]
        private static void ReapplyAll()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { LegacyRoot });
            s_Force = true;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var guid in guids)
                    AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                s_Force = false;
            }
            Debug.Log($"[Athoria] Importação reaplicada em {guids.Length} texturas da arte Legacy.");
        }
    }
}
