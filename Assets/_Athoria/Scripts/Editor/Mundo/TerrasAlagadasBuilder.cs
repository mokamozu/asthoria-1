using System.Collections.Generic;
using System.IO;
using System.Linq;
using Athoria.Gameplay.Items;
using Athoria.Gameplay.Loot;
using Athoria.Gameplay.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static Athoria.Editor.WorldBuildKit;
using G = Athoria.Editor.FloodedTerrainGenerator;

namespace Athoria.Editor
{
    /// <summary>
    /// Gera a região Terras Alagadas: 1000 × 1000 m em 64 pedaços de terreno carregados por
    /// distância. Noroeste: a Capital vitoriana em terra alta. Leste: a vila de palafitas na
    /// bacia alagada. Sudoeste: o brejo profundo com a arena do Ancião do Brejo. Valas de corpos
    /// nos arredores, poças negras da peste, canais, estradas elevadas, grama e juncos
    /// instanciados, neblina amarelada. O NavMesh da região inteira é gerado aqui.
    /// Tudo é determinístico (mesma semente = mesmo mapa).
    /// Relevo e pintura: FloodedTerrainGenerator · Capital: FloodedCapital · demais locais: FloodedSites.
    /// </summary>
    public static class TerrasAlagadasBuilder
    {
        public const string SceneName = "TerrasAlagadas";
        public const string ScenePath = "Assets/_Athoria/Scenes/TerrasAlagadas.unity";
        private const string ChunkFolder = "Assets/_Athoria/Scenes/TerrasAlagadas";
        private const string DataFolder = "Assets/_Athoria/Data/Mundo/TerrasAlagadas";
        private const string TexFolder = "Assets/_Athoria/Art/Mundo/Texturas";
        private const string VegFolder = "Assets/_Athoria/Prefabs/Mundo/Vegetacao";

        private const float Half = G.Half;
        private const float ChunkSize = 125f;
        private const int Grid = 8;
        private const int HeightRes = 129;
        private const int AlphaRes = 128;
        public const float WaterLevel = G.WaterLevel;

        /// <summary>Altura do chão (m) em qualquer ponto da região.</summary>
        public static float Height(float x, float z) => G.Height(x, z);

        [MenuItem("Athoria/Cenas/Criar ou recriar Terras Alagadas (mundo aberto)")]
        public static void BuildMenu() => Build(true);

