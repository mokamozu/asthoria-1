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

namespace Athoria.Editor
{
    /// <summary>
    /// Gera a região Terras Alagadas (Etapa 13): 400×400 m de pântano em 16
    /// pedaços de terreno carregados por distância, estradas, vegetação e os
    /// locais — vila, torre de vigia, covil, capela, acampamento, entrada da
    /// cripta e uma clareira secreta. O NavMesh da região inteira é gerado
    /// aqui e salvo como asset. Tudo é determinístico (mesma semente = mesmo mapa).
    /// </summary>
    public static class TerrasAlagadasBuilder
    {
        public const string SceneName = "TerrasAlagadas";
        public const string ScenePath = "Assets/_Athoria/Scenes/TerrasAlagadas.unity";
        private const string ChunkFolder = "Assets/_Athoria/Scenes/TerrasAlagadas";
        private const string DataFolder = "Assets/_Athoria/Data/Mundo/TerrasAlagadas";
        private const string TexFolder = "Assets/_Athoria/Art/Mundo/Texturas";
        private const string VegFolder = "Assets/_Athoria/Prefabs/Mundo/Vegetacao";

        private const float Half = 200f;
        private const float ChunkSize = 100f;
        private const int Grid = 4;
        private const int HeightRes = 129;
        private const int AlphaRes = 128;
        private const float BaseY = -6f;
        private const float HeightRange = 60f;
        public const float WaterLevel = 0.5f;

        private struct Site
        {
            public Vector2 C;
            public float R, H;
            public Site(float x, float z, float r, float h) { C = new Vector2(x, z); R = r; H = h; }
        }

        // Locais (centro, raio plano, altura do chão).
        private static readonly Site Arrival = new(-150f, -150f, 8f, 1.4f);
        private static readonly Site Village = new(-105f, -95f, 28f, 2f);
        private static readonly Site Tower = new(-25f, -150f, 9f, 1.6f);
        private static readonly Site Den = new(60f, -60f, 13f, 1.5f);
        private static readonly Site Chapel = new(15f, 55f, 20f, 1.8f);
        private static readonly Site Camp = new(125f, -15f, 17f, 2.2f);
        private static readonly Site CryptDoor = new(125f, 135f, 10f, 2.2f);
        private static readonly Site Glade = new(-158f, 118f, 9f, 1.4f);
        private static readonly Site[] Sites = { Arrival, Village, Tower, Den, Chapel, Camp, CryptDoor, Glade };

        // Morros somados ao relevo (centro, raio, altura).
        private static readonly Vector4[] Mounds =
        {
            new(125f, 162f, 30f, 15f),   // morro da cripta
            new(-158f, 118f, 22f, 6f),   // borda da clareira
            new(-60f, 150f, 35f, 10f),
            new(170f, 60f, 40f, 12f),
        };

        // Estradas (polilinhas).
        private static readonly Vector2[][] Roads =
        {
            new Vector2[] { new(-150f, -150f), new(-105f, -95f), new(-60f, -40f), new(-20f, 10f), new(15f, 55f), new(70f, 100f), new(125f, 135f) },
            new Vector2[] { new(-105f, -95f), new(-60f, -135f), new(-25f, -150f) },
            new Vector2[] { new(15f, 55f), new(70f, 20f), new(125f, -15f) },
        };

        [MenuItem("Athoria/Cenas/Criar ou recriar Terras Alagadas (mundo aberto)")]
        public static void BuildMenu() => Build(true);

        public static void Build(bool askBeforeOverwrite)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (askBeforeOverwrite && File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Terras Alagadas", "Recriar a região inteira (terreno, locais e NavMesh)?", "Recriar", "Cancelar"))
                return;

            try
            {
                Directory.CreateDirectory(ChunkFolder);
                Directory.CreateDirectory(DataFolder);
                Directory.CreateDirectory(VegFolder);
                EnsureContent();

                Progress("Texturas e vegetação...", 0.05f);
                var layers = BuildTerrainLayers();
                var trees = BuildVegetation();

                Progress("Cena base...", 0.1f);
                var baseScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                // Precisa ter arquivo antes de abrir os pedaços juntos (o Unity não aceita cena sem nome com outras abertas).
                EditorSceneManager.SaveScene(baseScene, ScenePath);
                var streamerChunks = new List<WorldStreamer.Chunk>();
                var chunkScenes = new Scene[Grid, Grid];
                var chunkRoots = new Transform[Grid, Grid];

                for (var cx = 0; cx < Grid; cx++)
                for (var cz = 0; cz < Grid; cz++)
                {
                    Progress($"Terreno {cx * Grid + cz + 1}/16...", 0.1f + 0.4f * (cx * Grid + cz) / 16f);
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
                Progress("Atmosfera...", 0.55f);
                BuildAtmosphere(streamerChunks);

                Progress("Locais...", 0.6f);
                var content = new List<Transform>();
                BuildSites(content);

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

                Progress("NavMesh da região (pode demorar)...", 0.75f);
                BakeNavMesh();

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
                Debug.Log("[Athoria] Terras Alagadas criada em " + ScenePath);
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

        // ================= Relevo =================

        private static float Fbm(float x, float z, int octaves, float freq, float seed)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (var i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x * freq + seed, z * freq + seed * 1.37f) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return sum / norm;
        }

        /// <summary>Altura do chão (m) em qualquer ponto da região.</summary>
        public static float Height(float x, float z)
        {
            var h = (Fbm(x, z, 4, 0.011f, 13.1f) - 0.47f) * 6f + 0.7f;
            h += Mathf.Max(0f, Fbm(x, z, 3, 0.0065f, 57.3f) - 0.56f) * 55f;

            foreach (var m in Mounds)
            {
                var d = Vector2.Distance(new Vector2(x, z), new Vector2(m.x, m.y));
                if (d >= m.z) continue;
                var t = 1f - d / m.z;
                h += m.w * t * t * (3f - 2f * t) * (0.75f + 0.5f * Fbm(x, z, 2, 0.04f, 71f));
            }

            var edge = Mathf.InverseLerp(165f, 198f, Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)));
            h += edge * edge * 32f + edge * Fbm(x, z, 2, 0.05f, 91f) * 10f;

            var rd = RoadDistance(x, z);
            if (rd < 7f)
            {
                var k = 1f - Mathf.SmoothStep(0f, 1f, (rd - 3f) / 4f);
                h = Mathf.Lerp(h, Mathf.Clamp(h, 1.05f, 6f), k);
            }

            foreach (var s in Sites)
            {
                var d = Vector2.Distance(new Vector2(x, z), s.C);
                var f = d <= s.R ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (d - s.R) / (s.R * 0.8f));
                if (f > 0f) h = Mathf.Lerp(h, s.H, f);
            }
            return h;
        }

