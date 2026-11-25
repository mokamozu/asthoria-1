using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Athoria.Editor
{
    /// <summary>Arte, sons e cena base compartilhados pelo menu principal e pela seleção de personagens.</summary>
    internal static class MenuArtImport
    {
        private const string TilesPath = "Assets/_Athoria/Art/Legacy/UI/ui_tilesheet.png";
        private const string AmbientFolder = "Assets/_Athoria/Art/Legacy/Ambiente";
        private const string VitralPath = "Assets/_Athoria/Art/UI/Menu/vitral_luz.png";
        private const string SoundFolder = "Assets/_Athoria/Audio/UI";

        public static Texture2D Tiles => AssetDatabase.LoadAssetAtPath<Texture2D>(TilesPath);
        public static Texture2D Ambient(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{AmbientFolder}/{name}.png");
        public static Texture2D Vitral => AssetDatabase.LoadAssetAtPath<Texture2D>(VitralPath);
        private const string MenuArtFolder = "Assets/_Athoria/Art/UI/Menu";
        public static Texture2D MenuTexture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{MenuArtFolder}/{name}.png");
        public static AudioClip Sound(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{SoundFolder}/{name}.wav");

        /// <summary>Ajusta a importação: camadas do céu repetem (paralaxe), vitral suave, tileset nítido.</summary>
        public static void Prepare()
        {
            AssetDatabase.Refresh();
            foreach (var layer in new[] { "ceu_00", "lua", "sombra", "nuvem" })
                Configure($"{AmbientFolder}/{layer}.png", i =>
                {
                    i.wrapMode = TextureWrapMode.Repeat;
                    i.filterMode = FilterMode.Point;
                    i.mipmapEnabled = false;
                    i.textureCompression = TextureImporterCompression.Uncompressed;
                });
            Configure(TilesPath, i =>
            {
                i.filterMode = FilterMode.Point;
                i.mipmapEnabled = false;
                i.textureCompression = TextureImporterCompression.Uncompressed;
            });
            // Vitral, parede e vidro dos botões: pixel art nítida.
            foreach (var name in new[] { "vitral_vidro", "vitral_moldura", "vitral_mascara", "parede_pedra", "vidro_botao" })
                Configure($"{MenuArtFolder}/{name}.png", i =>
                {
                    i.textureType = TextureImporterType.Default;
                    i.alphaIsTransparency = true;
                    i.wrapMode = name == "parede_pedra" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                    i.filterMode = FilterMode.Point;
                    i.mipmapEnabled = false;
                    i.npotScale = TextureImporterNPOTScale.None;
                    i.textureCompression = TextureImporterCompression.Uncompressed;
                });
            Configure($"{MenuArtFolder}/vidro_reflexo.png", i =>
            {
                i.textureType = TextureImporterType.Default;
                i.alphaIsTransparency = true;
                i.wrapMode = TextureWrapMode.Clamp;
                i.filterMode = FilterMode.Bilinear;
                i.mipmapEnabled = false;
                i.textureCompression = TextureImporterCompression.Uncompressed;
            });
            Configure(VitralPath, i =>
            {
                i.textureType = TextureImporterType.Default;
                i.alphaIsTransparency = true;
                i.wrapMode = TextureWrapMode.Clamp;
                i.filterMode = FilterMode.Bilinear;
                i.mipmapEnabled = false;
                i.textureCompression = TextureImporterCompression.Uncompressed;
            });
        }

        private static void Configure(string path, System.Action<TextureImporter> setup)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            var before = EditorJsonUtility.ToJson(importer);
            setup(importer);
            if (EditorJsonUtility.ToJson(importer) != before) importer.SaveAndReimport();
        }

        /// <summary>Câmera, EventSystem e Canvas 1920×1080 numa cena vazia. Devolve o Canvas.</summary>
        public static GameObject NewMenuScene()
        {
            var camGo = new GameObject("Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();   // sem ele nenhum som dos menus é ouvido

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            CharacterSelectSceneBuilder.ConfigureUiInput(es.AddComponent<InputSystemUIInputModule>());

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            return canvasGo;
        }

        public static void AssignSounds(SerializedObject so)
        {
            AudioClip Clip(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{SoundFolder}/menu_{n}.wav");
            so.FindProperty("hoverSound").objectReferenceValue = Clip("hover");
            so.FindProperty("clickSound").objectReferenceValue = Clip("click");
            so.FindProperty("confirmSound").objectReferenceValue = Clip("confirm");
            so.FindProperty("backSound").objectReferenceValue = Clip("back");
            so.FindProperty("changeSound").objectReferenceValue = Clip("change");
        }

        /// <summary>Menu principal primeiro, seleção em segundo; o resto mantém a ordem.</summary>
        public static void OrderBuildScenes()
        {
            var first = new List<string> { MainMenuSceneBuilder.ScenePath, CharacterSelectSceneBuilder.ScenePath };
            var scenes = EditorBuildSettings.scenes
                .Where(s => !first.Contains(s.path) && !s.path.EndsWith("SampleScene.unity"))
                .ToList();
            for (var i = first.Count - 1; i >= 0; i--)
                if (File.Exists(first[i])) scenes.Insert(0, new EditorBuildSettingsScene(first[i], true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
