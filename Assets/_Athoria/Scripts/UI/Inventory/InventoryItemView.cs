using Athoria.Gameplay.Inventory;
using Athoria.Gameplay.Items;
using UnityEngine;
using UnityEngine.UI;

namespace Athoria.UI.Inventory
{
    /// <summary>Desenho de um item na grade: borda da raridade, ícone e quantidade. Não recebe cliques.</summary>
    public sealed class InventoryItemView : MonoBehaviour
    {
        [SerializeField] private Image border;
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private Text quantity;

        public RectTransform Rect => (RectTransform)transform;

        public void Show(ItemStack stack, float cellSize)
        {
            var color = ItemText.RarityColor(stack.Item.Rarity);
            border.color = new Color(color.r, color.g, color.b, 0.85f);
            background.color = new Color(color.r * 0.18f, color.g * 0.18f, color.b * 0.18f, 0.95f);
            icon.sprite = stack.Item.Icon;
            icon.enabled = stack.Item.Icon != null;
            quantity.text = stack.Item.Stackable && stack.Quantity > 1 ? stack.Quantity.ToString() : string.Empty;
            Rect.sizeDelta = new Vector2(stack.Width * cellSize - 4f, stack.Height * cellSize - 4f);
        }
    }
}
