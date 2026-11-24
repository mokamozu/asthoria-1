using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Athoria.Editor
{
    /// <summary>
    /// Gera ícones de item fotografando o modelo 3D numa cena de pré-visualização.
    /// O ícone tem a proporção do item na grade (ex.: 1×3 para uma espada).
    /// </summary>
    public static class IconBaker
    {
        public const string Folder = "Assets/_Athoria/Art/Icones/Gerados";
        private const int PixelsPerCell = 96;

        public static Sprite Bake(GameObject model, string fileName, int cellsW, int cellsH, Vector3 euler)
        {
            if (model == null) return null;
            Directory.CreateDirectory(Folder);
            var w = Mathf.Max(1, cellsW) * PixelsPerCell;
            var h = Mathf.Max(1, cellsH) * PixelsPerCell;

            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var go = Object.Instantiate(model);
                SceneManager.MoveGameObjectToScene(go, scene);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(euler));
                var renderers = go.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) return null;
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);

                var camGo = new GameObject("Camera Icone");
                SceneManager.MoveGameObjectToScene(camGo, scene);
                var cam = camGo.AddComponent<Camera>();
                cam.scene = scene;
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 50f;
                var aspect = (float)w / h;
                cam.aspect = aspect;
                cam.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect) * 1.12f;
                camGo.transform.SetPositionAndRotation(bounds.center + new Vector3(0f, 0f, -10f), Quaternion.identity);

                AddLight(scene, new Vector3(35f, -30f, 0f), 1.5f, Color.white);
                AddLight(scene, new Vector3(-20f, 150f, 0f), 0.6f, new Color(0.8f, 0.85f, 1f));

                var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = previous;
                cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);

                var path = $"{Folder}/{fileName}.png";
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void AddLight(Scene scene, Vector3 euler, float intensity, Color color)
        {
            var go = new GameObject("Luz");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.rotation = Quaternion.Euler(euler);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = color;
        }
    }
}
