using System.Collections.Generic;
using System.IO;
using System.Linq;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Summons;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace Athoria.Editor
{
    /// <summary>
    /// Espíritos invocáveis: importa o modelo gerado no Blender (Tools/Blender/lobo_espiritual.py),
    /// cria os materiais PSX, o Animator Controller com os clipes e o Prefab do Lobo Espiritual,
    /// e a ficha SpiritData. Sem o FBX, a ficha fica sem prefab (a habilidade avisa no jogo).
    /// Os números da ficha são PROVISÓRIOS (tirados do Lobo Cinzento inimigo) até o balanceamento.
    /// </summary>
    public static class SpiritContentFactory
    {
        private const string ModelFolder = "Assets/_Athoria/Art/Models/Espiritos/LoboEspiritual";
        private const string Fbx = ModelFolder + "/LoboEspiritual.fbx";
        private const string PrefabFolder = "Assets/_Athoria/Prefabs/Espiritos";
        private const string AnimFolder = "Assets/_Athoria/Animation/Espiritos";
        private const string DataFolder = "Assets/_Athoria/Data/Personagens";
        private static readonly string[] Clips = { "Idle", "Walk", "Run", "Attack", "Hurt", "Death", "Summon" };
        private static readonly HashSet<string> Loops = new() { "Idle", "Walk", "Run" };

        [MenuItem("Athoria/Dados/Gerar espíritos")]
        public static void GenerateMenu()
        {
            var data = BuildSpiritWolf();
            Debug.Log($"[Athoria] Lobo Espiritual: {(data.Prefab != null ? "prefab pronto" : "sem modelo (FBX ausente)")}.");
        }

        public static SpiritData BuildSpiritWolf()
        {
            Directory.CreateDirectory(DataFolder);
            var path = $"{DataFolder}/Espirito_LoboEspiritual.asset";
            var data = AssetDatabase.LoadAssetAtPath<SpiritData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<SpiritData>();
                AssetDatabase.CreateAsset(data, path);
            }
            data.EditorSetInfo("Lobo Espiritual",
                "Espírito lupino materializado por Akane. Acompanha a invocadora e ataca sozinho os inimigos próximos. " +
                "Ocupa 1 Mantra enquanto existe.");
            // PROVISÓRIO (baseado no Lobo Cinzento): mantras, vida, dano, intervalo, alcance, preparação, atordoamento,
            // andar, correr, detecção, distância máx. da invocadora, acompanhamento, reaparecer, tempos de animação.
            data.EditorConfigure(1, 45f, 8f, 1.0f, 1.8f, 0.35f, 1f, 2f, 6.5f, 12f, 18f, 2.5f, 25f, 1.6f, 0.45f, 1.6f);

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            if (fbx == null)
            {
                Debug.LogWarning($"[Athoria] Modelo do Lobo Espiritual não encontrado em {Fbx}. Rode Tools/Blender/lobo_espiritual.py.");
                data.EditorSetPrefab(null, null, null);
                return data;
            }

            var mats = Materials();
            ConfigureImport(mats);
            var controller = BuildController();
            data.EditorSetPrefab(BuildPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Fbx), controller), null, null);
            AssetDatabase.SaveAssets();
            return data;
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
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                PsxAssets.Crisp(t);
                return t;
            }

            var corpo = PsxAssets.MaterialFor(Tex("Lobo_Corpo"), false, null, "LoboEspiritual_Corpo");
            var mascara = PsxAssets.MaterialFor(Tex("Lobo_Mascara"), false, null, "LoboEspiritual_Mascara");
            var espirito = PsxAssets.MaterialFor(Tex("Lobo_Espirito"), true, null, "LoboEspiritual_Espirito");
            var brilhoTex = Tex("Lobo_Brilho");
            var brilho = PsxAssets.MaterialFor(brilhoTex, true, null, "LoboEspiritual_Brilho");
            Glow(espirito, espirito.GetTexture("_BaseMap"), new Color(0.22f, 0.24f, 0.32f));   // etéreo: um pouco de luz própria
            Glow(brilho, brilhoTex, new Color(2.6f, 2.1f, 1.0f));                              // olhos
            var lagrimaTex = Tex("Lobo_Lagrima");
            lagrimaTex.wrapMode = TextureWrapMode.Repeat;
            var lagrima = PsxAssets.MaterialFor(lagrimaTex, true, null, "LoboEspiritual_Lagrima");
            Glow(lagrima, lagrimaTex, new Color(2.4f, 1.9f, 0.9f));                            // lágrimas líquidas
            return new Dictionary<string, Material>
            {
                ["Corpo"] = corpo, ["Mascara"] = mascara, ["Espirito"] = espirito, ["Brilho"] = brilho, ["Lagrima"] = lagrima
            };
        }

        private static void Glow(Material m, Texture tex, Color color)
        {
            m.SetTexture("_EmissionMap", tex);
            m.SetColor("_EmissionColor", color);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
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
            importer.animationCompression = ModelImporterAnimationCompression.Off;   // mantém as poses secas
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var pair in mats)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);

            // Clipes com nomes limpos ("LoboEspiritual|Idle" → "Idle"); locomoção em loop.
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
            var path = $"{AnimFolder}/LoboEspiritual.controller";
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
                    Debug.LogWarning($"[Athoria] Lobo Espiritual: clipe '{name}' não encontrado no FBX.");
                    continue;
                }
                var state = sm.AddState(name, new Vector3(300, y += 60, 0));
                state.motion = clip;
                if (name == "Idle") sm.defaultState = state;
            }
            return controller;
        }

        /// <summary>Gotas douradas que pingam da ponta de uma lágrima (pixels quadrados, sem filtro).</summary>
        private static ParticleSystem BuildDrip(Transform tearBone)
        {
            var go = new GameObject("Gotas");
            go.transform.SetParent(tearBone, false);
            go.transform.position = tearBone.position + Vector3.down * 0.42f;   // ponta da lágrima, perto do chão
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.032f);
            main.startColor = new Color(1f, 0.85f, 0.4f);
            main.gravityModifier = 1.2f;
            main.maxParticles = 24;
            var emission = ps.emission;
            emission.rateOverTime = 5f;
            var shape = ps.shape;
            shape.enabled = false;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = DripMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        private static Material DripMaterial()
        {
            var path = "Assets/_Athoria/Materials/LoboEspiritual_Gota.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (mat == null)
            {
                Directory.CreateDirectory("Assets/_Athoria/Materials");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_BaseColor", new Color(1.6f, 1.3f, 0.6f));   // brilha sem luz, como as lágrimas
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------- prefab ----------
        private static GameObject BuildPrefab(GameObject fbx, AnimatorController controller)
        {
            Directory.CreateDirectory(PrefabFolder);
            var root = new GameObject("Lobo Espiritual");
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
                var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (var r in renderers)
                {
                    r.updateWhenOffscreen = true;   // o osso Root se move (bote, queda)
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }

                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.4f;
                agent.height = 1.1f;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                root.AddComponent<Health>();
                root.AddComponent<PlayerAlly>();
                var col = root.AddComponent<CapsuleCollider>();
                col.direction = 2;
                col.center = new Vector3(0f, 0.7f, 0f);
                col.radius = 0.28f;
                col.height = 1.4f;
                root.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
                root.AddComponent<SpiritCompanion>();
                root.AddComponent<SpiritBrain>();
                var liquid = root.AddComponent<SpiritLiquid>();
                var drips = new List<ParticleSystem>();
                foreach (var boneName in new[] { "Tear_L_2", "Tear_R_2" })
                {
                    var bone = model.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == boneName);
                    if (bone != null) drips.Add(BuildDrip(bone));
                }
                var lso = new SerializedObject(liquid);
                var lr = lso.FindProperty("renderers");
                lr.arraySize = renderers.Length;
                for (var i = 0; i < renderers.Length; i++) lr.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
                var ld = lso.FindProperty("drips");
                ld.arraySize = drips.Count;
                for (var i = 0; i < drips.Count; i++) ld.GetArrayElementAtIndex(i).objectReferenceValue = drips[i];
                lso.ApplyModifiedPropertiesWithoutUndo();
                var view = root.AddComponent<SpiritAnimator>();
                var so = new SerializedObject(view);
                so.FindProperty("animator").objectReferenceValue = animator;
                var glow = so.FindProperty("glowRenderers");
                glow.arraySize = renderers.Length;
                for (var i = 0; i < renderers.Length; i++) glow.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
                so.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/LoboEspiritual.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
