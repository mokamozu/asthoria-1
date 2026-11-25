using Unity.AI.Navigation;
using UnityEngine;

namespace Athoria.Gameplay.World
{
    /// <summary>
    /// Gera o NavMesh da cena ao carregar (antes dos inimigos ligarem os agentes).
    /// Assim não é preciso "assar" o NavMesh à mão sempre que a cena for recriada.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    [RequireComponent(typeof(NavMeshSurface))]
    public sealed class RuntimeNavMeshBaker : MonoBehaviour
    {
        private void Awake()
        {
            var surface = GetComponent<NavMeshSurface>();
            if (surface.navMeshData == null) surface.BuildNavMesh();
        }
    }
}
