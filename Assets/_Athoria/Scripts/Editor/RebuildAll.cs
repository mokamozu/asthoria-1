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
                    "Recriar todos os Prefabs, dados, ícones e as cenas (Menu, Seleção, Sandbox, Terras Alagadas e Cripta)?\n\nO mundo aberto leva alguns minutos.\n\n" +
                    "Mudanças feitas à mão nesses arquivos gerados serão perdidas.",
                    "Reconstruir", "Cancelar"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            try
            {
                EditorUtility.DisplayProgressBar("Athoria", "Render retrô...", 0.05f);
                RetroRenderSetup.Run();
                EditorUtility.DisplayProgressBar("Athoria", "HUD...", 0.1f);
                HudPrefabBuilder.Build();
                EditorUtility.DisplayProgressBar("Athoria", "Inventário...", 0.25f);
                InventoryUiBuilder.Build();
                EditorUtility.DisplayProgressBar("Athoria", "Jogador, armas, itens e ícones...", 0.45f);
                PlayerPrefabBuilder.Build();
                EditorUtility.DisplayProgressBar("Athoria", "Cena de seleção...", 0.7f);
                CharacterSelectSceneBuilder.Build();
                MainMenuSceneBuilder.Build();
                EditorUtility.DisplayProgressBar("Athoria", "Sandbox...", 0.85f);
                SandboxSceneBuilder.Build(false);
                EditorUtility.DisplayProgressBar("Athoria", "Cripta da Capela...", 0.9f);
                CriptaBuilder.Build(false);
                GameDatabaseBuilder.Build();
                TerrasAlagadasBuilder.Build(false);   // mostra o próprio progresso
                GameDatabaseBuilder.Build();          // tabelas e itens criados pela região
                MenuArtImport.OrderBuildScenes();     // menu principal sempre primeiro
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
                Debug.Log("[Athoria] Reconstrução completa. Abra SelecaoPersonagem ou Sandbox e dê Play.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
