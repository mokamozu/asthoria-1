using System.IO;
using UnityEditor;
using UnityEngine;

namespace Athoria.Editor
{
    /// <summary>Cria (ou reaproveita) materiais URP Lit simples para conteúdo provisório.</summary>
    public static class EditorMaterials
    {
        public const string Folder = "Assets/_Athoria/Materials/Provisorios";

        public static Material GetOrCreate(string name, Color color, float metallic = 0f, float smoothness = 0.2f, Color? emission = null)
        {
            Directory.CreateDirectory(Folder);
            var path = $"{Folder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            // Shader de PS1 (luz por vértice, tremor, névoa); sem ele, o Lit do URP.
            var shader = Shader.Find("Athoria/PSX Lit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader) mat.shader = shader;

            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            if (!emission.HasValue) mat.SetColor("_EmissionColor", Color.black);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
