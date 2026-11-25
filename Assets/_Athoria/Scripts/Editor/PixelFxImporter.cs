using System;
using System.IO;
using Athoria.Gameplay.Effects;
using UnityEditor;
using UnityEngine;

namespace Athoria.Editor
{
    /// <summary>
    /// Importa sprite sheets no formato do Aseprite (PNG + JSON), como as
    /// exportadas pelo PixelFX (github.com/patch-gg/pixelfx, licença MIT).
    /// Para cada JSON em Art/FX/PixelFX: ajusta a textura (pixel art nítida) e
    /// cria/atualiza um asset SpriteFxAnimation ao lado dela.
    /// </summary>
    public static class PixelFxImporter
    {
        public const string Root = "Assets/_Athoria/Art/FX/PixelFX";

        [Serializable] private class RectJ { public int x, y, w, h; }
        [Serializable] private class SizeJ { public int w, h; }
        [Serializable] private class FrameJ { public string filename; public RectJ frame; public int duration; }
        [Serializable] private class PixelFxJ { public string id; public float fps; public bool loop; public int[] frameSize; public int[] pivot; }
        [Serializable] private class MetaJ { public string image; public SizeJ size; public PixelFxJ pixelfx; }
        [Serializable] private class SheetJ { public FrameJ[] frames; public MetaJ meta; }

        [MenuItem("Athoria/Efeitos/Importar sprite sheets PixelFX")]
        public static void ImportMenu()
        {
            var count = ImportAll();
            Debug.Log($"[Athoria] {count} efeito(s) PixelFX importado(s) de {Root}.");
        }

        public static int ImportAll()
        {
            if (!Directory.Exists(Root)) return 0;
            var count = 0;
            foreach (var json in Directory.GetFiles(Root, "*.json", SearchOption.AllDirectories))
                if (Import(json.Replace('\\', '/')) != null) count++;
            AssetDatabase.SaveAssets();
            return count;
        }

        public static SpriteFxAnimation Load(string name) =>
            AssetDatabase.LoadAssetAtPath<SpriteFxAnimation>($"{Root}/{name}/{name}.asset");

        public static SpriteFxAnimation Import(string jsonPath)
        {
            var sheet = JsonUtility.FromJson<SheetJ>(File.ReadAllText(jsonPath));
            if (sheet?.frames == null || sheet.meta == null || sheet.frames.Length == 0)
            {
                Debug.LogWarning("[Athoria] JSON de sprite sheet inválido: " + jsonPath);
                return null;
            }

            var dir = Path.GetDirectoryName(jsonPath)!.Replace('\\', '/');
            var pngPath = $"{dir}/{sheet.meta.image}";
            if (!File.Exists(pngPath))
            {
                Debug.LogWarning("[Athoria] Imagem da sprite sheet não encontrada: " + pngPath);
                return null;
            }

            // Textura: pixel art nítida, sem compressão, grande o bastante para a folha inteira.
            AssetDatabase.ImportAsset(pngPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(pngPath);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 8192;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);

            var texH = sheet.meta.size != null && sheet.meta.size.h > 0 ? sheet.meta.size.h : texture.height;
            var rects = new Rect[sheet.frames.Length];
            var durations = new float[sheet.frames.Length];
            for (var i = 0; i < sheet.frames.Length; i++)
            {
                var f = sheet.frames[i].frame;
                rects[i] = new Rect(f.x, texH - f.y - f.h, f.w, f.h);    // JSON: origem em cima; Unity: embaixo
                durations[i] = Mathf.Max(1, sheet.frames[i].duration) / 1000f;
            }

            var pivot = new Vector2(0.5f, 0f);
            var px = sheet.meta.pixelfx;
            if (px?.pivot != null && px.pivot.Length == 2 && px.frameSize != null && px.frameSize.Length == 2)
                pivot = new Vector2(px.pivot[0] / (float)px.frameSize[0], 1f - px.pivot[1] / (float)px.frameSize[1]);

            var assetPath = $"{dir}/{Path.GetFileNameWithoutExtension(jsonPath)}.asset";
            var anim = AssetDatabase.LoadAssetAtPath<SpriteFxAnimation>(assetPath);
            if (anim == null)
            {
                anim = ScriptableObject.CreateInstance<SpriteFxAnimation>();
                AssetDatabase.CreateAsset(anim, assetPath);
            }
            anim.EditorSetup(texture, rects, durations, pivot, px != null && px.loop);
            return anim;
        }
    }
}