        public static void Build(bool askBeforeOverwrite)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (askBeforeOverwrite && File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Terras Alagadas", "Recriar a região inteira (1000 m: terreno, Capital, vila, brejo, NavMesh)? Pode levar alguns minutos.", "Recriar", "Cancelar"))
                return;

            try
            {
                Directory.CreateDirectory(ChunkFolder);
                Directory.CreateDirectory(DataFolder);
                Directory.CreateDirectory(VegFolder);
                EnsureContent();

                Progress("Texturas procedurais...", 0.02f);
                ProceduralTextures.EnsureAll();
                FloodedMeshKit.Begin();
                var mats = FloodedMaterials.Create();

                Progress("Camadas do chão e vegetação...", 0.05f);
                var layers = G.BuildLayers(DataFolder);
                var trees = BuildVegetation();

                Progress("Cena base...", 0.08f);
                var baseScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(baseScene, ScenePath);
                var streamerChunks = new List<WorldStreamer.Chunk>();
                var chunkScenes = new Scene[Grid, Grid];
                var chunkRoots = new Transform[Grid, Grid];

                for (var cx = 0; cx < Grid; cx++)
                for (var cz = 0; cz < Grid; cz++)
                {
                    Progress($"Terreno {cx * Grid + cz + 1}/{Grid * Grid}...", 0.08f + 0.4f * (cx * Grid + cz) / (Grid * Grid));
                    var name = $"TA_{cx}_{cz}";
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    SceneManager.SetActiveScene(scene);
                    var ox = -Half + cx * ChunkSize;
                    var oz = -Half + cz * ChunkSize;
                    BuildTerrain(name, ox, oz, layers, trees);
                    chunkRoots[cx, cz] = new GameObject("Conteudo").transform;
                    EditorSceneManager.SaveScene(scene, $"{ChunkFolder}/{name}.unity");
                    chunkScenes[cx, cz] = scene;
                    streamerChunks.Add(new WorldStreamer.Chunk { scene = name, area = new Rect(ox, oz, ChunkSize, ChunkSize) });
                }

                SceneManager.SetActiveScene(baseScene);
                Progress("Atmosfera, água e vegetação rasteira...", 0.5f);
                BuildAtmosphere(streamerChunks, mats);

                Progress("Capital...", 0.55f);
                var content = new List<Transform>();
                FloodedCapital.Build(content, mats, KnightLoot());
                Progress("Vila de palafitas...", 0.6f);
                FloodedSites.StiltVillage(content, mats);
                Progress("Brejo profundo e arena...", 0.63f);
                FloodedSites.DeepSwamp(content, mats);
                Progress("Valas e peste...", 0.66f);
                FloodedSites.BodyDisposal(content, mats);
                FloodedSites.PlagueZones(content, mats);
                FloodedSites.Wanderers(content);

                // Cada objeto vai para o pedaço do mapa onde está.
                foreach (var t in content)
                {
                    if (t == null) continue;
                    var cx = Mathf.Clamp(Mathf.FloorToInt((t.position.x + Half) / ChunkSize), 0, Grid - 1);
                    var cz = Mathf.Clamp(Mathf.FloorToInt((t.position.z + Half) / ChunkSize), 0, Grid - 1);
                    if (t.parent != null) t.SetParent(null, true);
                    SceneManager.MoveGameObjectToScene(t.gameObject, chunkScenes[cx, cz]);
                    t.SetParent(chunkRoots[cx, cz], true);
                }

                Progress("NavMesh da região (pode demorar alguns minutos)...", 0.72f);
                BakeNavMesh();
                FloodedMeshKit.End();

                Progress("Salvando...", 0.95f);
                for (var cx = 0; cx < Grid; cx++)
                for (var cz = 0; cz < Grid; cz++)
                    EditorSceneManager.SaveScene(chunkScenes[cx, cz]);
                EditorSceneManager.SaveScene(baseScene, ScenePath);
                for (var cx = 0; cx < Grid; cx++)
                for (var cz = 0; cz < Grid; cz++)
                    EditorSceneManager.CloseScene(chunkScenes[cx, cz], true);

                var paths = new List<string> { ScenePath };
                foreach (var c in streamerChunks) paths.Add($"{ChunkFolder}/{c.scene}.unity");
                AddToBuild(paths.ToArray());
                Debug.Log("[Athoria] Terras Alagadas (1000 m) criada em " + ScenePath);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void Progress(string text, float p) => EditorUtility.DisplayProgressBar("Terras Alagadas", text, p);

        /// <summary>Garante itens, inimigos e baús (quando a região é gerada sozinha).</summary>
        private static void EnsureContent()
        {
            if (Item("ChaveCripta") == null || !File.Exists(EnemyContentFactory.PrefabPath(EnemyContentFactory.Knight)) ||
                !File.Exists(LootContentFactory.ChestPath))
            {
                var items = ItemContentFactory.GenerateAll();
                var loot = LootContentFactory.GenerateAll(items);
                EnemyContentFactory.GenerateAll(CombatContentFactory.GenerateAll(), loot);
            }
        }

        /// <summary>O Cavaleiro Corrompido (agora na praça da Capital) leva a chave da Cripta.</summary>
        private static LootTable KnightLoot() => LootContentFactory.Table("CavaleiroCapela", "Loot do Cavaleiro da Capital", t =>
        {
            t.guaranteed = Entries(Entry("ChaveCripta", 1f), Entry("EssenciaPeste", 1f), Entry("PocaoVida", 1f, 2, 2));
            t.guaranteedPicks = 1;
            t.guaranteedMinimumRarity = ItemRarity.Rare;
            t.minimumRarity = ItemRarity.Uncommon;
            t.rarityBonus = 0.5f;
            t.entries = Entries(Entry("Amuleto", 1f), Entry("AnelForca", 1f), Entry("AnelVitalidade", 1f), Entry("AnelMana", 0.8f));
            t.rolls = new Vector2Int(1, 1);
            t.nothingWeight = 1f;
            t.gold = new Vector2Int(40, 90);
        });

        // ================= Terreno =================

        private static void BuildTerrain(string name, float ox, float oz, TerrainLayer[] layers, GameObject[][] groups)
        {
            var path = $"{DataFolder}/Terreno_{name}.asset";
            AssetDatabase.DeleteAsset(path);
            var td = new TerrainData { heightmapResolution = HeightRes, alphamapResolution = AlphaRes, baseMapResolution = 256 };
            td.size = new Vector3(ChunkSize, G.HeightRange, ChunkSize);

            var heights = new float[HeightRes, HeightRes];
            var step = ChunkSize / (HeightRes - 1);
            for (var zi = 0; zi < HeightRes; zi++)
            for (var xi = 0; xi < HeightRes; xi++)
                heights[zi, xi] = Mathf.Clamp01((G.Height(ox + xi * step, oz + zi * step) - G.BaseY) / G.HeightRange);
            td.SetHeights(0, 0, heights);

            td.terrainLayers = layers;
            var alpha = new float[AlphaRes, AlphaRes, layers.Length];
            var w = new float[layers.Length];
            var astep = ChunkSize / AlphaRes;
            for (var zi = 0; zi < AlphaRes; zi++)
            for (var xi = 0; xi < AlphaRes; xi++)
            {
                G.SplatWeights(ox + (xi + 0.5f) * astep, oz + (zi + 0.5f) * astep, w);
                for (var l = 0; l < layers.Length; l++) alpha[zi, xi, l] = w[l];
            }
            td.SetAlphamaps(0, 0, alpha);

            // Árvores. Grupos: 0 folhosas · 1 mortas · 2 coníferas · 3 beira d'água · 4 chão · 5 brejo (salgueiros e ciprestes com barba-de-velho)
            var protoList = new List<TreePrototype>();
            var groupStart = new int[groups.Length];
            for (var g = 0; g < groups.Length; g++)
            {
                groupStart[g] = protoList.Count;
                foreach (var prefab in groups[g]) protoList.Add(new TreePrototype { prefab = prefab });
            }
            td.treePrototypes = protoList.ToArray();
            var instances = new List<TreeInstance>();
            var rng = new System.Random(name.GetHashCode() ^ 0x5eed);
            for (var gz = 0f; gz < ChunkSize; gz += 4f)
            for (var gx = 0f; gx < ChunkSize; gx += 4f)
            {
                var x = ox + gx + (float)rng.NextDouble() * 4f;
                var z = oz + gz + (float)rng.NextDouble() * 4f;
                if (Mathf.Abs(x) > Half - 6f || Mathf.Abs(z) > Half - 6f) continue;
                if (Blocked(x, z)) continue;
                var h = G.Height(x, z);
                var depth = WaterLevel - h;
                var slope = G.Slope(x, z);
                var roll = (float)rng.NextDouble();
                var swamp = G.SwampMask(x, z);
                var basin = G.BasinMask(x, z);
                var density = G.Fbm(x, z, 2, 0.02f, 7f);

                int group;
                if (swamp > 0.45f)
                {
                    if (depth > 0.9f || roll > 0.3f * swamp) continue;
                    group = roll < 0.13f ? 5 : roll < 0.2f ? 1 : 3;                                    // brejo fechado
                }
                else if (basin > 0.5f)
                {
                    if (roll > 0.02f || depth > 1.3f) continue;
                    group = 1;                                                                        // tocos mortos na água
                }
                else if (depth > -0.6f && depth < 0.6f && roll < 0.22f) group = 3;                    // margem
                else if (depth < -0.3f && slope < 28f && roll < density * 0.2f)
                    group = density > 0.6f ? 0 : roll < 0.05f ? 1 : 2;
                else if (depth < -0.2f && slope < 20f && roll > 0.975f) group = 4;
                else continue;
                if (groups[group].Length == 0) continue;
                var proto = groupStart[group] + rng.Next(groups[group].Length);
                var scale = 0.8f + (float)rng.NextDouble() * 0.5f;
                instances.Add(new TreeInstance
                {
                    position = new Vector3((x - ox) / ChunkSize, 0f, (z - oz) / ChunkSize),
                    prototypeIndex = proto,
                    widthScale = scale,
                    heightScale = scale * (0.9f + (float)rng.NextDouble() * 0.3f),
                    rotation = (float)rng.NextDouble() * Mathf.PI * 2f,
                    color = Color.white,
                    lightmapColor = Color.white
                });
            }
            td.SetTreeInstances(instances.ToArray(), true);
            AssetDatabase.CreateAsset(td, path);

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terreno " + name;
            go.transform.position = new Vector3(ox, G.BaseY, oz);
            go.isStatic = true;
            var terrain = go.GetComponent<Terrain>();
            terrain.allowAutoConnect = true;
            terrain.groupingID = 7;
            terrain.treeDistance = 110f;
            terrain.treeBillboardDistance = 90f;
            terrain.heightmapPixelError = 6f;
            terrain.basemapDistance = 90f;
            terrain.drawInstanced = true;
        }

        /// <summary>Lugares sem árvores: Capital, estradas, valas, arena, poças da peste, vila e chegada.</summary>
        private static bool Blocked(float x, float z)
        {
            if (G.CityMask(x, z, 6f) > 0.01f) return true;
            if (G.RoadDistance(x, z) < 5.5f) return true;
            var p = new Vector2(x, z);
            foreach (var s in G.Disposals) if (Vector2.Distance(p, s.C) < s.R + 3f) return true;
            foreach (var pz in G.PlagueZones) if (Vector2.Distance(p, new Vector2(pz.x, pz.y)) < pz.z * 1.05f) return true;
            if (Vector2.Distance(p, G.Arena) < G.ArenaRadius + 4f) return true;
            if (Vector2.Distance(p, G.Village) < 70f) return true;
            if (Vector2.Distance(p, G.Arrival) < 15f) return true;
            return false;
        }

        // ================= Vegetação (Prefabs para o sistema de árvores do Terrain) =================

        /// <summary>
        /// Vegetação PS1 dos pacotes (Tree Pack, Retro Tree Pack, Forest Model Pack).
        /// Se os modelos ainda não foram importados, cai para as árvores procedurais antigas.
        /// </summary>
        private static GameObject[][] BuildVegetation()
        {
            var old = BuildProceduralVegetation();
            const string Tp = "TreePack/tree_pack_1.1/models/";
            const string Rt = "RetroTreePack/Retro Tree Pack v1.0/Retro Tree Pack/Models/Low Res/other-formats/FBX/";
            const string Fm = "ForestModelPack/Forest Model Pack/Fbx/";
            var swampTint = new Color(0.78f, 0.86f, 0.74f);

            GameObject[] VegGroup(GameObject fallback, params GameObject[] items)
            {
                var list = items.Where(i => i != null).ToArray();
                return list.Length > 0 ? list : fallback != null ? new[] { fallback } : new GameObject[0];
            }
            GameObject TreeOf(string fbx, string name, float height, float radius, Color? tint = null) =>
                PsxAssets.Wrap(fbx, name, "Vegetacao", height, radius, lodGroup: true, tint: tint);

            var broad = VegGroup(old[0],
                TreeOf(Tp + "tree17.fbx", "Arvore_Pantano_A", 7.5f, 0.35f, swampTint),
                TreeOf(Tp + "tree21.fbx", "Arvore_Pantano_B", 7f, 0.35f, swampTint),
                TreeOf(Tp + "tree22.fbx", "Arvore_Pantano_C", 8f, 0.35f, swampTint),
                TreeOf(Rt + "tree_rt_4.fbx", "Arvore_Retro_A", 7f, 0.35f, swampTint));
            var dead = VegGroup(old[1],
                TreeOf(Rt + "dead_tree_rt_1.fbx", "Arvore_Morta_A", 6.5f, 0.3f),
                TreeOf(Rt + "dead_tree_rt_2.fbx", "Arvore_Morta_B", 6f, 0.3f));
            var conifer = VegGroup(old[2],
                TreeOf(Rt + "tree_rt_1.fbx", "Conifera_A", 9f, 0.35f, swampTint),
                TreeOf(Rt + "tree_rt_3.fbx", "Conifera_B", 10f, 0.35f, swampTint),
                TreeOf(Fm + "Spurce_1/Spruce_1.fbx", "Conifera_C", 9.5f, 0.35f, swampTint),
                TreeOf(Tp + "tree05.fbx", "Conifera_D", 8.5f, 0.35f, swampTint));
            var shore = VegGroup(old[3], old[3],
                TreeOf(Tp + "bush02.fbx", "Arbusto_A", 1.5f, 0f, swampTint),
                TreeOf(Tp + "bush06.fbx", "Arbusto_B", 1.3f, 0f, swampTint),
                TreeOf(Tp + "bush08.fbx", "Arbusto_C", 1.4f, 0f),
                TreeOf(Fm + "Fern/Fern.fbx", "Samambaia", 0.9f, 0f));
            var ground = VegGroup(null,
                TreeOf(Fm + "Log_1/Log_1.fbx", "Tronco_Caido", 0.7f, 0f),
                TreeOf(Fm + "Stump_1/Stump_1.fbx", "Toco", 0.8f, 0.35f),
                TreeOf(Fm + "StumpWithMushroom/StumpWithMushroom.fbx", "Toco_Cogumelos", 0.9f, 0.35f),
                TreeOf(Fm + "Mushroom_1/Mushroom.fbx", "Cogumelo", 0.35f, 0f));
            // brejo: salgueiros e ciprestes com barba-de-velho pendurada + árvores mortas retorcidas
            var mossy = new[] { old[0], old[2] }.Concat(dead).Where(t => t != null).ToArray();
            return new[] { broad, dead, conifer, shore, ground, mossy };
        }

        private static GameObject[] BuildProceduralVegetation()
        {
            var bark = Mat("CascaEscura", new Color(0.16f, 0.12f, 0.09f));
            var deadBark = Mat("CascaMorta", new Color(0.24f, 0.22f, 0.2f));
            var leaves = Mat("FolhagemPantano", new Color(0.12f, 0.19f, 0.08f));
            var drape = Mat("BarbaDeVelho", new Color(0.2f, 0.25f, 0.14f));
            var reeds = Mat("Junco", new Color(0.36f, 0.36f, 0.18f));

            GameObject MakeTree(string name, System.Action<Transform> build, float colliderRadius, float colliderHeight)
            {
                var root = new GameObject(name);
                try
                {
                    build(root.transform);
                    if (colliderRadius > 0f)
                    {
                        var col = root.AddComponent<CapsuleCollider>();
                        col.radius = colliderRadius;
                        col.height = colliderHeight;
                        col.center = new Vector3(0f, colliderHeight * 0.5f, 0f);
                    }
                    // O sistema de árvores do Terrain precisa de um LODGroup quando o modelo tem várias peças.
                    var lod = root.AddComponent<LODGroup>();
                    lod.SetLODs(new[] { new LOD(0.015f, root.GetComponentsInChildren<Renderer>()) });
                    lod.RecalculateBounds();
                    return PrefabUtility.SaveAsPrefabAsset(root, $"{VegFolder}/{name}.prefab");
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }

            void P2(Transform t, string n, PrimitiveType type, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default)
            {
                var go = Part(t, n, type, pos, scale, m, euler, false);
                go.isStatic = false;
                go.GetComponent<Renderer>().shadowCastingMode = scale.x < 0.1f ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }

            var willow = MakeTree("Salgueiro", t =>
            {
                P2(t, "Tronco", PrimitiveType.Cylinder, new Vector3(0, 2.2f, 0), new Vector3(0.38f, 2.2f, 0.38f), bark, new Vector3(0, 0, 4));
                for (var i = 0; i < 4; i++)
                {
                    var a = i * 90f * Mathf.Deg2Rad;
                    P2(t, "Copa", PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 1.2f, 4.7f + (i % 2) * 0.4f, Mathf.Sin(a) * 1.2f), new Vector3(3f, 1.7f, 3f), leaves);
                }
                for (var i = 0; i < 12; i++)
                {
                    var a = i * 30f * Mathf.Deg2Rad;
                    P2(t, "Barba", PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 2.2f, 3.4f, Mathf.Sin(a) * 2.2f), new Vector3(0.07f, 1.9f + (i % 3) * 0.4f, 0.07f), drape);
                }
            }, 0.4f, 4.5f);

            var dead = MakeTree("ArvoreMorta", t =>
            {
                P2(t, "Tronco", PrimitiveType.Cylinder, new Vector3(0, 2.4f, 0), new Vector3(0.32f, 2.4f, 0.32f), deadBark);
                P2(t, "Galho A", PrimitiveType.Cube, new Vector3(0.5f, 4f, 0), new Vector3(0.12f, 1.6f, 0.12f), deadBark, new Vector3(0, 0, -40));
                P2(t, "Galho B", PrimitiveType.Cube, new Vector3(-0.4f, 4.4f, 0.2f), new Vector3(0.1f, 1.3f, 0.1f), deadBark, new Vector3(20, 0, 45));
                P2(t, "Galho C", PrimitiveType.Cube, new Vector3(0, 3.4f, -0.5f), new Vector3(0.1f, 1.2f, 0.1f), deadBark, new Vector3(-50, 0, 0));
            }, 0.32f, 4.5f);

            var cypress = MakeTree("Cipreste", t =>
            {
                P2(t, "Base", PrimitiveType.Cylinder, new Vector3(0, 0.5f, 0), new Vector3(0.75f, 0.5f, 0.75f), bark);
                P2(t, "Tronco", PrimitiveType.Cylinder, new Vector3(0, 3.5f, 0), new Vector3(0.4f, 3f, 0.4f), bark);
                P2(t, "Copa Baixa", PrimitiveType.Sphere, new Vector3(0, 5.6f, 0), new Vector3(3.2f, 1.1f, 3.2f), leaves);
                P2(t, "Copa Alta", PrimitiveType.Sphere, new Vector3(0.3f, 6.8f, 0.2f), new Vector3(2.4f, 0.9f, 2.4f), leaves);
                for (var i = 0; i < 6; i++)
                {
                    var a = i * 60f * Mathf.Deg2Rad;
                    P2(t, "Barba", PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 1.3f, 4.6f, Mathf.Sin(a) * 1.3f), new Vector3(0.06f, 1.4f, 0.06f), drape);
                }
            }, 0.45f, 6f);

            var reed = MakeTree("Juncos", t =>
            {
                var r = new System.Random(4);
                for (var i = 0; i < 9; i++)
                {
                    var h = 0.5f + (float)r.NextDouble() * 0.5f;
                    P2(t, "Junco", PrimitiveType.Cylinder, new Vector3((float)r.NextDouble() * 0.6f - 0.3f, h, (float)r.NextDouble() * 0.6f - 0.3f),
                        new Vector3(0.035f, h, 0.035f), reeds, new Vector3((float)r.NextDouble() * 12f - 6f, 0, (float)r.NextDouble() * 12f - 6f));
                }
            }, 0f, 0f);

            return new[] { willow, dead, cypress, reed };
        }

