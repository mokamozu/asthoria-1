using System.Collections.Generic;
using System.IO;
using Athoria.Gameplay.Items;
using Athoria.Gameplay.Stats;
using UnityEditor;
using UnityEngine;

namespace Athoria.Editor
{
    /// <summary>
    /// Itens PROVISÓRIOS para testar o inventário: poções, material, item de
    /// missão, e os dados de item das armas (tamanho na grade, peso, valor,
    /// raridade). Também gera os ícones a partir dos modelos.
    /// </summary>
    public static class ItemContentFactory
    {
        private const string DataFolder = "Assets/_Athoria/Data/Itens";
        private const string ModelFolder = "Assets/_Athoria/Prefabs/Itens";

        [MenuItem("Athoria/Dados/Gerar itens e ícones")]
        public static void GenerateMenu()
        {
            GenerateAll();
            Debug.Log("[Athoria] Itens e ícones gerados.");
        }

        public static Dictionary<string, ItemData> GenerateAll()
        {
            Directory.CreateDirectory(DataFolder);
            Directory.CreateDirectory(ModelFolder);
            var result = new Dictionary<string, ItemData>();

            // Armas: dados de item + ícone
            var weapons = PlaceholderWeaponFactory.GenerateAll();
            void W(string id, int w, int h, float weight, int value, ItemRarity rarity = ItemRarity.Common)
            {
                if (!weapons.TryGetValue(id, out var data)) return;
                data.EditorSetItem(data.ItemType, rarity, w, h, 1, weight, value);
                var icon = IconBaker.Bake(data.WorldModel, "Icone_" + id, w, h, new Vector3(0f, 0f, 0f));
                data.EditorSetInfo(data.DisplayName, data.Description, icon);
                result[id] = data;
            }
            W("Espada", 1, 3, 3f, 40);
            W("Machado", 2, 3, 5f, 45);
            W("Adaga", 1, 2, 1f, 20);
            W("Martelo", 2, 4, 9f, 60);
            W("Lanca", 1, 4, 5f, 45);
            W("Escudo", 2, 3, 6f, 35);
            W("Cajado", 1, 4, 3f, 70, ItemRarity.Uncommon);
            W("Arpao", 1, 4, 4f, 40);
            W("Rifle", 2, 4, 7f, 120, ItemRarity.Rare);
            W("Khopesh", 2, 3, 3f, 55, ItemRarity.Uncommon);
            // Armas próprias de cada cavaleira
            W("Alabarda", 1, 4, 6f, 60, ItemRarity.Uncommon);
            W("Katana", 1, 3, 3f, 60, ItemRarity.Uncommon);
            W("Escopeta", 2, 3, 6f, 110, ItemRarity.Rare);
            W("Montante", 1, 4, 8f, 70, ItemRarity.Uncommon);
            W("Kanabo", 1, 3, 7f, 60, ItemRarity.Uncommon);
            W("PresaSerpente", 1, 2, 1f, 45, ItemRarity.Uncommon);

            // Modelos
            var glass = EditorMaterials.GetOrCreate("Vidro", new Color(0.75f, 0.8f, 0.85f), 0.1f, 0.9f);
            var cork = EditorMaterials.GetOrCreate("Rolha", new Color(0.45f, 0.3f, 0.15f), 0f, 0.1f);
            var red = EditorMaterials.GetOrCreate("LiquidoVermelho", new Color(0.7f, 0.05f, 0.05f), 0f, 0.8f, new Color(0.25f, 0f, 0f));
            var green = EditorMaterials.GetOrCreate("LiquidoVerde", new Color(0.3f, 0.65f, 0.15f), 0f, 0.8f, new Color(0.05f, 0.15f, 0f));
            var blue = EditorMaterials.GetOrCreate("LiquidoAzul", new Color(0.15f, 0.3f, 0.85f), 0f, 0.8f, new Color(0f, 0.05f, 0.3f));
            var iron = EditorMaterials.GetOrCreate("FerroEnferrujado", new Color(0.4f, 0.27f, 0.17f), 0.6f, 0.25f);
            var herb = EditorMaterials.GetOrCreate("Erva", new Color(0.25f, 0.42f, 0.15f), 0f, 0.15f);

            var potionRed = Potion("Modelo_PocaoVida", red, glass, cork);
            var potionGreen = Potion("Modelo_PocaoVigor", green, glass, cork);
            var potionBlue = Potion("Modelo_PocaoMana", blue, glass, cork);
            var keyModel = Model("Modelo_Chave", t =>
            {
                Part(t, PrimitiveType.Cylinder, new Vector3(0, 0.16f, 0), new Vector3(0.09f, 0.012f, 0.09f), iron, new Vector3(90, 0, 0));
                Part(t, PrimitiveType.Cube, new Vector3(0, 0, 0), new Vector3(0.025f, 0.26f, 0.02f), iron);
                Part(t, PrimitiveType.Cube, new Vector3(0.03f, -0.1f, 0), new Vector3(0.05f, 0.025f, 0.02f), iron);
                Part(t, PrimitiveType.Cube, new Vector3(0.025f, -0.06f, 0), new Vector3(0.04f, 0.02f, 0.02f), iron);
            });
            var herbModel = Model("Modelo_Erva", t =>
            {
                for (var i = 0; i < 5; i++)
                {
                    var angle = -30f + i * 15f;
                    Part(t, PrimitiveType.Cube, new Vector3((i - 2) * 0.015f, 0.08f, 0), new Vector3(0.012f, 0.2f, 0.012f), herb, new Vector3(0, 0, angle));
                    Part(t, PrimitiveType.Sphere, new Vector3(-Mathf.Sin(angle * Mathf.Deg2Rad) * 0.18f + (i - 2) * 0.015f, 0.18f, 0), Vector3.one * 0.04f, herb);
                }
                Part(t, PrimitiveType.Cube, new Vector3(0, -0.01f, 0), new Vector3(0.06f, 0.02f, 0.03f), cork);
            });

            result["PocaoVida"] = Consumable("PocaoVida", "Poção de Vida Menor", "Um frasco de líquido rubro que fecha feridas.", ItemRarity.Common,
                ConsumableEffect.RestoreHealth, 40f, 5, 0.3f, 25, potionRed);
            result["PocaoVigor"] = Consumable("PocaoVigor", "Poção de Vigor Menor", "Devolve o fôlego em meio ao combate.", ItemRarity.Common,
                ConsumableEffect.RestoreStamina, 60f, 5, 0.3f, 20, potionGreen);
            result["PocaoMana"] = Consumable("PocaoMana", "Poção de Mana Menor", "Essência destilada que restaura a energia mágica.", ItemRarity.Uncommon,
                ConsumableEffect.RestoreMana, 30f, 5, 0.3f, 35, potionBlue);
            result["Erva"] = Plain("Erva", "Erva do Pântano", "Material comum para preparos e remédios.", ItemCategory.Material, ItemRarity.Common, 1, 1, 20, 0.1f, 4, herbModel);
            result["ChaveCripta"] = Plain("ChaveCripta", "Chave da Cripta", "Pesada, fria e marcada com um selo gasto. Abre a cripta das Terras Alagadas.",
                ItemCategory.Quest, ItemRarity.Rare, 1, 2, 1, 0.3f, 0, keyModel);
            result["Chave"] = Plain("Chave", "Chave Enferrujada", "Uma chave antiga. Deve abrir alguma porta por aqui.", ItemCategory.Quest, ItemRarity.Uncommon, 1, 2, 1, 0.2f, 0, keyModel);

            // ---------------- Armaduras e acessórios (provisórios) ----------------
            var steelM = EditorMaterials.GetOrCreate("Aco", new Color(0.62f, 0.63f, 0.66f), 0.85f, 0.55f);
            var leatherM = EditorMaterials.GetOrCreate("Couro", new Color(0.22f, 0.13f, 0.07f), 0f, 0.25f);
            var bronzeM = EditorMaterials.GetOrCreate("BronzeEnvelhecido", new Color(0.55f, 0.38f, 0.18f), 0.8f, 0.45f);
            var goldM = EditorMaterials.GetOrCreate("OuroVelho", new Color(0.8f, 0.62f, 0.25f), 0.9f, 0.6f);
            var rubyM = EditorMaterials.GetOrCreate("Rubi", new Color(0.75f, 0.05f, 0.1f), 0.2f, 0.9f, new Color(0.3f, 0f, 0f));
            var emeraldM = EditorMaterials.GetOrCreate("Esmeralda", new Color(0.1f, 0.6f, 0.3f), 0.2f, 0.9f, new Color(0f, 0.2f, 0.05f));
            var sapphireM = EditorMaterials.GetOrCreate("Safira", new Color(0.1f, 0.25f, 0.8f), 0.2f, 0.9f, new Color(0f, 0.05f, 0.3f));
            var amberM = EditorMaterials.GetOrCreate("Ambar", new Color(0.9f, 0.55f, 0.1f), 0.2f, 0.9f, new Color(0.3f, 0.15f, 0f));
            var pearlM = EditorMaterials.GetOrCreate("Perola", new Color(0.9f, 0.9f, 0.85f), 0.2f, 0.9f);

            var helmModel = Model("Modelo_Elmo", t =>
            {
                Part(t, PrimitiveType.Sphere, new Vector3(0, 0.12f, 0), new Vector3(0.22f, 0.2f, 0.22f), steelM);
                Part(t, PrimitiveType.Cube, new Vector3(0, 0.06f, -0.1f), new Vector3(0.18f, 0.06f, 0.03f), steelM);
                Part(t, PrimitiveType.Cube, new Vector3(0, 0.1f, -0.11f), new Vector3(0.03f, 0.1f, 0.02f), bronzeM);
                Part(t, PrimitiveType.Cube, new Vector3(0, 0.01f, 0), new Vector3(0.24f, 0.03f, 0.24f), steelM);
            });
            var chestModel = Model("Modelo_Couraca", t =>
            {
                Part(t, PrimitiveType.Cube, new Vector3(0, 0, 0), new Vector3(0.34f, 0.42f, 0.12f), leatherM);
                Part(t, PrimitiveType.Cube, new Vector3(-0.2f, 0.16f, 0), new Vector3(0.1f, 0.1f, 0.14f), bronzeM);
                Part(t, PrimitiveType.Cube, new Vector3(0.2f, 0.16f, 0), new Vector3(0.1f, 0.1f, 0.14f), bronzeM);
                Part(t, PrimitiveType.Cube, new Vector3(0, -0.12f, -0.065f), new Vector3(0.3f, 0.04f, 0.01f), bronzeM);
            });
            var glovesModel = Model("Modelo_Manoplas", t =>
            {
                Part(t, PrimitiveType.Cube, new Vector3(-0.07f, 0, 0), new Vector3(0.1f, 0.16f, 0.06f), bronzeM);
                Part(t, PrimitiveType.Cube, new Vector3(0.07f, 0, 0), new Vector3(0.1f, 0.16f, 0.06f), bronzeM);
                Part(t, PrimitiveType.Cube, new Vector3(-0.07f, -0.1f, 0), new Vector3(0.11f, 0.05f, 0.07f), leatherM);
                Part(t, PrimitiveType.Cube, new Vector3(0.07f, -0.1f, 0), new Vector3(0.11f, 0.05f, 0.07f), leatherM);
            });
            var legsModel = Model("Modelo_Grevas", t =>
            {
                Part(t, PrimitiveType.Cylinder, new Vector3(-0.06f, 0, 0), new Vector3(0.09f, 0.18f, 0.09f), leatherM);
                Part(t, PrimitiveType.Cylinder, new Vector3(0.06f, 0, 0), new Vector3(0.09f, 0.18f, 0.09f), leatherM);
                Part(t, PrimitiveType.Cube, new Vector3(-0.06f, -0.02f, -0.045f), new Vector3(0.07f, 0.12f, 0.02f), steelM);
                Part(t, PrimitiveType.Cube, new Vector3(0.06f, -0.02f, -0.045f), new Vector3(0.07f, 0.12f, 0.02f), steelM);
            });
            var bootsModel = Model("Modelo_Botas", t =>
            {
                Part(t, PrimitiveType.Cube, new Vector3(-0.07f, 0.06f, 0), new Vector3(0.08f, 0.16f, 0.08f), leatherM);
                Part(t, PrimitiveType.Cube, new Vector3(0.07f, 0.06f, 0), new Vector3(0.08f, 0.16f, 0.08f), leatherM);
                Part(t, PrimitiveType.Cube, new Vector3(-0.07f, -0.02f, -0.04f), new Vector3(0.08f, 0.05f, 0.14f), leatherM);
                Part(t, PrimitiveType.Cube, new Vector3(0.07f, -0.02f, -0.04f), new Vector3(0.08f, 0.05f, 0.14f), leatherM);
            });
            GameObject RingModel(string name, Material gem) => Model(name, t =>
            {
                Part(t, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.12f, 0.012f, 0.12f), goldM, new Vector3(90, 0, 0));
                Part(t, PrimitiveType.Sphere, new Vector3(0, 0.065f, 0), Vector3.one * 0.05f, gem);
            });
            var amuletModel = Model("Modelo_Amuleto", t =>
            {
                Part(t, PrimitiveType.Cube, new Vector3(-0.05f, 0.08f, 0), new Vector3(0.008f, 0.16f, 0.008f), goldM, new Vector3(0, 0, -20));
                Part(t, PrimitiveType.Cube, new Vector3(0.05f, 0.08f, 0), new Vector3(0.008f, 0.16f, 0.008f), goldM, new Vector3(0, 0, 20));
                Part(t, PrimitiveType.Cylinder, new Vector3(0, -0.02f, 0), new Vector3(0.08f, 0.01f, 0.08f), bronzeM, new Vector3(90, 0, 0));
                Part(t, PrimitiveType.Sphere, new Vector3(0, -0.02f, -0.012f), Vector3.one * 0.04f, rubyM);
            });

