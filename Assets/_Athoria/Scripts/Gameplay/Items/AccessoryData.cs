using System;
using System.Text;
using Athoria.Gameplay.Stats;
using UnityEngine;

namespace Athoria.Gameplay.Items
{
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