        // ================= Atmosfera e sistemas da cena base =================

        private static void BuildAtmosphere(List<WorldStreamer.Chunk> chunks, FloodedMaterials mats)
        {
            // Céu encoberto de fim de tarde: luz difusa e fraca, sombras suaves.
            var sun = new GameObject("Luz Encoberta").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.82f, 0.82f, 0.72f);
            sun.intensity = 0.85f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            var skyPath = DataFolder + "/CeuEncoberto.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null) { sky = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(sky, skyPath); }
            sky.SetFloat("_SunSize", 0f);
            sky.SetFloat("_AtmosphereThickness", 2.2f);
            sky.SetColor("_SkyTint", new Color(0.55f, 0.56f, 0.46f));
            sky.SetColor("_GroundColor", new Color(0.3f, 0.31f, 0.25f));
            sky.SetFloat("_Exposure", 0.7f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.3f;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.47f, 0.49f, 0.41f);
            RenderSettings.ambientEquatorColor = new Color(0.32f, 0.33f, 0.27f);
            RenderSettings.ambientGroundColor = new Color(0.13f, 0.13f, 0.1f);

            // Neblina amarelada e doentia (#7D8268), exponencial ao quadrado.
            var fogColor = new Color(0x7D / 255f, 0x82 / 255f, 0x68 / 255f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = 0.018f;

            // Água do pântano: uma grade só para a região inteira.
            var water = new GameObject("Agua do Pantano");
            water.transform.position = new Vector3(0f, WaterLevel, 0f);
            water.AddComponent<MeshFilter>().sharedMesh = FloodedMeshKit.Grid(Half * 2f, 100);
            var wr = water.AddComponent<MeshRenderer>();
            wr.sharedMaterial = mats.SwampWater;
            wr.shadowCastingMode = ShadowCastingMode.Off;

            var systems = new GameObject("_Regiao");
            var area = systems.AddComponent<AreaInfo>();
            AddFog(systems, fogColor, 0f, 130f);
            var fso = new SerializedObject(systems.GetComponent<AreaFog>());
            fso.FindProperty("exponentialSquared").boolValue = true;
            fso.FindProperty("density").floatValue = 0.018f;
            fso.ApplyModifiedPropertiesWithoutUndo();
            RenderSettings.fogMode = FogMode.ExponentialSquared;   // AddFog deixa linear; a região usa exp²
            RenderSettings.fogDensity = 0.018f;
            var aso = new SerializedObject(area);
            aso.FindProperty("id").stringValue = "terras_alagadas";
            aso.FindProperty("displayName").stringValue = "Terras Alagadas";
            aso.FindProperty("subtitle").stringValue = "Pântanos da peste";
            aso.ApplyModifiedPropertiesWithoutUndo();
            var swamp = systems.AddComponent<SwampWater>();
            var sso = new SerializedObject(swamp);
            sso.FindProperty("waterLevel").floatValue = WaterLevel;
            sso.ApplyModifiedPropertiesWithoutUndo();
            var streamer = systems.AddComponent<WorldStreamer>();
            var stso = new SerializedObject(streamer);
            var arr = stso.FindProperty("chunks");
            arr.arraySize = chunks.Count;
            for (var i = 0; i < chunks.Count; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("scene").stringValue = chunks[i].scene;
                e.FindPropertyRelative("area").rectValue = chunks[i].area;
            }
            // Neblina total em ~130 m: carrega só os pedaços por perto.
            stso.FindProperty("loadDistance").floatValue = 150f;
            stso.FindProperty("unloadDistance").floatValue = 210f;
            stso.ApplyModifiedPropertiesWithoutUndo();

            BuildFoliage(systems);

            var spawns = new GameObject("Pontos de Chegada").transform;
            var arrival = G.P(G.Arrival.x, G.Arrival.y, 0.1f);
            Spawn(spawns, "inicio", arrival, 245f, true);
            Spawn(spawns, "saida_cripta", FloodedCapital.CryptExit(), 180f, false);
            AddPersistentObjects(arrival, 245f);
        }

