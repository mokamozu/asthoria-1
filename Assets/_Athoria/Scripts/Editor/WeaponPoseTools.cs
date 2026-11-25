using Athoria.Gameplay.Combat;
using UnityEditor;
using UnityEngine;

namespace Athoria.Editor
{
    /// <summary>
    /// Ajuste manual das armas na mão: com o jogo rodando, seleciona a ficha da arma que
    /// está na mão para mexer nas poses pelo Inspector e ver o resultado na hora.
    /// As poses ajustadas à mão ficam salvas e não são sobrescritas pela geração automática.
    /// </summary>
    public static class WeaponPoseTools
    {
        [MenuItem("Athoria/Ajustes/Selecionar a arma que está na mão (Play) %#w")]
        public static void SelectWeaponInHand()
        {
            var loadout = Object.FindAnyObjectByType<WeaponLoadout>();
            if (!Application.isPlaying || loadout == null || loadout.MainHand == null)
            {
                Debug.LogWarning("[Athoria] Dê Play e equipe uma arma; depois use este atalho (Ctrl+Shift+W).");
                return;
            }
            Selection.activeObject = loadout.MainHand;
            EditorGUIUtility.PingObject(loadout.MainHand);
            Debug.Log($"[Athoria] Ajuste '{loadout.MainHand.DisplayName}' no Inspector: Model Pose (arma na mão), " +
                      "Holding Arm Pose (braço direito) e Support Arm Pose (braço esquerdo).");
        }

        [MenuItem("Athoria/Ajustes/Listar armas com poses ajustadas à mão")]
        public static void ListEdited()
        {
            var any = false;
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponData"))
            {
                var w = AssetDatabase.LoadAssetAtPath<Athoria.Gameplay.Items.WeaponData>(AssetDatabase.GUIDToAssetPath(guid));
                if (w == null || !w.PosesEditedByHand) continue;
                Debug.Log($"[Athoria] Poses ajustadas à mão: {w.DisplayName}", w);
                any = true;
            }
            if (!any) Debug.Log("[Athoria] Nenhuma arma com poses ajustadas à mão.");
        }
    }
}
