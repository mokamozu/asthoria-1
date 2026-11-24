using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Athoria.UI.Inventory
{
    /// <summary>Uma célula da grade. Recebe cliques e passagem do mouse e repassa para a tela.</summary>
    public sealed class InventoryCell : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image image;

        public int X { get; private set; }
        public int Y { get; private set; }
        public Image Image => image;

        private InventoryScreen _screen;

        public void Setup(InventoryScreen screen, int x, int y)
        {
            _screen = screen;
            X = x;
            Y = y;
        }

        public void OnPointerClick(PointerEventData e) => _screen.OnCellClick(this, e.button);
        public void OnPointerEnter(PointerEventData e) => _screen.OnCellEnter(this);
        public void OnPointerExit(PointerEventData e) => _screen.OnCellExit(this);
    }
}