        /// <summary>Grama, ervas secas, mato entre as pedras e juncos (instanciados, com vento).</summary>
        private static void BuildFoliage(GameObject host)
        {
            var grass = ProceduralTextures.Foliage("TA_FolhaGrama", "FolhaGrama", new Color(0.95f, 1f, 0.88f), new Color(0.9f, 0.82f, 0.55f), 0.12f);
            var weeds = ProceduralTextures.Foliage("TA_FolhaErva", "FolhaErvaSeca", Color.white, new Color(0.8f, 0.72f, 0.5f), 0.16f);
            var reeds = ProceduralTextures.Foliage("TA_FolhaJunco", "FolhaJunco", Color.white, new Color(0.85f, 0.78f, 0.55f), 0.22f);
            FoliageInstancer.Layer L(string name, FoliageInstancer.Shape shape, Material mat, float density, Vector2 height, float width,
                int[] layers, float minWeight, Vector2 depth, float clump, float distance) => new()
            {
                name = name, shape = shape, material = mat, density = density, height = height, widthRatio = width,
                terrainLayers = layers, minWeight = minWeight, depth = depth, clumping = clump, drawDistance = distance, maxSlope = 34f
            };
            var foliage = host.AddComponent<FoliageInstancer>();
            foliage.EditorSetup(new[]
            {
                L("Grama Baixa", FoliageInstancer.Shape.Tuft, grass, 2.2f, new Vector2(0.25f, 0.55f), 1.3f, new[] { G.LayerGrass }, 0.4f, new Vector2(-99f, -0.08f), 0.25f, 40f),
                L("Erva Seca", FoliageInstancer.Shape.Tuft, weeds, 0.35f, new Vector2(0.4f, 0.9f), 1f, new[] { G.LayerGrass, G.LayerMud, G.LayerRoad }, 0.3f, new Vector2(-99f, -0.05f), 0.5f, 50f),
                L("Mato nas Pedras", FoliageInstancer.Shape.Tuft, weeds, 0.12f, new Vector2(0.12f, 0.3f), 1.2f, new[] { G.LayerCobble }, 0.5f, new Vector2(-99f, -0.05f), 0.6f, 35f),
                L("Juncos", FoliageInstancer.Shape.Reeds, reeds, 0.5f, new Vector2(1.3f, 2.4f), 0.6f, new[] { G.LayerSwampBed, G.LayerMud }, 0.3f, new Vector2(-0.4f, 1.0f), 0.45f, 75f),
            },
            new[]
            {
                new Vector4(G.Swamp.x, G.Swamp.y, 220f, 3.5f),
                new Vector4(G.Arena.x, G.Arena.y, 75f, 4f),   // juncos fechados em volta da arena escondem a clareira
            },
            G.PlagueZones.Select(z => new Vector4(z.x, z.y, z.z * 0.9f, 0f))
                .Append(new Vector4(G.Arena.x, G.Arena.y, G.ArenaRadius + 1f, 0f))
                .ToArray(),
            16f);
        }

        private static void BakeNavMesh()
        {
            var go = new GameObject("NavMesh da Regiao");
            var surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.3f;
            surface.overrideTileSize = true;
            surface.tileSize = 128;
            surface.BuildNavMesh();

            const string path = DataFolder + "/NavMesh_TerrasAlagadas.asset";
            AssetDatabase.DeleteAsset(path);
            if (surface.navMeshData != null) AssetDatabase.CreateAsset(surface.navMeshData, path);
            else Debug.LogWarning("[Athoria] NavMesh da região não foi gerado.");
        }
    }
}
