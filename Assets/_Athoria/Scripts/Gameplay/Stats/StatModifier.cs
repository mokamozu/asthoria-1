using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Athoria.Gameplay.Stats
{
    public enum StatType
    {
        [InspectorName("Vida máxima")] MaxHealth,
        [InspectorName("Vigor máximo")] MaxStamina,
        [InspectorName("Mana máxima")] MaxMana,
        [InspectorName("Armadura")] Armor,
        [InspectorName("Dano físico (%)")] PhysicalDamagePercent,
        [InspectorName("Dano mágico (%)")] MagicDamagePercent,
        [InspectorName("Velocidade (%)")] MoveSpeedPercent,
        [InspectorName("Resistência física (%)")] ResistPhysical,
        [InspectorName("Resistência a fogo (%)")] ResistFire,
        [InspectorName("Resistência a gelo (%)")] ResistFrost,
        [InspectorName("Resistência a raio (%)")] ResistLightning,
        [InspectorName("Resistência a veneno (%)")] ResistPoison,
        [InspectorName("Resistência arcana (%)")] ResistArcane
    }

    /// <summary>Bônus (positivo) ou penalidade (negativo) de um atributo.</summary>
    [Serializable]
    public struct StatModifier
    {
        public StatType stat;
        public float value;

        public StatModifier(StatType stat, float value)
        {
            this.stat = stat;
            this.value = value;
        }
    }

    public static class StatText
    {
        public static bool IsPercent(StatType s) => s >= StatType.PhysicalDamagePercent;

        public static string Name(StatType s) => s switch
        {
            StatType.MaxHealth => "Vida máxima",
            StatType.MaxStamina => "Vigor máximo",
            StatType.MaxMana => "Mana máxima",
            StatType.Armor => "Armadura",
            StatType.PhysicalDamagePercent => "Dano físico",
            StatType.MagicDamagePercent => "Dano mágico",
            StatType.MoveSpeedPercent => "Velocidade",
            StatType.ResistPhysical => "Resistência física",
            StatType.ResistFire => "Resistência a fogo",
            StatType.ResistFrost => "Resistência a gelo",
            StatType.ResistLightning => "Resistência a raio",
            StatType.ResistPoison => "Resistência a veneno",
            _ => "Resistência arcana"
        };

        /// <summary>"+20 Vida máxima" em verde, "-5% Velocidade" em vermelho (rich text).</summary>
        public static string Line(StatType s, float v, bool colored = true)
        {
            var number = $"{(v >= 0 ? "+" : "")}{v:0.#}{(IsPercent(s) ? "%" : "")}";
            var text = $"{number} {Name(s)}";
            if (!colored) return text;
            return v >= 0 ? $"<color=#8fdc7a>{text}</color>" : $"<color=#ff7a6a>{text}</color>";
        }

        public static string Lines(IEnumerable<StatModifier> mods)
        {
            var sb = new StringBuilder();
            foreach (var m in mods)
            {
                if (Mathf.Approximately(m.value, 0f)) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(Line(m.stat, m.value));
            }
            return sb.ToString();
        }
    }
}
