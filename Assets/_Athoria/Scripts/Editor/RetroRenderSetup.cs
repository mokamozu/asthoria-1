using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Athoria.Editor
{
    /// <summary>
    /// Configura os renderers do URP para o visual PS1/N64:
    /// renderização Forward (luz por vértice das tochas nos materiais PSX) e
    /// um passe de tela cheia com o shader "Athoria/PSX Tela" (cores de 15 bits
    /// com pontilhado). Roda no "Reconstruir tudo" e pelo menu Athoria → PS1.
    /// </summary>
    public static class RetroRenderSetup
    {
        private static readonly string[] Renderers = { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" };
        private const string MaterialPath = "Assets/_Athoria/Materials/PSX/PSX_Tela.mat";
        private const string FeatureName = "PSX Tela (cores e pontilhado)";

        [MenuItem("Athoria/PS1/Configurar render retrô (névoa, pontilhado)", priority = 301)]
        public static void Run()
        {
            var shader = Shader.Find("Athoria/PSX Tela");
            if (shader == null)
            {
                Debug.LogWarning("[Athoria] Shader Athoria/PSX Tela não encontrado (o Unity já importou a pasta Shaders?).");
                return;
            }
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(MaterialPath));
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            mat.shader = shader;

            foreach (var path in Renderers)
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null) continue;
                data.renderingMode = RenderingMode.Forward;

                var feature = data.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault(f => f != null && f.name == FeatureName);
                if (feature == null)
                {
                    feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                    feature.name = FeatureName;
                    AssetDatabase.AddObjectToAsset(feature, data);
                    data.rendererFeatures.Add(feature);
                    // mapa interno do renderer (o editor do URP faz o mesmo ao adicionar uma feature)
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                    var so = new SerializedObject(data);
                    var map = so.FindProperty("m_RendererFeatureMap");
                    map.arraySize++;
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                feature.passMaterial = mat;
                feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
                feature.fetchColorBuffer = true;
                feature.SetActive(true);
                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(data);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Athoria] Render retrô configurado (Forward + PSX Tela).");
        }
    }
}
