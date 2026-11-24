using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Items;
using UnityEngine;

namespace Athoria.Gameplay.Characters
{
    /// <summary>Aplica os efeitos contínuos das características da personagem.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerTraits : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField, Tooltip("Segundos sem levar dano para começar a regenerar.")]
        private float outOfCombatDelay = 6f;

        private CharacterTrait[] _traits = System.Array.Empty<CharacterTrait>();
        private float _healthRegen;
        private float _lastDamageTime = float.NegativeInfinity;

        public float DodgeStaminaMultiplier { get; private set; } = 1f;
        public float MoveSpeedMultiplier { get; private set; } = 1f;
        public CharacterTrait[] Traits => _traits;

        private void Awake()
        {
            if (health == null) health = GetComponent<Health>();
        }

        private void OnEnable() { if (health != null) health.Damaged += OnDamaged; }
        private void OnDisable() { if (health != null) health.Damaged -= OnDamaged; }

        /// <summary>Define as características (chamado ao montar a personagem).</summary>
        public void SetTraits(CharacterTrait[] traits)
        {
            _traits = traits ?? System.Array.Empty<CharacterTrait>();
            _healthRegen = 0f;
            var speedPercent = 0f;
            var dodgeReduction = 0f;
            foreach (var t in _traits)
            {
                switch (t.effect)
                {
                    case TraitEffect.HealthRegenOutOfCombat: _healthRegen += t.value; break;
                    case TraitEffect.MoveSpeedPercent: speedPercent += t.value; break;
                    case TraitEffect.DodgeStaminaReductionPercent: dodgeReduction += t.value; break;
                }
            }
            MoveSpeedMultiplier = 1f + speedPercent / 100f;
            DodgeStaminaMultiplier = Mathf.Clamp01(1f - dodgeReduction / 100f);
        }

        /// <summary>Bônus de vida/vigor/mana fixos das características.</summary>
        public float FlatBonus(TraitEffect effect)
        {
            var total = 0f;
            foreach (var t in _traits) if (t.effect == effect) total += t.value;
            return total;
        }

        public float DamageMultiplierFor(WeaponCategory category)
        {
            var percent = 0f;
            foreach (var t in _traits)
                if (t.effect == TraitEffect.DamagePercentWithCategory && t.category == category) percent += t.value;
            return 1f + percent / 100f;
        }

        private void OnDamaged(DamageInfo info, DamageResult result)
        {
            if (result == DamageResult.Damaged || result == DamageResult.Killed) _lastDamageTime = Time.time;
        }

        private void Update()
        {
            if (_healthRegen <= 0f || health == null || !health.IsAlive) return;
            if (health.Current >= health.Max || Time.time - _lastDamageTime < outOfCombatDelay) return;
            health.Heal(_healthRegen * Time.deltaTime);
        }
    }
}
