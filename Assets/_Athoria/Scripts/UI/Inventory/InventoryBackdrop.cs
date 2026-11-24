using UnityEngine;
using UnityEngine.EventSystems;

namespace Athoria.UI.Inventory
{
    /// <summary>Fundo escurecido atrás do inventário: clicar nele com um item na mão larga o item no chão.</summary>
    public sealed class InventoryBackdrop : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private InventoryScreen screen;
        public void OnPointerClick(PointerEventData e) => screen.OnBackdropClick();
    }
}
