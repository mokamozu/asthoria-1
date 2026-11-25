using System.Collections.Generic;
using System.IO;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Items;
using UnityEditor;
using UnityEngine;

namespace Athoria.Editor
{
    /// <summary>
    /// Gera os conjuntos padrão de ações dos braços em primeira pessoa. Os assets só são
    /// escritos quando não existem (ajustes feitos à mão ficam); use o menu para recriar.
    /// Convenções: posição em metros e rotação em graus, somadas à pose de cada braço no
    /// espaço da câmera (X direita, Y cima, Z frente). Curvas: antecipação → execução rápida
    /// → acomodação, com o ponto mais rápido por volta de 60–70% do gesto.
    /// </summary>
    public static class ViewModelActionFactory
    {
        private const string Folder = "Assets/_Athoria/Data/Combate";
        public const string DefaultPath = Folder + "/AcoesBracos_Padrao.asset";
        public const string TwoHandedPath = Folder + "/AcoesBracos_DuasMaos.asset";

        [MenuItem("Athoria/Dados/Recriar ações dos braços (1ª pessoa)")]
        private static void Recreate() { GetOrCreateDefault(true); Debug.Log("[Athoria] Ações dos braços recriadas."); }

        public static ViewModelActionSet GetOrCreateDefault(bool force = false)
        {
            Directory.CreateDirectory(Folder);
            var def = Build(DefaultPath, force, Default());
            var two = Build(TwoHandedPath, force, TwoHanded());
            AssignTwoHanded(two);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static ViewModelActionSet Build(string path, bool force, List<ViewModelAction> actions)
        {
            var set = AssetDatabase.LoadAssetAtPath<ViewModelActionSet>(path);
            if (set != null && !force && set.actions != null && set.actions.Length > 0) return set;
            if (set == null) { set = ScriptableObject.CreateInstance<ViewModelActionSet>(); AssetDatabase.CreateAsset(set, path); }
            set.actions = actions.ToArray();
            EditorUtility.SetDirty(set);
            return set;
        }

        /// <summary>Armas de duas mãos sem conjunto próprio usam o de duas mãos (sacar das costas).</summary>
        private static void AssignTwoHanded(ViewModelActionSet two)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponData"))
            {
                var w = AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(guid));
                if (w == null || !w.IsTwoHanded || w.ViewModelActions != null) continue;
                var so = new SerializedObject(w);
                so.FindProperty("viewModelActions").objectReferenceValue = two;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ------------------------------------------------------------------ ajudantes

        private static readonly Vector3 Z = Vector3.zero;

        private static ViewModelKey K(float t, Vector3 rp, Vector3 re, Vector3 lp, Vector3 le,
            float support = 1f, float rFingers = 1f, float lFingers = 1f) => new()
        {
            time = t, rightPosition = rp, rightEuler = re, leftPosition = lp, leftEuler = le,
            supportGrip = support, rightFingers = rFingers, leftFingers = lFingers,
        };

        /// <summary>Os dois braços juntos (golpe recebido, esquiva, aparar).</summary>
        private static ViewModelKey Both(float t, Vector3 p, Vector3 e, float support = 1f) => K(t, p, e, p, e, support);

        private static ViewModelKey Rest(float t, float support = 1f) => K(t, Z, Z, Z, Z, support);

        private static ViewModelAction A(ViewModelActionId id, float duration, float eventTime, bool combat, int priority, params ViewModelKey[] keys)
            => new() { id = id, duration = duration, eventTime = eventTime, interruptsCombat = combat, priority = priority, keys = keys };

        private static Vector3 V(float x, float y, float z) => new(x, y, z);

        // ------------------------------------------------------------------ conjuntos

        private static List<ViewModelAction> Common() => new()
        {
            // Aparar: estoque curto para fora, impacto empurra de volta, acomoda.
            A(ViewModelActionId.Parry, 0.42f, 0.15f, true, 3,
                Rest(0f),
                Both(0.14f, V(-0.03f, 0.06f, 0.05f), V(-15f, -10f, 20f)),
                Both(0.34f, V(0.02f, 0.02f, -0.05f), V(8f, 5f, -6f)),
                Rest(1f)),
            // Esquivas: os braços ficam para trás do movimento e rolam um pouco.
            A(ViewModelActionId.DodgeLeft, 0.36f, 0f, true, 2,
                Rest(0f), Both(0.25f, V(0.06f, -0.03f, -0.02f), V(0f, 4f, -10f)), Rest(1f)),
            A(ViewModelActionId.DodgeRight, 0.36f, 0f, true, 2,
                Rest(0f), Both(0.25f, V(-0.06f, -0.03f, -0.02f), V(0f, -4f, 10f)), Rest(1f)),
            A(ViewModelActionId.DodgeBack, 0.36f, 0f, true, 2,
                Rest(0f), Both(0.25f, V(0f, 0.03f, -0.06f), V(-8f, 0f, 0f)), Rest(1f)),
            // Levar dano: tranco rápido para baixo/trás, volta mais devagar.
            A(ViewModelActionId.HitReaction, 0.34f, 0f, true, 2,
                Rest(0f), Both(0.12f, V(0.01f, -0.05f, -0.06f), V(12f, 4f, -6f)),
                Both(0.4f, V(0f, -0.02f, -0.02f), V(4f, 1f, -2f)), Rest(1f)),
            // Inspecionar: vira a lâmina para a câmera, segura, gira para o outro lado e volta.
            A(ViewModelActionId.Inspect, 2.2f, 0.5f, false, 0,
                Rest(0f),
                K(0.15f, V(-0.06f, 0.04f, 0.02f), V(0f, -35f, 25f), V(-0.03f, -0.08f, -0.02f), V(6f, 0f, 0f), 0f),
                K(0.45f, V(-0.07f, 0.05f, 0.03f), V(-5f, -40f, 30f), V(-0.03f, -0.09f, -0.02f), V(6f, 0f, 0f), 0f),
                K(0.6f, V(-0.04f, 0.03f, 0f), V(10f, 25f, -40f), V(-0.03f, -0.08f, -0.02f), V(6f, 0f, 0f), 0f),
                K(0.85f, V(-0.04f, 0.035f, 0.01f), V(8f, 28f, -42f), V(-0.03f, -0.08f, -0.02f), V(6f, 0f, 0f), 0f),
                Rest(1f)),
            // Interagir (alavanca, porta, NPC): a mão esquerda vai à frente, aberta.
            A(ViewModelActionId.Interact, 0.5f, 0.45f, false, 1,
                K(0f, Z, Z, Z, Z, 1f, 1f, 0.55f),
                K(0.45f, V(0.03f, -0.06f, -0.02f), V(8f, 0f, 0f), V(0.12f, 0.08f, 0.22f), V(-30f, 0f, -20f), 0f, 1f, 0.15f),
                K(1f, Z, Z, Z, Z, 1f, 1f, 0.55f)),
            // Pegar item: a mão desce, abre, fecha no item e volta.
            A(ViewModelActionId.PickUp, 0.6f, 0.5f, false, 1,
                K(0f, Z, Z, Z, Z, 1f, 1f, 0.55f),
                K(0.38f, V(0.03f, -0.05f, -0.02f), V(6f, 0f, 0f), V(0.1f, -0.2f, 0.25f), V(35f, 0f, -15f), 0f, 1f, 0.1f),
                K(0.55f, V(0.03f, -0.05f, -0.02f), V(6f, 0f, 0f), V(0.1f, -0.21f, 0.25f), V(38f, 0f, -15f), 0f, 1f, 1f),
                K(1f, Z, Z, Z, Z, 1f, 1f, 0.55f)),
            // Abrir baú: as duas mãos descem e levantam a tampa.
            A(ViewModelActionId.OpenChest, 0.8f, 0.5f, false, 1,
                K(0f, Z, Z, Z, Z, 1f, 1f, 0.55f),
                K(0.35f, V(-0.06f, -0.22f, 0.1f), V(30f, 0f, 10f), V(0.12f, -0.18f, 0.22f), V(30f, 0f, -15f), 0f, 0.6f, 0.3f),
                K(0.6f, V(-0.06f, -0.12f, 0.14f), V(5f, 0f, 10f), V(0.12f, -0.08f, 0.26f), V(5f, 0f, -15f), 0f, 0.6f, 0.4f),
                K(1f, Z, Z, Z, Z, 1f, 1f, 0.55f)),
            // Usar consumível: a mão esquerda leva o frasco à boca, inclina e volta.
            A(ViewModelActionId.UseConsumable, 1.0f, 0.6f, false, 1,
                K(0f, Z, Z, Z, Z, 1f, 1f, 0.55f),
                K(0.3f, V(0.05f, -0.12f, -0.04f), V(15f, 0f, -10f), V(0.15f, 0.12f, 0.05f), V(-50f, 0f, -40f), 0f, 1f, 1f),
                K(0.55f, V(0.05f, -0.12f, -0.04f), V(15f, 0f, -10f), V(0.18f, 0.2f, 0f), V(-75f, 0f, -50f), 0f, 1f, 1f),
                K(0.72f, V(0.05f, -0.12f, -0.04f), V(15f, 0f, -10f), V(0.18f, 0.2f, 0f), V(-78f, 0f, -52f), 0f, 1f, 1f),
                K(1f, Z, Z, Z, Z, 1f, 1f, 0.55f)),
        };

        private static List<ViewModelAction> Default()
        {
            var list = Common();
            // Sacar da cintura: sobe por baixo, passa um pouco do ponto e acomoda; a mão de apoio pega no final.
            list.Add(A(ViewModelActionId.Draw, 0.55f, 0.5f, true, 1,
                K(0f, V(0.05f, -0.35f, -0.1f), V(40f, 0f, -30f), V(0f, -0.25f, -0.05f), V(20f, 0f, 10f), 0f, 1f, 0.6f),
                K(0.45f, V(0.02f, -0.04f, 0.03f), V(-12f, 0f, 6f), V(0f, -0.06f, 0f), V(4f, 0f, 2f), 0.3f),
                K(0.7f, V(0f, 0.01f, 0f), V(3f, 0f, -1f), Z, Z, 0.9f),
                Rest(1f)));
            // Guardar: gira a ponta para baixo e leva para a cintura; a mão de apoio solta antes.
            list.Add(A(ViewModelActionId.Sheathe, 0.45f, 0.8f, true, 1,
                Rest(0f),
                K(0.4f, V(0.04f, -0.12f, -0.02f), V(25f, 0f, -20f), V(0f, -0.08f, 0f), V(6f, 0f, 4f), 0f),
                K(1f, V(0.08f, -0.35f, -0.1f), V(45f, 0f, -35f), V(0f, -0.2f, 0f), V(15f, 0f, 8f), 0f)));
            return list;
        }

        private static List<ViewModelAction> TwoHanded()
        {
            var list = Common();
            // Sacar das costas: as mãos vêm de cima do ombro direito e descem para a guarda.
            list.Add(A(ViewModelActionId.Draw, 0.7f, 0.5f, true, 1,
                Both(0f, V(-0.08f, 0.28f, -0.22f), V(-60f, 20f, 30f), 1f),
                Both(0.4f, V(-0.02f, 0.1f, -0.05f), V(-20f, 8f, 12f), 1f),
                Both(0.65f, V(0f, -0.03f, 0.02f), V(6f, -2f, -3f), 1f),
                Rest(1f)));
            list.Add(A(ViewModelActionId.Sheathe, 0.55f, 0.8f, true, 1,
                Rest(0f),
                Both(0.45f, V(-0.03f, 0.12f, -0.06f), V(-25f, 10f, 14f), 1f),
                Both(1f, V(-0.08f, 0.28f, -0.22f), V(-60f, 20f, 30f), 1f)));
            return list;
        }
    }
}
