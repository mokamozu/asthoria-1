using System;
using Athoria.Core.Data;
using Athoria.Gameplay.Items;
using UnityEngine;

namespace Athoria.Gameplay.Loot
{
    /// <summary>Uma linha da tabela: qual item, com que peso e em que quantidade.</summary>
    [Serializable]
    public sealed class LootEntry
    {
        public ItemData item;
        [Min(0f), Tooltip("Chance relativa às outras linhas.")] public float weight = 1f;
        public Vector2Int quantity = new(1, 1);
    }

    /// <summary>
    /// Tabela de loot de um inimigo ou baú. Cada sorteio escolhe UMA linha
    /// pelo peso (ou nada); os itens garantidos sempre caem. Armas, armaduras e
    /// acessórios ganham raridade e bônus sorteados (ver LootRoller).
    /// </summary>
    [CreateAssetMenu(menuName = "Athoria/Loot/Tabela de loot", fileName = "Loot_")]
    public sealed class LootTable : GameData
    {
        [Header("Sorteios")]
        [Tooltip("Quantos sorteios fazer (mín, máx).")]
        public Vector2Int rolls = new(1, 1);
        [Min(0f), Tooltip("Peso de \"não cair nada\" em cada sorteio.")]
        public float nothingWeight;
        public LootEntry[] entries = Array.Empty<LootEntry>();

        [Header("Sempre cai")]
        public LootEntry[] guaranteed = Array.Empty<LootEntry>();
        [Min(0), Tooltip("Sorteios extras da lista que nunca dão \"nada\" e usam a raridade mínima dos garantidos.")]
        public int guaranteedPicks;

        [Header("Ouro")]
        [Range(0f, 1f)] public float goldChance = 1f;
        public Vector2Int gold;

        [Header("Raridade (armas, armaduras, acessórios)")]
        public ItemRarity minimumRarity = ItemRarity.Common;
        public ItemRarity maximumRarity = ItemRarity.Legendary;
        [Tooltip("Raridade mínima só para os itens garantidos (ex.: chefe sempre solta Raro ou melhor).")]
        public ItemRarity guaranteedMinimumRarity = ItemRarity.Common;
        [Min(0f), Tooltip("Aumenta a chance de raridades altas (0 = normal, 1 = dobro).")]
        public float rarityBonus;
    }
}
