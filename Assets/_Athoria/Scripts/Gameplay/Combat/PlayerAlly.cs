using System.Collections.Generic;
using UnityEngine;

namespace Athoria.Gameplay.Combat
{
    /// <summary>
    /// Marca uma criatura do lado da jogadora (espíritos invocados). Os inimigos
    /// (EnemyAI) consultam esta lista para escolher quem atacar além da jogadora.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class PlayerAlly : MonoBehaviour
    {
        private static readonly List<PlayerAlly> s_all = new();
        public static IReadOnlyList<PlayerAlly> All => s_all;

        [SerializeField, Tooltip("Altura (m) do ponto que os inimigos miram.")] private float aimHeight = 0.6f;

        private Health _health;

        public Health Health => _health != null ? _health : (_health = GetComponent<Health>());
        public bool IsAlive => isActiveAndEnabled && Health != null && Health.IsAlive;
        public Vector3 AimPoint => transform.position + Vector3.up * aimHeight;

        private void OnEnable() => s_all.Add(this);
        private void OnDisable() => s_all.Remove(this);
    }
}
