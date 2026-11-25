using System;
using System.Text;
using Athoria.Gameplay.Stats;
using UnityEngine;

namespace Athoria.Gameplay.Items
{
    public enum ArmorSlot
    {
        [InspectorName("Cabeça")] Head,
        [InspectorName("Peitoral")] Chest,
        [InspectorName("Mãos")] Hands,
        [InspectorName("Pernas")] Legs,
        [InspectorName("Pés")] Feet
    }

    public enum AccessoryKind
    {
        [InspectorName("Amuleto")] Amulet,
        [InspectorName("Anel")] Ring
    }

    /// <summary>
    /// Peças de armadura. Por decisão de design, armaduras são SOMENTE ESTÉTICAS:
    /// não somam armadura nem bônus (os campos ficam para um futuro sistema de visual).
    /// </summary>
    [CreateAssetMenu(menuName = "Athoria/Itens/Armadura", fileName = "NovaArmadura")]
    public sealed class ArmorData : ItemData
    {
        [Header("Armadura")]
        [SerializeField] private ArmorSlot slot;
        [SerializeField, Min(0f)] private float armor = 5f;
        [SerializeField] private StatModifier[] modifiers = Array.Empty<StatModifier>();

        public ArmorSlot Slot => slot;
        public float Armor => armor * Quality;
        public StatModifier[] Modifiers => modifiers;

        public override ItemCategory ItemType => slot switch
        {
            ArmorSlot.Head => ItemCategory.Helmet,
            ArmorSlot.Hands => ItemCategory.Gloves,
            ArmorSlot.Feet => ItemCategory.Boots,
            _ => ItemCategory.Armor
        };

        public override string TooltipStats => "Peça estética (sem atributos)";

#if UNITY_EDITOR
        public void EditorSetArmor(ArmorSlot newSlot, float newArmor, StatModifier[] newModifiers)
        {
            slot = newSlot;
            armor = newArmor;
            modifiers = newModifiers ?? Array.Empty<StatModifier>();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
