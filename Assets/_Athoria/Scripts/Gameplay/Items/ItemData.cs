using System;
using Athoria.Core.Data;
using Athoria.Gameplay.Stats;
using UnityEngine;

namespace Athoria.Gameplay.Items
{
    public enum ItemCategory
    {
        [InspectorName("Arma")] Weapon,
        [InspectorName("Armadura")] Armor,
        [InspectorName("Capacete")] Helmet,
        [InspectorName("Luvas")] Gloves,
        [InspectorName("Botas")] Boots,
        [InspectorName("Escudo")] Shield,
        [InspectorName("Amuleto")] Amulet,
        [InspectorName("Anel")] Ring,
        [InspectorName("Consumível")] Consumable,
        [InspectorName("Material")] Material,
        [InspectorName("Item de missão")] Quest
    }

    public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary }

    public static class ItemText
    {
        public static string Category(ItemCategory c) => c switch
        {
            ItemCategory.Weapon => "Arma",
            ItemCategory.Armor => "Armadura",
            ItemCategory.Helmet => "Capacete",
            ItemCategory.Gloves => "Luvas",
            ItemCategory.Boots => "Botas",
            ItemCategory.Shield => "Escudo",
            ItemCategory.Amulet => "Amuleto",
            ItemCategory.Ring => "Anel",
            ItemCategory.Consumable => "Consumível",
            ItemCategory.Material => "Material",
            _ => "Item de missão"
        };

        public static string Rarity(ItemRarity r) => r switch
        {
            ItemRarity.Common => "Comum",
            ItemRarity.Uncommon => "Incomum",
            ItemRarity.Rare => "Raro",
            ItemRarity.Epic => "Épico",
            _ => "Lendário"
        };

        public static Color RarityColor(ItemRarity r) => r switch
        {
            ItemRarity.Common => new Color(0.82f, 0.8f, 0.76f),
            ItemRarity.Uncommon => new Color(0.4f, 0.85f, 0.4f),
            ItemRarity.Rare => new Color(0.35f, 0.6f, 1f),
            ItemRarity.Epic => new Color(0.75f, 0.4f, 1f),
            _ => new Color(1f, 0.6f, 0.15f)
        };
    }

    /// <summary>
    /// Item base: tudo que pode ir para o inventário. Tamanho na grade em
    /// células (estilo Diablo II), pilha máxima, peso, valor e raridade.
    /// </summary>
    [CreateAssetMenu(menuName = "Athoria/Itens/Item", fileName = "NovoItem")]
    public class ItemData : GameData
    {
        [Header("Item")]
        [SerializeField] private ItemCategory itemCategory = ItemCategory.Material;
        [SerializeField] private ItemRarity rarity;
        [SerializeField, Min(1)] private int gridWidth = 1;
        [SerializeField, Min(1)] private int gridHeight = 1;
        [SerializeField, Min(1)] private int maxStack = 1;
        [SerializeField, Min(0f)] private float weight = 1f;
        [SerializeField, Min(0)] private int value = 1;
        [SerializeField, Tooltip("Modelo usado no chão e para gerar o ícone.")] private GameObject worldModel;

        public virtual ItemCategory ItemType => itemCategory;
        public ItemRarity Rarity => rarity;
        public int GridWidth => gridWidth;
        public int GridHeight => gridHeight;
        public int MaxStack => maxStack;
        public bool Stackable => maxStack > 1;
        public float Weight => weight;
        public int Value => value;
        public virtual GameObject WorldModel => worldModel;

        /// <summary>Linhas extras de atributos mostradas na dica (tooltip).</summary>
        public virtual string TooltipStats => string.Empty;

        // ---------------- Item sorteado (loot) ----------------
        // Um item que cai de inimigos/baús é uma CÓPIA do item base com raridade,
        // qualidade (multiplica dano/armadura) e bônus próprios. Para o save
        // basta guardar: Id do base + raridade + qualidade + bônus.

        [SerializeField, HideInInspector] private ItemData baseItem;
        [SerializeField, HideInInspector] private float quality = 1f;
        [SerializeField, HideInInspector] private StatModifier[] bonusModifiers = Array.Empty<StatModifier>();

        /// <summary>O asset original (ele mesmo, se não for sorteado).</summary>
        public ItemData BaseItem => baseItem != null ? baseItem : this;
        public bool IsRolled => baseItem != null;
        /// <summary>Multiplicador de dano (armas) e armadura (armaduras). 1 = normal.</summary>
        public float Quality => quality > 0f ? quality : 1f;
        /// <summary>Bônus sorteados (afixos). Vazio em itens normais.</summary>
        public StatModifier[] BonusModifiers => bonusModifiers ?? Array.Empty<StatModifier>();

        /// <summary>Atributos + bônus sorteados, para a dica do item.</summary>
        public string FullTooltipStats
        {
            get
            {
                var stats = TooltipStats;
                var bonus = StatText.Lines(BonusModifiers);
                if (bonus.Length == 0) return stats;
                return stats.Length == 0 ? bonus : stats + "\n" + bonus;
            }
        }

        /// <summary>Cria a cópia sorteada deste item (sempre a partir do original).</summary>
        public ItemData CreateRolled(ItemRarity newRarity, float newQuality, StatModifier[] bonuses, string newName, float valueMultiplier)
        {
            var original = BaseItem;
            var copy = Instantiate(original);
            copy.name = original.name + " (" + newRarity + ")";
            copy.baseItem = original;
            copy.rarity = newRarity;
            copy.quality = Mathf.Max(0.1f, newQuality);
            copy.bonusModifiers = bonuses ?? Array.Empty<StatModifier>();
            copy.value = Mathf.Max(1, Mathf.RoundToInt(original.value * valueMultiplier));
            copy.ApplyRuntimeName(newName);
            return copy;
        }

#if UNITY_EDITOR
        public void EditorSetItem(ItemCategory newCategory, ItemRarity newRarity, int width, int height, int stack, float newWeight, int newValue, GameObject model = null)
        {
            itemCategory = newCategory;
            rarity = newRarity;
            gridWidth = width;
            gridHeight = height;
            maxStack = stack;
            weight = newWeight;
            value = newValue;
            if (model != null) worldModel = model;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
