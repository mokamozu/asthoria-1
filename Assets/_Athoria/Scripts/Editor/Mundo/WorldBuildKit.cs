using Athoria.Core;
using Athoria.Gameplay.AI;
using Athoria.Gameplay.Items;
using Athoria.Gameplay.Loot;
using Athoria.Gameplay.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

namespace Athoria.Editor
{
    /// <summary>Peças reutilizadas pelos construtores de cenas do mundo (região e masmorras).</summary>
    internal static class WorldBuildKit
    {
        public static Material Mat(string name, Color color, float metallic = 0f, float smoothness = 0.15f, Color? emission = null) =>
            EditorMaterials.GetOrCreate(name, color, metallic, smoothness, emission);

        public static Transform Group(string name, Vector3 position, float yaw = 0f, Transform parent = null)
        {
            var t = new GameObject(name).transform;
            if (parent != null) t.SetParent(parent, false);
            t.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return t;
        }

        public static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat,
            Vector3 euler = default, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }

        public static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat, Vector3 euler = default) =>
            Part(parent, name, PrimitiveType.Cube, localPos, size, mat, euler);

        /// <summary>Neblina de jogo antigo na cena: névoa linear + câmera que não desenha além dela.</summary>
        public static void AddFog(GameObject host, Color color, float start, float end, bool solidBackground = true)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = color;
            RenderSettings.fogStartDistance = start;
            RenderSettings.fogEndDistance = end;
            var fog = host.AddComponent<AreaFog>();
            var so = new SerializedObject(fog);
            so.FindProperty("fogColor").colorValue = color;
            so.FindProperty("fogStart").floatValue = start;
            so.FindProperty("fogEnd").floatValue = end;
            so.FindProperty("solidBackground").boolValue = solidBackground;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static Light Torch(Transform parent, Vector3 localPos, float range = 9f, float intensity = 2.2f)
        {
            var wood = Mat("MadeiraEscura", new Color(0.2f, 0.13f, 0.07f));
            var flame = Mat("Chama", new Color(1f, 0.55f, 0.15f), 0f, 0.3f, new Color(3f, 1.4f, 0.3f));
            var t = Group("Tocha", Vector3.zero, 0f, parent);
            t.localPosition = localPos;
            Part(t, "Cabo", PrimitiveType.Cylinder, new Vector3(0, -0.3f, 0), new Vector3(0.07f, 0.3f, 0.07f), wood, default, false);
            Part(t, "Chama", PrimitiveType.Sphere, new Vector3(0, 0.05f, 0), new Vector3(0.16f, 0.24f, 0.16f), flame, default, false);
            var lightGo = new GameObject("Luz");
            lightGo.transform.SetParent(t, false);
            lightGo.transform.localPosition = new Vector3(0, 0.25f, 0);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.3f);
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            lightGo.AddComponent<TorchFlicker>();
            return light;
        }

        public static void Discoverable(Transform parent, string id, string displayName, bool secret, int xp, Vector3 worldCenter, Vector3 size)
        {
            var go = new GameObject("Local - " + displayName);
            go.transform.SetParent(parent, true);
            go.transform.position = worldCenter;
            go.transform.rotation = Quaternion.identity;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            go.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            var loc = go.AddComponent<DiscoverableLocation>();
            var so = new SerializedObject(loc);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("isSecret").boolValue = secret;
            so.FindProperty("experience").intValue = xp;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static GameObject Instance(string prefabPath, Transform parent, Vector3 worldPos, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Athoria] Prefab não encontrado: " + prefabPath);
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(worldPos, Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        public static GameObject Enemy(string id, Transform parent, Vector3 worldPos, float yaw, LootTable lootOverride = null, Transform[] patrol = null,
            string uniqueId = null)
        {
            var go = Instance(EnemyContentFactory.PrefabPath(id), parent, worldPos, yaw);
            if (go == null) return null;
            if (!string.IsNullOrEmpty(uniqueId) && go.TryGetComponent<EnemyAI>(out var unique))
            {
                var uso = new SerializedObject(unique);
                uso.FindProperty("uniqueId").stringValue = uniqueId;
                uso.ApplyModifiedPropertiesWithoutUndo();
            }
            if (lootOverride != null && go.TryGetComponent<LootDropper>(out var dropper))
            {
                var dso = new SerializedObject(dropper);
                dso.FindProperty("table").objectReferenceValue = lootOverride;
                dso.ApplyModifiedPropertiesWithoutUndo();
            }
            if (patrol != null && go.TryGetComponent<EnemyAI>(out var ai))
            {
                var so = new SerializedObject(ai);
                var prop = so.FindProperty("patrolPoints");
                prop.arraySize = patrol.Length;
                for (var i = 0; i < patrol.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = patrol[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return go;
        }

        public static GameObject PlaceChest(Transform parent, bool locked, string id, Vector3 worldPos, float yaw, LootTable lootOverride = null)
        {
            var go = Instance(locked ? LootContentFactory.LockedChestPath : LootContentFactory.ChestPath, parent, worldPos, yaw);
            if (go == null) return null;
            var so = new SerializedObject(go.GetComponent<Athoria.Gameplay.World.Chest>());
            so.FindProperty("id").stringValue = id;
            if (lootOverride != null) so.FindProperty("loot").objectReferenceValue = lootOverride;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        public static SpawnPoint Spawn(Transform parent, string id, Vector3 worldPos, float yaw, bool isDefault)
        {
            var go = new GameObject("Chegada - " + id);
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(worldPos, Quaternion.Euler(0f, yaw, 0f));
            var sp = go.AddComponent<SpawnPoint>();
            var so = new SerializedObject(sp);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("isDefault").boolValue = isDefault;
            so.ApplyModifiedPropertiesWithoutUndo();
            return sp;
        }

        public static Portal PortalDoor(Transform parent, Vector3 localPos, Vector3 size, string targetScene, string targetSpawn, string prompt, ItemData key)
        {
            var door = Box(parent, "Porta (passagem)", localPos, size, Mat("PortaEscura", new Color(0.06f, 0.05f, 0.05f), 0.2f, 0.1f));
            var portal = door.AddComponent<Portal>();
            var so = new SerializedObject(portal);
            so.FindProperty("targetScene").stringValue = targetScene;
            so.FindProperty("targetSpawn").stringValue = targetSpawn;
            so.FindProperty("prompt").stringValue = prompt;
            so.FindProperty("requiredKey").objectReferenceValue = key;
            so.ApplyModifiedPropertiesWithoutUndo();
            return portal;
        }

        /// <summary>Jogador, HUD, inventário e EventSystem — persistem entre cenas.</summary>
        public static GameObject AddPersistentObjects(Vector3 playerPos, float yaw)
        {
            GameObject player = null;
            var playerPrefab = PlayerPrefabBuilder.GetOrBuild();
            if (playerPrefab != null)
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.transform.SetPositionAndRotation(playerPos, Quaternion.Euler(0f, yaw, 0f));
                Persist(player, "Player");
                player.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            }

            var hud = HudPrefabBuilder.GetOrBuild();
            if (hud != null) Persist((GameObject)PrefabUtility.InstantiatePrefab(hud), "HUD");
            var inventory = InventoryUiBuilder.GetOrBuild();
            if (inventory != null) Persist((GameObject)PrefabUtility.InstantiatePrefab(inventory), "Inventario");

            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            CharacterSelectSceneBuilder.ConfigureUiInput(es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>());
            Persist(es, "EventSystem");

            new GameObject("_GameBootstrap").AddComponent<GameBootstrap>();
            return player;
        }

        private static void Persist(GameObject go, string key)
        {
            var p = go.AddComponent<PersistAcrossScenes>();
            var so = new SerializedObject(p);
            so.FindProperty("key").stringValue = key;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static ItemData Item(string id)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemData>($"Assets/_Athoria/Data/Itens/Item_{id}.asset");
            if (item == null) item = AssetDatabase.LoadAssetAtPath<ItemData>($"Assets/_Athoria/Data/Armas/Arma_{id}.asset");
            return item;
        }

        public static LootEntry Entry(string id, float weight, int min = 1, int max = 1)
        {
            var item = Item(id);
            return item == null ? null : new LootEntry { item = item, weight = weight, quantity = new Vector2Int(min, max) };
        }

        public static LootEntry[] Entries(params LootEntry[] e) => System.Array.FindAll(e, x => x != null);

        public static void AddToBuild(params string[] paths)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var path in paths)
                if (!scenes.Exists(s => s.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
