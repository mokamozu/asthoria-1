using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Athoria.Editor
{
    /// <summary>
    /// Universal Animation Library 1 e 2 (Quaternius, CC0): 86 animações humanoides.
    /// Configura a importação como no guia do autor (Humanoid, Bake Axis Conversion,
    /// loop nos clipes "_Loop"), com o avatar do UAL1 compartilhado pelo UAL2 e pelos
    /// braços em primeira pessoa, e monta Animator Controllers com estados = nomes dos clipes.
    /// </summary>
    public static class UalLibrary
    {
        public const string Folder = "Assets/_Athoria/Art/Animacoes/UAL";
        public const string Ual1 = Folder + "/UAL1_Standard.fbx";
        public const string Ual2 = Folder + "/UAL2_Standard.fbx";
        private const string ImportTag = "athoria-ual-v1";
        private static readonly HashSet<string> ExtraLoops = new() { "Sword_Idle", "Pistol_Aim_Neutral", "Pistol_Aim_Up", "Pistol_Aim_Down" };

        public static bool Available => File.Exists(Ual1) && File.Exists(Ual2);

        public static GameObject Model => AssetDatabase.LoadAssetAtPath<GameObject>(Ual1);

        public static Avatar Avatar => AssetDatabase.LoadAllAssetsAtPath(Ual1).OfType<Avatar>().FirstOrDefault();

        [MenuItem("Athoria/Dados/Configurar Universal Animation Library")]
        public static void ConfigureMenu()
        {
            Configure(true);
            Debug.Log($"[Athoria] UAL configurada: {Clips().Count} clipes, avatar {(Avatar != null && Avatar.isValid ? "válido" : "INVÁLIDO")}.");
        }

        /// <summary>Configura a importação (só reimporta se mudou).</summary>
        public static void Configure(bool force = false)
        {
            if (!Available) return;
            Setup(Ual1, null, force);
            Setup(Ual2, Avatar, force);
        }

        private static void Setup(string path, Avatar source, bool force)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;
            if (!force && importer.userData == ImportTag && importer.animationType == ModelImporterAnimationType.Human) return;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.animationType = ModelImporterAnimationType.Human;
            if (source == null) importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            else
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = source;
            }
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.userData = ImportTag;
            importer.SaveAndReimport();

            importer = (ModelImporter)AssetImporter.GetAtPath(path);
            var clips = importer.defaultClipAnimations;
            foreach (var c in clips)
            {
                var n = Clean(c.name);
                c.name = n;
                c.loopTime = n.EndsWith("_Loop") || ExtraLoops.Contains(n);
                c.loopPose = false;
                // sem deslocamento: a IA/controle movem o personagem (arquivo sem root motion)
                c.lockRootRotation = true;
                c.lockRootHeightY = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalOrientation = true;
                c.keepOriginalPositionY = true;
                c.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static string Clean(string n) => n.Contains("|") ? n.Substring(n.LastIndexOf('|') + 1) : n;

        /// <summary>Todos os clipes das duas bibliotecas, por nome ("Sword_Regular_A", "Walk_Loop"…).</summary>
        public static Dictionary<string, AnimationClip> Clips()
        {
            var result = new Dictionary<string, AnimationClip>();
            foreach (var path in new[] { Ual1, Ual2 })
                foreach (var c in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                    if (!c.name.StartsWith("__preview__")) result[Clean(c.name)] = c;
            return result;
        }

        /// <summary>Controller com um estado por clipe (nome do estado = nome do clipe).</summary>
        public static AnimatorController BuildController(string path, IEnumerable<string> states, string defaultState)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = controller.layers[0].stateMachine;
            var clips = Clips();
            var y = 0;
            foreach (var name in states.Distinct())
            {
                if (string.IsNullOrEmpty(name)) continue;
                if (!clips.TryGetValue(name, out var clip))
                {
                    Debug.LogWarning($"[Athoria] UAL: clipe '{name}' não encontrado.");
                    continue;
                }
                var state = sm.AddState(name, new Vector3(320, y += 55, 0));
                state.motion = clip;
                state.writeDefaultValues = true;
                if (name == defaultState) sm.defaultState = state;
            }
            return controller;
        }

        /// <summary>Procura um osso pelo nome dentro do modelo.</summary>
        public static Transform Bone(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
    }
}
