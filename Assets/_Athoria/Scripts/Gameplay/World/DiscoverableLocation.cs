using Athoria.Core.Events;
using Athoria.Gameplay.Stats;
using UnityEngine;

namespace Athoria.Gameplay.World
{
    public readonly struct LocationDiscovered : IGameEvent
    {
        public readonly string Id;
        public readonly string Name;
        public readonly bool IsSecret;
        public readonly int Experience;

        public LocationDiscovered(string id, string name, bool isSecret, int experience)
        {
            Id = id;
            Name = name;
            IsSecret = isSecret;
            Experience = experience;
        }
    }

    /// <summary>
    /// Volume que, na primeira entrada do jogador, marca o local como descoberto
    /// e concede XP. Locais secretos dão mais XP e outra mensagem.
    /// O Id será usado pelo sistema de salvamento (Etapa 14).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class DiscoverableLocation : MonoBehaviour
    {
        [SerializeField, Tooltip("Identificador único e estável (usado no save).")]
        private string id;
        [SerializeField] private string displayName = "Local desconhecido";
        [SerializeField] private bool isSecret;
        [SerializeField, Min(0)] private int experience = 30;

        private bool _discovered;

        public string Id => id;
        public bool Discovered => _discovered;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
            id = System.Guid.NewGuid().ToString("N");
        }

        private void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
            if (string.IsNullOrEmpty(id)) id = name;
        }

        /// <summary>Marca como já descoberto sem dar XP (carregamento de save).</summary>
        public void MarkDiscovered() => _discovered = true;

        private void OnTriggerEnter(Collider other)
        {
            if (_discovered) return;
            var player = other.GetComponentInParent<PlayerExperience>();
            if (player == null) return;

            _discovered = true;
            EventBus.Publish(new LocationDiscovered(id, displayName, isSecret, experience));
            player.AddExperience(experience, isSecret ? XpSource.Secret : XpSource.Discovery, displayName);
        }
    }
}
