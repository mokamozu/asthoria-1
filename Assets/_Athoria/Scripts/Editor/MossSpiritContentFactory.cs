using System.Collections.Generic;
using System.IO;
using System.Linq;
using Athoria.Gameplay.AI;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Loot;
using Athoria.Gameplay.Stats;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace Athoria.Editor
{
    /// <summary>
    /// Espírito do Musgo: importa o modelo do Blender (Tools/Blender/espirito_musgo.py),
    /// cria os materiais PSX, o Animator Controller (Idle, Hiding, Running, Death) e o Prefab.
    /// Criatura medrosa que se esconde como moita, foge e solta Musgo ao morrer.
    /// </summary>
    public static class MossSpiritContentFactory
    {
        private const string ModelFolder = "Assets/_Athoria/Art/Models/Inimigos/EspiritoMusgo";
        private const string Fbx = ModelFolder + "/EspiritoMusgo.fbx";
        private const string PrefabFolder = "Assets/_Athoria/Prefabs/Inimigos";
        private const string AnimFolder = "Assets/_Athoria/Animation/Inimigos";
        public const string PrefabPath = PrefabFolder + "/Inimigo_EspiritoMusgo.prefab";
        private static readonly string[] Clips = { "Idle", "Hiding", "Running", "Death" };
        private static readonly HashSet<string> Loops = new() { "Idle", "Hiding", "Running" };

        [MenuItem("Athoria/Dados/Gerar Espírito do Musgo")]
        public static void GenerateMenu()
        {
            var items = ItemContentFactory.GenerateAll();
            var prefab = Build(LootContentFactory.GenerateAll(items));
            Debug.Log(prefab != null ? "[Athoria] Espírito do Musgo gerado em " + PrefabPath : "[Athoria] Espírito do Musgo: FBX não encontrado.");
        }

        public static GameObject Build(Dictionary<string, LootTable> loot)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            if (fbx == null)
            {
                Debug.LogWarning($"[Athoria] Modelo do Espírito do Musgo não encontrado em {Fbx}. Rode Tools/Blender/espirito_musgo.py.");
                return null;
            }
            Directory.CreateDirectory(PrefabFolder);
            ConfigureImport(Materials());
            var controller = BuildController();
            var prefab = BuildPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Fbx), controller,
                loot != null && loot.TryGetValue(LootContentFactory.MossSpirit, out var t) ? t : null);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        /// <summary>Coloca um espírito escondido na cena (antes de gerar o NavMesh).</summary>
        public static GameObject Place(Transform parent, Vector3 position, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        // ---------- importação ----------
        private static Dictionary<string, Material> Materials()
        {
            Texture2D Tex(string n)
            {
                var p = $"{ModelFolder}/{n}.png";
                if (AssetImporter.GetAtPath(p) is TextureImporter ti && !ti.alphaIsTransparency)
                {
                    ti.alphaIsTransparency = true;
                    ti.SaveAndReimport();
                }
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                PsxAssets.Crisp(tex);
                return tex;
            }

            Texture2D Painted(string n)
            {
                var p = $"{ModelFolder}/{n}.png";
                if (AssetImporter.GetAtPath(p) is TextureImporter ti && (ti.filterMode != FilterMode.Bilinear || ti.maxTextureSize < 2048))
                {
                    ti.filterMode = FilterMode.Bilinear;
                    ti.maxTextureSize = 2048;
                    ti.npotScale = TextureImporterNPOTScale.None;
                    ti.mipmapEnabled = true;
                    ti.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            }

            var olhoTex = Tex("Musgo_Olho");
            var olhos = PsxAssets.MaterialFor(olhoTex, false, null, "EspiritoMusgo_Olhos");
            olhos.SetTexture("_EmissionMap", olhoTex);
            olhos.SetColor("_EmissionColor", new Color(2.2f, 2.6f, 0.6f));      // verde-limão neon
            olhos.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(olhos);
            return new Dictionary<string, Material>
            {
                // manto pintado = a própria referência projetada: filtro suave, uma face só (o forro escuro é a "Bota")
                ["Musgo"] = PsxAssets.MaterialFor(Painted("Musgo_Manto"), false, null, "EspiritoMusgo_Manto"),
                ["Pele"] = PsxAssets.MaterialFor(Tex("Musgo_Pele"), false, null, "EspiritoMusgo_Pele", true),
                ["Olhos"] = olhos,
                ["Bota"] = PsxAssets.MaterialFor(Tex("Musgo_Bota"), false, null, "EspiritoMusgo_Bota"),
                ["Folha"] = PsxAssets.MaterialFor(Tex("Musgo_Folha"), true, null, "EspiritoMusgo_Folha"),
            };
        }

        private static void ConfigureImport(Dictionary<string, Material> mats)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var pair in mats)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            var clips = importer.defaultClipAnimations;
            foreach (var c in clips)
            {
                var n = c.name.Contains("|") ? c.name.Substring(c.name.LastIndexOf('|') + 1) : c.name;
                c.name = n;
                c.loopTime = Loops.Contains(n);
                c.loopPose = false;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static AnimatorController BuildController()
        {
            Directory.CreateDirectory(AnimFolder);
            var path = $"{AnimFolder}/EspiritoMusgo.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = controller.layers[0].stateMachine;
            var clips = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            var y = 0;
            foreach (var name in Clips)
            {
                if (!clips.TryGetValue(name, out var clip))
                {
                    Debug.LogWarning($"[Athoria] Espírito do Musgo: clipe '{name}' não encontrado no FBX.");
                    continue;
                }
                var state = sm.AddState(name, new Vector3(300, y += 60, 0));
                state.motion = clip;
                if (name == "Hiding") sm.defaultState = state;
            }
            return controller;
        }

        // ---------- prefab ----------
        private static GameObject BuildPrefab(GameObject fbx, AnimatorController controller, LootTable loot)
        {
            var root = new GameObject("Espírito do Musgo");
            try
            {
                var model = (GameObject)Object.Instantiate(fbx, root.transform);
                model.name = "Modelo";
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // o FBX chega olhando para -Z
                var animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>()) r.updateWhenOffscreen = true;

                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.35f;
                agent.height = 1.8f;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                root.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

                var col = root.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 0.85f, 0f);
                col.radius = 0.38f;
                col.height = 1.7f;

                var health = root.AddComponent<Health>();
                var hso = new SerializedObject(health);
                hso.FindProperty("maxHealth").floatValue = 30f;                     // provisório
                hso.ApplyModifiedPropertiesWithoutUndo();

                var surf = root.AddComponent<SurfaceMaterial>();
                var sso = new SerializedObject(surf);
                sso.FindProperty("kind").enumValueIndex = (int)SurfaceKind.Straw;
                sso.ApplyModifiedPropertiesWithoutUndo();

                var reward = root.AddComponent<ExperienceReward>();
                var rso = new SerializedObject(reward);
                rso.FindProperty("experience").intValue = 12;                        // provisório
                rso.FindProperty("displayName").stringValue = "Espírito do Musgo";
                rso.ApplyModifiedPropertiesWithoutUndo();

                if (loot != null)
                {
                    var dropper = root.AddComponent<LootDropper>();
                    var lso = new SerializedObject(dropper);
                    lso.FindProperty("table").objectReferenceValue = loot;
                    lso.ApplyModifiedPropertiesWithoutUndo();
                }

                var ai = root.AddComponent<MossSpiritAI>();
                var aso = new SerializedObject(ai);
                aso.FindProperty("animator").objectReferenceValue = animator;
                aso.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