        private static float G(float x, float z) => Height(x, z);
        private static Vector3 P(float x, float z, float up = 0f) => new(x, Height(x, z) + up, z);

        private static float RoadDistance(float x, float z)
        {
            var p = new Vector2(x, z);
            var best = float.MaxValue;
            foreach (var road in Roads)
                for (var i = 0; i < road.Length - 1; i++)
                {
                    var a = road[i];
                    var b = road[i + 1];
                    var ab = b - a;
                    var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                    best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
                }
            return best;
        }

        private static float SiteDistance(float x, float z, out Site nearest)
        {
            var best = float.MaxValue;
            nearest = Sites[0];
            foreach (var s in Sites)
            {
                var d = Vector2.Distance(new Vector2(x, z), s.C) - s.R;
                if (d < best) { best = d; nearest = s; }
            }
            return best;
        }

        private static float Slope(float x, float z)
        {
            var dx = Height(x + 1f, z) - Height(x - 1f, z);
            var dz = Height(x, z + 1f) - Height(x, z - 1f);
            return Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz) * 0.5f) * Mathf.Rad2Deg;
        }

        // ================= Terreno =================

        /// <summary>Texturas PS1 (pacotes SBS e Stark Crafts) para cada camada; null = usa a procedural antiga.</summary>
        private static Texture2D PsxTerrainTexture(string layer) => layer switch
        {
            "Lama" => PsxAssets.Texture("SBS_Tiny128/128x128/Grass/Grass_12-128x128.png"),
            "GramaPantano" => PsxAssets.Texture("StarkCrafts_Forest/PSX_Forest_Level_byStarkCrafts/PSX_ForestGround_Tex/PSX_Seamless_WildForestGrass_128px.png"),
            "Musgo" => PsxAssets.Texture("SBS_Tiny128/128x128/Grass/Grass_19-128x128.png"),
            "Rocha" => PsxAssets.Texture("SBS_Tiny3_Small/128x128/Stone/Stone_06-128x128.png"),
            "Terra" => PsxAssets.Texture("StarkCrafts_Forest/PSX_Forest_Level_byStarkCrafts/PSX_ForestGround_Tex/PSX_Seamless_ForestEarthGround_128px.png"),
            _ => null
        };

        private static TerrainLayer[] BuildTerrainLayers()
        {
            AssetDatabase.Refresh();
            TerrainLayer Layer(string tex, float tile)
            {
                var path = $"{DataFolder}/Camada_{tex}.terrainlayer";
                var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (layer == null)
                {
                    layer = new TerrainLayer();
                    AssetDatabase.CreateAsset(layer, path);
                }
                var psx = PsxTerrainTexture(tex);
                layer.diffuseTexture = psx != null ? psx : AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexFolder}/Terreno_{tex}.png");
                layer.tileSize = psx != null ? new Vector2(3.5f, 3.5f) : new Vector2(tile, tile);
                layer.smoothness = tex == "Lama" ? 0.3f : 0f;
                layer.metallic = 0f;
                EditorUtility.SetDirty(layer);
                return layer;
            }
            // 0 lama · 1 grama · 2 musgo · 3 rocha · 4 terra (estrada)
            return new[] { Layer("Lama", 5f), Layer("GramaPantano", 6f), Layer("Musgo", 7f), Layer("Rocha", 8f), Layer("Terra", 4f) };
        }

        private static void BuildTerrain(string name, float ox, float oz, TerrainLayer[] layers, GameObject[][] groups)
        {
            var path = $"{DataFolder}/Terreno_{name}.asset";
            AssetDatabase.DeleteAsset(path);

            var td = new TerrainData
            {
                heightmapResolution = HeightRes,
                alphamapResolution = AlphaRes,
                baseMapResolution = 256
            };
            td.size = new Vector3(ChunkSize, HeightRange, ChunkSize);

            var heights = new float[HeightRes, HeightRes];
            var step = ChunkSize / (HeightRes - 1);
            for (var zi = 0; zi < HeightRes; zi++)
            for (var xi = 0; xi < HeightRes; xi++)
                heights[zi, xi] = Mathf.Clamp01((Height(ox + xi * step, oz + zi * step) - BaseY) / HeightRange);
            td.SetHeights(0, 0, heights);

            td.terrainLayers = layers;
            var alpha = new float[AlphaRes, AlphaRes, layers.Length];
            var astep = ChunkSize / AlphaRes;
            for (var zi = 0; zi < AlphaRes; zi++)
            for (var xi = 0; xi < AlphaRes; xi++)
            {
                var x = ox + (xi + 0.5f) * astep;
                var z = oz + (zi + 0.5f) * astep;
                var h = Height(x, z);
                var slope = Slope(x, z);
                var rd = RoadDistance(x, z);
                var w = new float[5];
                w[0] = Mathf.Clamp01(1f - (h - (WaterLevel - 0.3f)) / 0.9f);             // lama perto/abaixo da água
                var dry = 1f - w[0];
                var mossMix = Fbm(x, z, 2, 0.06f, 33f);
                w[1] = dry * (1f - mossMix);
                w[2] = dry * mossMix;
                var rock = Mathf.SmoothStep(0f, 1f, (slope - 26f) / 14f) + Mathf.Clamp01((h - 10f) / 6f);
                w[3] = Mathf.Clamp01(rock) * 2f;
                w[4] = rd < 3.2f ? 3f : rd < 4.5f ? (4.5f - rd) * 2f : 0f;
                var sum = w[0] + w[1] + w[2] + w[3] + w[4];
                for (var l = 0; l < 5; l++) alpha[zi, xi, l] = sum > 0f ? w[l] / sum : (l == 1 ? 1f : 0f);
            }
            td.SetAlphamaps(0, 0, alpha);

            // Árvores
            // Grupos: 0 folhosas · 1 mortas · 2 coníferas · 3 beira d'água · 4 chão (troncos, tocos, cogumelos)
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
                if (Mathf.Abs(x) > 192f || Mathf.Abs(z) > 192f) continue;
                var h = Height(x, z);
                if (RoadDistance(x, z) < 5f || SiteDistance(x, z, out _) < 4f) continue;
                var slope = Slope(x, z);
                var density = Fbm(x, z, 2, 0.02f, 7f);
                var roll = (float)rng.NextDouble();

                int group;
                if (h > WaterLevel - 0.6f && h < WaterLevel + 0.6f && roll < 0.35f) group = 3;               // juncos e arbustos na beira d'água
                else if (h > WaterLevel - 0.3f && h < 9f && slope < 28f && roll < density * 0.32f)
                    group = density > 0.6f ? 0 : roll < 0.08f ? 1 : 2;                                            // folhosa / morta / conífera
                else if (h > WaterLevel && slope < 20f && roll > 0.97f) group = 4;                                // troncos caídos, tocos, cogumelos
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
            go.transform.position = new Vector3(ox, BaseY, oz);
            go.isStatic = true;
            var terrain = go.GetComponent<Terrain>();
            terrain.allowAutoConnect = true;
            terrain.groupingID = 7;
            terrain.treeDistance = 65f;            // a neblina esconde o resto
            terrain.treeBillboardDistance = 65f;
            terrain.heightmapPixelError = 8f;
            terrain.basemapDistance = 50f;
            terrain.drawInstanced = true;
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
            return new[] { broad, dead, conifer, shore, ground };
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

        private static void BuildAtmosphere(List<WorldStreamer.Chunk> chunks)
        {
            var sun = new GameObject("Luz da Lua").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.72f, 0.78f, 0.7f);
            sun.intensity = 0.75f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(38f, -50f, 0f);

            var skyPath = DataFolder + "/CeuPantano.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.SetFloat("_SunSize", 0.02f);
            sky.SetFloat("_AtmosphereThickness", 1.4f);
            sky.SetColor("_SkyTint", new Color(0.35f, 0.42f, 0.36f));
            sky.SetColor("_GroundColor", new Color(0.12f, 0.14f, 0.12f));
            sky.SetFloat("_Exposure", 0.55f);
            // Céu noturno PS1 (HDRI do pacote); sem ele, fica o céu procedural acima.
            var nightHdr = PsxAssets.Texture("Sky_Night/PSX_Nightsky_HDRI/PSX_NIGHTSKY.hdr");
            if (nightHdr != null)
            {
                var psxSkyPath = DataFolder + "/CeuNoturnoPSX.mat";
                var psxSky = AssetDatabase.LoadAssetAtPath<Material>(psxSkyPath);
                if (psxSky == null)
                {
                    psxSky = new Material(Shader.Find("Skybox/Panoramic"));
                    AssetDatabase.CreateAsset(psxSky, psxSkyPath);
                }
                psxSky.SetTexture("_MainTex", nightHdr);
                psxSky.SetColor("_Tint", new Color(0.55f, 0.62f, 0.56f));
                psxSky.SetFloat("_Exposure", 0.8f);
                psxSky.SetFloat("_Rotation", 40f);
                EditorUtility.SetDirty(psxSky);
                sky = psxSky;
            }
            RenderSettings.skybox = sky;
            // O céu HDRI é claro: reflexo do ambiente mais fraco, senão a água do pântano fica branca.
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.25f;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.22f, 0.26f, 0.24f);
            RenderSettings.ambientEquatorColor = new Color(0.13f, 0.15f, 0.12f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.04f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.2f, 0.24f, 0.21f);
            RenderSettings.fogDensity = 0.016f;

            // Água do pântano: um plano só para a região inteira.
            var waterMat = AssetDatabase.LoadAssetAtPath<Material>(DataFolder + "/AguaPantano.mat");
            if (waterMat == null)
            {
                waterMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(waterMat, DataFolder + "/AguaPantano.mat");
            }
            waterMat.SetColor("_BaseColor", new Color(0.09f, 0.13f, 0.08f, 0.82f));
            waterMat.SetFloat("_Smoothness", 0.75f);
            waterMat.SetFloat("_Metallic", 0.1f);
            waterMat.SetFloat("_Surface", 1f);
            waterMat.SetFloat("_Blend", 0f);
            waterMat.SetOverrideTag("RenderType", "Transparent");
            waterMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            waterMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            waterMat.SetInt("_ZWrite", 0);
            waterMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            waterMat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(waterMat);

            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Agua do Pantano";
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.transform.position = new Vector3(0f, WaterLevel, 0f);
            water.transform.localScale = new Vector3(42f, 1f, 42f);
            var wr = water.GetComponent<Renderer>();
            wr.sharedMaterial = waterMat;
            wr.shadowCastingMode = ShadowCastingMode.Off;

            var systems = new GameObject("_Regiao");
            var area = systems.AddComponent<AreaInfo>();
            // Neblina do pântano (PS1/N64): esconde o horizonte e corta o desenho em ~60 m.
            AddFog(systems, new Color(0.1f, 0.13f, 0.12f), 12f, 58f);
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
            // Com a neblina em ~60 m, carrega só os pedaços próximos.
            stso.FindProperty("loadDistance").floatValue = 95f;
            stso.FindProperty("unloadDistance").floatValue = 135f;
            stso.ApplyModifiedPropertiesWithoutUndo();

            // Pontos de chegada ficam na cena base (sempre carregada).
            var spawns = new GameObject("Pontos de Chegada").transform;
            Spawn(spawns, "inicio", P(Arrival.C.x, Arrival.C.y, 0.1f), 40f, true);
            Spawn(spawns, "saida_cripta", P(CryptDoor.C.x, CryptDoor.C.y - 5f, 0.1f), 180f, false);

            AddPersistentObjects(P(Arrival.C.x, Arrival.C.y, 0.1f), 40f);
        }

        private static void BakeNavMesh()
        {
            var go = new GameObject("NavMesh da Regiao");
            var surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.25f;
            surface.overrideTileSize = true;
            surface.tileSize = 128;
            surface.BuildNavMesh();

            const string path = DataFolder + "/NavMesh_TerrasAlagadas.asset";
            AssetDatabase.DeleteAsset(path);
            if (surface.navMeshData != null) AssetDatabase.CreateAsset(surface.navMeshData, path);
            else Debug.LogWarning("[Athoria] NavMesh da região não foi gerado.");
        }

        // ================= Locais =================

        private static void BuildSites(List<Transform> content)
        {
            // Materiais com as texturas PS1 dos pacotes (sem elas, ficam as cores lisas antigas).
            const string Mm = "ModularMedieval/";
            const string Dg = "Dungeon/PSX_Dungeon/";
            var stone = PsxAssets.Textured(Dg + "Textures/TEX_Wall_03.png", "PedraPantano", new Color(0.8f, 0.82f, 0.78f), Mat("PedraPantano", new Color(0.27f, 0.28f, 0.25f)));
            var mossStone = PsxAssets.Textured(Dg + "Textures/TEX_Wall_03.png", "PedraMusgo", new Color(0.62f, 0.74f, 0.55f), Mat("PedraMusgo", new Color(0.2f, 0.25f, 0.17f)));
            var wood = PsxAssets.Textured(Mm + "Textures/wood_planks.jpg", "MadeiraPantano", new Color(0.8f, 0.75f, 0.7f), Mat("MadeiraPantano", new Color(0.26f, 0.18f, 0.11f)));
            var darkWood = PsxAssets.Textured(Mm + "Textures/generic_wood.png", "MadeiraEscura", new Color(0.55f, 0.48f, 0.42f), Mat("MadeiraEscura", new Color(0.2f, 0.13f, 0.07f)));
            var thatch = PsxAssets.Textured(Mm + "Textures/hay_dried.png", "PalhaEscura", new Color(0.7f, 0.66f, 0.55f), Mat("Palha Escura", new Color(0.33f, 0.28f, 0.16f)));

            // Kits modulares: vila medieval (Modular Medieval) e pedra antiga (PSX Dungeon).
            var medieval = KitStyle.Of(
                PsxAssets.Kit(Mm + "Building blocks/wooden_floor_2x2.fbx"),
                PsxAssets.Kit(Mm + "Building blocks/wood_support_beam.fbx"),
                PsxAssets.Kit(Mm + "Decorations/door_wood.fbx"),
                PsxAssets.Kit(Mm + "Building blocks/plaster_wall_stone_base.fbx"),
                PsxAssets.Kit(Mm + "Building blocks/plaster_wall_stone_base_alt.fbx"));
            var ancient = KitStyle.Of(
                PsxAssets.Kit(Dg + "Models/Floor_Tiles.fbx"),
                PsxAssets.Kit(Dg + "Models/Pillar.fbx"),
                PsxAssets.Kit(Dg + "Models/Door_01.fbx"),
                PsxAssets.Kit(Dg + "Models/Wall_01.fbx"),
                PsxAssets.Kit(Dg + "Models/Wall_02.fbx"),
                PsxAssets.Kit(Dg + "Models/Wall_01.fbx"),
                PsxAssets.Kit(Dg + "Models/Wall_Ruin.fbx"));
            var ruined = KitStyle.Of(null, PsxAssets.Kit(Dg + "Models/Pillar.fbx"), null,
                PsxAssets.Kit(Dg + "Models/Wall_Ruin.fbx"), PsxAssets.Kit(Dg + "Models/Wall_Half.fbx"));
            var roof = PsxAssets.Kit(Mm + "Roofs/roof_straw.fbx");
            var bone = Mat("Osso", new Color(0.78f, 0.74f, 0.63f), 0f, 0.25f);
            var cloth = Mat("LonaVelha", new Color(0.32f, 0.27f, 0.2f));
            var ember = Mat("Brasa", new Color(0.9f, 0.35f, 0.1f), 0f, 0.3f, new Color(2.5f, 0.8f, 0.15f));

            Transform Add(Transform t) { content.Add(t); return t; }
            var places = new GameObject("Locais (descoberta)").transform;
            content.Add(places);   // fica no pedaço (0,0); os gatilhos são movidos um a um abaixo
            var discoveries = new List<Transform>();
            void Discover(string id, string name, bool secret, int xp, Site s, float height = 4f)
            {
                Discoverable(places, id, name, secret, xp, P(s.C.x, s.C.y, height * 0.5f), new Vector3(s.R * 1.6f, height, s.R * 1.6f));
            }

            // ---- Chegada: placa da estrada ----
            var sign = Add(Group("Placa da Estrada", P(-144f, -146f), 40f));
            Box(sign, "Poste", new Vector3(0, 1.1f, 0), new Vector3(0.15f, 2.2f, 0.15f), darkWood);
            Box(sign, "Placa Vila", new Vector3(0.45f, 1.8f, 0), new Vector3(0.9f, 0.25f, 0.05f), wood, new Vector3(0, 0, -6));
            Box(sign, "Placa Torre", new Vector3(-0.4f, 1.45f, 0), new Vector3(0.8f, 0.22f, 0.05f), wood, new Vector3(0, 90, 8));

            // ---- Vila Junco ----
            var houseSpots = new[]
            {
                new Vector3(-12f, -10f, 30f), new Vector3(11f, -14f, -20f), new Vector3(-17f, 9f, 80f),
                new Vector3(15f, 8f, -100f), new Vector3(0f, 18f, 180f), new Vector3(-3f, -21f, 10f)
            };
            for (var i = 0; i < houseSpots.Length; i++)
            {
                var hs = houseSpots[i];
                var house = Add(House($"Casa {i + 1}", P(Village.C.x + hs.x, Village.C.y + hs.y), hs.z, wood, darkWood, thatch, roof));
                KitDresser.Dress(house, medieval, i, n => n.StartsWith("Telhado") || n.StartsWith("Degrau"));
                if (i == 0)
                {
                    var c = PlaceChest(null, false, "ta_bau_vila", house.TransformPoint(new Vector3(0f, 0.98f, 1.2f)), hs.z + 180f);
                    if (c != null) Add(c.transform);
                }
            }
            var well = Add(Group("Poço", P(Village.C.x, Village.C.y)));
            for (var i = 0; i < 8; i++)
            {
                var a = i * 45f;
                var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                Box(well, "Pedra", dir * 0.9f + Vector3.up * 0.4f, new Vector3(0.7f, 0.8f, 0.35f), stone, new Vector3(0, a, 0));
            }
            Box(well, "Trave", new Vector3(0, 2f, 0), new Vector3(2.2f, 0.12f, 0.12f), darkWood);
            Box(well, "Pilar A", new Vector3(-1f, 1.2f, 0), new Vector3(0.12f, 1.6f, 0.12f), darkWood);
            Box(well, "Pilar B", new Vector3(1f, 1.2f, 0), new Vector3(0.12f, 1.6f, 0.12f), darkWood);
            KitDresser.Dress(well, medieval, 50);
            BuildVillageProps(Add(Group("Adereços da Vila", P(Village.C.x, Village.C.y))));
            // Lanternas da vila
            for (var i = 0; i < 6; i++)
            {
                var a = i * 60f * Mathf.Deg2Rad;
                var pos = new Vector2(Village.C.x + Mathf.Cos(a) * 9f, Village.C.y + Mathf.Sin(a) * 9f);
                var post = Add(Group("Lanterna", P(pos.x, pos.y)));
                Box(post, "Poste", new Vector3(0, 1.2f, 0), new Vector3(0.12f, 2.4f, 0.12f), darkWood);
                Torch(post, new Vector3(0, 2.55f, 0), 10f, 1.6f);
                KitDresser.Dress(post, medieval, 60 + i);
            }
            // Píer para dentro da água
            var pier = Add(Group("Píer", P(Village.C.x + 26f, Village.C.y - 2f), 90f));
            for (var i = 0; i < 12; i++)
            {
                Box(pier, "Tábua", new Vector3(0, WaterLevel + 0.35f - pier.position.y, i * 1.1f), new Vector3(2.2f, 0.1f, 1f), wood);
                if (i % 3 == 0)
                {
                    Box(pier, "Estaca E", new Vector3(-1.1f, WaterLevel - 0.6f - pier.position.y, i * 1.1f), new Vector3(0.15f, 2f, 0.15f), darkWood);
                    Box(pier, "Estaca D", new Vector3(1.1f, WaterLevel - 0.6f - pier.position.y, i * 1.1f), new Vector3(0.15f, 2f, 0.15f), darkWood);
                }
            }
            KitDresser.Dress(pier, medieval, 70);
            Discover("ta_vila", "Vila Junco", false, 30, Village);

            // ---- Torre de Vigia (escada em espiral por dentro) ----
            var tower = Add(Group("Torre de Vigia", P(Tower.C.x, Tower.C.y)));
            const int segments = 14;
            const float radius = 3f, towerHeight = 12f;
            for (var i = 0; i < segments; i++)
            {
                if (i == 0) continue;   // porta (virada para o sul)
                var a = i * 360f / segments;
                var dir = Quaternion.Euler(0, a, 0) * Vector3.back;
                var ruinedTop = i % 4 == 1 ? 2f : 0f;
                var h = towerHeight - ruinedTop;
                Box(tower, "Muro", dir * radius + Vector3.up * h * 0.5f, new Vector3(1.45f, h, 0.5f), i % 3 == 0 ? mossStone : stone, new Vector3(0, a, 0));
            }
            Box(tower, "Verga", Vector3.back * radius + Vector3.up * 3.5f, new Vector3(1.45f, 1f, 0.5f), stone);
            Box(tower, "Coluna Central", new Vector3(0, towerHeight * 0.5f, 0), new Vector3(0.9f, towerHeight, 0.9f), stone);
            const int steps = 42;
            for (var i = 0; i < steps; i++)
            {
                var a = 20f + i * 17f;
                var dir = Quaternion.Euler(0, a, 0) * Vector3.back;
                Box(tower, "Degrau", dir * 1.6f + Vector3.up * (0.14f + i * 0.27f), new Vector3(0.8f, 0.28f, 2.2f), stone, new Vector3(0, a, 0));
            }
            var topY = 0.14f + steps * 0.27f;
            for (var i = 0; i < 8; i++)
            {
                var a = i * 45f;
                var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                if (i >= 2 && i <= 4) continue;   // abertura sobre o fim da escada
                Box(tower, "Piso do Topo", dir * 1.7f + Vector3.up * topY, new Vector3(1.6f, 0.2f, 1.9f), wood, new Vector3(0, a, 0));
            }
            var topChest = PlaceChest(null, false, "ta_bau_torre", tower.TransformPoint(new Vector3(0f, topY + 0.1f, 1.8f)), 180f);
            if (topChest != null) Add(topChest.transform);
            Torch(tower, new Vector3(-0.9f, 2.6f, -radius + 0.4f));
            Discover("ta_torre", "Torre de Vigia", false, 30, Tower, 3f);
            Discoverable(places, "ta_torre_topo", "Topo da Torre", true, 60, tower.position + Vector3.up * (topY + 1f), new Vector3(5f, 2f, 5f));

            // ---- Covil dos Lobos ----
            var den = Add(Group("Covil dos Lobos", P(Den.C.x, Den.C.y)));
            var rng = new System.Random(77);
            for (var i = 0; i < 9; i++)
            {
                var a = (float)rng.NextDouble() * 360f;
                var d = Den.R * (0.8f + (float)rng.NextDouble() * 0.5f);
                var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                var size = new Vector3(2f + (float)rng.NextDouble() * 2.5f, 1.5f + (float)rng.NextDouble() * 2f, 2f + (float)rng.NextDouble() * 2f);
                Box(den, "Rocha", dir * d + Vector3.up * size.y * 0.3f, size, i % 2 == 0 ? stone : mossStone,
                    new Vector3((float)rng.NextDouble() * 20f, a, (float)rng.NextDouble() * 20f));
            }
            for (var i = 0; i < 10; i++)
            {
                var pos = new Vector3((float)rng.NextDouble() * 10f - 5f, 0.05f, (float)rng.NextDouble() * 10f - 5f);
                Part(den, "Osso", PrimitiveType.Cylinder, pos, new Vector3(0.06f, 0.25f, 0.06f), bone, new Vector3(90f, (float)rng.NextDouble() * 180f, 0f), false);
            }
            Add(Enemy(EnemyContentFactory.Wolf, null, P(Den.C.x - 3f, Den.C.y), 0f)?.transform);
            Add(Enemy(EnemyContentFactory.Wolf, null, P(Den.C.x + 3f, Den.C.y + 2f), 90f)?.transform);
            Add(Enemy(EnemyContentFactory.Wolf, null, P(Den.C.x, Den.C.y - 4f), 200f)?.transform);
            Add(Enemy(EnemyContentFactory.Wolf, null, P(Den.C.x + 4f, Den.C.y - 3f), 300f)?.transform);
            Discover("ta_covil", "Covil dos Lobos", false, 25, Den);

            // ---- Capela Afogada (Cavaleiro Corrompido) ----
            var chapel = Add(Group("Capela Afogada", P(Chapel.C.x, Chapel.C.y), 0f));
            // Nave 10 × 18 com paredes quebradas; entrada ao sul.
            Box(chapel, "Piso", new Vector3(0, 0.05f, 0), new Vector3(10f, 0.1f, 18f), stone);
            Box(chapel, "Parede Oeste A", new Vector3(-5f, 2f, -4f), new Vector3(0.6f, 4f, 10f), stone);
            Box(chapel, "Parede Oeste B", new Vector3(-5f, 1f, 6f), new Vector3(0.6f, 2f, 6f), mossStone);
            Box(chapel, "Parede Leste A", new Vector3(5f, 1.4f, -6f), new Vector3(0.6f, 2.8f, 6f), mossStone);
            Box(chapel, "Parede Leste B", new Vector3(5f, 2.5f, 4f), new Vector3(0.6f, 5f, 10f), stone);
            Box(chapel, "Fundo", new Vector3(0f, 3f, 9f), new Vector3(10.6f, 6f, 0.6f), stone);
            Box(chapel, "Frente E", new Vector3(-3.5f, 2.5f, -9f), new Vector3(3.6f, 5f, 0.6f), stone);
            Box(chapel, "Frente D", new Vector3(3.5f, 1.5f, -9f), new Vector3(3.6f, 3f, 0.6f), mossStone);
            for (var i = 0; i < 3; i++)
            {
                Box(chapel, "Coluna", new Vector3(-2.6f, 2.2f, -4f + i * 5f), new Vector3(0.7f, 4.4f, 0.7f), stone);
                if (i != 1) Box(chapel, "Coluna", new Vector3(2.6f, 2.2f, -4f + i * 5f), new Vector3(0.7f, 4.4f, 0.7f), stone);
            }
            Box(chapel, "Coluna Caída", new Vector3(3.2f, 0.4f, 1.5f), new Vector3(0.7f, 0.7f, 4f), stone, new Vector3(0, 25, 90));
            Box(chapel, "Altar", new Vector3(0, 0.55f, 7.2f), new Vector3(2.4f, 1.1f, 1.2f), stone);
            Torch(chapel, new Vector3(-4.4f, 2.4f, 6f));
            Torch(chapel, new Vector3(4.4f, 2.4f, 6f));
            var knightLoot = LootContentFactory.Table("CavaleiroCapela", "Loot do Cavaleiro da Capela", t =>
            {
                t.guaranteed = Entries(Entry("ChaveCripta", 1f), Entry("EssenciaPeste", 1f), Entry("PocaoVida", 1f, 2, 2));
                t.guaranteedPicks = 1;
                t.guaranteedMinimumRarity = ItemRarity.Rare;
                t.minimumRarity = ItemRarity.Uncommon;
                t.rarityBonus = 0.5f;
                t.entries = Entries(Entry("Couraca", 1f), Entry("Elmo", 1f), Entry("Manoplas", 1f), Entry("Grevas", 1f), Entry("Martelo", 1f),
                    Entry("Espada", 1f), Entry("Escudo", 0.8f), Entry("Amuleto", 0.6f), Entry("AnelForca", 0.6f));
                t.rolls = new Vector2Int(1, 1);
                t.nothingWeight = 1f;
                t.gold = new Vector2Int(40, 90);
            });
            Add(Enemy(EnemyContentFactory.Knight, null, chapel.TransformPoint(new Vector3(0f, 0f, 5f)), 180f, knightLoot, null, "ta_cavaleiro_capela")?.transform);
            Add(Enemy(EnemyContentFactory.Skeleton, null, chapel.TransformPoint(new Vector3(-2.5f, 0f, -6f)), 180f)?.transform);
            Add(Enemy(EnemyContentFactory.Skeleton, null, chapel.TransformPoint(new Vector3(2.5f, 0f, -6f)), 180f)?.transform);
            Add(Enemy(EnemyContentFactory.Archer, null, P(Chapel.C.x - 9f, Chapel.C.y + 4f), 160f)?.transform);
            KitDresser.Dress(chapel, ancient, 80);
            Discover("ta_capela", "Capela Afogada", false, 40, Chapel, 6f);

            // ---- Acampamento dos Ossos ----
            var camp = Add(Group("Acampamento dos Ossos", P(Camp.C.x, Camp.C.y), 0f));
            for (var i = 0; i < 18; i++)
            {
                if (i == 13 || i == 14) continue;   // entrada a oeste
                var a = i * 20f;
                var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                Box(camp, "Paliçada", dir * (Camp.R - 2f) + Vector3.up * 1.4f, new Vector3(4f, 2.8f, 0.35f), darkWood, new Vector3(0, a + 90f, (i % 3) * 4f));
            }
            var fire = Group("Fogueira", Vector3.zero, 0f, camp);
            fire.localPosition = Vector3.zero;
            for (var i = 0; i < 5; i++)
                Part(fire, "Tora", PrimitiveType.Cylinder, new Vector3(0, 0.15f, 0), new Vector3(0.18f, 0.5f, 0.18f), darkWood, new Vector3(80f, i * 72f, 0f), false);
            Part(fire, "Brasas", PrimitiveType.Sphere, new Vector3(0, 0.15f, 0), new Vector3(0.7f, 0.25f, 0.7f), ember, default, false);
            var fireLight = new GameObject("Luz da Fogueira").AddComponent<Light>();
            fireLight.transform.SetParent(fire, false);
            fireLight.transform.localPosition = new Vector3(0, 0.8f, 0);
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.55f, 0.25f);
            fireLight.range = 14f;
            fireLight.intensity = 2.6f;
            fireLight.gameObject.AddComponent<TorchFlicker>();
            for (var i = 0; i < 3; i++)
            {
                var a = 40f + i * 110f;
                var tent = Group("Tenda", Vector3.zero, a, camp);
                tent.localPosition = Quaternion.Euler(0, a, 0) * Vector3.forward * 8f;
                Box(tent, "Lona E", new Vector3(-0.8f, 1f, 0), new Vector3(0.08f, 2.3f, 3f), cloth, new Vector3(0, 0, -40f));
                Box(tent, "Lona D", new Vector3(0.8f, 1f, 0), new Vector3(0.08f, 2.3f, 3f), cloth, new Vector3(0, 0, 40f));
            }
            // Plataforma dos arqueiros com rampa
            Box(camp, "Plataforma", new Vector3(5f, 1.9f, 6f), new Vector3(4f, 0.25f, 4f), wood);
            for (var i = 0; i < 4; i++)
                Box(camp, "Pilar", new Vector3(3.2f + (i % 2) * 3.6f, 0.9f, 4.2f + (i / 2) * 3.6f), new Vector3(0.25f, 1.9f, 0.25f), darkWood);
            Box(camp, "Rampa", new Vector3(5f, 0.95f, 1.5f), new Vector3(1.6f, 0.15f, 5.2f), wood, new Vector3(-22f, 0, 0));
            Add(Enemy(EnemyContentFactory.Archer, null, camp.TransformPoint(new Vector3(4.5f, 2.05f, 6.5f)), 270f)?.transform);
            Add(Enemy(EnemyContentFactory.Archer, null, camp.TransformPoint(new Vector3(5.8f, 2.05f, 5.4f)), 230f)?.transform);
            Add(Enemy(EnemyContentFactory.Skeleton, null, camp.TransformPoint(new Vector3(-3f, 0f, 2f)), 270f)?.transform);
            Add(Enemy(EnemyContentFactory.Skeleton, null, camp.TransformPoint(new Vector3(-2f, 0f, -4f)), 250f)?.transform);
            Add(Enemy(EnemyContentFactory.Skeleton, null, camp.TransformPoint(new Vector3(2f, 0f, -6f)), 300f)?.transform);
            var campChest = PlaceChest(null, false, "ta_bau_acampamento", camp.TransformPoint(new Vector3(-1f, 0f, 9f)), 180f);
            if (campChest != null) Add(campChest.transform);
            Discover("ta_acampamento", "Acampamento dos Ossos", false, 35, Camp);

            // ---- Entrada da Cripta (no morro, ao norte) ----
            var crypt = Add(Group("Entrada da Cripta", P(CryptDoor.C.x, CryptDoor.C.y + 6f), 0f));
            Box(crypt, "Fachada", new Vector3(0, 3f, 1.5f), new Vector3(9f, 6f, 3f), stone);
            Box(crypt, "Pilar E", new Vector3(-2f, 2.2f, -0.3f), new Vector3(0.9f, 4.4f, 0.9f), mossStone);
            Box(crypt, "Pilar D", new Vector3(2f, 2.2f, -0.3f), new Vector3(0.9f, 4.4f, 0.9f), mossStone);
            Box(crypt, "Lintel", new Vector3(0f, 4.6f, -0.3f), new Vector3(5f, 0.6f, 1f), stone);
            Box(crypt, "Degrau", new Vector3(0f, 0.15f, -1.4f), new Vector3(4f, 0.3f, 1.2f), stone);
            PortalDoor(crypt, new Vector3(0f, 1.8f, -0.05f), new Vector3(3.1f, 3.6f, 0.2f), CriptaBuilder.SceneName, "entrada",
                "Entrar na Cripta", Item("ChaveCripta"));
            Torch(crypt, new Vector3(-2.8f, 2.6f, -0.6f));
            Torch(crypt, new Vector3(2.8f, 2.6f, -0.6f));
            KitDresser.Dress(crypt, ancient, 90);
            Discover("ta_cripta", "Entrada da Cripta", false, 30, CryptDoor);

            // ---- Clareira Esquecida (secreta: anel de rochas com uma fresta) ----
            var glade = Add(Group("Clareira Esquecida", P(Glade.C.x, Glade.C.y)));
            for (var i = 0; i < 16; i++)
            {
                if (i == 4) continue;   // a fresta, voltada para o leste
                var a = i * 22.5f;
                var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                Box(glade, "Rocha Alta", dir * (Glade.R + 0.5f) + Vector3.up * 2.5f, new Vector3(4.2f, 5f + (i % 3), 2.2f), i % 2 == 0 ? mossStone : stone,
                    new Vector3(0, a + 90f, (i % 2) * 6f));
            }
            Torch(glade, new Vector3(0f, 1.2f, 1.2f), 7f, 1.2f);
            var forgottenLoot = LootContentFactory.Table("BauEsquecido", "Baú Esquecido", t =>
            {
                t.guaranteedPicks = 2;
                t.guaranteedMinimumRarity = ItemRarity.Rare;
                t.minimumRarity = ItemRarity.Uncommon;
                t.rarityBonus = 2f;
                t.entries = Entries(Entry("Amuleto", 1f), Entry("AnelVitalidade", 1f), Entry("AnelForca", 1f), Entry("AnelMana", 1f),
                    Entry("AnelResistencia", 1f), Entry("AnelAgilidade", 1f), Entry("Espada", 0.6f), Entry("Couraca", 0.6f));
                t.rolls = new Vector2Int(0, 0);
                t.gold = new Vector2Int(60, 120);
            });
            var gladeChest = PlaceChest(null, false, "ta_bau_clareira", glade.TransformPoint(new Vector3(0f, 0f, 2f)), 180f, forgottenLoot);
            if (gladeChest != null) Add(gladeChest.transform);
            Discover("ta_clareira", "Clareira Esquecida", true, 80, Glade);

            // ---- Andarilhos pela estrada ----
            Add(Enemy(EnemyContentFactory.Wolf, null, P(-20f, 25f), 30f)?.transform);
            Add(Enemy(EnemyContentFactory.Wolf, null, P(-17f, 28f), 60f)?.transform);
            Add(Enemy(EnemyContentFactory.Wolf, null, P(95f, 70f), 200f)?.transform);
            Add(Enemy(EnemyContentFactory.Wolf, null, P(98f, 67f), 220f)?.transform);
            Add(Enemy(EnemyContentFactory.Skeleton, null, P(-62f, -36f), 140f)?.transform);
            Add(Enemy(EnemyContentFactory.Skeleton, null, P(70f, 24f), 90f)?.transform);

            // ---- Detalhes espalhados: rochas, troncos caídos, ruínas ----
            var scatter = new System.Random(1234);
            for (var i = 0; i < 90; i++)
            {
                var x = (float)scatter.NextDouble() * 360f - 180f;
                var z = (float)scatter.NextDouble() * 360f - 180f;
                if (RoadDistance(x, z) < 5f || SiteDistance(x, z, out _) < 6f || Height(x, z) < WaterLevel - 0.4f) continue;
                var kind = scatter.Next(3);
                var yaw = (float)scatter.NextDouble() * 360f;
                var t = Add(Group(kind == 0 ? "Rocha" : kind == 1 ? "Tronco Caído" : "Ruína", P(x, z), yaw));
                if (kind == 0)
                    Box(t, "Rocha", new Vector3(0, 0.4f, 0), new Vector3(1f + (float)scatter.NextDouble() * 2f, 0.8f + (float)scatter.NextDouble() * 1.4f, 1f + (float)scatter.NextDouble() * 2f),
                        scatter.Next(2) == 0 ? stone : mossStone, new Vector3((float)scatter.NextDouble() * 15f, 0, (float)scatter.NextDouble() * 15f));
                else if (kind == 1)
                    Part(t, "Tronco", PrimitiveType.Cylinder, new Vector3(0, 0.3f, 0), new Vector3(0.55f, 2.2f + (float)scatter.NextDouble() * 1.5f, 0.55f), darkWood, new Vector3(90f, 0, 4f));
                else
                {
                    Box(t, "Muro Ruído", new Vector3(0, 0.9f, 0), new Vector3(3f, 1.8f, 0.5f), stone);
                    Box(t, "Bloco", new Vector3(1.8f, 0.3f, 0.8f), new Vector3(0.8f, 0.6f, 0.6f), mossStone, new Vector3(0, 30f, 0));
                    KitDresser.Dress(t, ruined, i);
                }
            }

            // Gatilhos de descoberta vão para o pedaço de cada um.
            var triggers = new List<Transform>();
            foreach (Transform child in places) triggers.Add(child);
            foreach (var t in triggers)
            {
                t.SetParent(null, true);
                content.Add(t);
            }
            content.Remove(places);
            Object.DestroyImmediate(places.gameObject);
        }

        /// <summary>Barris, caixotes, feno, sacos e uma barraca de feira em volta do poço e das casas.</summary>
        private static void BuildVillageProps(Transform parent)
        {
            const string M = "ModularMedieval/Market/";
            var stall = PsxAssets.Kit(M + "market_stall_red.fbx", 2.8f, true);
            var barrel = PsxAssets.Kit(M + "barrel.fbx", 1f, true);
            var barrelApples = PsxAssets.Kit(M + "barrel_apples.fbx", 1f, true);
            var crate = PsxAssets.Kit(M + "crate.fbx", 0.7f, true);
            var cratePotatoes = PsxAssets.Kit(M + "crate_potatoes.fbx", 0.7f, true);
            var hay = PsxAssets.Kit(M + "haybale_dry.fbx", 1.1f, true);
            var sack = PsxAssets.Kit(M + "jutesack_closed.fbx", 0.7f);
            var basket = PsxAssets.Kit(M + "basket_apples.fbx", 0.35f);
            var table = PsxAssets.Kit(M + "table_prebuild.fbx", 0.95f, true);
            var vase = PsxAssets.Kit(M + "terracotta_vase.fbx", 0.6f);

            void At(GameObject prefab, float dx, float dz, float yaw)
            {
                var x = Village.C.x + dx;
                var z = Village.C.y + dz;
                PsxAssets.Place(prefab, parent, P(x, z), yaw);
            }
            // Feira ao lado do poço
            At(stall, -5f, 4.5f, 150f);
            At(table, 4.5f, -4f, 20f);
            At(basket, 4.6f, -3.9f, 0f);
            At(vase, 5.4f, -3.4f, 0f);
            At(crate, -6.8f, 3f, 10f);
            At(cratePotatoes, -6.6f, 4.1f, 35f);
            At(barrelApples, -3.4f, 6.4f, 0f);
            // Pelas casas e pelo píer
            var rng = new System.Random(31);
            var props = new[] { barrel, barrel, crate, cratePotatoes, hay, sack, sack, vase, barrelApples };
            var spots = new[]
            {
                new Vector2(-7.5f, -12f), new Vector2(-8.2f, -11f), new Vector2(14.5f, -11f), new Vector2(-13f, 5.5f), new Vector2(-14f, 4.6f),
                new Vector2(18.5f, 5.5f), new Vector2(12.5f, 11.5f), new Vector2(3f, 15f), new Vector2(-6.5f, -18f), new Vector2(22f, -3.5f),
                new Vector2(22.5f, -1.5f), new Vector2(21.2f, -2.4f), new Vector2(-2.5f, 13f), new Vector2(8.5f, 1.5f)
            };
            foreach (var s in spots) At(props[rng.Next(props.Length)], s.x, s.y, (float)rng.NextDouble() * 360f);
            At(hay, -19.5f, -2f, 15f);
            At(hay, -18.6f, -0.6f, 70f);
        }

        private static Transform House(string name, Vector3 ground, float yaw, Material wood, Material darkWood, Material thatch, GameObject roofKit = null)
        {
            var house = Group(name, ground, yaw);
            const float w = 5f, d = 4f, wallH = 2.6f, stilt = 0.9f;
            // Palafitas
            for (var ix = -1; ix <= 1; ix += 2)
            for (var iz = -1; iz <= 1; iz += 2)
                Box(house, "Palafita", new Vector3(ix * (w * 0.5f - 0.2f), stilt * 0.5f - 0.3f, iz * (d * 0.5f - 0.2f)), new Vector3(0.25f, stilt + 0.6f, 0.25f), darkWood);
            Box(house, "Assoalho", new Vector3(0, stilt, 0), new Vector3(w, 0.15f, d), wood);
            var floorTop = stilt + 0.075f;
            Box(house, "Parede Fundo", new Vector3(0, floorTop + wallH * 0.5f, d * 0.5f), new Vector3(w, wallH, 0.15f), wood);
            Box(house, "Parede E", new Vector3(-w * 0.5f, floorTop + wallH * 0.5f, 0), new Vector3(0.15f, wallH, d), wood);
            Box(house, "Parede D", new Vector3(w * 0.5f, floorTop + wallH * 0.5f, 0), new Vector3(0.15f, wallH, d), wood);
            Box(house, "Frente E", new Vector3(-1.6f, floorTop + wallH * 0.5f, -d * 0.5f), new Vector3(1.8f, wallH, 0.15f), wood);
            Box(house, "Frente D", new Vector3(1.6f, floorTop + wallH * 0.5f, -d * 0.5f), new Vector3(1.8f, wallH, 0.15f), wood);
            Box(house, "Verga", new Vector3(0, floorTop + wallH - 0.3f, -d * 0.5f), new Vector3(1.4f, 0.6f, 0.15f), wood);
            // Telhado de duas águas
            Box(house, "Telhado E", new Vector3(-1.35f, floorTop + wallH + 0.75f, 0), new Vector3(3.2f, 0.15f, d + 0.8f), thatch, new Vector3(0, 0, 32f));
            Box(house, "Telhado D", new Vector3(1.35f, floorTop + wallH + 0.75f, 0), new Vector3(3.2f, 0.15f, d + 0.8f), thatch, new Vector3(0, 0, -32f));
            if (roofKit != null)
            {
                // Telhado de palha do kit por cima; as placas continuam só como colisão.
                var roofHolder = Group("Telhado (kit)", Vector3.zero, 0f, house);
                roofHolder.localPosition = Vector3.zero;
                roofHolder.localRotation = Quaternion.identity;
                const float roofH = 1.9f;
                if (KitDresser.Fit(roofKit, roofHolder, new Vector3(0f, floorTop + wallH - 0.1f + roofH * 0.5f, 0f), new Vector3(w + 0.9f, roofH, d + 0.9f), false))
                    foreach (var r in house.GetComponentsInChildren<MeshRenderer>())
                        if (r.name.StartsWith("Telhado ")) r.enabled = false;
            }
            // Degraus até a porta
            Box(house, "Degrau 1", new Vector3(0, 0.25f, -d * 0.5f - 1.1f), new Vector3(1.3f, 0.5f, 0.6f), darkWood);
            Box(house, "Degrau 2", new Vector3(0, 0.55f, -d * 0.5f - 0.5f), new Vector3(1.3f, 0.3f, 0.6f), darkWood);
            return house;
        }
    }
}