            StatModifier M(StatType st, float v) => new(st, v);
            result["Elmo"] = Armor("Elmo", "Elmo de Ferro", "Simples, pesado e confiável.", ArmorSlot.Head, ItemRarity.Common, 2, 2, 3f, 30, 6f, new StatModifier[0], helmModel);
            result["Couraca"] = Armor("Couraca", "Couraça de Couro Batido", "Protege bem o tronco, mas limita os movimentos.", ArmorSlot.Chest, ItemRarity.Common, 2, 3, 8f, 60, 14f,
                new[] { M(StatType.MoveSpeedPercent, -4f) }, chestModel);
            result["Manoplas"] = Armor("Manoplas", "Manoplas de Bronze", "Golpes com as mãos protegidas batem mais forte.", ArmorSlot.Hands, ItemRarity.Uncommon, 2, 2, 2f, 45, 4f,
                new[] { M(StatType.PhysicalDamagePercent, 5f) }, glovesModel);
            result["Grevas"] = Armor("Grevas", "Grevas do Pântano", "Couro tratado contra as águas venenosas.", ArmorSlot.Legs, ItemRarity.Uncommon, 2, 3, 4f, 50, 7f,
                new[] { M(StatType.ResistPoison, 10f) }, legsModel);
            result["Botas"] = Armor("Botas", "Botas de Viajante", "Leves e gastas pela estrada.", ArmorSlot.Feet, ItemRarity.Common, 2, 2, 2f, 35, 3f,
                new[] { M(StatType.MoveSpeedPercent, 6f) }, bootsModel);

