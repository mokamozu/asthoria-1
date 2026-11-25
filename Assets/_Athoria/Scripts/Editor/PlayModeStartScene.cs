using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Athoria.Editor
{
    /// <summary>
    /// Faz o Play do editor começar sempre pela tela de seleção de personagens,
    /// como no jogo final, não importa qual cena esteja aberta.
    /// Pode ser desligado pelo menu (para testar uma cena direto).
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeStartScene
    {
        private const string MenuPath = "Athoria/Play começa pelo Menu Principal";
        private const string PrefKey = "Athoria.PlayFromSelection";

        static PlayModeStartScene()
        {
            EditorApplication.delayCall += Apply;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        /// <summary>Lembra a cena aberta: depois da seleção, o jogo vai para ela.</summary>
        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode) return;
            var active = EditorSceneManager.GetActiveScene();
            var isGameScene = !string.IsNullOrEmpty(active.path) && active.path != CharacterSelectSceneBuilder.ScenePath
                              && active.path != MainMenuSceneBuilder.ScenePath
                              && !active.name.StartsWith("TA_");   // pedaços do mundo abrem a região inteira
            SessionState.SetString("Athoria.PlayTarget", isGameScene ? active.name : string.Empty);
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        [MenuItem(MenuPath, priority = 1)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static void Apply()
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuSceneBuilder.ScenePath);
            EditorSceneManager.playModeStartScene = Enabled ? scene : null;
            if (Enabled && scene == null)
                Debug.LogWarning("[Athoria] Menu principal não encontrado; rode Athoria > Reconstruir tudo.");
        }
    }
}
