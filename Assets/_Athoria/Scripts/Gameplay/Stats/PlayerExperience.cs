using System;
using Athoria.Core.Events;
using UnityEngine;

namespace Athoria.Gameplay.Stats
{
    public enum XpSource { Enemy, Discovery, Secret, Quest, Dungeon, Other }

    public readonly struct ExperienceChanged : IGameEvent
    {
        public readonly int Level;
        /// <summary>XP acumulado dentro do nível atual.</summary>
        public readonly int Current;
        /// <summary>XP necessário para o próximo nível (0 = nível máximo).</summary>
        public readonly int Required;
        public readonly int Total;

        public ExperienceChanged(int level, int current, int required, int total)
        {
            Level = level;
            Current = current;
            Required = required;
            Total = total;
        }
    }

    public readonly struct ExperienceGained : IGameEvent
    {
        public readonly int Amount;
        public readonly XpSource Source;
        public readonly string Reason;

        public ExperienceGained(int amount, XpSource source, string reason)
        {
            Amount = amount;
            Source = source;
            Reason = reason;
        }
    }

    public readonly struct LevelUp : IGameEvent
    {
        public readonly int NewLevel;
        public LevelUp(int newLevel) => NewLevel = newLevel;
    }

    /// <summary>
    /// Experiência e nível do jogador. Qualquer sistema concede XP com AddExperience
    /// (inimigos, descobertas, segredos, missões, masmorras).
    /// Sem atributos por enquanto: cada nível restaura e aumenta um pouco
    /// vida, stamina e mana (valores configuráveis; podem ser zerados).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerExperience : MonoBehaviour
    {
        [Header("Curva de XP")]
        [SerializeField, Min(1)] private int baseXp = 100;
        [SerializeField, Min(1f), Tooltip("Expoente da curva: XP do nível N = base × N^expoente.")]
        private float growthExponent = 1.35f;
        [SerializeField, Min(1)] private int maxLevel = 50;

        [Header("Ganho por nível")]
        [SerializeField, Min(0f)] private float healthPerLevel = 8f;
        [SerializeField, Min(0f)] private float staminaPerLevel = 4f;
        [SerializeField, Min(0f)] private float manaPerLevel = 3f;
        [SerializeField] private bool restoreOnLevelUp = true;

        [Header("Referências")]
        [SerializeField] private PlayerStats stats;

        private int _level = 1;
        private int _current;
        private int _total;

        public int Level => _level;
        public int Current => _current;
        public int Total => _total;
        public int Required => _level >= maxLevel ? 0 : XpForLevel(_level);
        public bool IsMaxLevel => _level >= maxLevel;

        public event Action<int> LeveledUp;

        private void Reset()
        {
            stats = GetComponent<PlayerStats>();
        }

        private void Awake()
        {
            if (stats == null) stats = GetComponent<PlayerStats>();
        }

        private void Start() => Publish();

        /// <summary>XP necessário para sair do nível informado.</summary>
        public int XpForLevel(int level)
        {
            var raw = baseXp * Mathf.Pow(level, growthExponent);
            return Mathf.Max(5, Mathf.RoundToInt(raw / 5f) * 5);
        }

        public void AddExperience(int amount, XpSource source, string reason = null)
        {
            if (amount <= 0 || IsMaxLevel) return;

            _total += amount;
            _current += amount;
            EventBus.Publish(new ExperienceGained(amount, source, reason));

            while (!IsMaxLevel && _current >= XpForLevel(_level))
            {
                _current -= XpForLevel(_level);
                _level++;
                ApplyLevelUp();
                LeveledUp?.Invoke(_level);
                EventBus.Publish(new LevelUp(_level));
            }
            if (IsMaxLevel) _current = 0;
            Publish();
        }

        [ContextMenu("Teste: ganhar 100 XP")]
        private void DebugGive100() => AddExperience(100, XpSource.Other, "Teste");

        /// <summary>Restaura o estado salvo (Etapa 14).</summary>
        public void LoadState(int level, int current, int total)
        {
            _level = Mathf.Clamp(level, 1, maxLevel);
            _current = Mathf.Max(0, current);
            _total = Mathf.Max(0, total);
            ApplyLevelBonus(false);
            Publish();
        }

        private void ApplyLevelUp() => ApplyLevelBonus(restoreOnLevelUp);

        /// <summary>Bônus acumulado de todos os níveis acima do 1 (camada "nível" do PlayerStats).</summary>
        private void ApplyLevelBonus(bool refill)
        {
            if (stats == null) return;
            var levels = _level - 1;
            stats.SetModifiers(this, new[]
            {
                new StatModifier(StatType.MaxHealth, healthPerLevel * levels),
                new StatModifier(StatType.MaxStamina, staminaPerLevel * levels),
                new StatModifier(StatType.MaxMana, manaPerLevel * levels)   // ignorado sem magia
            }, refill);
        }

        private void Publish() => EventBus.Publish(new ExperienceChanged(_level, _current, Required, _total));
    }
}
