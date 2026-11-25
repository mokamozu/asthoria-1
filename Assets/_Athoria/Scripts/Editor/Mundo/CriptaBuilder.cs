using System.IO;
using Athoria.Gameplay.Items;
using Athoria.Gameplay.Loot;
using Athoria.Gameplay.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using static Athoria.Editor.WorldBuildKit;

namespace Athoria.Editor
{
    /// <summary>
    /// Masmorra interior ligada às Terras Alagadas: salão de entrada, corredor
    /// com duas salas laterais e o salão final com o guardião. Cena separada,
    /// carregada pela porta da cripta (Portal) — o jogador e os itens vêm junto.
    /// </summary>
    public static class CriptaBuilder
    {
        public const string SceneName = "CriptaDaCapela";
        public const string ScenePath = "Assets/_Athoria/Scenes/CriptaDaCapela.unity";
        private const float WallH = 4f;
        private const float Thick = 0.6f;

        [MenuItem("Athoria/Cenas/Criar ou recriar Cripta da Capela (masmorra)")]
        public static void BuildMenu() => Build(true);

        public static void Build(bool askBeforeOverwrite)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (askBeforeOverwrite && File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Cripta", "Recriar a Cripta da Capela?", "Recriar", "Cancelar"))
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.07f, 0.07f, 0.08f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.02f, 0.025f, 0.02f);
            RenderSettings.fogDensity = 0.045f;

            const string Dg = "Dungeon/PSX_Dungeon/";
            var stone = PsxAssets.Textured(Dg + "Textures/TEX_Wall_03.png", "PedraCripta", new Color(0.62f, 0.62f, 0.6f), Mat("PedraCripta", new Color(0.2f, 0.2f, 0.19f)));
            var mossStone = PsxAssets.Textured(Dg + "Textures/TEX_Wall_03.png", "PedraMusgoCripta", new Color(0.5f, 0.6f, 0.45f), Mat("PedraMusgo", new Color(0.2f, 0.25f, 0.17f)));
            var floorMat = PsxAssets.Textured(Dg + "Textures/TEX_Ground_04.png", "PisoCripta", new Color(0.6f, 0.6f, 0.58f), Mat("PisoCripta", new Color(0.15f, 0.15f, 0.14f), 0f, 0.3f));
            var bone = Mat("Osso", new Color(0.78f, 0.74f, 0.63f), 0f, 0.25f);
            var water = Mat("AguaParada", new Color(0.08f, 0.12f, 0.07f), 0.1f, 0.95f);

            var geo = new GameObject("Geometria").transform;
            Box(geo, "Piso", new Vector3(0f, -0.1f, 20f), new Vector3(24f, 0.2f, 54f), floorMat);
            Box(geo, "Teto", new Vector3(0f, WallH + 0.1f, 20f), new Vector3(24f, 0.2f, 54f), stone);

            // Salão de entrada: x -5..5, z -5..5
            WallX(geo, -5f, -5f, 5f, stone);
            WallZ(geo, -5f, -5f, 5f, stone);
            WallZ(geo, 5f, -5f, 5f, mossStone);
            WallX(geo, 5f, -5f, -2f, stone);
            WallX(geo, 5f, 2f, 5f, stone);
            // Corredor: x -2..2, z 5..25, com aberturas para as salas laterais (z 13..17)
            WallZ(geo, -2f, 5f, 13f, stone);
            WallZ(geo, -2f, 17f, 25f, stone);
            WallZ(geo, 2f, 5f, 13f, mossStone);
            WallZ(geo, 2f, 17f, 25f, stone);
            // Sala oeste: x -10..-2, z 11..19
            WallX(geo, 11f, -10f, -2f, stone);
            WallX(geo, 19f, -10f, -2f, stone);
            WallZ(geo, -10f, 11f, 19f, mossStone);
            // Sala leste: x 2..10, z 11..19
            WallX(geo, 11f, 2f, 10f, stone);
            WallX(geo, 19f, 2f, 10f, mossStone);
            WallZ(geo, 10f, 11f, 19f, stone);
            // Salão final: x -9..9, z 25..45
            WallX(geo, 25f, -9f, -2f, stone);
            WallX(geo, 25f, 2f, 9f, stone);
            WallZ(geo, -9f, 25f, 45f, stone);
            WallZ(geo, 9f, 25f, 45f, mossStone);
            WallX(geo, 45f, -9f, 9f, stone);

