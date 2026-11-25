using System.Collections.Generic;
using System.Text;
using Athoria.Gameplay.Items;
using Athoria.Gameplay.Stats;
using UnityEngine;

namespace Athoria.Gameplay.Loot
{
    /// <summary>Resultado de um sorteio: um item (já com raridade) ou ouro.</summary>
    public readonly struct LootDrop
    {
        public readonly ItemData Item;
        public readonly int Quantity;
        public readonly int Gold;

        public LootDrop(ItemData item, int quantity)
        {
            Item = item;
            Quantity = quantity;
            Gold = 0;
        }

        public LootDrop(int gold)
        {
            Item = null;
            Quantity = 0;
            Gold = gold;
        }

        public bool IsGold => Gold > 0;
    }

    /// <summary>
    /// Sorteio de loot no estilo Diablo: escolhe itens da tabela, sorteia a
    /// raridade e gera os bônus (afixos). Quanto mais raro, mais bônus, bônus
    /// maiores e mais dano/armadura base.
    ///   Comum 0 bônus · Incomum 1 · Raro 2 · Épico 3 · Lendário 4
    /// </summary>
    public static class LootRoller
    {
        // Chance base de cada raridade (Comum, Incomum, Raro, Épico, Lendário).
        private static readonly float[] RarityWeights = { 60f, 25f, 10f, 4f, 1f };
        private static readonly float[] QualityByRarity = { 1f, 1.1f, 1.2f, 1.35f, 1.5f };
        private static readonly float[] ValueByRarity = { 1f, 1.6f, 2.5f, 4f, 7f };

        private readonly struct Affix
        {
            public readonly StatType Stat;
            public readonly float Min, Max;
            public readonly string Suffix;

            public Affix(StatType stat, float min, float max, string suffix)
            {
                Stat = stat;
                Min = min;
                Max = max;
                Suffix = suffix;
            }
        }

        // Valores para o 1º nível de raridade; sobem 50% a cada nível acima.
        private static readonly Affix Health = new(StatType.MaxHealth, 5f, 10f, "do Urso");
        private static readonly Affix Stamina = new(StatType.MaxStamina, 5f, 10f, "do Lobo");
        private static readonly Affix Mana = new(StatType.MaxMana, 5f, 10f, "do Sábio");
        private static readonly Affix ArmorAffix = new(StatType.Armor, 2f, 4f, "da Muralha");
        private static readonly Affix Physical = new(StatType.PhysicalDamagePercent, 3f, 6f, "da Fúria");
        private static readonly Affix Magic = new(StatType.MagicDamagePercent, 3f, 6f, "do Arcano");
        private static readonly Affix Speed = new(StatType.MoveSpeedPercent, 1.5f, 3f, "da Raposa");
        private static readonly Affix ResPhysical = new(StatType.ResistPhysical, 3f, 6f, "do Escudo");
        private static readonly Affix ResFire = new(StatType.ResistFire, 4f, 8f, "da Brasa");
        private static readonly Affix ResFrost = new(StatType.ResistFrost, 4f, 8f, "do Inverno");
        private static readonly Affix ResLightning = new(StatType.ResistLightning, 4f, 8f, "da Tempestade");
        private static readonly Affix ResPoison = new(StatType.ResistPoison, 4f, 8f, "do Pântano");
        private static readonly Affix ResArcane = new(StatType.ResistArcane, 4f, 8f, "do Selo");

        private static readonly List<Affix> Pool = new();

        /// <summary>Só equipamentos ganham raridade e bônus; poções e materiais não.</summary>
        public static bool CanHaveRarity(ItemData item) => item is WeaponData or ArmorData or AccessoryData;

        /// <summary>Sorteia a tabela inteira. A lista é limpa antes.</summary>
        public static void Roll(LootTable table, List<LootDrop> into)
        {
            into.Clear();
            if (table == null) return;

            var guaranteedMin = (ItemRarity)Mathf.Max((int)table.minimumRarity, (int)table.guaranteedMinimumRarity);
            if (table.guaranteed != null)
                foreach (var g in table.guaranteed) Add(table, g, guaranteedMin, into);

            for (var i = 0; i < table.guaranteedPicks; i++)
            {
                var entry = Pick(table, false);
                if (entry != null) Add(table, entry, guaranteedMin, into);
            }

            var rolls = Random.Range(table.rolls.x, Mathf.Max(table.rolls.x, table.rolls.y) + 1);
            for (var i = 0; i < rolls; i++)
            {
                var entry = Pick(table, true);
                if (entry != null) Add(table, entry, table.minimumRarity, into);
            }

            if (table.gold.y > 0 && Random.value < table.goldChance)
            {
                var amount = Random.Range(table.gold.x, table.gold.y + 1);
                if (amount > 0) into.Add(new LootDrop(amount));
            }
        }

        /// <summary>
        /// As cavaleiras têm armas fixas e as armaduras são só estéticas: armas, escudos e
        /// armaduras não caem mais de inimigos nem de baús.
        /// </summary>
        public static bool IsDroppable(ItemData item) => item != null && item is not WeaponData && item is not ArmorData;

