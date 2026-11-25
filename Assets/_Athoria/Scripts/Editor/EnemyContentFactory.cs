using System.Collections.Generic;
using System.IO;
using Athoria.Gameplay.AI;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Effects;
using Athoria.Gameplay.Loot;
using Athoria.Gameplay.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.Rendering;

namespace Athoria.Editor
{
    /// <summary>
    /// Gera os inimigos da Etapa 11: dados (EnemyData), flecha e Prefabs com
    /// modelos provisórios de primitivas — Lobo Cinzento, Esqueleto Guerreiro,
    /// Esqueleto Arqueiro e Cavaleiro Corrompido.
    /// </summary>
    public static partial class EnemyContentFactory
    {
        private const string DataFolder = "Assets/_Athoria/Data/Inimigos";
        private const string PrefabFolder = "Assets/_Athoria/Prefabs/Inimigos";
        private const string SfxFolder = "Assets/_Athoria/Audio/SFX/Provisorios";

        public const string Wolf = "Lobo";
        public const string Skeleton = "Esqueleto";
        public const string Archer = "EsqueletoArqueiro";
        public const string Knight = "CavaleiroCorrompido";

        private static Dictionary<string, LootTable> _loot;

        public static string PrefabPath(string id) => $"{PrefabFolder}/Inimigo_{id}.prefab";

        [MenuItem("Athoria/Dados/Gerar inimigos")]
        public static void GenerateMenu()
        {
            GenerateAll(CombatContentFactory.GenerateAll(), LootContentFactory.GenerateAll(ItemContentFactory.GenerateAll()));
            Debug.Log("[Athoria] Inimigos gerados em " + PrefabFolder);
        }

        /// <summary>Gera tudo e devolve os Prefabs por id.</summary>
        public static Dictionary<string, GameObject> GenerateAll(CombatFeedback feedback, Dictionary<string, LootTable> loot = null)
        {
            _loot = loot;
            Directory.CreateDirectory(DataFolder);
            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh();

            var arrow = BuildArrow(feedback);
            // Humanoides com esqueleto e as animações da Universal Animation Library, quando ela está no projeto;
            // sem ela, os modelos provisórios de primitivas com animação procedural.
            var ual = UalLibrary.Available;
            if (ual) PrepareHumanoids();
            var result = new Dictionary<string, GameObject>
            {
                [Wolf] = BuildWolf(WolfData(), feedback),
                [Skeleton] = ual ? BuildHumanoidSkeleton(SkeletonData(), feedback, false) : BuildSkeleton(SkeletonData(), feedback, false),
                [Archer] = ual ? BuildHumanoidSkeleton(ArcherData(arrow), feedback, true) : BuildSkeleton(ArcherData(arrow), feedback, true),
                [Knight] = ual ? BuildHumanoidKnight(KnightData(), feedback) : BuildKnight(KnightData(), feedback)
            };
            AssetDatabase.SaveAssets();
            return result;
        }

        // ================= Dados =================

        private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxFolder}/{name}.wav");

