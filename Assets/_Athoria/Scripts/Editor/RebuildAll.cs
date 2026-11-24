using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Athoria.Editor
{
    /// <summary>
    /// Um comando só para regenerar todo o conteúdo gerado por código
    /// (dados, ícones, Prefabs e cenas) depois de cada etapa.
    /// </summary>
    public static class RebuildAll
    {
        [MenuItem("Athoria/Reconstruir tudo (Prefabs e Cenas)", priority = 0)]
        public static void Run()
        {
            if (!EditorUtility.DisplayDialog("Athoria",
                    "Recriar todos os Prefabs, dados, ícones e as cenas Sandbox e Seleção de Personagens?\n\n" +
                    "Mudanças feitas à mão nesses arquivos gerados serão perdidas.",
                    "Reconstruir", "Cancelar"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            try
            {
                EditorUtility.DisplayProgressBar("Athoria", "HUD...", 0.1f);
                HudPrefabBuilder.Build();
                EditorUtility.DisplayProgressBar("Athoria", "Inventário...", 0.25f);
                InventoryUiBuilder.Build();
                EditorUtility.DisplayProgressBar("Athoria", "Jogador, armas, itens e ícones...", 0.45f);
                PlayerPrefabBuilder.Build();
                EditorUtility.DisplayProgressBar("Athoria", "Cena de seleção...", 0.7f);
                CharacterSelectSceneBuilder.Build();
                EditorUtility.DisplayProgressBar("Athoria", "Sandbox...", 0.85f);
                SandboxSceneBuilder.Build(false);
                Debug.Log("[Athoria] Reconstrução completa. Abra SelecaoPersonagem ou Sandbox e dê Play.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
