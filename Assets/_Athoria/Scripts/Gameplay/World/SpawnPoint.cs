using UnityEngine;

namespace Athoria.Gameplay.World
{
    /// <summary>Ponto de chegada numa cena (início da região, saída de uma masmorra...).</summary>
    public sealed class SpawnPoint : MonoBehaviour
    {
        [SerializeField] private string id = "inicio";
        [SerializeField, Tooltip("Usado quando o jogador entra na cena sem um ponto definido.")]
        private bool isDefault;

        public string Id => id;
        public bool IsDefault => isDefault;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
            Gizmos.DrawRay(transform.position + Vector3.up, transform.forward);
        }
    }
}
