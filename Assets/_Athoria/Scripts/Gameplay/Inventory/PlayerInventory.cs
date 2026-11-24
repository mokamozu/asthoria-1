using System;
using Athoria.Core.Events;
using Athoria.Gameplay.Items;
using Athoria.Gameplay.Player;
using Athoria.Gameplay.World;
using UnityEngine;

namespace Athoria.Gameplay.Inventory
{
    public readonly struct InventoryChanged : IGameEvent { }

    /// <summary>
    /// Inventário do jogador: grade de itens, ouro e peso. Acima da capacidade
    /// de carga o jogador fica mais lento.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInventory : MonoBehaviour
    {
        [Serializable]
        public struct StartingItem
        {
            public ItemData item;
            [Min(1)] public int quantity;
        }

        [SerializeField, Min(1)] private int width = 10;
        [SerializeField, Min(1)] private int height = 6;
        [SerializeField, Min(1f)] private float carryCapacity = 60f;
        [SerializeField, Range(0.1f, 1f)] private float overweightSpeedMultiplier = 0.6f;
        [SerializeField] private StartingItem[] startingItems = Array.Empty<StartingItem>();
        [SerializeField, Min(0)] private int startingGold;

        private PlayerController _controller;
        private PlayerEquipment _equipment;

        public InventoryGrid Grid { get; private set; }
        public int Gold { get; private set; }
        public float Weight => Grid.TotalWeight;
        public float Capacity => carryCapacity;
        public bool IsOverweight => Weight > carryCapacity;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _equipment = GetComponent<PlayerEquipment>();
            Grid = new InventoryGrid(width, height);
            Grid.Changed += OnGridChanged;
            Gold = startingGold;
            foreach (var s in startingItems)
                if (s.item != null) Grid.Add(s.item, Mathf.Max(1, s.quantity));
        }

        private void Start() => OnGridChanged();

        private void OnGridChanged()
        {
            if (_controller != null) _controller.EncumbranceMultiplier = IsOverweight ? overweightSpeedMultiplier : 1f;
            EventBus.Publish(new InventoryChanged());
        }

        /// <summary>Adiciona itens; retorna a quantidade que não coube.</summary>
        public int AddItem(ItemData item, int quantity = 1)
        {
            var left = Grid.Add(item, quantity);
            var added = quantity - left;
            if (added > 0)
                EventBus.Publish(new GameMessage(added > 1 ? $"+{added} {item.DisplayName}" : $"+ {item.DisplayName}"));
            if (left > 0) EventBus.Publish(new GameMessage("Inventário cheio"));
            return left;
        }

        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            Gold += amount;
            EventBus.Publish(new GameMessage($"+{amount} de ouro"));
            EventBus.Publish(new InventoryChanged());
        }

        public bool SpendGold(int amount)
        {
            if (amount < 0 || Gold < amount) return false;
            Gold -= amount;
            EventBus.Publish(new InventoryChanged());
            return true;
        }

        /// <summary>Botão direito no item: usa consumível ou equipa (arma, escudo, armadura, anel, amuleto).</summary>
        public void UseOrEquip(GridEntry entry)
        {
            if (entry == null) return;
            var item = entry.Stack.Item;
            if (item is ConsumableData consumable)
            {
                if (!consumable.Use(gameObject))
                {
                    EventBus.Publish(new GameMessage("Não teria efeito agora"));
                    return;
                }
                entry.Stack.Quantity--;
                if (entry.Stack.Quantity <= 0) Grid.Remove(entry);
                else Grid.NotifyChanged();
                return;
            }

            if (_equipment != null && _equipment.TryGetSlotFor(item, out var slot) && _equipment.CanEquip(item, slot))
            {
                Grid.Remove(entry);
                var previous = _equipment.Equip(item, slot);
                ReturnToGrid(previous);
                EventBus.Publish(new GameMessage($"Equipou {item.DisplayName}"));
                return;
            }

            EventBus.Publish(new GameMessage("Este item não pode ser usado"));
        }

        /// <summary>Tira um item do equipamento e guarda na grade. Falha se não houver espaço.</summary>
        public bool UnequipToGrid(EquipmentSlot slot)
        {
            var item = _equipment != null ? _equipment.Get(slot) : null;
            if (item == null) return false;
            if (!Grid.FindFree(item.GridWidth, item.GridHeight, out _, out _))
            {
                EventBus.Publish(new GameMessage("Sem espaço no inventário"));
                return false;
            }
            _equipment.Unequip(slot);
            Grid.Add(item, 1);
            return true;
        }

        /// <summary>Guarda um item na grade; se não couber, larga no chão.</summary>
        public void ReturnToGrid(ItemData item)
        {
            if (item == null) return;
            if (Grid.Add(item, 1) > 0) Drop(new ItemStack(item, 1));
        }

        /// <summary>Larga a pilha no chão, à frente do jogador.</summary>
        public void Drop(ItemStack stack)
        {
            if (stack == null || stack.Quantity <= 0) return;
            var pos = transform.position + transform.forward * 1.2f + Vector3.up * 0.5f;
            if (Physics.Raycast(pos, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore)) pos = hit.point;
            WorldItem.Spawn(stack.Item, stack.Quantity, pos, Quaternion.Euler(0f, transform.eulerAngles.y + 90f, 0f));
            EventBus.Publish(new InventoryChanged());
        }
    }
}
