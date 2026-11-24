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
        private const string MenuPath = "Athoria/Play começa pela Seleção de Personagens";
        private const string PrefKey = "Athoria.PlayFromSelection";

        static PlayModeStartScene() => EditorApplication.delayCall += Apply;

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
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(CharacterSelectSceneBuilder.ScenePath);
            EditorSceneManager.playModeStartScene = Enabled ? scene : null;
            if (Enabled && scene == null)
                Debug.LogWarning("[Athoria] Cena de seleção não encontrada; rode Athoria > Reconstruir tudo.");
        }
    }
}
