using Athoria.Gameplay.Combat;
using UnityEngine;

namespace Athoria.Gameplay.Stats
{
    /// <summary>Concede XP a quem derrotar este alvo (inimigos, bonecos de treino).</summary>
    [RequireComponent(typeof(Health))]
    public sealed class ExperienceReward : MonoBehaviour
    {
        [SerializeField, Min(0)] private int experience = 25;
        [SerializeField] private string displayName;

        private Health _health;

        public int Experience => experience;

        private void Awake() => _health = GetComponent<Health>();
        private void OnEnable() => _health.Died += OnDied;
        private void OnDisable() => _health.Died -= OnDied;

        private void OnDied(DamageInfo info)
        {
            if (info.Source == null) return;
            var receiver = info.Source.GetComponentInParent<PlayerExperience>();
            if (receiver != null)
                receiver.AddExperience(experience, XpSource.Enemy, string.IsNullOrEmpty(displayName) ? name : displayName);
        }
    }
}