        private static LootEntry Pick(LootTable table, bool allowNothing)
        {
            var total = allowNothing ? table.nothingWeight : 0f;
            if (table.entries != null)
                foreach (var e in table.entries) if (IsDroppable(e?.item)) total += e.weight;
            if (total <= 0f) return null;

            var roll = Random.value * total;
            if (table.entries != null)
                foreach (var e in table.entries)
                {
                    if (!IsDroppable(e?.item)) continue;
                    roll -= e.weight;
                    if (roll <= 0f) return e;
                }
            return null;   // caiu no "nada"
        }

        private static void Add(LootTable table, LootEntry entry, ItemRarity minimum, List<LootDrop> into)
        {
            if (!IsDroppable(entry?.item)) return;
            var quantity = Mathf.Max(1, Random.Range(entry.quantity.x, Mathf.Max(entry.quantity.x, entry.quantity.y) + 1));
            if (!CanHaveRarity(entry.item))
            {
                into.Add(new LootDrop(entry.item, quantity));
                return;
            }
            // Equipamentos não empilham: cada um tem a sua raridade.
            for (var i = 0; i < quantity; i++)
                into.Add(new LootDrop(RollItem(entry.item, RollRarity(minimum, table.maximumRarity, table.rarityBonus)), 1));
        }

        public static ItemRarity RollRarity(ItemRarity minimum, ItemRarity maximum, float bonus)
        {
            var lo = (int)minimum;
            var hi = Mathf.Max(lo, (int)maximum);
            var total = 0f;
            for (var r = lo; r <= hi; r++) total += Weight(r, bonus);
            var roll = Random.value * total;
            for (var r = lo; r <= hi; r++)
            {
                roll -= Weight(r, bonus);
                if (roll <= 0f) return (ItemRarity)r;
            }
            return (ItemRarity)hi;
        }

        private static float Weight(int rarity, float bonus) => RarityWeights[rarity] * (rarity > 0 ? 1f + bonus : 1f);

        /// <summary>Gera a cópia sorteada de um equipamento com a raridade dada.</summary>
        public static ItemData RollItem(ItemData baseItem, ItemRarity rarity)
        {
            var tier = (int)rarity;
            BuildPool(baseItem.BaseItem);

            var bonuses = new List<StatModifier>();
            string suffix = null;
            for (var i = 0; i < tier && Pool.Count > 0; i++)
            {
                var index = Random.Range(0, Pool.Count);
                var affix = Pool[index];
                Pool.RemoveAt(index);
                var scale = 1f + 0.5f * (tier - 1);
                var value = Mathf.Max(1f, Mathf.Round(Random.Range(affix.Min, affix.Max) * scale));
                bonuses.Add(new StatModifier(affix.Stat, value));
                suffix ??= affix.Suffix;   // o nome vem do primeiro bônus
            }

            var quality = QualityByRarity[tier] * Random.Range(0.95f, 1.05f);
            var name = suffix == null ? baseItem.BaseItem.DisplayName : $"{baseItem.BaseItem.DisplayName} {suffix}";
            return baseItem.CreateRolled(rarity, quality, bonuses.ToArray(), name, ValueByRarity[tier]);
        }

        /// <summary>Que bônus fazem sentido para cada tipo de item.</summary>
        private static void BuildPool(ItemData item)
        {
            Pool.Clear();
            switch (item)
            {
                case WeaponData w when w.IsShield:
                    Pool.AddRange(new[] { ArmorAffix, ResPhysical, ResFire, ResFrost, ResLightning, ResPoison, Health });
                    break;
                case WeaponData w when w.Category == WeaponCategory.Magic:
                    Pool.AddRange(new[] { Magic, Mana, Stamina, Health, ResArcane });
                    break;
                case WeaponData:
                    Pool.AddRange(new[] { Physical, Stamina, Health, Speed });
                    break;
                case ArmorData a:
                    Pool.AddRange(new[] { Health, ArmorAffix, Stamina, ResPhysical, ResFire, ResFrost, ResLightning, ResPoison, ResArcane });
                    if (a.Slot == ArmorSlot.Feet) Pool.Add(Speed);
                    if (a.Slot == ArmorSlot.Hands) Pool.Add(Physical);
                    break;
                default:   // anéis e amuletos: qualquer bônus
                    Pool.AddRange(new[] { Health, Stamina, Mana, ArmorAffix, Physical, Magic, Speed, ResPhysical, ResFire, ResFrost, ResLightning, ResPoison, ResArcane });
                    break;
            }
        }

        /// <summary>Texto curto do que caiu (para mensagens/depuração).</summary>
        public static string Describe(List<LootDrop> drops)
        {
            var sb = new StringBuilder();
            foreach (var d in drops)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(d.IsGold ? $"{d.Gold} ouro" : d.Quantity > 1 ? $"{d.Item.DisplayName} x{d.Quantity}" : d.Item.DisplayName);
            }
            return sb.ToString();
        }
    }
}
