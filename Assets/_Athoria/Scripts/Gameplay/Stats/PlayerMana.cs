using Athoria.Core.Events;
using UnityEngine;

namespace Athoria.Gameplay.Stats
{
    public readonly struct ManaChanged : IGameEvent
    {
        public readonly float Current;
        public readonly float Max;

        public ManaChanged(float current, float max)
        {
            Current = current;
            Max = max;
        }
    }

    /// <summary>Mana do jogador: gasta com magias, recupera sozinha depois de um tempo sem uso.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerMana : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float maxMana = 50f;
        [SerializeField, Min(0f)] private float regenPerSecond = 4f;
        [SerializeField, Min(0f)] private float regenDelay = 1.5f;

        private float _current;
        private float _lastSpendTime = float.NegativeInfinity;

        public float Current => _current;
        public float Max => maxMana;
        public float Normalized => maxMana > 0f ? _current / maxMana : 0f;

        private void Awake() => _current = maxMana;
        private void Start() => Publish();

        private void Update()
        {
            if (_current >= maxMana || Time.time - _lastSpendTime < regenDelay) return;
            _current = Mathf.Min(maxMana, _current + regenPerSecond * Time.deltaTime);
            Publish();
        }

        public bool CanSpend(float amount) => _current >= amount;

        public bool TrySpend(float amount)
        {
            if (amount <= 0f) return true;
            if (_current < amount) return false;
            _current -= amount;
            _lastSpendTime = Time.time;
            Publish();
            return true;
        }

        /// <summary>Define o valor atual (carregar save).</summary>
        public void SetCurrent(float value)
        {
            _current = Mathf.Clamp(value, 0f, maxMana);
            Publish();
        }

        public void Restore(float amount)
        {
            if (amount <= 0f) return;
            _current = Mathf.Min(maxMana, _current + amount);
            Publish();
        }

        /// <summary>Altera o máximo (Inteligência/equipamentos usarão isto).</summary>
        public void SetMax(float newMax, bool refill)
        {
            maxMana = Mathf.Max(0f, newMax);
            _current = refill ? maxMana : Mathf.Min(_current, maxMana);
            Publish();
        }

        private void Publish() => EventBus.Publish(new ManaChanged(_current, maxMana));
    }
}