            // Detalhes
            for (var i = 0; i < 4; i++)
            {
                Box(geo, "Pilar", new Vector3(-4.5f, WallH * 0.5f, 29f + i * 4.5f), new Vector3(1f, WallH, 1f), stone);
                Box(geo, "Pilar", new Vector3(4.5f, WallH * 0.5f, 29f + i * 4.5f), new Vector3(1f, WallH, 1f), stone);
            }
            Box(geo, "Altar", new Vector3(0f, 0.6f, 42.5f), new Vector3(3f, 1.2f, 1.4f), stone);
            Box(geo, "Sarcófago O1", new Vector3(-8f, 0.45f, 13f), new Vector3(2.2f, 0.9f, 1f), stone);
            Box(geo, "Sarcófago O2", new Vector3(-8f, 0.45f, 17f), new Vector3(2.2f, 0.9f, 1f), stone);
            Box(geo, "Sarcófago L1", new Vector3(8f, 0.45f, 13f), new Vector3(2.2f, 0.9f, 1f), mossStone);
            var puddle = Part(geo, "Poça", PrimitiveType.Cylinder, new Vector3(-1.5f, 0.01f, 33f), new Vector3(4f, 0.01f, 3f), water, default, false);
            puddle.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var rng = new System.Random(9);
            for (var i = 0; i < 24; i++)
            {
                var pos = new Vector3((float)rng.NextDouble() * 16f - 8f, 0.04f, 26f + (float)rng.NextDouble() * 18f);
                Part(geo, "Osso", PrimitiveType.Cylinder, pos, new Vector3(0.06f, 0.22f, 0.06f), bone, new Vector3(90f, (float)rng.NextDouble() * 180f, 0f), false);
            }

            // Kit PSX Dungeon por cima dos blocos (a colisão continua nos blocos).
            var dungeon = KitStyle.Of(
                PsxAssets.Kit(Dg + "Models/Floor_Tiles.fbx"),
                PsxAssets.Kit(Dg + "Models/Pillar.fbx"),
                PsxAssets.Kit(Dg + "Models/Door_02.fbx"),
                PsxAssets.Kit(Dg + "Models/Wall_01.fbx"),
                PsxAssets.Kit(Dg + "Models/Wall_02.fbx"),
                PsxAssets.Kit(Dg + "Models/Wall_01.fbx"),
                PsxAssets.Kit(Dg + "Models/Skull_Wall.fbx"));
            KitDresser.Dress(geo, dungeon, 7);
            BuildProps(Dg);

            // Tochas
            var torches = new GameObject("Tochas").transform;
            Torch(torches, new Vector3(-4.4f, 2.4f, 0f));
            Torch(torches, new Vector3(4.4f, 2.4f, 0f));
            Torch(torches, new Vector3(-1.5f, 2.4f, 10f), 7f, 1.6f);
            Torch(torches, new Vector3(1.5f, 2.4f, 21f), 7f, 1.6f);
            Torch(torches, new Vector3(-9.4f, 2.4f, 15f), 7f, 1.6f);
            Torch(torches, new Vector3(9.4f, 2.4f, 15f), 7f, 1.6f);
            Torch(torches, new Vector3(-8.4f, 2.6f, 32f));
            Torch(torches, new Vector3(8.4f, 2.6f, 32f));
            Torch(torches, new Vector3(-2f, 2.6f, 44.4f));
            Torch(torches, new Vector3(2f, 2.6f, 44.4f));

