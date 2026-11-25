using Athoria.Gameplay.Combat;
using UnityEngine;

namespace Athoria.Gameplay.Loot
{
    /// <summary>Solta o loot da tabela quando este Health morre (inimigos, objetos quebráveis).</summary>
    [RequireComponent(typeof(Health))]
    public sealed class LootDropper : MonoBehaviour
    {
        [SerializeField] private LootTable table;
        [SerializeField, Min(0f)] private float spread = 1.4f;
        [SerializeField] private Vector3 offset = new(0f, 0.8f, 0f);

        private Health _health;

        public LootTable Table => table;

        private void Awake() => _health = GetComponent<Health>();
        private void OnEnable() => _health.Died += OnDied;
        private void OnDisable() => _health.Died -= OnDied;

        private void OnDied(DamageInfo info)
        {
            if (table == null) return;
            var away = info.Direction.sqrMagnitude > 0.01f ? info.Direction : transform.forward;
            LootSpawner.RollAndSpawn(table, transform.position + offset, away, spread);
        }
    }
}
