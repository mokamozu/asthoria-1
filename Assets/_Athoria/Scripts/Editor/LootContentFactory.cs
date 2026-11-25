using System.Collections.Generic;
using System.IO;
using System.Linq;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Items;
using Athoria.Gameplay.Loot;
using Athoria.Gameplay.World;
using UnityEditor;
using UnityEngine;

namespace Athoria.Editor
{
    /// <summary>
    /// Gera as tabelas de loot (inimigos e baús) e os Prefabs de baú da Etapa 12.
    /// As tabelas ficam em Data/Loot e podem ser ajustadas no Inspector.
    /// </summary>
    public static class LootContentFactory
    {
        private const string DataFolder = "Assets/_Athoria/Data/Loot";
        private const string PrefabFolder = "Assets/_Athoria/Prefabs/Mundo";
        private const string SfxFolder = "Assets/_Athoria/Audio/SFX/Provisorios";

        public const string Wolf = "Lobo";
        public const string Skeleton = "Esqueleto";
        public const string Archer = "EsqueletoArqueiro";
        public const string Knight = "CavaleiroCorrompido";
        public const string MossAncient = "AnciaoDoBrejo";
        public const string MossSpirit = "EspiritoMusgo";
        public const string ChestCommon = "BauComum";
        public const string ChestLocked = "BauTrancado";

        public const string ChestPath = PrefabFolder + "/Bau.prefab";
        public const string LockedChestPath = PrefabFolder + "/BauTrancado.prefab";

        [MenuItem("Athoria/Dados/Gerar tabelas de loot e baús")]
        public static void GenerateMenu()
        {
            GenerateAll(ItemContentFactory.GenerateAll());
            Debug.Log("[Athoria] Tabelas de loot e baús gerados.");
        }

