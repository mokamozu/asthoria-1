using UnityEditor;
using UnityEngine;

namespace Athoria.Editor
{
    /// <summary>Garante que as layers usadas pelo jogo existam no projeto.</summary>
    public static class ProjectLayers
    {
        /// <summary>Retorna o índice da layer, criando-a no primeiro slot livre (8-31) se preciso.</summary>
        public static int Ensure(string layerName)
        {
            var existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (var i = 8; i < layers.arraySize; i++)
            {
                var element = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(element.stringValue)) continue;
                element.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[Athoria] Layer '{layerName}' criada no índice {i}.");
                return i;
            }

            Debug.LogError($"[Athoria] Não há slot livre para a layer '{layerName}'.");
            return -1;
        }
    }
}
