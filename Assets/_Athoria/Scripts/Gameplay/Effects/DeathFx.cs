using Athoria.Gameplay.Combat;
using UnityEngine;

namespace Athoria.Gameplay.Effects
{
    /// <summary>Toca um efeito quando este Health morre (fumaça, explosão...).</summary>
    [RequireComponent(typeof(Health))]
    public sealed class DeathFx : MonoBehaviour
    {
        [SerializeField] private SpriteFxAnimation effect;
        [SerializeField] private Vector3 offset;
        [SerializeField, Min(0.01f)] private float scale = 1f;

        private Health _health;

        private void Awake() => _health = GetComponent<Health>();
        private void OnEnable() => _health.Died += OnDied;
        private void OnDisable() => _health.Died -= OnDied;

        private void OnDied(DamageInfo _) => SpriteFx.SpawnOnGround(effect, transform.position + offset, scale);
    }
}