        public static Dictionary<string, LootTable> GenerateAll(Dictionary<string, ItemData> items)
        {
            Directory.CreateDirectory(DataFolder);
            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh();
            PixelFxImporter.ImportAll();
            var burst = PixelFxImporter.Load("fx_bau_brilho");
            if (burst != null) burst.EditorSetScale(64f, 1f);

            LootEntry E(string id, float weight, int min = 1, int max = 1) =>
                items.TryGetValue(id, out var item) ? new LootEntry { item = item, weight = weight, quantity = new Vector2Int(min, max) } : null;
            LootEntry[] List(params LootEntry[] entries) => entries.Where(e => e != null).ToArray();

            var tables = new Dictionary<string, LootTable>();

            tables[Wolf] = Table(Wolf, "Loot do Lobo", t =>
            {
                t.rolls = new Vector2Int(1, 2);
                t.nothingWeight = 4f;
                t.entries = List(E("PeleLobo", 5f), E("PresaLobo", 4f, 1, 2), E("PocaoVida", 1f));
                t.gold = Vector2Int.zero;
            });

            tables[Skeleton] = Table(Skeleton, "Loot do Esqueleto", t =>
            {
                t.rolls = new Vector2Int(1, 2);
                t.nothingWeight = 3f;
                t.entries = List(E("OssoAntigo", 5f, 1, 2), E("Espada", 1f), E("Escudo", 1f), E("Adaga", 1f), E("Machado", 0.6f),
                    E("Elmo", 0.6f), E("PocaoVigor", 1.5f));
                t.goldChance = 0.7f;
                t.gold = new Vector2Int(3, 10);
            });

            tables[Archer] = Table(Archer, "Loot do Esqueleto Arqueiro", t =>
            {
                t.rolls = new Vector2Int(1, 2);
                t.nothingWeight = 3f;
                t.entries = List(E("OssoAntigo", 4f), E("PontaFlecha", 4f, 2, 5), E("PocaoVida", 1.5f), E("Botas", 0.6f), E("AnelAgilidade", 0.4f));
                t.goldChance = 0.6f;
                t.gold = new Vector2Int(2, 8);
            });

            var gear = List(E("Couraca", 1f), E("Elmo", 1f), E("Manoplas", 1f), E("Grevas", 1f), E("Botas", 1f),
                E("Espada", 1f), E("Machado", 1f), E("Martelo", 1f), E("Lanca", 1f), E("Escudo", 0.8f),
                E("Amuleto", 0.6f), E("AnelForca", 0.6f), E("AnelVitalidade", 0.6f), E("AnelResistencia", 0.5f));

            tables[Knight] = Table(Knight, "Loot do Cavaleiro Corrompido", t =>
            {
                // Chefe: 1 equipamento Raro ou melhor garantido, mais a Essência da Peste.
                t.guaranteed = List(E("EssenciaPeste", 1f), E("PocaoVida", 1f, 2, 2));
                t.guaranteedPicks = 1;
                t.guaranteedMinimumRarity = ItemRarity.Rare;
                t.minimumRarity = ItemRarity.Uncommon;
                t.rarityBonus = 0.5f;
                t.rolls = new Vector2Int(1, 1);
                t.nothingWeight = 1f;
                t.entries = gear;
                t.gold = new Vector2Int(40, 90);
            });

            // Espírito do Musgo: sempre solta musgo (40 despertam o Ancião do Brejo). Quantidade provisória.
            tables[MossSpirit] = Table(MossSpirit, "Loot do Espírito do Musgo", t =>
            {
                t.guaranteed = List(E("Musgo", 1f, 4, 7));
                t.rolls = new Vector2Int(1, 1);
                t.nothingWeight = 4f;
                t.entries = List(E("PocaoVida", 1f));
                t.goldChance = 0f;
            });

            // Chefe do brejo: dois equipamentos Raros ou melhores garantidos, poções e bastante ouro.
            // (Recompensas provisórias até haver itens próprios do Ancião.)
            tables[MossAncient] = Table(MossAncient, "Loot do Ancião do Brejo", t =>
            {
                t.guaranteed = List(E("PocaoVida", 1f, 3, 3), E("PocaoVigor", 1f, 2, 2));
                t.guaranteedPicks = 2;
                t.guaranteedMinimumRarity = ItemRarity.Rare;
                t.minimumRarity = ItemRarity.Uncommon;
                t.rarityBonus = 0.8f;
                t.rolls = new Vector2Int(1, 2);
                t.nothingWeight = 0.5f;
                t.entries = gear;
                t.gold = new Vector2Int(150, 300);
            });

            tables[ChestCommon] = Table(ChestCommon, "Baú Comum", t =>
            {
                t.rolls = new Vector2Int(2, 3);
                t.nothingWeight = 1f;
                t.entries = List(E("PocaoVida", 3f), E("PocaoVigor", 2f), E("PocaoMana", 1f), E("Erva", 2f, 1, 3),
                    E("Espada", 1f), E("Adaga", 1f), E("Lanca", 1f), E("Escudo", 1f), E("Elmo", 1f), E("Botas", 1f), E("Manoplas", 0.7f),
                    E("AnelVitalidade", 0.4f), E("AnelMana", 0.4f), E("AnelAgilidade", 0.4f));
                t.gold = new Vector2Int(10, 30);
            });

            tables[ChestLocked] = Table(ChestLocked, "Baú Trancado", t =>
            {
                t.guaranteedPicks = 1;
                t.guaranteedMinimumRarity = ItemRarity.Rare;
                t.minimumRarity = ItemRarity.Uncommon;
                t.rarityBonus = 1f;
                t.rolls = new Vector2Int(2, 2);
                t.nothingWeight = 0f;
                t.entries = gear.Concat(List(E("PocaoVida", 2f, 1, 2), E("PocaoMana", 1f))).ToArray();
                t.gold = new Vector2Int(40, 80);
            });

            BuildChest(ChestPath, "Baú", tables[ChestCommon], null, false);
            BuildChest(LockedChestPath, "Baú Trancado", tables[ChestLocked], items.GetValueOrDefault("Chave"), true);
            AssetDatabase.SaveAssets();
            return tables;
        }

        internal static LootTable Table(string id, string displayName, System.Action<LootTable> fill)
        {
            var path = $"{DataFolder}/Loot_{id}.asset";
            var table = AssetDatabase.LoadAssetAtPath<LootTable>(path);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<LootTable>();
                AssetDatabase.CreateAsset(table, path);
            }
            // Valores padrão antes de preencher (a tabela é recriada do zero a cada geração).
            table.rolls = new Vector2Int(1, 1);
            table.nothingWeight = 0f;
            table.entries = new LootEntry[0];
            table.guaranteed = new LootEntry[0];
            table.guaranteedPicks = 0;
            table.goldChance = 1f;
            table.gold = Vector2Int.zero;
            table.minimumRarity = ItemRarity.Common;
            table.maximumRarity = ItemRarity.Legendary;
            table.guaranteedMinimumRarity = ItemRarity.Common;
            table.rarityBonus = 0f;
            fill(table);
            table.EditorSetInfo(displayName, "Tabela de loot gerada pela Etapa 12.");
            EditorUtility.SetDirty(table);
            return table;
        }

        // ---------------- Baú ----------------