            // Saída (volta para a frente da cripta nas Terras Alagadas)
            var exit = Group("Saída", new Vector3(0f, 0f, -4.6f));
            PortalDoor(exit, new Vector3(0f, 1.6f, 0f), new Vector3(2.4f, 3.2f, 0.2f), TerrasAlagadasBuilder.SceneName, "saida_cripta", "Sair da cripta", null);
            KitDresser.Dress(exit, dungeon, 8);
            Spawn(null, "entrada", new Vector3(0f, 0.05f, -2.5f), 0f, true);

            // Inimigos
            var enemies = new GameObject("Inimigos").transform;
            Enemy(EnemyContentFactory.Skeleton, enemies, new Vector3(0f, 0f, 15f), 180f);
            Enemy(EnemyContentFactory.Skeleton, enemies, new Vector3(-6f, 0f, 15f), 90f);
            Enemy(EnemyContentFactory.Skeleton, enemies, new Vector3(-5f, 0f, 12.5f), 70f);
            Enemy(EnemyContentFactory.Archer, enemies, new Vector3(7f, 0f, 17f), 250f);
            Enemy(EnemyContentFactory.Skeleton, enemies, new Vector3(6f, 0f, 13f), 270f);
            Enemy(EnemyContentFactory.Skeleton, enemies, new Vector3(-3f, 0f, 30f), 180f);
            Enemy(EnemyContentFactory.Archer, enemies, new Vector3(3f, 0f, 38f), 180f);
            Enemy(EnemyContentFactory.Knight, enemies, new Vector3(0f, 0f, 39f), 180f, null, null, "cripta_guardiao");

            // Recompensas
            var cryptLoot = LootContentFactory.Table("BauCripta", "Baú da Cripta", t =>
            {
                t.guaranteedPicks = 2;
                t.guaranteedMinimumRarity = ItemRarity.Rare;
                t.minimumRarity = ItemRarity.Uncommon;
                t.rarityBonus = 1.5f;
                t.entries = Entries(Entry("Couraca", 1f), Entry("Elmo", 1f), Entry("Manoplas", 1f), Entry("Grevas", 1f), Entry("Botas", 1f),
                    Entry("Espada", 1f), Entry("Machado", 1f), Entry("Martelo", 1f), Entry("Lanca", 1f), Entry("Escudo", 1f),
                    Entry("Amuleto", 0.8f), Entry("AnelForca", 0.6f), Entry("AnelVitalidade", 0.6f), Entry("AnelResistencia", 0.6f),
                    Entry("PocaoVida", 2f, 2, 3));
                t.rolls = new Vector2Int(1, 2);
                t.gold = new Vector2Int(80, 160);
            });
            var chests = new GameObject("Baús").transform;
            PlaceChest(chests, false, "cripta_bau_oeste", new Vector3(-8.6f, 0f, 15f), 90f);
            PlaceChest(chests, false, "cripta_bau_final", new Vector3(0f, 0f, 44f), 180f, cryptLoot);

            var places = new GameObject("Locais").transform;
            Discoverable(places, "cripta_salao", "Salão do Guardião", false, 50, new Vector3(0f, 2f, 30f), new Vector3(16f, 4f, 6f));

            // Sistemas
            var systems = new GameObject("_Area");
            var area = systems.AddComponent<AreaInfo>();
            AddFog(systems, new Color(0.02f, 0.025f, 0.02f), 4f, 26f);
            var aso = new SerializedObject(area);
            aso.FindProperty("id").stringValue = "cripta_da_capela";
            aso.FindProperty("displayName").stringValue = "Cripta da Capela";
            aso.FindProperty("subtitle").stringValue = "Terras Alagadas";
            aso.ApplyModifiedPropertiesWithoutUndo();

            var surface = geo.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            geo.gameObject.AddComponent<RuntimeNavMeshBaker>();

            AddPersistentObjects(new Vector3(0f, 0.05f, -2.5f), 0f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuild(ScenePath);
            Debug.Log("[Athoria] Cripta criada em " + ScenePath);
        }

