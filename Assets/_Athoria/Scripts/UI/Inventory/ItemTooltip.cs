using System.Text;
using Athoria.Gameplay.Inventory;
using Athoria.Gameplay.Items;
using UnityEngine;
using UnityEngine.UI;

namespace Athoria.UI.Inventory
{
    /// <summary>Caixa de informações do item sob o mouse.</summary>
    public sealed class ItemTooltip : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private Text nameText;
        [SerializeField] private Text typeText;
        [SerializeField] private Text statsText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text footerText;
        [SerializeField] private Text compareText;

        public void Show(ItemStack stack, string actionHint, string comparison = null)
        {
            if (compareText != null)
            {
                compareText.text = comparison ?? string.Empty;
                compareText.gameObject.SetActive(!string.IsNullOrEmpty(comparison));
            }
            var item = stack.Item;
            nameText.text = item.DisplayName;
            nameText.color = ItemText.RarityColor(item.Rarity);
            typeText.text = $"{ItemText.Category(item.ItemType)}  •  {ItemText.Rarity(item.Rarity)}";

            var stats = item.TooltipStats;
            statsText.text = stats;
            statsText.gameObject.SetActive(!string.IsNullOrEmpty(stats));
            descriptionText.text = item.Description;
            descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(item.Description));

            var sb = new StringBuilder();
            sb.Append($"Peso {stack.Weight:0.#}   •   Valor {item.Value * stack.Quantity}");
            if (item.Stackable) sb.Append($"   •   {stack.Quantity}/{item.MaxStack}");
            if (!string.IsNullOrEmpty(actionHint)) sb.Append('\n').Append(actionHint);
            footerText.text = sb.ToString();

            gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>Posiciona perto do mouse sem sair da tela (coordenadas locais do canvas).</summary>
        public void MoveTo(Vector2 localPoint, RectTransform canvas)
        {
            var size = panel.rect.size;
            var half = canvas.rect.size * 0.5f;
            var pos = localPoint + new Vector2(24f, -24f);
            if (pos.x + size.x > half.x) pos.x = localPoint.x - size.x - 24f;
            if (pos.y - size.y < -half.y) pos.y = -half.y + size.y;
            panel.anchoredPosition = pos;
        }
    }
}