        private static void BuildChest(string path, string displayName, LootTable table, ItemData key, bool locked)
        {
            // Texturas PS1 do pacote PSX Dungeon (sem elas, cores lisas).
            const string T = "Dungeon/PSX_Dungeon/Textures/";
            var wood = PsxAssets.Textured(T + "TEX_Planks_01.png", "MadeiraBau", new Color(0.85f, 0.75f, 0.65f),
                EditorMaterials.GetOrCreate("MadeiraBau", new Color(0.36f, 0.22f, 0.11f), 0f, 0.2f));
            var band = locked
                ? PsxAssets.Textured(T + "TEX_Metal_01.png", "FerroNegroBau", new Color(0.45f, 0.45f, 0.5f),
                    EditorMaterials.GetOrCreate("FerroNegro", new Color(0.12f, 0.12f, 0.13f), 0.85f, 0.4f))
                : PsxAssets.Textured(T + "TEX_Rusty_Metal_01.png", "FerroVelhoBau", Color.white,
                    EditorMaterials.GetOrCreate("FerroVelho", new Color(0.35f, 0.34f, 0.33f), 0.8f, 0.35f));
            var lockMat = locked
                ? EditorMaterials.GetOrCreate("CadeadoRubro", new Color(0.45f, 0.06f, 0.05f), 0.7f, 0.5f, new Color(0.35f, 0.02f, 0f))
                : EditorMaterials.GetOrCreate("OuroVelho", new Color(0.8f, 0.62f, 0.25f), 0.9f, 0.6f);

            var root = new GameObject(displayName);
            try
            {
                const float w = 0.9f, h = 0.5f, d = 0.56f;
                Part(root.transform, "Caixa", new Vector3(0, h * 0.5f, 0), new Vector3(w, h, d), wood);
                Part(root.transform, "Faixa Esquerda", new Vector3(-w * 0.3f, h * 0.5f, 0), new Vector3(0.06f, h + 0.01f, d + 0.02f), band);
                Part(root.transform, "Faixa Direita", new Vector3(w * 0.3f, h * 0.5f, 0), new Vector3(0.06f, h + 0.01f, d + 0.02f), band);
                Part(root.transform, "Base", new Vector3(0, 0.03f, 0), new Vector3(w + 0.04f, 0.06f, d + 0.04f), band);

                // Tampa com pivô na dobradiça (atrás, em cima): gira para trás ao abrir.
                var lid = new GameObject("Tampa").transform;
                lid.SetParent(root.transform, false);
                lid.localPosition = new Vector3(0, h, -d * 0.5f);
                Part(lid, "Tampa", new Vector3(0, 0.09f, d * 0.5f), new Vector3(w, 0.18f, d), wood);
                Part(lid, "Faixa Esquerda", new Vector3(-w * 0.3f, 0.09f, d * 0.5f), new Vector3(0.06f, 0.19f, d + 0.02f), band);
                Part(lid, "Faixa Direita", new Vector3(w * 0.3f, 0.09f, d * 0.5f), new Vector3(0.06f, 0.19f, d + 0.02f), band);
                Part(lid, "Fecho", new Vector3(0, 0.02f, d + 0.015f), new Vector3(0.12f, 0.12f, 0.03f), lockMat);
                if (locked) Part(root.transform, "Cadeado", new Vector3(0, h - 0.1f, d * 0.5f + 0.04f), new Vector3(0.14f, 0.16f, 0.06f), lockMat);

                var col = root.AddComponent<BoxCollider>();
                col.center = new Vector3(0, (h + 0.18f) * 0.5f, 0);
                col.size = new Vector3(w + 0.04f, h + 0.18f, d + 0.04f);

                var surface = root.AddComponent<SurfaceMaterial>();
                var sso = new SerializedObject(surface);
                sso.FindProperty("kind").enumValueIndex = (int)SurfaceKind.Wood;
                sso.ApplyModifiedPropertiesWithoutUndo();

                var chest = root.AddComponent<Chest>();
                var so = new SerializedObject(chest);
                so.FindProperty("loot").objectReferenceValue = table;
                so.FindProperty("requiredKey").objectReferenceValue = locked ? key : null;
                so.FindProperty("lid").objectReferenceValue = lid;
                so.FindProperty("openFx").objectReferenceValue = PixelFxImporter.Load("fx_bau_brilho");
                so.FindProperty("fxScale").floatValue = 1f;
                so.FindProperty("openSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxFolder}/bau_abrir.wav");
                so.FindProperty("lockedSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxFolder}/bau_trancado.wav");
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void Part(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
