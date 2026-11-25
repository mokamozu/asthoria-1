using System.IO;
using System.Linq;
using Athoria.Core;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Athoria.Editor
{
    /// <summary>
    /// Gera a cena Sandbox: área de testes com obstáculos para validar
    /// movimentação (pulo, agachar, rampa, escada) e, depois, combate.
    /// </summary>
    public static class SandboxSceneBuilder
    {
        private const string ScenePath = "Assets/_Athoria/Scenes/Sandbox.unity";
        private const string MaterialFolder = "Assets/_Athoria/Materials";

        [MenuItem("Athoria/Cenas/Criar ou recriar Sandbox")]
        public static void Build() => Build(true);

        public static void Build(bool askBeforeOverwrite)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (askBeforeOverwrite && File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Sandbox", "Já existe uma cena Sandbox. Recriar do zero?", "Recriar", "Cancelar"))
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var stone = GetOrCreateMaterial("Sandbox_Pedra", new Color(0.22f, 0.21f, 0.20f));
            var bronze = GetOrCreateMaterial("Sandbox_Bronze", new Color(0.45f, 0.32f, 0.18f));
            var blood = GetOrCreateMaterial("Sandbox_VermelhoEscuro", new Color(0.35f, 0.07f, 0.06f));

            // Atmosfera: luz fria, ambiente escuro e névoa.
            var sun = new GameObject("Sol").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.85f, 0.82f, 0.95f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.20f, 0.20f, 0.26f);
            RenderSettings.ambientEquatorColor = new Color(0.12f, 0.11f, 0.12f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.04f, 0.04f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.10f, 0.10f, 0.12f);
            RenderSettings.fogDensity = 0.015f;

            var root = new GameObject("Geometria").transform;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Chao";
            ground.transform.SetParent(root);
            ground.transform.localScale = new Vector3(10f, 1f, 10f); // 100 x 100 m
            ground.GetComponent<Renderer>().sharedMaterial = stone;

            // Muralhas
            Box(root, "Muralha Norte", new Vector3(0, 2, 50), new Vector3(100, 4, 1), stone);
            Box(root, "Muralha Sul", new Vector3(0, 2, -50), new Vector3(100, 4, 1), stone);
            Box(root, "Muralha Leste", new Vector3(50, 2, 0), new Vector3(1, 4, 100), stone);
            Box(root, "Muralha Oeste", new Vector3(-50, 2, 0), new Vector3(1, 4, 100), stone);

            // Teste de pulo
            Box(root, "Caixa 0.5m", new Vector3(4, 0.25f, 8), new Vector3(2, 0.5f, 2), bronze);
            Box(root, "Caixa 1m", new Vector3(7, 0.5f, 8), new Vector3(2, 1f, 2), bronze);
            Box(root, "Caixa 1.5m", new Vector3(10, 0.75f, 8), new Vector3(2, 1.5f, 2), bronze);

            // Teste de agachar: túnel com teto a 1.2 m
            Box(root, "Tunel Parede A", new Vector3(-9, 0.6f, 10), new Vector3(0.5f, 1.2f, 6), stone);
            Box(root, "Tunel Parede B", new Vector3(-6, 0.6f, 10), new Vector3(0.5f, 1.2f, 6), stone);
            Box(root, "Tunel Teto", new Vector3(-7.5f, 1.4f, 10), new Vector3(3.5f, 0.4f, 6), stone);

            // Rampa (20°)
            var ramp = Box(root, "Rampa 20 graus", new Vector3(0, 1.0f, 20), new Vector3(4, 0.3f, 6), bronze);
            ramp.transform.rotation = Quaternion.Euler(-20f, 0f, 0f);

            // Escada: 8 degraus de 25 cm
            var stairs = new GameObject("Escada").transform;
            stairs.SetParent(root);
            for (var i = 0; i < 8; i++)
            {
                var h = 0.25f * (i + 1);
                Box(stairs, $"Degrau {i + 1}", new Vector3(-15, h * 0.5f, 18 + i * 0.5f), new Vector3(3, h, 0.5f), stone);
            }

            // Pilares para testes de combate (cobertura / linha de visão)
            for (var i = 0; i < 4; i++)
            {
                var x = -12f + i * 8f;
                Box(root, $"Pilar {i + 1}", new Vector3(x, 2f, 32), new Vector3(1.2f, 4f, 1.2f), blood);
            }

            // Porta (teste de interação): parede com vão de 2 m x 2.2 m.
            var doorway = new GameObject("Passagem com Porta").transform;
            doorway.SetParent(root);
            Box(doorway, "Parede Esquerda", new Vector3(-4f, 1.5f, 4f), new Vector3(6f, 3f, 0.3f), stone);
            Box(doorway, "Parede Direita", new Vector3(4f, 1.5f, 4f), new Vector3(6f, 3f, 0.3f), stone);
            Box(doorway, "Verga", new Vector3(0f, 2.6f, 4f), new Vector3(2f, 0.8f, 0.3f), stone);
            var hinge = new GameObject("Porta (dobradiça)");
            hinge.transform.SetParent(doorway);
            hinge.transform.position = new Vector3(-1f, 0f, 4f);
            hinge.AddComponent<SimpleDoor>();
            var doorSurface = hinge.AddComponent<SurfaceMaterial>();
            var dsSo = new SerializedObject(doorSurface);
            dsSo.FindProperty("kind").enumValueIndex = (int)SurfaceKind.Wood;
            dsSo.ApplyModifiedPropertiesWithoutUndo();
            hinge.AddComponent<NavMeshModifier>().ignoreFromBuild = true;   // porta abre: não é parede no NavMesh
            var panel = Box(hinge.transform, "Folha da Porta", Vector3.zero, new Vector3(1.96f, 2.18f, 0.12f), bronze);
            panel.isStatic = false;
            panel.transform.localPosition = new Vector3(0.99f, 1.1f, 0f);

            // Combate: bonecos de treino depois da porta e armadilha de dardos à direita.
            var items = ItemContentFactory.GenerateAll();
            var lootTables = LootContentFactory.GenerateAll(items);
            var feedback = CombatContentFactory.GenerateAll();
            var dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CombatContentFactory.DummyPath);
            var combatArea = new GameObject("Area de Combate").transform;
            combatArea.SetParent(root);
            if (dummyPrefab != null)
            {
                Vector3[] spots = { new(-3f, 0f, 12f), new(0f, 0f, 13.5f), new(3f, 0f, 12f) };
                foreach (var spot in spots)
                {
                    var d = (GameObject)PrefabUtility.InstantiatePrefab(dummyPrefab, combatArea);
                    d.transform.SetPositionAndRotation(spot, Quaternion.Euler(0f, 180f, 0f));
                }
            }
            var trapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CombatContentFactory.TrapPath);
            if (trapPrefab != null)
            {
                Box(combatArea, "Pedestal da Armadilha", new Vector3(16f, 0.65f, -6f), new Vector3(1f, 1.3f, 1f), stone);
                var trap = (GameObject)PrefabUtility.InstantiatePrefab(trapPrefab, combatArea);
                trap.transform.SetPositionAndRotation(new Vector3(16f, 1.6f, -6f), Quaternion.Euler(0f, -90f, 0f));
            }

            // Inimigos (Etapa 11)
            BuildEnemies(root, feedback, stone, blood, lootTables);
            BuildChests(root);

            // Locais descobríveis (XP de exploração)
            var places = new GameObject("Locais").transform;
            places.SetParent(root);
            Discoverable(places, "sandbox_patio", "Pátio de Treino", false, 30, new Vector3(0f, 1.5f, 11f), new Vector3(12f, 3f, 10f));
            Discoverable(places, "sandbox_armadilha", "Posto da Armadilha", false, 20, new Vector3(11f, 1.5f, -6f), new Vector3(6f, 3f, 6f));
            Discoverable(places, "sandbox_topo_escada", "Topo da Escada", true, 60, new Vector3(-15f, 2.6f, 21.5f), new Vector3(3f, 1.2f, 1.5f));

            // Jogador
            var playerPrefab = PlayerPrefabBuilder.GetOrBuild();
            if (playerPrefab != null)
            {
                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, -6f), Quaternion.identity);
            }

            // Interface: EventSystem (cliques na UI) e inventário
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            CharacterSelectSceneBuilder.ConfigureUiInput(es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>());
            var inventoryPrefab = InventoryUiBuilder.GetOrBuild();
            if (inventoryPrefab != null) PrefabUtility.InstantiatePrefab(inventoryPrefab);

            // Itens no chão perto do início (teste de pegar itens)
            var loot = new GameObject("Itens no Chao").transform;
            loot.SetParent(root);
            PlaceItem(loot, items["PocaoVida"], 2, new Vector3(2f, 0f, -3f));
            PlaceItem(loot, items["Martelo"], 1, new Vector3(-2.5f, 0f, -3f));
            PlaceItem(loot, items["Chave"], 1, new Vector3(0f, 0f, -1.5f));
            PlaceItem(loot, items["Escudo"], 1, new Vector3(4f, 0f, -1f));
            PlaceGold(loot, 40, new Vector3(-4f, 0f, -1f));
            PlaceItem(loot, items["Couraca"], 1, new Vector3(-1.5f, 0f, -5f));
            PlaceItem(loot, items["Manoplas"], 1, new Vector3(1.5f, 0f, -5f));
            PlaceItem(loot, items["Grevas"], 1, new Vector3(-3f, 0f, -6.5f));
            PlaceItem(loot, items["AnelResistencia"], 1, new Vector3(3f, 0f, -6.5f));
            PlaceItem(loot, items["AnelAgilidade"], 1, new Vector3(-1f, 0f, -8f));
            PlaceItem(loot, items["Amuleto"], 1, new Vector3(1f, 0f, -8f));
            // Itens são pequenos e somem quando pegos: não entram no NavMesh.
            loot.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            var hudPrefab = HudPrefabBuilder.GetOrBuild();
            if (hudPrefab != null) PrefabUtility.InstantiatePrefab(hudPrefab);

            var bootstrap = new GameObject("_GameBootstrap");
            bootstrap.AddComponent<GameBootstrap>();
            Athoria.Editor.WorldBuildKit.AddFog(bootstrap, new Color(0.10f, 0.10f, 0.12f), 18f, 70f);

            // NavMesh: gerado ao carregar a cena a partir de tudo dentro de "Geometria".
            var surface = root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;
            root.gameObject.AddComponent<RuntimeNavMeshBaker>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log("[Athoria] Cena Sandbox criada em " + ScenePath);
        }

        private static void BuildChests(Transform geometry)
        {
            var common = AssetDatabase.LoadAssetAtPath<GameObject>(LootContentFactory.ChestPath);
            var locked = AssetDatabase.LoadAssetAtPath<GameObject>(LootContentFactory.LockedChestPath);
            var chests = new GameObject("Baus").transform;
            chests.SetParent(geometry);
            PlaceChest(chests, common, "sandbox_bau_inicio", new Vector3(7f, 0f, -9f), -65f);
            PlaceChest(chests, common, "sandbox_bau_covil", new Vector3(28.5f, 0f, 27.5f), -135f);
            // Trancado atrás do altar do Cavaleiro: a Chave Enferrujada está no chão perto do início.
            PlaceChest(chests, locked, "sandbox_bau_ruinas", new Vector3(-38.6f, 0f, 30f), 90f);
        }

        private static void PlaceChest(Transform parent, GameObject prefab, string id, Vector3 position, float yaw)
        {
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var so = new SerializedObject(go.GetComponent<Athoria.Gameplay.World.Chest>());
            so.FindProperty("id").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildEnemies(Transform geometry, CombatFeedback feedback, Material stone, Material blood,
            System.Collections.Generic.Dictionary<string, Athoria.Gameplay.Loot.LootTable> loot)
        {
            var prefabs = EnemyContentFactory.GenerateAll(feedback, loot);
            var enemies = new GameObject("Inimigos").transform;

            // Matilha de lobos — leste, depois da rampa.
            Enemy(enemies, prefabs[EnemyContentFactory.Wolf], new Vector3(24f, 0f, 22f), 200f);
            Enemy(enemies, prefabs[EnemyContentFactory.Wolf], new Vector3(26.5f, 0f, 24f), 220f);
            Enemy(enemies, prefabs[EnemyContentFactory.Wolf], new Vector3(23f, 0f, 25.5f), 190f);

            // Esqueletos atrás dos pilares — norte (os pilares dão cobertura contra o arqueiro).
            Enemy(enemies, prefabs[EnemyContentFactory.Skeleton], new Vector3(-4f, 0f, 38f), 180f);
            Enemy(enemies, prefabs[EnemyContentFactory.Skeleton], new Vector3(4f, 0f, 38f), 180f);
            Enemy(enemies, prefabs[EnemyContentFactory.Archer], new Vector3(0f, 0f, 44f), 180f);

            // Cavaleiro Corrompido guardando ruínas — oeste.
            var ruins = new GameObject("Ruinas do Cavaleiro").transform;
            ruins.SetParent(geometry);
            Box(ruins, "Coluna Quebrada A", new Vector3(-33f, 1.2f, 26f), new Vector3(1.4f, 2.4f, 1.4f), stone);
            Box(ruins, "Coluna Quebrada B", new Vector3(-33f, 0.7f, 34f), new Vector3(1.4f, 1.4f, 1.4f), stone);
            Box(ruins, "Altar", new Vector3(-36f, 0.5f, 30f), new Vector3(1.2f, 1f, 3f), blood);
            Enemy(enemies, prefabs[EnemyContentFactory.Knight], new Vector3(-31f, 0f, 30f), 90f);

            var places = new GameObject("Locais dos Inimigos").transform;
            places.SetParent(geometry);
            Discoverable(places, "sandbox_covil", "Covil dos Lobos", false, 25, new Vector3(24.5f, 1.5f, 23.5f), new Vector3(10f, 3f, 10f));
            Discoverable(places, "sandbox_ruinas", "Ruínas do Cavaleiro", false, 40, new Vector3(-32f, 1.5f, 30f), new Vector3(10f, 3f, 12f));
        }

        private static void Enemy(Transform parent, GameObject prefab, Vector3 position, float yaw)
        {
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            // No Sandbox os inimigos voltam depois de mortos, para dar para testar de novo.
            var ai = go.GetComponent<Athoria.Gameplay.AI.EnemyAI>();
            var so = new SerializedObject(ai);
            so.FindProperty("respawnDelay").floatValue = 30f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void PlaceItem(Transform parent, Athoria.Gameplay.Items.ItemData item, int qty, Vector3 pos)
        {
            var go = new GameObject("Item - " + item.DisplayName);
            go.transform.SetParent(parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            var wi = go.AddComponent<WorldItem>();
            var so = new SerializedObject(wi);
            so.FindProperty("item").objectReferenceValue = item;
            so.FindProperty("quantity").intValue = qty;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void PlaceGold(Transform parent, int amount, Vector3 pos)
        {
            var go = new GameObject("Ouro");
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var wi = go.AddComponent<WorldItem>();
            var so = new SerializedObject(wi);
            so.FindProperty("gold").intValue = amount;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Discoverable(Transform parent, string id, string displayName, bool secret, int xp, Vector3 center, Vector3 size)
        {
            var go = new GameObject("Local - " + displayName);
            go.transform.SetParent(parent);
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            var loc = go.AddComponent<DiscoverableLocation>();
            var so = new SerializedObject(loc);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("isSecret").boolValue = secret;
            so.FindProperty("experience").intValue = xp;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            Directory.CreateDirectory(MaterialFolder);
            var path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
