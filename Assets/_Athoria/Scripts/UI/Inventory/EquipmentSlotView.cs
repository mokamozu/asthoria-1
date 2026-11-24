using Athoria.Gameplay.Inventory;
using Athoria.Gameplay.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Athoria.UI.Inventory
{
    /// <summary>Um espaço de equipamento na tela (cabeça, anel 1...).</summary>
    public sealed class EquipmentSlotView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private EquipmentSlot slot;
        [SerializeField] private Image frame;
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private Text label;

        private InventoryScreen _screen;
        private static readonly Color EmptyFrame = new(0.3f, 0.22f, 0.12f);

        public EquipmentSlot Slot => slot;

        public void Bind(InventoryScreen screen) => _screen = screen;

        public void Show(ItemData item)
        {
            icon.sprite = item != null ? item.Icon : null;
            icon.enabled = item != null && item.Icon != null;
            label.enabled = item == null;
            var c = item != null ? ItemText.RarityColor(item.Rarity) : EmptyFrame;
            frame.color = c;
            background.color = item != null ? new Color(c.r * 0.16f, c.g * 0.16f, c.b * 0.16f, 0.95f) : new Color(0.07f, 0.06f, 0.055f, 0.95f);
        }

        /// <summary>Destaque enquanto um item está na mão: verde (aceita) ou vermelho.</summary>
        public void Highlight(bool? accepts)
        {
            if (accepts == null) return;
            background.color = accepts.Value ? new Color(0.15f, 0.35f, 0.15f) : new Color(0.35f, 0.1f, 0.08f);
        }

        public void OnPointerClick(PointerEventData e) => _screen.OnEquipmentClick(this, e.button);
        public void OnPointerEnter(PointerEventData e) => _screen.OnEquipmentEnter(this);
        public void OnPointerExit(PointerEventData e) => _screen.OnEquipmentExit(this);
    }
}
