using UnityEngine;

namespace Athoria.Gameplay.Combat
{
    public enum SurfaceKind { Stone, Wood, Metal, Flesh, Straw }

    /// <summary>Define o material de um objeto para sons e partículas de impacto. Sem ele, vale Pedra.</summary>
    public sealed class SurfaceMaterial : MonoBehaviour
    {
        [SerializeField] private SurfaceKind kind = SurfaceKind.Stone;
        public SurfaceKind Kind => kind;

        public static SurfaceKind Of(Collider collider)
        {
            if (collider == null) return SurfaceKind.Stone;
            var surface = collider.GetComponentInParent<SurfaceMaterial>();
            return surface != null ? surface.kind : SurfaceKind.Stone;
        }
    }
}
