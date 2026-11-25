using Athoria.Core.Events;
using UnityEngine;

namespace Athoria.Gameplay.Stats
{
    public readonly struct StaminaChanged : IGameEvent
    {
        public readonly float Current;
        public readonly float Max;
        public readonly bool IsExhausted;

        public StaminaChanged(float current, float max, bool isExhausted)
        {
            Current = current;
            Max = max;
            IsExhausted = isExhausted;
        }
    }

    /// <summary>
    /// Stamina do jogador. Gasta com corrida e pulo (e, nas próximas etapas,
    /// ataques, bloqueio e esquiva). Ao zerar, o personagem fica exausto e só
    /// volta a gastar depois de recuperar uma fração mínima.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStamina : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxStamina = 100f;
        [SerializeField, Min(0f)] private float regenPerSecond = 24f;
        [SerializeField, Min(0f), Tooltip("Segundos sem gastar antes de começar a recuperar.")]
        private float regenDelay = 0.9f;
        [SerializeField, Range(0f, 1f), Tooltip("Fração que precisa recuperar para sair da exaustão.")]
        private float exhaustionRecoverFraction = 0.3f;

        private float _current;
        private float _lastSpendTime = float.NegativeInfinity;

        public float Current => _current;
        public float Max => maxStamina;
        public float Normalized => _current / maxStamina;
        public bool IsExhausted { get; private set; }

        private void Awake() => _current = maxStamina;

        private void Start() => Publish();

        private void Update()
        {
            if (_current >= maxStamina || Time.time - _lastSpendTime < regenDelay) return;

            _current = Mathf.Min(maxStamina, _current + regenPerSecond * Time.deltaTime);
            if (IsExhausted && _current >= maxStamina * exhaustionRecoverFraction)
                IsExhausted = false;
            Publish();
        }

        public bool CanSpend(float amount) => !IsExhausted && _current >= amount;

        /// <summary>Gasto pontual (pulo, ataque). Só gasta se houver o suficiente.</summary>
        public bool TrySpend(float amount)
        {
            if (!CanSpend(amount)) return false;
            Consume(amount);
            return true;
        }

        /// <summary>Gasto contínuo (corrida). Pode levar a stamina a zero e causar exaustão.</summary>
        public void Drain(float amount)
        {
            if (IsExhausted || amount <= 0f) return;
            Consume(amount);
        }

        /// <summary>Define o valor atual (carregar save).</summary>
        public void SetCurrent(float value)
        {
            _current = Mathf.Clamp(value, 0f, maxStamina);
            IsExhausted = false;
            Publish();
        }

        public void Restore(float amount)
        {
            if (amount <= 0f) return;
            _current = Mathf.Min(maxStamina, _current + amount);
            if (IsExhausted && _current >= maxStamina * exhaustionRecoverFraction) IsExhausted = false;
            Publish();
        }

        /// <summary>Altera o máximo (atributos e equipamentos usarão isto).</summary>
        public void SetMax(float newMax, bool refill)
        {
            maxStamina = Mathf.Max(1f, newMax);
            _current = refill ? maxStamina : Mathf.Min(_current, maxStamina);
            Publish();
        }

        private void Consume(float amount)
        {
            _current = Mathf.Max(0f, _current - amount);
            _lastSpendTime = Time.time;
            if (_current <= 0f) IsExhausted = true;
            Publish();
        }

        private void Publish() => EventBus.Publish(new StaminaChanged(_current, maxStamina, IsExhausted));
    }
}