            result["AnelVitalidade"] = Accessory("AnelVitalidade", "Anel de Vitalidade", "Aumenta a vida máxima.", AccessoryKind.Ring, ItemRarity.Uncommon, 80,
                new[] { M(StatType.MaxHealth, 25f) }, RingModel("Modelo_AnelVitalidade", rubyM));
            result["AnelForca"] = Accessory("AnelForca", "Anel de Força", "Aumenta o dano físico.", AccessoryKind.Ring, ItemRarity.Uncommon, 80,
                new[] { M(StatType.PhysicalDamagePercent, 10f) }, RingModel("Modelo_AnelForca", amberM));
            result["AnelMana"] = Accessory("AnelMana", "Anel de Mana", "Aumenta a mana máxima.", AccessoryKind.Ring, ItemRarity.Uncommon, 80,
                new[] { M(StatType.MaxMana, 20f) }, RingModel("Modelo_AnelMana", sapphireM));
            result["AnelResistencia"] = Accessory("AnelResistencia", "Anel de Resistência", "Reduz o dano físico e de fogo recebido.", AccessoryKind.Ring, ItemRarity.Rare, 140,
                new[] { M(StatType.ResistPhysical, 10f), M(StatType.ResistFire, 15f) }, RingModel("Modelo_AnelResistencia", pearlM));
            result["AnelAgilidade"] = Accessory("AnelAgilidade", "Anel de Agilidade", "Melhora a velocidade de movimento.", AccessoryKind.Ring, ItemRarity.Uncommon, 80,
                new[] { M(StatType.MoveSpeedPercent, 8f) }, RingModel("Modelo_AnelAgilidade", emeraldM));
            result["Amuleto"] = Accessory("Amuleto", "Amuleto de Bronze", "Um pingente antigo com uma pedra rubra.", AccessoryKind.Amulet, ItemRarity.Rare, 150,
                new[] { M(StatType.MaxHealth, 15f), M(StatType.MagicDamagePercent, 10f) }, amuletModel);