        /// <summary>Barris, caixas, velas, lustre, correntes e destroços da masmorra.</summary>
        private static void BuildProps(string dg)
        {
            var props = new GameObject("Adereços").transform;
            GameObject K(string model, float height, bool collider = false) => PsxAssets.Kit(dg + "Models/" + model + ".fbx", height, collider);
            void At(GameObject prefab, float x, float y, float z, float yaw) => PsxAssets.Place(prefab, props, new Vector3(x, y, z), yaw);

            var barrel = K("Barrel", 1f, true);
            var box = K("Box", 0.8f, true);
            var candleA = K("Candle_01", 0.28f);
            var candleB = K("Candle_02", 0.22f);
            var candleC = K("Candle_03", 0.32f);
            var chandelier = K("Chandelier", 1.2f);
            var chain = K("Chain", 2.2f);
            var debris = K("Debris", 0.4f);
            var collapsed = K("Pillar_Collapsed_01", 1.6f, true);
            var collapsed2 = K("Pillar_Collapsed_02", 1.2f, true);

            // Salão de entrada
            At(barrel, -4f, 0f, -4f, 10f);
            At(barrel, -3f, 0f, -4.1f, 70f);
            At(box, 4f, 0f, -3.9f, 15f);
            At(debris, 3.5f, 0f, 3.6f, 40f);
            // Sala oeste (baú) e sala leste
            At(box, -9.2f, 0f, 11.9f, 5f);
            At(box, -9.1f, 0.8f, 12f, 30f);
            At(barrel, -3f, 0f, 18.2f, 0f);
            At(barrel, 9.1f, 0f, 18.2f, 0f);
            At(box, 9.1f, 0f, 11.9f, 50f);
            At(debris, 6f, 0f, 18f, 120f);
            At(candleA, -8f, 0.9f, 13f, 0f);
            At(candleC, -8.4f, 0.9f, 17f, 0f);
            At(candleB, 8.3f, 0.9f, 13.2f, 0f);
            // Correntes nas paredes do corredor
            At(chain, -1.7f, 1.6f, 8f, 90f);
            At(chain, 1.7f, 1.6f, 21f, -90f);
            // Salão final: lustre, velas no altar, pilares caídos
            At(chandelier, 0f, 2.7f, 34f, 0f);
            At(candleA, -1.1f, 1.2f, 42.5f, 0f);
            At(candleB, -0.4f, 1.2f, 42.8f, 0f);
            At(candleC, 0.5f, 1.2f, 42.4f, 0f);
            At(candleA, 1.2f, 1.2f, 42.7f, 0f);
            At(collapsed, -7.6f, 0f, 27f, 30f);
            At(collapsed2, 7.4f, 0f, 43.4f, 200f);
            At(debris, -6.5f, 0f, 41f, 10f);
            At(debris, 6.8f, 0f, 30f, 200f);

            // Brilho das velas do altar
            var glow = new GameObject("Luz das Velas").AddComponent<Light>();
            glow.transform.SetParent(props, false);
            glow.transform.position = new Vector3(0f, 1.8f, 42.3f);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.7f, 0.35f);
            glow.range = 6f;
            glow.intensity = 1.4f;
            glow.gameObject.AddComponent<TorchFlicker>();
        }

        private static void WallX(Transform parent, float z, float x0, float x1, Material m) =>
            Box(parent, "Parede", new Vector3((x0 + x1) * 0.5f, WallH * 0.5f, z), new Vector3(Mathf.Abs(x1 - x0) + Thick, WallH, Thick), m);

        private static void WallZ(Transform parent, float x, float z0, float z1, Material m) =>
            Box(parent, "Parede", new Vector3(x, WallH * 0.5f, (z0 + z1) * 0.5f), new Vector3(Thick, WallH, Mathf.Abs(z1 - z0) + Thick), m);
    }
}
