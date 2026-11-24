using System;
using System.Collections.Generic;
using Athoria.Core.Events;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Items;
using Athoria.Gameplay.Stats;
using UnityEngine;

namespace Athoria.Gameplay.Inventory
{
    public enum EquipmentSlot
    {
        Head, Chest, Hands, Legs, Feet,
        MainWeapon, SecondaryWeapon, Shield,
        Amulet, Ring1, Ring2, Ring3, Ring4
    }

    public readonly struct EquipmentChanged : IGameEvent { }

    /// <summary>
    /// Equipamento do jogador (estilo Dark Souls / Diablo). Armas usam os
    /// atalhos do WeaponLoadout (principal = 1, secundária = 2, escudo = mão
    /// secundária). Armaduras e acessórios somam bônus no PlayerStats.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerEquipment : MonoBehaviour
    {
        public const int SlotCount = 13;

        [SerializeField] private WeaponLoadout loadout;
        [SerializeField] private PlayerStats stats;

        private readonly ItemData[] _gear = new ItemData[SlotCount];
        private readonly List<StatModifier> _scratch = new();

        public event Action Changed;

        private void Awake()
        {
            if (loadout == null) loadout = GetComponent<WeaponLoadout>();
            if (stats == null) stats = GetComponent<PlayerStats>();
        }

        private void OnEnable() { if (loadout != null) loadout.Changed += Notify; }
        private void OnDisable() { if (loadout != null) loadout.Changed -= Notify; }
        private void Start() => Recalculate();

        public static string SlotName(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.Head => "Cabeça",
            EquipmentSlot.Chest => "Peitoral",
            EquipmentSlot.Hands => "Mãos",
            EquipmentSlot.Legs => "Pernas",
            EquipmentSlot.Feet => "Pés",
            EquipmentSlot.MainWeapon => "Arma principal",
            EquipmentSlot.SecondaryWeapon => "Arma secundária",
            EquipmentSlot.Shield => "Escudo",
            EquipmentSlot.Amulet => "Amuleto",
            _ => "Anel"
        };

        public ItemData Get(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.MainWeapon => loadout != null ? loadout.GetQuickSlot(0) : null,
            EquipmentSlot.SecondaryWeapon => loadout != null ? loadout.GetQuickSlot(1) : null,
            EquipmentSlot.Shield => loadout != null ? loadout.OffHand : null,
            _ => _gear[(int)slot]
        };

        public bool CanEquip(ItemData item, EquipmentSlot slot)
        {
            if (item == null) return false;
            switch (slot)
            {
                case EquipmentSlot.MainWeapon:
                case EquipmentSlot.SecondaryWeapon:
                    return item is WeaponData w && !w.IsShield;
                case EquipmentSlot.Shield:
                    return item is WeaponData s && s.IsShield;
                case EquipmentSlot.Amulet:
                    return item is AccessoryData a && a.Kind == AccessoryKind.Amulet;
                case EquipmentSlot.Ring1:
                case EquipmentSlot.Ring2:
                case EquipmentSlot.Ring3:
                case EquipmentSlot.Ring4:
                    return item is AccessoryData r && r.Kind == AccessoryKind.Ring;
                default:
                    return item is ArmorData armor && (int)armor.Slot == (int)slot;
            }
        }

        /// <summary>Melhor espaço para equipar o item (anéis: primeiro anel vazio).</summary>
        public bool TryGetSlotFor(ItemData item, out EquipmentSlot slot)
        {
            switch (item)
            {
                case ArmorData armor:
                    slot = (EquipmentSlot)(int)armor.Slot;
                    return true;
                case AccessoryData acc when acc.Kind == AccessoryKind.Amulet:
                    slot = EquipmentSlot.Amulet;
                    return true;
                case AccessoryData:
                    for (var s = EquipmentSlot.Ring1; s <= EquipmentSlot.Ring4; s++)
                        if (Get(s) == null) { slot = s; return true; }
                    slot = EquipmentSlot.Ring1;
                    return true;
                case WeaponData w when w.IsShield:
                    slot = EquipmentSlot.Shield;
                    return true;
                case WeaponData:
                    slot = loadout != null && loadout.ActiveSlot == 1 ? EquipmentSlot.SecondaryWeapon : EquipmentSlot.MainWeapon;
                    return true;
                default:
                    slot = default;
                    return false;
            }
        }

        /// <summary>Equipa e devolve o item que estava no espaço (ou null).</summary>
        public ItemData Equip(ItemData item, EquipmentSlot slot)
        {
            if (!CanEquip(item, slot)) return item;
            var previous = Get(slot);
            Set(slot, item);
            if (slot == EquipmentSlot.MainWeapon || slot == EquipmentSlot.SecondaryWeapon)
            {
                var index = slot == EquipmentSlot.MainWeapon ? 0 : 1;
                if (loadout.ActiveSlot != index) loadout.SelectSlot(index);
            }
            return previous;
        }

        public ItemData Unequip(EquipmentSlot slot)
        {
            var previous = Get(slot);
            if (previous != null) Set(slot, null);
            return previous;
        }

        private void Set(EquipmentSlot slot, ItemData item)
        {
            switch (slot)
            {
                case EquipmentSlot.MainWeapon: loadout.SetQuickSlot(0, item as WeaponData); return;   // loadout avisa via Changed
                case EquipmentSlot.SecondaryWeapon: loadout.SetQuickSlot(1, item as WeaponData); return;
                case EquipmentSlot.Shield: loadout.SetOffHand(item as WeaponData); return;
            }
            _gear[(int)slot] = item;
            Recalculate();
            Notify();
        }

        /// <summary>Soma armadura e bônus de tudo que está equipado.</summary>
        public void CollectModifiers(List<StatModifier> into)
        {
            into.Clear();
            foreach (var item in _gear)
            {
                switch (item)
                {
                    case ArmorData armor:
                        into.Add(new StatModifier(StatType.Armor, armor.Armor));
                        into.AddRange(armor.Modifiers);
                        break;
                    case AccessoryData acc:
                        into.AddRange(acc.Modifiers);
                        break;
                }
            }
        }

        private void Recalculate()
        {
            if (stats == null) return;
            CollectModifiers(_scratch);
            stats.SetModifiers(this, _scratch);
        }

        private void Notify()
        {
            Changed?.Invoke();
            EventBus.Publish(new EquipmentChanged());
        }

        /// <summary>Estado salvo: um item (ou null) por espaço de armadura/acessório.</summary>
        public ItemData[] GetGearSnapshot() => (ItemData[])_gear.Clone();
    }
}
