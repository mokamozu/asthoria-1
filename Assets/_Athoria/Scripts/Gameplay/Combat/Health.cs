using System;
using Athoria.Core.Events;
using UnityEngine;

namespace Athoria.Gameplay.Combat
{
    /// <summary>
    /// Vida genérica: jogador, inimigos, bonecos de treino, objetos quebráveis.
    /// Antes de aplicar o dano consulta os IDamageModifier do mesmo objeto.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Tooltip("Segundos imune após levar dano.")] private float invulnerabilityAfterHit;
        [SerializeField, Tooltip("Marca o Health do jogador (números de dano, HUD).")] private bool isPlayer;

        private float _current;
        private float _invulnerableUntil;
        private IDamageModifier[] _modifiers;

        public float Current => _current;
        public float Max => maxHealth;
        public float Normalized => _current / maxHealth;
        public bool IsAlive => _current > 0f;
        public bool IsPlayer => isPlayer;
        /// <summary>Ignora todo dano sem reagir (ex.: chefe adormecido, disfarçado de pedra).</summary>
        public bool Invulnerable { get; set; }

        public event Action<DamageInfo, DamageResult> Damaged;
        public event Action<DamageInfo> Died;
        public event Action Revived;
        /// <summary>(atual, máximo)</summary>
        public event Action<float, float> Changed;

        private void Awake()
        {
            _current = maxHealth;
            _modifiers = GetComponents<IDamageModifier>();
        }

        private void Start() => NotifyChanged();

        private void NotifyChanged()
        {
            Changed?.Invoke(_current, maxHealth);
            EventBus.Publish(new HealthChanged(this, _current, maxHealth));
        }

        public DamageResult ApplyDamage(DamageInfo info)
        {
            if (!IsAlive || Invulnerable || info.Amount <= 0f || Time.time < _invulnerableUntil) return DamageResult.Ignored;

            foreach (var modifier in _modifiers)
            {
                if (modifier.ModifyIncomingDamage(ref info)) continue;
                info.Amount = 0f;
                return Report(info, DamageResult.Blocked);
            }

            if (info.Amount <= 0f) return Report(info, DamageResult.Blocked);

            _current = Mathf.Max(0f, _current - info.Amount);
            if (invulnerabilityAfterHit > 0f) _invulnerableUntil = Time.time + invulnerabilityAfterHit;
            NotifyChanged();

            var result = _current <= 0f ? DamageResult.Killed : DamageResult.Damaged;
            Report(info, result);
            if (result == DamageResult.Killed) Died?.Invoke(info);
            return result;
        }

        private DamageResult Report(DamageInfo info, DamageResult result)
        {
            Damaged?.Invoke(info, result);
            EventBus.Publish(new DamageApplied(this, info, result));
            return result;
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            _current = Mathf.Min(maxHealth, _current + amount);
            NotifyChanged();
        }

        /// <summary>Define a vida atual (carregar save). Não mata nem dispara eventos de dano.</summary>
        public void SetCurrent(float value)
        {
            _current = Mathf.Clamp(value, 1f, maxHealth);
            NotifyChanged();
        }

        public void Revive(float fraction = 1f)
        {
            _current = Mathf.Clamp(maxHealth * fraction, 1f, maxHealth);
            _invulnerableUntil = 0f;
            NotifyChanged();
            Revived?.Invoke();
        }

        /// <summary>Altera a vida máxima (atributos/equipamentos).</summary>
        public void SetMax(float newMax, bool refill)
        {
            var ratio = Normalized;
            maxHealth = Mathf.Max(1f, newMax);
            _current = refill ? maxHealth : Mathf.Clamp(maxHealth * ratio, IsAlive ? 1f : 0f, maxHealth);
            NotifyChanged();
        }
    }
}