            // ---------------- Materiais que caem de inimigos (Etapa 12) ----------------
            var furM = EditorMaterials.GetOrCreate("PeloCinzento", new Color(0.28f, 0.26f, 0.24f), 0f, 0.1f);
            var boneM = EditorMaterials.GetOrCreate("Osso", new Color(0.78f, 0.74f, 0.63f), 0f, 0.25f);
            var plagueM = EditorMaterials.GetOrCreate("BrilhoPeste", new Color(0.25f, 0.6f, 0.15f), 0f, 0.5f, new Color(0.5f, 2.2f, 0.3f));
            var oldIronM = EditorMaterials.GetOrCreate("FerroVelho", new Color(0.35f, 0.34f, 0.33f), 0.8f, 0.35f);

            var peltModel = Model("Modelo_PeleLobo", t =>
            {
                Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0.34f, 0.03f, 0.26f), furM);
                Part(t, PrimitiveType.Cube, new Vector3(0f, 0f, -0.18f), new Vector3(0.06f, 0.025f, 0.12f), furM);
                Part(t, PrimitiveType.Cube, new Vector3(0.19f, 0f, 0.1f), new Vector3(0.06f, 0.02f, 0.08f), furM);
                Part(t, PrimitiveType.Cube, new Vector3(-0.19f, 0f, 0.1f), new Vector3(0.06f, 0.02f, 0.08f), furM);
            });
            var fangModel = Model("Modelo_PresaLobo", t =>
            {
                Part(t, PrimitiveType.Cube, new Vector3(0f, 0.04f, 0f), new Vector3(0.025f, 0.09f, 0.025f), boneM, new Vector3(0f, 0f, 12f));
                Part(t, PrimitiveType.Cube, new Vector3(0.012f, 0.1f, 0f), new Vector3(0.015f, 0.05f, 0.015f), boneM, new Vector3(0f, 0f, 25f));
            });
            var boneModel = Model("Modelo_OssoAntigo", t =>
            {
                Part(t, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.04f, 0.13f, 0.04f), boneM);
                Part(t, PrimitiveType.Sphere, new Vector3(0.02f, 0.14f, 0f), Vector3.one * 0.06f, boneM);
                Part(t, PrimitiveType.Sphere, new Vector3(-0.02f, 0.14f, 0f), Vector3.one * 0.06f, boneM);
                Part(t, PrimitiveType.Sphere, new Vector3(0.02f, -0.14f, 0f), Vector3.one * 0.06f, boneM);
                Part(t, PrimitiveType.Sphere, new Vector3(-0.02f, -0.14f, 0f), Vector3.one * 0.06f, boneM);
            });
            var arrowheadModel = Model("Modelo_PontaFlecha", t =>
            {
                for (var i = 0; i < 3; i++)
                    Part(t, PrimitiveType.Cube, new Vector3((i - 1) * 0.05f, 0f, 0f), new Vector3(0.03f, 0.06f, 0.01f), oldIronM, new Vector3(0f, 0f, 45f));
            });
            var essenceModel = Model("Modelo_EssenciaPeste", t =>
            {
                Part(t, PrimitiveType.Sphere, new Vector3(0, 0.07f, 0), Vector3.one * 0.1f, plagueM);
                Part(t, PrimitiveType.Sphere, new Vector3(0, 0.07f, 0), Vector3.one * 0.14f, glass);
                Part(t, PrimitiveType.Cylinder, new Vector3(0, 0.16f, 0), new Vector3(0.045f, 0.02f, 0.045f), cork);
            });

            result["PeleLobo"] = Plain("PeleLobo", "Pele de Lobo", "Couro grosso e cinzento. Bom para armaduras leves.",
                ItemCategory.Material, ItemRarity.Common, 2, 2, 5, 1f, 8, peltModel);
            result["PresaLobo"] = Plain("PresaLobo", "Presa de Lobo", "Afiada como uma adaga. Ferreiros pagam bem.",
                ItemCategory.Material, ItemRarity.Common, 1, 1, 10, 0.1f, 6, fangModel);
            result["OssoAntigo"] = Plain("OssoAntigo", "Osso Antigo", "Ainda vibra com o que o mantinha em pé.",
                ItemCategory.Material, ItemRarity.Common, 1, 2, 10, 0.3f, 3, boneModel);
            result["PontaFlecha"] = Plain("PontaFlecha", "Pontas de Flecha", "Ferro velho, ainda afiado.",
                ItemCategory.Material, ItemRarity.Common, 1, 1, 20, 0.05f, 2, arrowheadModel);
            result["EssenciaPeste"] = Plain("EssenciaPeste", "Essência da Peste",
                "Um líquido verde que pulsa devagar, como se respirasse. Vem dos corrompidos das Terras Alagadas.",
                ItemCategory.Material, ItemRarity.Rare, 1, 1, 5, 0.2f, 60, essenceModel);

            AssetDatabase.SaveAssets();
            return result;
        }

        private static ArmorData Armor(string id, string name, string desc, ArmorSlot slot, ItemRarity rarity, int w, int h, float weight, int value,
            float armor, StatModifier[] mods, GameObject model)
        {
            var data = Load<ArmorData>(id);
            data.EditorSetItem(ItemCategory.Armor, rarity, w, h, 1, weight, value, model);
            data.EditorSetArmor(slot, armor, mods);
            data.EditorSetInfo(name, desc, IconBaker.Bake(model, "Icone_" + id, w, h, new Vector3(0f, 25f, 0f)));
            return data;
        }

        private static AccessoryData Accessory(string id, string name, string desc, AccessoryKind kind, ItemRarity rarity, int value,
            StatModifier[] mods, GameObject model)
        {
            var data = Load<AccessoryData>(id);
            data.EditorSetItem(kind == AccessoryKind.Ring ? ItemCategory.Ring : ItemCategory.Amulet, rarity, 1, 1, 1, 0.1f, value, model);
            data.EditorSetAccessory(kind, mods);
            data.EditorSetInfo(name, desc, IconBaker.Bake(model, "Icone_" + id, 1, 1, new Vector3(20f, 20f, 0f)));
            return data;
        }

        private static ConsumableData Consumable(string id, string name, string desc, ItemRarity rarity, ConsumableEffect effect, float amount,
            int stack, float weight, int value, GameObject model)
        {
            var data = Load<ConsumableData>(id);
            data.EditorSetItem(ItemCategory.Consumable, rarity, 1, 1, stack, weight, value, model);
            data.EditorSetEffect(effect, amount);
            data.EditorSetInfo(name, desc, IconBaker.Bake(model, "Icone_" + id, 1, 1, new Vector3(0f, 20f, 0f)));
            return data;
        }

        private static ItemData Plain(string id, string name, string desc, ItemCategory category, ItemRarity rarity, int w, int h, int stack,
            float weight, int value, GameObject model)
        {
            var data = Load<ItemData>(id);
            data.EditorSetItem(category, rarity, w, h, stack, weight, value, model);
            data.EditorSetInfo(name, desc, IconBaker.Bake(model, "Icone_" + id, w, h, new Vector3(0f, 20f, 0f)));
            return data;
        }

        private static T Load<T>(string id) where T : ItemData
        {
            var path = $"{DataFolder}/Item_{id}.asset";
            var data = AssetDatabase.LoadAssetAtPath<T>(path);
            if (data != null) return data;
            data = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(data, path);
            return data;
        }

        private static GameObject Potion(string name, Material liquid, Material glass, Material cork) => Model(name, t =>
        {
            Part(t, PrimitiveType.Sphere, new Vector3(0, 0.07f, 0), Vector3.one * 0.14f, liquid);
            Part(t, PrimitiveType.Cylinder, new Vector3(0, 0.16f, 0), new Vector3(0.05f, 0.035f, 0.05f), glass);
            Part(t, PrimitiveType.Cylinder, new Vector3(0, 0.205f, 0), new Vector3(0.045f, 0.015f, 0.045f), cork);
        });

        private static GameObject Model(string name, System.Action<Transform> build)
        {
            var root = new GameObject(name);
            try
            {
                build(root.transform);
                return PrefabUtility.SaveAsPrefabAsset(root, $"{ModelFolder}/{name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
