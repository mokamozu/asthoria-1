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

    /// <summary>Peças de armadura: armadura + bônus/penalidades.</summary>
    [CreateAssetMenu(menuName = "Athoria/Itens/Armadura", fileName = "NovaArmadura")]
    public sealed class ArmorData : ItemData
    {
        [Header("Armadura")]
        [SerializeField] private ArmorSlot slot;
        [SerializeField, Min(0f)] private float armor = 5f;
        [SerializeField] private StatModifier[] modifiers = Array.Empty<StatModifier>();

        public ArmorSlot Slot => slot;
        public float Armor => armor;
        public StatModifier[] Modifiers => modifiers;

        public override ItemCategory ItemType => slot switch
        {
            ArmorSlot.Head => ItemCategory.Helmet,
            ArmorSlot.Hands => ItemCategory.Gloves,
            ArmorSlot.Feet => ItemCategory.Boots,
            _ => ItemCategory.Armor
        };

        public override string TooltipStats
        {
            get
            {
                var sb = new StringBuilder($"Armadura {armor:0}");
                var extra = StatText.Lines(modifiers);
                if (extra.Length > 0) sb.Append('\n').Append(extra);
                return sb.ToString();
            }
        }

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

    /// <summary>Amuletos e anéis: efeitos passivos enquanto equipados.</summary>
    [CreateAssetMenu(menuName = "Athoria/Itens/Acessório (anel/amuleto)", fileName = "NovoAcessorio")]
    public sealed class AccessoryData : ItemData
    {
        [Header("Acessório")]
        [SerializeField] private AccessoryKind kind = AccessoryKind.Ring;
        [SerializeField] private StatModifier[] modifiers = Array.Empty<StatModifier>();

        public AccessoryKind Kind => kind;
        public StatModifier[] Modifiers => modifiers;
        public override ItemCategory ItemType => kind == AccessoryKind.Ring ? ItemCategory.Ring : ItemCategory.Amulet;
        public override string TooltipStats => StatText.Lines(modifiers);

#if UNITY_EDITOR
        public void EditorSetAccessory(AccessoryKind newKind, StatModifier[] newModifiers)
        {
            kind = newKind;
            modifiers = newModifiers ?? Array.Empty<StatModifier>();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