        private static EnemyData Data(string id)
        {
            var path = $"{DataFolder}/Inimigo_{id}.asset";
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<EnemyData>();
                AssetDatabase.CreateAsset(data, path);
            }
            data.loot = _loot != null && _loot.TryGetValue(id, out var table) ? table : null;
            return data;
        }

        private static EnemyData WolfData()
        {
            var d = Data(Wolf);
            d.EditorSetInfo("Lobo Cinzento",
                "Caça em matilha. Rodeia a presa, morde rápido e salta de longe. Ferido, recua — e volta.");
            d.maxHealth = 45f; d.physicalReduction = 0f; d.poise = 1f; d.staggerDuration = 0.45f;
            d.canBlock = false;
            d.walkSpeed = 2f; d.runSpeed = 6.5f; d.turnSpeed = 540f; d.agentRadius = 0.45f; d.agentHeight = 1f;
            d.sightRange = 18f; d.sightAngle = 160f; d.senseRadius = 3f; d.hearingRange = 12f; d.memoryTime = 5f; d.alertRadius = 14f;
            d.leashRange = 30f; d.patrolRadius = 5f; d.patrolWait = new Vector2(1.5f, 4f);
            d.attackInterval = new Vector2(0.6f, 1.4f); d.circlesTarget = true; d.preferredDistance = 0f;
            d.retreatHealthFraction = 0.3f; d.retreatDuration = 2.5f; d.returnRegenPerSecond = 10f; d.sneakAttackMultiplier = 2f;
            d.attacks = new[]
            {
                new EnemyAttack { name = "Mordida", pose = EnemyAttackPose.Bite, minRange = 0f, maxRange = 1.8f, damage = 8f,
                    windup = 0.35f, activeTime = 0.15f, recovery = 0.5f, arc = 70f, stagger = 1f, lunge = 1.2f, weight = 1f },
                new EnemyAttack { name = "Bote", pose = EnemyAttackPose.Bite, minRange = 3f, maxRange = 5.5f, damage = 12f,
                    windup = 0.55f, activeTime = 0.3f, recovery = 0.8f, arc = 60f, stagger = 1.5f, lunge = 4.5f, cooldown = 4f, weight = 0.7f }
            };
            d.experience = 20;
            d.alertSound = Clip("lobo_rosnado");
            d.attackSounds = new[] { Clip("lobo_mordida") };
            d.hurtSound = Clip("lobo_ganido");
            d.deathSound = Clip("lobo_morte");
            EditorUtility.SetDirty(d);
            return d;
        }

        private static EnemyData SkeletonData()
        {
            var d = Data(Skeleton);
            d.EditorSetInfo("Esqueleto Guerreiro",
                "Ossos presos por algo que não deveria existir. Ergue o escudo quando vê o golpe chegando e responde na hora.");
            d.maxHealth = 70f; d.physicalReduction = 0f; d.poise = 2f; d.staggerDuration = 0.7f;
            d.canBlock = true; d.blockChance = 0.45f; d.blockAngle = 70f; d.blockDuration = 1.3f; d.blockAbsorb = 1f; d.guardHits = 3; d.riposteChance = 0.5f;
            d.walkSpeed = 1.4f; d.runSpeed = 3.6f; d.turnSpeed = 300f; d.agentRadius = 0.4f; d.agentHeight = 1.8f;
            d.sightRange = 15f; d.sightAngle = 120f; d.senseRadius = 2f; d.hearingRange = 8f; d.memoryTime = 4f; d.alertRadius = 10f;
            d.leashRange = 26f; d.patrolRadius = 3f; d.patrolWait = new Vector2(3f, 6f);
            d.attackInterval = new Vector2(1f, 2f); d.circlesTarget = false; d.preferredDistance = 0f;
            d.retreatHealthFraction = 0f; d.returnRegenPerSecond = 15f; d.sneakAttackMultiplier = 2f;
            d.attacks = new[]
            {
                new EnemyAttack { name = "Corte", pose = EnemyAttackPose.Slash, maxRange = 2.1f, damage = 12f,
                    windup = 0.55f, activeTime = 0.15f, recovery = 0.6f, arc = 100f, stagger = 1f, lunge = 0.3f, chainChance = 0.35f, chainTo = 1 },
                new EnemyAttack { name = "Golpe Descendente", pose = EnemyAttackPose.Overhead, maxRange = 2.2f, damage = 18f,
                    windup = 0.8f, activeTime = 0.15f, recovery = 0.8f, arc = 60f, stagger = 2f, heavy = true, lunge = 0.5f, weight = 0.6f }
            };
            d.experience = 30;
            d.alertSound = Clip("ossos_chocalho");
            d.attackSounds = null;
            d.hurtSound = Clip("ossos_chocalho");
            d.deathSound = Clip("ossos_quebra");
            EditorUtility.SetDirty(d);
            return d;
        }

        private static EnemyData ArcherData(Projectile arrow)
        {
            var d = Data(Archer);
            d.EditorSetInfo("Esqueleto Arqueiro",
                "Mantém distância e atira de onde tiver linha de visão. Se você chegar perto, recua para mirar de novo.");
            d.maxHealth = 45f; d.physicalReduction = 0f; d.poise = 1f; d.staggerDuration = 0.7f;
            d.canBlock = false;
            d.walkSpeed = 1.4f; d.runSpeed = 3.4f; d.turnSpeed = 300f; d.agentRadius = 0.4f; d.agentHeight = 1.8f;
            d.sightRange = 22f; d.sightAngle = 120f; d.senseRadius = 2f; d.hearingRange = 8f; d.memoryTime = 4f; d.alertRadius = 10f;
            d.leashRange = 26f; d.patrolRadius = 0f;
            d.attackInterval = new Vector2(1.2f, 2.2f); d.circlesTarget = false; d.preferredDistance = 9f;
            d.retreatHealthFraction = 0f; d.returnRegenPerSecond = 15f; d.sneakAttackMultiplier = 2f;
            d.attacks = new[]
            {
                new EnemyAttack { name = "Flecha", pose = EnemyAttackPose.Shoot, minRange = 2.5f, maxRange = 22f, damage = 10f,
                    windup = 0.9f, activeTime = 0.1f, recovery = 0.5f, stagger = 1f, projectile = arrow, projectileSpeed = 26f,
                    sound = Clip("arco_disparo") },
                new EnemyAttack { name = "Empurrão", pose = EnemyAttackPose.Thrust, maxRange = 1.6f, damage = 6f,
                    windup = 0.4f, activeTime = 0.12f, recovery = 0.5f, arc = 90f, stagger = 1f }
            };
            d.experience = 30;
            d.alertSound = Clip("ossos_chocalho");
            d.attackSounds = null;
            d.hurtSound = Clip("ossos_chocalho");
            d.deathSound = Clip("ossos_quebra");
            EditorUtility.SetDirty(d);
            return d;
        }

        private static EnemyData KnightData()
        {
            var d = Data(Knight);
            d.EditorSetInfo("Cavaleiro Corrompido",
                "Um cavaleiro tomado pela peste das Terras Alagadas. A armadura apodrece por dentro; o juramento, não. Guarda seu posto até o fim.");
            d.maxHealth = 320f; d.physicalReduction = 0.3f; d.poise = 5f; d.staggerDuration = 0.9f;
            d.canBlock = true; d.blockChance = 0.35f; d.blockAngle = 65f; d.blockDuration = 1f; d.blockAbsorb = 1f; d.guardHits = 4; d.riposteChance = 0.7f;
            d.walkSpeed = 1.3f; d.runSpeed = 3.2f; d.turnSpeed = 200f; d.agentRadius = 0.55f; d.agentHeight = 2.1f;
            d.sightRange = 16f; d.sightAngle = 110f; d.senseRadius = 2.5f; d.hearingRange = 8f; d.memoryTime = 6f; d.alertRadius = 0f;
            d.leashRange = 22f; d.patrolRadius = 0f;
            d.attackInterval = new Vector2(1.2f, 2.4f); d.circlesTarget = false; d.preferredDistance = 0f;
            d.retreatHealthFraction = 0f; d.returnRegenPerSecond = 40f; d.sneakAttackMultiplier = 1.5f;
            d.attacks = new[]
            {
                new EnemyAttack { name = "Golpe Montante", pose = EnemyAttackPose.Overhead, maxRange = 2.8f, damage = 30f,
                    windup = 1f, activeTime = 0.2f, recovery = 1f, arc = 60f, stagger = 2f, heavy = true, lunge = 0.8f },
                new EnemyAttack { name = "Varrida", pose = EnemyAttackPose.Slash, maxRange = 2.8f, damage = 20f,
                    windup = 0.7f, activeTime = 0.2f, recovery = 0.7f, arc = 150f, stagger = 1.5f, lunge = 0.4f, chainChance = 0.5f, chainTo = 0 },
                new EnemyAttack { name = "Estocada", pose = EnemyAttackPose.Thrust, minRange = 2.5f, maxRange = 5f, damage = 22f,
                    windup = 0.8f, activeTime = 0.3f, recovery = 0.9f, arc = 40f, stagger = 2f, heavy = true, lunge = 3f, cooldown = 5f, weight = 0.8f }
            };
            d.experience = 120;
            d.alertSound = Clip("cavaleiro_rugido");
            d.attackSounds = null;
            d.hurtSound = Clip("armadura_impacto");
            d.deathSound = Clip("cavaleiro_morte");
            EditorUtility.SetDirty(d);
            return d;
        }

        // ================= Flecha =================

        private static Projectile BuildArrow(CombatFeedback feedback)
        {
            var wood = EditorMaterials.GetOrCreate("MadeiraClara", new Color(0.55f, 0.42f, 0.25f), 0f, 0.15f);
            var iron = EditorMaterials.GetOrCreate("FerroVelho", new Color(0.35f, 0.34f, 0.33f), 0.8f, 0.35f);
            var feather = EditorMaterials.GetOrCreate("PenaEscura", new Color(0.12f, 0.1f, 0.1f), 0f, 0.05f);
            var root = new GameObject("Flecha");
            try
            {
                Part(root.transform, "Haste", PrimitiveType.Cube, new Vector3(0, 0, -0.2f), new Vector3(0.02f, 0.02f, 0.7f), wood);
                Part(root.transform, "Ponta", PrimitiveType.Cube, new Vector3(0, 0, 0.18f), new Vector3(0.04f, 0.04f, 0.08f), iron);
                Part(root.transform, "Penas", PrimitiveType.Cube, new Vector3(0, 0, -0.5f), new Vector3(0.07f, 0.005f, 0.12f), feather);
                var proj = root.AddComponent<Projectile>();
                var so = new SerializedObject(proj);
                so.FindProperty("feedback").objectReferenceValue = feedback;
                so.FindProperty("speed").floatValue = 26f;
                so.FindProperty("radius").floatValue = 0.07f;
                so.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/Flecha.prefab").GetComponent<Projectile>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ================= Modelos =================

        /// <summary>Raiz com tudo que todo inimigo precisa. Devolve o "Corpo" (pivô no chão) para montar o modelo.</summary>
        private static Transform NewRig(GameObject root, out Transform visual)
        {
            visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var body = new GameObject("Corpo").transform;
            body.SetParent(visual, false);
            return body;
        }

        private static GameObject Finish(GameObject root, Transform visual, EnemyData data, CombatFeedback feedback, SurfaceKind surface,
            EnemyAnimatorBase animator, Transform eyes, Transform muzzle, float deathFxScale, Vector3 colliderCenter, float colliderRadius,
            float colliderHeight, int colliderDirection, string id)
        {
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = data.agentRadius;
            agent.height = data.agentHeight;
            agent.speed = data.walkSpeed;
            agent.angularSpeed = data.turnSpeed;
            agent.acceleration = 24f;
            agent.stoppingDistance = 0.15f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            agent.enabled = false;

            // O inimigo não é obstáculo ao gerar o NavMesh (senão abre um buraco onde ele estava).
            root.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = colliderCenter;
            capsule.radius = colliderRadius;
            capsule.height = colliderHeight;
            capsule.direction = colliderDirection;

            var health = root.AddComponent<Health>();
            SetFloat(health, "maxHealth", data.maxHealth);

            var reaction = root.AddComponent<HitReaction>();
            SetRef(reaction, "pivot", visual);

            var surf = root.AddComponent<SurfaceMaterial>();
            var sso = new SerializedObject(surf);
            sso.FindProperty("kind").enumValueIndex = (int)surface;
            sso.ApplyModifiedPropertiesWithoutUndo();

            var reward = root.AddComponent<ExperienceReward>();
            var rso = new SerializedObject(reward);
            rso.FindProperty("experience").intValue = data.experience;
            rso.FindProperty("displayName").stringValue = data.DisplayName;
            rso.ApplyModifiedPropertiesWithoutUndo();

            var smoke = PixelFxImporter.Load("fx_morte_fumaca");
            if (smoke != null)
            {
                var dfx = root.AddComponent<DeathFx>();
                var dso = new SerializedObject(dfx);
                dso.FindProperty("effect").objectReferenceValue = smoke;
                dso.FindProperty("scale").floatValue = deathFxScale;
                dso.ApplyModifiedPropertiesWithoutUndo();
            }

            if (data.loot != null)
            {
                var dropper = root.AddComponent<LootDropper>();
                var lso = new SerializedObject(dropper);
                lso.FindProperty("table").objectReferenceValue = data.loot;
                lso.ApplyModifiedPropertiesWithoutUndo();
            }

            var ai = root.AddComponent<EnemyAI>();
            var aso = new SerializedObject(ai);
            aso.FindProperty("data").objectReferenceValue = data;
            aso.FindProperty("animator").objectReferenceValue = animator;
            aso.FindProperty("eyes").objectReferenceValue = eyes;
            aso.FindProperty("muzzle").objectReferenceValue = muzzle;
            aso.FindProperty("feedback").objectReferenceValue = feedback;
            aso.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(id));
        }

        private static EnemyAnimator Animator(GameObject root, Transform body, Transform head, Transform weaponArm, Transform shieldArm,
            Transform[] legs, float[] signs, Vector3 deathAxis, float deathAngle, float stride, float stepsPerMeter)
        {
            var anim = root.AddComponent<EnemyAnimator>();
            var so = new SerializedObject(anim);
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("head").objectReferenceValue = head;
            so.FindProperty("weaponArm").objectReferenceValue = weaponArm;
            so.FindProperty("shieldArm").objectReferenceValue = shieldArm;
            var legsProp = so.FindProperty("legs");
            legsProp.arraySize = legs.Length;
            for (var i = 0; i < legs.Length; i++) legsProp.GetArrayElementAtIndex(i).objectReferenceValue = legs[i];
            var signsProp = so.FindProperty("legSigns");
            signsProp.arraySize = signs.Length;
            for (var i = 0; i < signs.Length; i++) signsProp.GetArrayElementAtIndex(i).floatValue = signs[i];
            so.FindProperty("deathAxis").vector3Value = deathAxis;
            so.FindProperty("deathAngle").floatValue = deathAngle;
            so.FindProperty("strideAngle").floatValue = stride;
            so.FindProperty("stepsPerMeter").floatValue = stepsPerMeter;
            so.ApplyModifiedPropertiesWithoutUndo();
            return anim;
        }

        // ---------- Lobo ----------

        private static GameObject BuildWolf(EnemyData data, CombatFeedback feedback)
        {
            var fur = EditorMaterials.GetOrCreate("PeloCinzento", new Color(0.28f, 0.26f, 0.24f), 0f, 0.1f);
            var furDark = EditorMaterials.GetOrCreate("PeloEscuro", new Color(0.15f, 0.14f, 0.13f), 0f, 0.1f);
            var eyesMat = EditorMaterials.GetOrCreate("OlhoLobo", new Color(0.9f, 0.7f, 0.1f), 0f, 0.6f, new Color(1.6f, 1.1f, 0.1f));

            var root = new GameObject("Lobo Cinzento");
            try
            {
                var body = NewRig(root, out var visual);
                var torso = Part(body, "Tronco", PrimitiveType.Capsule, new Vector3(0, 0.62f, 0), new Vector3(0.42f, 0.55f, 0.42f), fur);
                torso.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Part(body, "Peito", PrimitiveType.Sphere, new Vector3(0, 0.62f, 0.38f), new Vector3(0.44f, 0.46f, 0.42f), fur);
                var tail = Part(body, "Cauda", PrimitiveType.Cube, new Vector3(0, 0.62f, -0.68f), new Vector3(0.09f, 0.09f, 0.45f), furDark);
                tail.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);

                var head = Pivot(body, "Cabeca", new Vector3(0, 0.78f, 0.55f));
                Part(head, "Cranio", PrimitiveType.Cube, new Vector3(0, 0.05f, 0.12f), new Vector3(0.3f, 0.27f, 0.32f), fur);
                Part(head, "Focinho", PrimitiveType.Cube, new Vector3(0, -0.02f, 0.36f), new Vector3(0.16f, 0.14f, 0.24f), furDark);
                Part(head, "Orelha E", PrimitiveType.Cube, new Vector3(-0.09f, 0.22f, 0.05f), new Vector3(0.07f, 0.13f, 0.05f), furDark);
                Part(head, "Orelha D", PrimitiveType.Cube, new Vector3(0.09f, 0.22f, 0.05f), new Vector3(0.07f, 0.13f, 0.05f), furDark);
                Part(head, "Olho E", PrimitiveType.Sphere, new Vector3(-0.08f, 0.1f, 0.27f), Vector3.one * 0.045f, eyesMat);
                Part(head, "Olho D", PrimitiveType.Sphere, new Vector3(0.08f, 0.1f, 0.27f), Vector3.one * 0.045f, eyesMat);

                var legs = new Transform[4];
                Vector3[] spots = { new(-0.15f, 0.5f, 0.35f), new(0.15f, 0.5f, 0.35f), new(-0.15f, 0.5f, -0.38f), new(0.15f, 0.5f, -0.38f) };
                string[] names = { "Pata DE", "Pata DD", "Pata TE", "Pata TD" };
                for (var i = 0; i < 4; i++)
                {
                    legs[i] = Pivot(body, names[i], spots[i]);
                    Part(legs[i], "Perna", PrimitiveType.Cube, new Vector3(0, -0.25f, 0), new Vector3(0.1f, 0.5f, 0.11f), i < 2 ? fur : furDark);
                }

                var eyes = Pivot(root.transform, "Olhos", new Vector3(0, 0.85f, 0.8f));
                var anim = Animator(root, body, head, null, null, legs, new[] { 1f, -1f, -1f, 1f }, Vector3.forward, 85f, 32f, 1.1f);
                return Finish(root, visual, data, feedback, SurfaceKind.Flesh, anim, eyes, null, 0.9f,
                    new Vector3(0, 0.6f, 0.05f), 0.35f, 1.5f, 2, Wolf);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------- Esqueletos ----------

        private static GameObject BuildSkeleton(EnemyData data, CombatFeedback feedback, bool archer)
        {
            var bone = EditorMaterials.GetOrCreate("Osso", new Color(0.78f, 0.74f, 0.63f), 0f, 0.25f);
            var boneDark = EditorMaterials.GetOrCreate("OssoEscuro", new Color(0.5f, 0.46f, 0.38f), 0f, 0.2f);
            var socket = EditorMaterials.GetOrCreate("OrbitaVazia", new Color(0.05f, 0.02f, 0.02f), 0f, 0f, new Color(0.9f, 0.08f, 0.05f));
            var iron = EditorMaterials.GetOrCreate("FerroVelho", new Color(0.35f, 0.34f, 0.33f), 0.8f, 0.35f);
            var wood = EditorMaterials.GetOrCreate("MadeiraClara", new Color(0.55f, 0.42f, 0.25f), 0f, 0.15f);
            var rag = EditorMaterials.GetOrCreate("TrapoVermelho", new Color(0.3f, 0.07f, 0.06f), 0f, 0.05f);

            var root = new GameObject(archer ? "Esqueleto Arqueiro" : "Esqueleto Guerreiro");
            try
            {
                var body = NewRig(root, out var visual);
                Part(body, "Pelvis", PrimitiveType.Cube, new Vector3(0, 0.95f, 0), new Vector3(0.32f, 0.12f, 0.18f), boneDark);
                Part(body, "Coluna", PrimitiveType.Cylinder, new Vector3(0, 1.15f, -0.02f), new Vector3(0.07f, 0.18f, 0.07f), bone);
                Part(body, "Costelas", PrimitiveType.Cube, new Vector3(0, 1.36f, 0), new Vector3(0.36f, 0.3f, 0.2f), bone);
                Part(body, "Trapo", PrimitiveType.Cube, new Vector3(0, 0.82f, 0.02f), new Vector3(0.34f, 0.28f, 0.2f), rag);

                var head = Pivot(body, "Cabeca", new Vector3(0, 1.55f, 0));
                Part(head, "Cranio", PrimitiveType.Sphere, new Vector3(0, 0.12f, 0), new Vector3(0.25f, 0.28f, 0.27f), bone);
                Part(head, "Mandibula", PrimitiveType.Cube, new Vector3(0, 0.0f, 0.05f), new Vector3(0.16f, 0.06f, 0.14f), boneDark);
                Part(head, "Orbita E", PrimitiveType.Cube, new Vector3(-0.06f, 0.14f, 0.12f), new Vector3(0.06f, 0.05f, 0.04f), socket);
                Part(head, "Orbita D", PrimitiveType.Cube, new Vector3(0.06f, 0.14f, 0.12f), new Vector3(0.06f, 0.05f, 0.04f), socket);

                var legs = new Transform[2];
                for (var i = 0; i < 2; i++)
                {
                    var x = i == 0 ? -0.1f : 0.1f;
                    legs[i] = Pivot(body, i == 0 ? "Perna E" : "Perna D", new Vector3(x, 0.92f, 0));
                    Part(legs[i], "Femur", PrimitiveType.Cylinder, new Vector3(0, -0.45f, 0), new Vector3(0.07f, 0.45f, 0.07f), bone);
                    Part(legs[i], "Pe", PrimitiveType.Cube, new Vector3(0, -0.88f, 0.06f), new Vector3(0.09f, 0.05f, 0.2f), boneDark);
                }

                // Braço direito (arma) e esquerdo (escudo/arco): pivô no ombro, braço pendurado para baixo.
                var right = Pivot(body, "Braco Direito", new Vector3(0.25f, 1.45f, 0));
                Part(right, "Umero", PrimitiveType.Cylinder, new Vector3(0, -0.3f, 0), new Vector3(0.06f, 0.3f, 0.06f), bone);
                var left = Pivot(body, "Braco Esquerdo", new Vector3(-0.25f, 1.45f, 0));
                Part(left, "Umero", PrimitiveType.Cylinder, new Vector3(0, -0.3f, 0), new Vector3(0.06f, 0.3f, 0.06f), bone);

                Transform muzzle = null;
                if (archer)
                {
                    // Arco ao longo do Z local: fica em pé quando o braço sobe para a frente.
                    var bow = Part(left, "Arco", PrimitiveType.Cylinder, new Vector3(0, -0.6f, 0), new Vector3(0.04f, 0.6f, 0.04f), wood);
                    bow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Part(left, "Corda", PrimitiveType.Cube, new Vector3(0, -0.52f, 0), new Vector3(0.008f, 0.008f, 1.15f), boneDark);
                    var quiver = Part(body, "Aljava", PrimitiveType.Cylinder, new Vector3(0.1f, 1.35f, -0.16f), new Vector3(0.12f, 0.25f, 0.12f), rag);
                    quiver.transform.localRotation = Quaternion.Euler(-15f, 0f, -15f);
                    muzzle = Pivot(body, "Saida", new Vector3(0f, 1.45f, 0.55f));
                }
                else
                {
                    Part(right, "Punho", PrimitiveType.Cube, new Vector3(0, -0.62f, 0.05f), new Vector3(0.05f, 0.05f, 0.18f), wood);
                    Part(right, "Guarda", PrimitiveType.Cube, new Vector3(0, -0.62f, 0.15f), new Vector3(0.22f, 0.04f, 0.04f), iron);
                    Part(right, "Lamina", PrimitiveType.Cube, new Vector3(0, -0.62f, 0.55f), new Vector3(0.06f, 0.02f, 0.75f), iron);
                    Part(left, "Escudo", PrimitiveType.Cube, new Vector3(-0.04f, -0.45f, 0.16f), new Vector3(0.5f, 0.6f, 0.05f), wood);
                    Part(left, "Bossa", PrimitiveType.Sphere, new Vector3(-0.04f, -0.45f, 0.2f), new Vector3(0.12f, 0.12f, 0.06f), iron);
                }

                var eyes = Pivot(root.transform, "Olhos", new Vector3(0, 1.68f, 0.15f));
                var anim = Animator(root, body, head, right, left, legs, new[] { 1f, -1f }, Vector3.right, -85f, 30f, 1.2f);
                return Finish(root, visual, data, feedback, SurfaceKind.Wood, anim, eyes, muzzle, 1f,
                    new Vector3(0, 0.9f, 0), 0.32f, 1.8f, 1, archer ? Archer : Skeleton);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------- Cavaleiro ----------

        private static GameObject BuildKnight(EnemyData data, CombatFeedback feedback)
        {
            var armor = EditorMaterials.GetOrCreate("ArmaduraCorroida", new Color(0.17f, 0.17f, 0.16f), 0.85f, 0.35f);
            var rust = EditorMaterials.GetOrCreate("Ferrugem", new Color(0.32f, 0.18f, 0.08f), 0.4f, 0.15f);
            var plague = EditorMaterials.GetOrCreate("BrilhoPeste", new Color(0.25f, 0.6f, 0.15f), 0f, 0.5f, new Color(0.5f, 2.2f, 0.3f));
            var cloth = EditorMaterials.GetOrCreate("TabardoPodre", new Color(0.18f, 0.2f, 0.1f), 0f, 0.05f);
            var steel = EditorMaterials.GetOrCreate("AcoEscuro", new Color(0.3f, 0.3f, 0.32f), 0.9f, 0.5f);

            var root = new GameObject("Cavaleiro Corrompido");
            try
            {
                var body = NewRig(root, out var visual);
                Part(body, "Torso", PrimitiveType.Cube, new Vector3(0, 1.45f, 0), new Vector3(0.62f, 0.62f, 0.38f), armor);
                Part(body, "Cintura", PrimitiveType.Cube, new Vector3(0, 1.05f, 0), new Vector3(0.5f, 0.22f, 0.32f), rust);
                Part(body, "Tabardo", PrimitiveType.Cube, new Vector3(0, 0.85f, 0.17f), new Vector3(0.4f, 0.55f, 0.03f), cloth);
                Part(body, "Rachadura", PrimitiveType.Cube, new Vector3(0.08f, 1.5f, 0.195f), new Vector3(0.05f, 0.35f, 0.01f), plague);
                Part(body, "Ombreira E", PrimitiveType.Sphere, new Vector3(-0.38f, 1.72f, 0), new Vector3(0.32f, 0.24f, 0.36f), armor);
                Part(body, "Ombreira D", PrimitiveType.Sphere, new Vector3(0.38f, 1.72f, 0), new Vector3(0.32f, 0.24f, 0.36f), armor);

                var head = Pivot(body, "Cabeca", new Vector3(0, 1.8f, 0));
                Part(head, "Elmo", PrimitiveType.Cube, new Vector3(0, 0.17f, 0), new Vector3(0.32f, 0.36f, 0.34f), armor);
                Part(head, "Viseira", PrimitiveType.Cube, new Vector3(0, 0.2f, 0.172f), new Vector3(0.24f, 0.04f, 0.01f), plague);
                Part(head, "Crista", PrimitiveType.Cube, new Vector3(0, 0.38f, -0.02f), new Vector3(0.04f, 0.1f, 0.3f), rust);

                var legs = new Transform[2];
                for (var i = 0; i < 2; i++)
                {
                    var x = i == 0 ? -0.15f : 0.15f;
                    legs[i] = Pivot(body, i == 0 ? "Perna E" : "Perna D", new Vector3(x, 0.98f, 0));
                    Part(legs[i], "Coxa", PrimitiveType.Cube, new Vector3(0, -0.3f, 0), new Vector3(0.2f, 0.55f, 0.22f), armor);
                    Part(legs[i], "Greva", PrimitiveType.Cube, new Vector3(0, -0.75f, 0.02f), new Vector3(0.18f, 0.45f, 0.2f), rust);
                    Part(legs[i], "Bota", PrimitiveType.Cube, new Vector3(0, -0.95f, 0.07f), new Vector3(0.2f, 0.08f, 0.32f), armor);
                }

                // Braço esquerdo parado (segura a lâmina com as duas mãos no bloqueio).
                var leftArm = Part(body, "Braco Esquerdo", PrimitiveType.Cube, new Vector3(-0.42f, 1.32f, 0.08f), new Vector3(0.16f, 0.55f, 0.18f), armor);
                leftArm.transform.localRotation = Quaternion.Euler(-20f, 0f, 8f);

                var right = Pivot(body, "Braco Direito", new Vector3(0.42f, 1.62f, 0));
                Part(right, "Braco", PrimitiveType.Cube, new Vector3(0, -0.3f, 0), new Vector3(0.17f, 0.6f, 0.19f), armor);
                Part(right, "Manopla", PrimitiveType.Cube, new Vector3(0, -0.66f, 0.03f), new Vector3(0.16f, 0.14f, 0.18f), rust);
                Part(right, "Guarda", PrimitiveType.Cube, new Vector3(0, -0.68f, 0.2f), new Vector3(0.36f, 0.05f, 0.06f), steel);
                Part(right, "Montante", PrimitiveType.Cube, new Vector3(0, -0.68f, 0.95f), new Vector3(0.11f, 0.03f, 1.45f), steel);
                Part(right, "Veio da Peste", PrimitiveType.Cube, new Vector3(0, -0.66f, 0.95f), new Vector3(0.02f, 0.034f, 1.3f), plague);

                var glow = new GameObject("Brilho da Peste").AddComponent<Light>();
                glow.transform.SetParent(head, false);
                glow.transform.localPosition = new Vector3(0, 0.2f, 0.35f);
                glow.type = LightType.Point;
                glow.color = new Color(0.45f, 1f, 0.3f);
                glow.range = 3f;
                glow.intensity = 1.2f;
                glow.shadows = LightShadows.None;

                var eyes = Pivot(root.transform, "Olhos", new Vector3(0, 1.98f, 0.18f));
                var anim = Animator(root, body, head, right, null, legs, new[] { 1f, -1f }, Vector3.right, -80f, 24f, 1f);
                return Finish(root, visual, data, feedback, SurfaceKind.Metal, anim, eyes, null, 1.5f,
                    new Vector3(0, 1.05f, 0), 0.42f, 2.1f, 1, Knight);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ================= Utilitários =================

        private static Transform Pivot(Transform parent, string name, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        /// <summary>Peça visual sem colisor (o inimigo usa uma cápsula só, na raiz).</summary>
        private static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            if (scale.x < 0.1f && scale.y < 0.1f || scale.y < 0.1f && scale.z < 0.1f) r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
