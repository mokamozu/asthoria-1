using System.IO;
using Athoria.Gameplay.Characters;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Debugging;
using Athoria.Gameplay.Interaction;
using Athoria.Gameplay.Inventory;
using Athoria.Gameplay.Player;
using Athoria.Gameplay.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Athoria.Editor
{
    /// <summary>
    /// Monta o Prefab do jogador com todos os componentes já ligados.
    /// Hierarquia: Player > CameraPivot > CameraOffset > Main Camera.
    /// </summary>
    public static class PlayerPrefabBuilder
    {
        public const string PrefabPath = "Assets/_Athoria/Prefabs/Player.prefab";
        private const string ControlsPath = "Assets/_Athoria/Input/Athoria_Controls.inputactions";

        [MenuItem("Athoria/Prefabs/Recriar Prefab do Player")]
        public static void RebuildMenu()
        {
            Build();
            Debug.Log("[Athoria] Prefab do Player recriado em " + PrefabPath);
        }

        public static GameObject GetOrBuild()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return existing != null ? existing : Build();
        }

        public static GameObject Build()
        {
            var controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsPath);
            if (controls == null)
            {
                Debug.LogError("[Athoria] Não encontrei " + ControlsPath);
                return null;
            }

            var viewModelLayer = ProjectLayers.Ensure(PlayerWeaponView.ViewModelLayerName);
            var weapons = PlaceholderWeaponFactory.GenerateAll();
            var feedback = CombatContentFactory.GenerateAll();
            var fists = CombatContentFactory.LoadFists();
            var characters = CharacterContentFactory.GenerateAll();
            var items = ItemContentFactory.GenerateAll();

            var root = new GameObject("Player") { tag = "Player" };
            try
            {
                var cc = root.AddComponent<CharacterController>();
                cc.height = 1.8f;
                cc.radius = 0.35f;
                cc.center = new Vector3(0f, 0.9f, 0f);
                cc.stepOffset = 0.4f;
                cc.slopeLimit = 45f;
                cc.skinWidth = 0.04f;
                cc.minMoveDistance = 0f;

                var pivot = new GameObject("CameraPivot").transform;
                pivot.SetParent(root.transform, false);
                pivot.localPosition = new Vector3(0f, 1.65f, 0f);

                var offset = new GameObject("CameraOffset").transform;
                offset.SetParent(pivot, false);

                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                camGo.transform.SetParent(offset, false);
                var cam = camGo.AddComponent<Camera>();
                cam.nearClipPlane = 0.05f;
                cam.fieldOfView = 75f;
                camGo.AddComponent<AudioListener>();

                // Câmera só dos braços/armas: desenhada por cima do mundo (overlay),
                // com FOV próprio, para a arma nunca atravessar paredes.
                var vmCamGo = new GameObject("ViewModel Camera");
                vmCamGo.transform.SetParent(camGo.transform, false);
                var vmCam = vmCamGo.AddComponent<Camera>();
                vmCam.nearClipPlane = 0.01f;
                vmCam.farClipPlane = 10f;
                vmCam.fieldOfView = 60f;
                var vmData = vmCam.GetUniversalAdditionalCameraData();
                vmData.renderType = CameraRenderType.Overlay;
                vmData.renderShadows = false;
                var mainData = cam.GetUniversalAdditionalCameraData();
                mainData.cameraStack.Add(vmCam);
                if (viewModelLayer >= 0)
                {
                    cam.cullingMask &= ~(1 << viewModelLayer);
                    vmCam.cullingMask = 1 << viewModelLayer;
                }

                // Rig dos braços
                var motionRoot = new GameObject("ViewModelRoot").transform;
                motionRoot.SetParent(camGo.transform, false);
                var rightArm = BuildArm(motionRoot, "Braco Direito", out var rightSocket);
                var leftArm = BuildArm(motionRoot, "Braco Esquerdo", out var leftSocket);
                // Pose inicial (vista no editor fora do Play); em jogo o PlayerWeaponView assume.
                rightArm.localPosition = new Vector3(0.26f, -0.42f, 0.32f);
                rightArm.localRotation = Quaternion.Euler(-14f, -12f, 0f);
                leftArm.localPosition = new Vector3(-0.3f, -0.78f, 0.15f);
                leftArm.localRotation = Quaternion.Euler(10f, 0f, 0f);

                var armsSkin = root.AddComponent<ArmsSkin>();
                armsSkin.EditorSetRenderers(motionRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true));

                var character = root.AddComponent<PlayerCharacter>();
                var traits = root.AddComponent<PlayerTraits>();
                var input = root.AddComponent<PlayerInputReader>();
                var stamina = root.AddComponent<PlayerStamina>();
                var mana = root.AddComponent<PlayerMana>();
                var experience = root.AddComponent<PlayerExperience>();
                var controller = root.AddComponent<PlayerController>();
                var look = root.AddComponent<PlayerLook>();
                var effects = root.AddComponent<PlayerCameraEffects>();
                var interactor = root.AddComponent<PlayerInteractor>();
                root.AddComponent<Athoria.Gameplay.Save.SaveHotkeys>();
                var overlay = root.AddComponent<PlayerDebugOverlay>();
                var loadout = root.AddComponent<WeaponLoadout>();
                root.AddComponent<FirearmAmmo>();
                root.AddComponent<Athoria.Gameplay.Summons.MantraPool>();
                root.AddComponent<Athoria.Gameplay.Summons.SummonController>();
                var weaponView = root.AddComponent<PlayerWeaponView>();
                var health = root.AddComponent<Health>();
                var combat = root.AddComponent<PlayerCombat>();
                var death = root.AddComponent<PlayerDeathHandler>();
                var inventory = root.AddComponent<PlayerInventory>();
                var stats = root.AddComponent<PlayerStats>();
                var equipment = root.AddComponent<PlayerEquipment>();
                var ability = root.AddComponent<PlayerAbility>();
                var quickItems = root.AddComponent<QuickItemSlots>();
                root.AddComponent<DamageNumbers>();

                Set(input, "actions", controls);
                Set(controller, "input", input);
                Set(controller, "stamina", stamina);
                Set(look, "input", input);
                Set(look, "cameraPivot", pivot);
                Set(effects, "controller", controller);
                Set(effects, "cameraPivot", pivot);
                Set(effects, "cameraOffset", offset);
                Set(effects, "targetCamera", cam);
                Set(interactor, "input", input);
                Set(interactor, "viewOrigin", camGo.transform);
                Set(overlay, "controller", controller);
                Set(overlay, "stamina", stamina);
                Set(overlay, "interactor", interactor);

                Set(loadout, "input", input);
                var loSo = new SerializedObject(loadout);
                var slots = loSo.FindProperty("quickSlots");
                slots.arraySize = WeaponLoadout.QuickSlotCount;
                string[] slotIds = { "Espada", "Lanca" };
                for (var i = 0; i < slotIds.Length; i++)
                    slots.GetArrayElementAtIndex(i).objectReferenceValue = weapons[slotIds[i]];
                loSo.FindProperty("offHand").objectReferenceValue = weapons["Escudo"];
                loSo.FindProperty("startingSlot").intValue = 0;
                loSo.ApplyModifiedPropertiesWithoutUndo();

                Set(weaponView, "loadout", loadout);
                Set(weaponView, "controller", controller);
                Set(weaponView, "input", input);
                Set(weaponView, "motionRoot", motionRoot);
                Set(weaponView, "rightArm", rightArm);
                Set(weaponView, "leftArm", leftArm);
                Set(weaponView, "rightSocket", rightSocket);
                Set(weaponView, "leftSocket", leftSocket);

                var hSo = new SerializedObject(health);
                hSo.FindProperty("isPlayer").boolValue = true;
                hSo.FindProperty("maxHealth").floatValue = 100f;
                hSo.FindProperty("invulnerabilityAfterHit").floatValue = 0.15f;
                hSo.ApplyModifiedPropertiesWithoutUndo();

                Set(combat, "input", input);
                Set(combat, "controller", controller);
                Set(combat, "stamina", stamina);
                Set(combat, "mana", mana);
                Set(combat, "traits", traits);
                Set(traits, "health", health);
                Set(character, "fallback", characters.Default);
                Set(combat, "weaponView", weaponView);
                Set(combat, "cameraEffects", effects);
                Set(combat, "health", health);
                Set(combat, "aimOrigin", camGo.transform);
                Set(combat, "feedback", feedback);
                Set(combat, "unarmed", fists);

                Set(death, "cameraPivot", pivot);
                var dSo = new SerializedObject(death);
                var list = dSo.FindProperty("disableOnDeath");
                Behaviour[] toDisable = { input, look, combat, interactor, effects };
                list.arraySize = toDisable.Length;
                for (var i = 0; i < toDisable.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = toDisable[i];
                dSo.ApplyModifiedPropertiesWithoutUndo();

                var invSo = new SerializedObject(inventory);
                var start = invSo.FindProperty("startingItems");
                (string id, int qty)[] startItems = { ("PocaoVida", 3), ("PocaoVigor", 2), ("PocaoMana", 1), ("Erva", 4), ("AnelVitalidade", 1), ("AnelForca", 1), ("AnelMana", 1) };
                start.arraySize = startItems.Length;
                for (var i = 0; i < startItems.Length; i++)
                {
                    var element = start.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("item").objectReferenceValue = items[startItems[i].id];
                    element.FindPropertyRelative("quantity").intValue = startItems[i].qty;
                }
                invSo.FindProperty("startingGold").intValue = 25;
                invSo.ApplyModifiedPropertiesWithoutUndo();

                Set(overlay, "health", health);
                Set(overlay, "combat", combat);
                Set(overlay, "mana", mana);
                Set(overlay, "experience", experience);
                Set(experience, "stats", stats);
                Set(equipment, "loadout", loadout);
                Set(equipment, "stats", stats);
                Set(ability, "input", input);
                Set(quickItems, "input", input);
                Set(quickItems, "inventory", inventory);
                var qSo = new SerializedObject(quickItems);
                var qSlots = qSo.FindProperty("slots");
                qSlots.arraySize = QuickItemSlots.Count;
                qSlots.GetArrayElementAtIndex(0).objectReferenceValue = items["PocaoVida"];
                qSlots.GetArrayElementAtIndex(1).objectReferenceValue = items["PocaoVigor"];
                qSo.ApplyModifiedPropertiesWithoutUndo();
                Set(combat, "stats", stats);

                if (viewModelLayer >= 0) SetLayerRecursively(motionRoot.gameObject, viewModelLayer);

                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Braço provisório: raiz no cotovelo, apontando para +Z, soquete na mão.</summary>
        private static Transform BuildArm(Transform parent, string name, out Transform socket)
        {
            var leather = EditorMaterials.GetOrCreate("CouroLuva", new Color(0.2f, 0.12f, 0.07f), 0f, 0.25f);
            var bronze = EditorMaterials.GetOrCreate("BronzeEnvelhecido", new Color(0.55f, 0.38f, 0.18f), 0.8f, 0.45f);
            var cloth = EditorMaterials.GetOrCreate("TecidoEscuro", new Color(0.12f, 0.11f, 0.12f), 0f, 0.1f);

            var arm = new GameObject(name).transform;
            arm.SetParent(parent, false);
            socket = new GameObject("Soquete Mao").transform;
            socket.SetParent(arm, false);
            socket.localPosition = new Vector3(0, 0, 0.35f);

            // Mãos PS1 (pacote FPS PSX Hands); sem o modelo, braço provisório de blocos.
            var right = name.Contains("Direito");
            if (PsxAssets.BuildArmModel(arm, right, socket.localPosition) != null) return arm;

            ArmPart(arm, "Antebraco", new Vector3(0, 0, 0.14f), new Vector3(0.075f, 0.075f, 0.3f), cloth);
            ArmPart(arm, "Bracadeira", new Vector3(0, 0, 0.24f), new Vector3(0.088f, 0.088f, 0.12f), bronze);
            ArmPart(arm, "Mao", new Vector3(0, 0, 0.35f), new Vector3(0.085f, 0.1f, 0.1f), leather);
            return arm;
        }

        private static void ArmPart(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[Athoria] Campo '{field}' não existe em {target.GetType().Name}.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
