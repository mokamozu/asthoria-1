using Athoria.Gameplay.AI;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Effects;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Athoria.Editor
{
    /// <summary>
    /// Inimigos humanoides (Esqueleto Guerreiro, Esqueleto Arqueiro, Cavaleiro Corrompido) montados
    /// sobre o manequim da Universal Animation Library: corpo com esqueleto de verdade, armas e
    /// peças presas nos ossos, e as animações da biblioteca (andar, correr, espada, defesa,
    /// tiro, levar dano e morte) via HumanoidEnemyAnimator.
    /// </summary>
    public static partial class EnemyContentFactory
    {
        private const string HumanoidController = "Assets/_Athoria/Animation/Inimigos/Humanoide.controller";
        private static AnimatorController _humanoidController;

        private static readonly string[] HumanoidStates =
        {
            "Sword_Idle", "Idle_Loop", "Pistol_Idle_Loop", "Walk_Loop", "Jog_Fwd_Loop",
            "Sword_Regular_A", "Sword_Regular_A_Rec", "Sword_Regular_B", "Sword_Regular_B_Rec", "Sword_Regular_C",
            "Sword_Attack", "Sword_Dash", "Sword_Block", "Melee_Hook", "Melee_Hook_Rec", "Punch_Jab",
            "Pistol_Shoot", "Hit_Chest", "Hit_Knockback", "Death01"
        };

        private static void PrepareHumanoids()
        {
            UalLibrary.Configure();
            _humanoidController = UalLibrary.BuildController(HumanoidController, HumanoidStates, "Sword_Idle");
        }

        private static HumanoidEnemyAnimator.AttackClip Atk(EnemyAttackPose pose, string clip, float impact, string recover = null) =>
            new() { pose = pose, clip = clip, impact = impact, recover = recover };

        /// <summary>Manequim da UAL dentro de "Visual", virado para +Z, com o Animator e o material do corpo.</summary>
        private static Transform Mannequin(Transform visual, float scale, Material body, Material joints, out Animator animator)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(UalLibrary.Model);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Modelo";
            model.transform.SetParent(visual, false);
            model.transform.localScale = Vector3.one * scale;
            // o bico do pé aponta para a frente: se apontar para -Z, vira o modelo
            var foot = UalLibrary.Bone(model.transform, "foot_l");
            var ball = UalLibrary.Bone(model.transform, "ball_l");
            if (foot != null && ball != null && (ball.position - foot.position).z < 0f)
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (var i = 0; i < mats.Length; i++) mats[i] = i == 1 && joints != null ? joints : body;
                r.sharedMaterials = mats;
                r.updateWhenOffscreen = true;
                r.shadowCastingMode = ShadowCastingModeOn;
            }
            animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.avatar = UalLibrary.Avatar;
            animator.runtimeAnimatorController = _humanoidController;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            return model.transform;
        }

        private const UnityEngine.Rendering.ShadowCastingMode ShadowCastingModeOn = UnityEngine.Rendering.ShadowCastingMode.On;

        /// <summary>Pivô na posição do mundo, preso ao osso (mantém a posição de repouso do manequim, em T).</summary>
        private static Transform Attach(Transform bone, string name, Vector3 worldPos)
        {
            var t = new GameObject(name).transform;
            t.SetPositionAndRotation(worldPos, Quaternion.identity);
            t.SetParent(bone, true);
            return t;
        }

        /// <summary>Peça (primitiva) com posição/tamanho no espaço do mundo, presa a um osso.</summary>
        private static GameObject BonePart(Transform bone, string name, PrimitiveType type, Vector3 worldPos, Vector3 worldSize, Material mat, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.transform.SetPositionAndRotation(worldPos, rot ?? Quaternion.identity);
            go.transform.localScale = worldSize;
            go.transform.SetParent(bone, true);
            return go;
        }

        /// <summary>Centro da mão fechada (na pose T, palma para baixo): entre o pulso e os dedos.</summary>
        private static Vector3 Grip(Transform model, string side)
        {
            var hand = UalLibrary.Bone(model, "hand_" + side);
            var finger = UalLibrary.Bone(model, "middle_01_" + side);
            var p = finger != null ? Vector3.Lerp(hand.position, finger.position, 0.75f) : hand.position;
            return p + Vector3.down * 0.03f * model.lossyScale.y;
        }

        private static HumanoidEnemyAnimator HumanoidAnim(GameObject root, Animator animator, string idle,
            HumanoidEnemyAnimator.AttackClip[] attacks, string stagger, float walkClip, float runClip)
        {
            var anim = root.AddComponent<HumanoidEnemyAnimator>();
            var so = new SerializedObject(anim);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("idle").stringValue = idle;
            so.FindProperty("stagger").stringValue = stagger;
            so.FindProperty("walkClipSpeed").floatValue = walkClip;
            so.FindProperty("runClipSpeed").floatValue = runClip;
            var list = so.FindProperty("attacks");
            list.arraySize = attacks.Length;
            for (var i = 0; i < attacks.Length; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("pose").enumValueIndex = (int)attacks[i].pose;
                e.FindPropertyRelative("clip").stringValue = attacks[i].clip;
                e.FindPropertyRelative("impact").floatValue = attacks[i].impact;
                e.FindPropertyRelative("recover").stringValue = attacks[i].recover ?? "";
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return anim;
        }

        // ---------- Esqueletos ----------

        private static GameObject BuildHumanoidSkeleton(EnemyData data, CombatFeedback feedback, bool archer)
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
                var visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                var model = Mannequin(visual, 0.98f, bone, boneDark, out var animator);
                var head = UalLibrary.Bone(model, "Head");
                var pelvis = UalLibrary.Bone(model, "pelvis");
                var spine = UalLibrary.Bone(model, "spine_03");
                var handR = UalLibrary.Bone(model, "hand_r");
                var forearmL = UalLibrary.Bone(model, "lowerarm_l");
                var handL = UalLibrary.Bone(model, "hand_l");
                var s = model.lossyScale.y;

                // caveira: órbitas vazias com brilho vermelho e mandíbula
                var face = head.position + Vector3.up * 0.1f * s + Vector3.forward * 0.1f * s;
                BonePart(head, "Orbita E", PrimitiveType.Cube, face + Vector3.left * 0.035f * s, new Vector3(0.04f, 0.03f, 0.03f) * s, socket);
                BonePart(head, "Orbita D", PrimitiveType.Cube, face + Vector3.right * 0.035f * s, new Vector3(0.04f, 0.03f, 0.03f) * s, socket);
                BonePart(head, "Mandibula", PrimitiveType.Cube, head.position + new Vector3(0f, 0.02f, 0.07f) * s, new Vector3(0.11f, 0.04f, 0.09f) * s, boneDark);
                BonePart(pelvis, "Trapo", PrimitiveType.Cube, pelvis.position + new Vector3(0f, -0.1f, 0f) * s, new Vector3(0.36f, 0.26f, 0.24f) * s, rag);

                var grip = Grip(model, "r");
                Transform muzzle = null;
                if (archer)
                {
                    // Arco na mão direita (o clipe de tiro estica o braço direito para a frente)
                    BonePart(handR, "Arco", PrimitiveType.Cylinder, grip, new Vector3(0.035f, 0.6f, 0.035f) * s, wood);
                    BonePart(handR, "Corda", PrimitiveType.Cube, grip + Vector3.back * 0.08f * s, new Vector3(0.006f, 1.15f, 0.006f) * s, boneDark);
                    BonePart(spine, "Aljava", PrimitiveType.Cylinder, spine.position + new Vector3(0.1f, 0f, -0.16f) * s, new Vector3(0.12f, 0.25f, 0.12f) * s, rag,
                        Quaternion.Euler(-15f, 0f, -15f));
                    muzzle = Attach(handR, "Saida", grip + Vector3.forward * 0.35f * s);
                }
                else
                {
                    // Espada (lâmina para a frente na pose T) e escudo no antebraço esquerdo
                    BonePart(handR, "Punho", PrimitiveType.Cube, grip, new Vector3(0.04f, 0.04f, 0.16f) * s, wood);
                    BonePart(handR, "Guarda", PrimitiveType.Cube, grip + Vector3.forward * 0.1f * s, new Vector3(0.2f, 0.035f, 0.035f) * s, iron);
                    BonePart(handR, "Lamina", PrimitiveType.Cube, grip + Vector3.forward * 0.48f * s, new Vector3(0.055f, 0.018f, 0.72f) * s, iron);
                    var shieldAt = Vector3.Lerp(forearmL.position, handL.position, 0.45f) + Vector3.up * 0.06f * s;
                    BonePart(forearmL, "Escudo", PrimitiveType.Cube, shieldAt, new Vector3(0.45f, 0.04f, 0.55f) * s, wood);
                    BonePart(forearmL, "Bossa", PrimitiveType.Sphere, shieldAt + Vector3.up * 0.035f * s, new Vector3(0.12f, 0.06f, 0.12f) * s, iron);
                }

                var eyes = Attach(head, "Olhos", face);
                var anim = archer
                    ? HumanoidAnim(root, animator, "Pistol_Idle_Loop", new[]
                    {
                        Atk(EnemyAttackPose.Shoot, "Pistol_Shoot", 0.15f),
                        Atk(EnemyAttackPose.Slash, "Punch_Jab", 0.3f),
                    }, "Hit_Chest", 1.4f, 3.6f)
                    : HumanoidAnim(root, animator, "Sword_Idle", new[]
                    {
                        Atk(EnemyAttackPose.Slash, "Sword_Regular_A", 0.62f, "Sword_Regular_A_Rec"),
                        Atk(EnemyAttackPose.Slash, "Sword_Regular_B", 0.5f, "Sword_Regular_B_Rec"),
                        Atk(EnemyAttackPose.Overhead, "Sword_Attack", 0.4f),
                        Atk(EnemyAttackPose.Thrust, "Sword_Dash", 0.35f),
                    }, "Hit_Chest", 1.4f, 3.6f);
                return Finish(root, visual, data, feedback, SurfaceKind.Wood, anim, eyes, muzzle, 1f,
                    new Vector3(0, 0.9f, 0), 0.32f, 1.8f, 1, archer ? Archer : Skeleton);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------- Cavaleiro ----------

        private static GameObject BuildHumanoidKnight(EnemyData data, CombatFeedback feedback)
        {
            var armor = EditorMaterials.GetOrCreate("ArmaduraCorroida", new Color(0.17f, 0.17f, 0.16f), 0.85f, 0.35f);
            var rust = EditorMaterials.GetOrCreate("Ferrugem", new Color(0.32f, 0.18f, 0.08f), 0.4f, 0.15f);
            var plague = EditorMaterials.GetOrCreate("BrilhoPeste", new Color(0.25f, 0.6f, 0.15f), 0f, 0.5f, new Color(0.5f, 2.2f, 0.3f));
            var cloth = EditorMaterials.GetOrCreate("TabardoPodre", new Color(0.18f, 0.2f, 0.1f), 0f, 0.05f);
            var steel = EditorMaterials.GetOrCreate("AcoEscuro", new Color(0.3f, 0.3f, 0.32f), 0.9f, 0.5f);

            var root = new GameObject("Cavaleiro Corrompido");
            try
            {
                var visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                var model = Mannequin(visual, 1.13f, armor, rust, out var animator);
                var s = model.lossyScale.y;
                var head = UalLibrary.Bone(model, "Head");
                var pelvis = UalLibrary.Bone(model, "pelvis");
                var spine = UalLibrary.Bone(model, "spine_03");
                var handR = UalLibrary.Bone(model, "hand_r");

                // elmo fechado com a fenda da viseira brilhando (peste), crista e ombreiras
                var helm = head.position + Vector3.up * 0.11f * s + Vector3.forward * 0.01f * s;
                BonePart(head, "Elmo", PrimitiveType.Cube, helm, new Vector3(0.27f, 0.3f, 0.29f) * s, armor);
                BonePart(head, "Viseira", PrimitiveType.Cube, helm + new Vector3(0f, 0.02f, 0.146f) * s, new Vector3(0.2f, 0.035f, 0.01f) * s, plague);
                BonePart(head, "Crista", PrimitiveType.Cube, helm + new Vector3(0f, 0.19f, -0.02f) * s, new Vector3(0.035f, 0.09f, 0.26f) * s, rust);
                foreach (var side in new[] { "l", "r" })
                {
                    var arm = UalLibrary.Bone(model, "upperarm_" + side);
                    BonePart(arm, "Ombreira " + side.ToUpperInvariant(), PrimitiveType.Sphere, arm.position + Vector3.up * 0.04f * s,
                        new Vector3(0.24f, 0.17f, 0.26f) * s, armor);
                }
                BonePart(pelvis, "Tabardo", PrimitiveType.Cube, pelvis.position + new Vector3(0f, -0.22f, 0.12f) * s, new Vector3(0.34f, 0.5f, 0.025f) * s, cloth);
                BonePart(spine, "Rachadura", PrimitiveType.Cube, spine.position + new Vector3(0.06f, 0f, 0.14f) * s, new Vector3(0.04f, 0.26f, 0.01f) * s, plague);

                // Montante: lâmina comprida para a frente na pose T
                var grip = Grip(model, "r");
                BonePart(handR, "Punho", PrimitiveType.Cube, grip, new Vector3(0.045f, 0.045f, 0.24f) * s, rust);
                BonePart(handR, "Guarda", PrimitiveType.Cube, grip + Vector3.forward * 0.14f * s, new Vector3(0.32f, 0.045f, 0.05f) * s, steel);
                BonePart(handR, "Montante", PrimitiveType.Cube, grip + Vector3.forward * 0.86f * s, new Vector3(0.1f, 0.025f, 1.4f) * s, steel);
                BonePart(handR, "Veio da Peste", PrimitiveType.Cube, grip + Vector3.forward * 0.86f * s, new Vector3(0.02f, 0.03f, 1.25f) * s, plague);

                var glow = new GameObject("Brilho da Peste").AddComponent<Light>();
                glow.transform.position = helm + Vector3.forward * 0.3f * s;
                glow.transform.SetParent(head, true);
                glow.type = LightType.Point;
                glow.color = new Color(0.45f, 1f, 0.3f);
                glow.range = 3f;
                glow.intensity = 1.2f;
                glow.shadows = LightShadows.None;

                var eyes = Attach(head, "Olhos", helm + Vector3.forward * 0.15f * s);
                var anim = HumanoidAnim(root, animator, "Sword_Idle", new[]
                {
                    Atk(EnemyAttackPose.Overhead, "Sword_Attack", 0.4f),
                    Atk(EnemyAttackPose.Slash, "Sword_Regular_A", 0.62f, "Sword_Regular_A_Rec"),
                    Atk(EnemyAttackPose.Slash, "Sword_Regular_B", 0.5f, "Sword_Regular_B_Rec"),
                    Atk(EnemyAttackPose.Thrust, "Sword_Dash", 0.35f),
                }, "Hit_Knockback", 1.4f, 3.6f);
                return Finish(root, visual, data, feedback, SurfaceKind.Metal, anim, eyes, null, 1.5f,
                    new Vector3(0, 1.05f, 0), 0.42f, 2.1f, 1, Knight);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
