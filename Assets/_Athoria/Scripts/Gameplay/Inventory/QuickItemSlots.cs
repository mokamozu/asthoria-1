using Athoria.Core.Events;
using Athoria.Gameplay.Items;
using Athoria.Gameplay.Player;
using UnityEngine;

namespace Athoria.Gameplay.Inventory
{
    public readonly struct QuickItemsChanged : IGameEvent
    {
        public readonly ItemData Slot3, Slot4;
        public readonly int Count3, Count4;

        public QuickItemsChanged(ItemData slot3, int count3, ItemData slot4, int count4)
        {
            Slot3 = slot3; Count3 = count3;
            Slot4 = slot4; Count4 = count4;
        }
    }

    /// <summary>
    /// Atalhos de consumíveis nas teclas 3 e 4. O atalho guarda o TIPO de item;
    /// usar consome uma unidade do inventário. Trocar: no inventário, passe o
    /// mouse num consumível e aperte 3 ou 4.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuickItemSlots : MonoBehaviour
    {
        public const int Count = 2;
        /// <summary>Índice do atalho de teclado (0-3) do primeiro slot de item.</summary>
        public const int FirstHotbarIndex = 2;

        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private ConsumableData[] slots = new ConsumableData[Count];
        private Athoria.Gameplay.Combat.PlayerWeaponView _view;
        private bool _using;

        public ConsumableData Get(int index) => index >= 0 && index < Count ? slots[index] : null;

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            _view = GetComponentInChildren<Athoria.Gameplay.Combat.PlayerWeaponView>(true);
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
            if (slots == null || slots.Length != Count) System.Array.Resize(ref slots, Count);
        }

        private void OnEnable()
        {
            if (input != null) input.HotbarPressed += OnHotbar;
            EventBus.Subscribe<InventoryChanged>(OnInventoryChanged);
        }

        private void OnDisable()
        {
            if (input != null) input.HotbarPressed -= OnHotbar;
            EventBus.Unsubscribe<InventoryChanged>(OnInventoryChanged);
        }

        private void Start() => Publish();

        private void OnInventoryChanged(InventoryChanged _) => Publish();

        public void Assign(int index, ConsumableData item, bool silent = false)
        {
            if (index < 0 || index >= Count) return;
            slots[index] = item;
            if (!silent) EventBus.Publish(new GameMessage($"Atalho {index + FirstHotbarIndex + 1}: {(item != null ? item.DisplayName : "vazio")}"));
            Publish();
        }

        private void OnHotbar(int hotbarIndex)
        {
            var index = hotbarIndex - FirstHotbarIndex;
            if (index < 0 || index >= Count) return;
            Use(index);
        }

        public void Use(int index)
        {
            var item = Get(index);
            if (item == null || inventory == null || _using) return;
            foreach (var entry in inventory.Grid.Entries)
            {
                if (entry.Stack.Item != item) continue;
                // Leva o frasco à boca; o efeito acontece no gole.
                _using = true;
                void Drink()
                {
                    _using = false;
                    foreach (var e in inventory.Grid.Entries)
                        if (e.Stack.Item == item) { inventory.UseOrEquip(e); return; }
                }
                if (_view == null || !_view.PlayAction(Athoria.Gameplay.Combat.ViewModelActionId.UseConsumable, false, Drink)) { if (_using) Drink(); }
                return;
            }
            EventBus.Publish(new GameMessage($"Sem {item.DisplayName}"));
        }

        private void Publish()
        {
            if (inventory == null || inventory.Grid == null) return;
            EventBus.Publish(new QuickItemsChanged(
                slots[0], slots[0] != null ? inventory.Grid.Count(slots[0]) : 0,
                slots[1], slots[1] != null ? inventory.Grid.Count(slots[1]) : 0));
        }
    }
}
