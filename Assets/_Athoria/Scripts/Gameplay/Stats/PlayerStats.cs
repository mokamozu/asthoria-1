using System;
using System.Collections.Generic;
using Athoria.Core.Events;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Player;
using UnityEngine;

namespace Athoria.Gameplay.Stats
{
    public readonly struct PlayerStatsChanged : IGameEvent { }

    /// <summary>
    /// Estatísticas finais do jogador, somadas em camadas: base da personagem
    /// (+ características), bônus de nível e equipamentos. Aplica os máximos de
    /// vida/vigor/mana, a velocidade e reduz o dano recebido (armadura e resistências).
    /// </summary>
    [DefaultExecutionOrder(-400)]
    [DisallowMultipleComponent]
    public sealed class PlayerStats : MonoBehaviour, IDamageModifier
    {
        [SerializeField, Range(0f, 95f)] private float resistanceCap = 75f;

        private readonly Dictionary<object, StatModifier[]> _layers = new();
        private float _baseHealth = 100f, _baseStamina = 100f, _baseMana = 50f, _baseSpeed = 1f;
        private Health _health;
        private PlayerStamina _stamina;
        private PlayerMana _mana;
        private PlayerController _controller;

        public float BaseHealth => _baseHealth;
        public float BaseStamina => _baseStamina;
        public float BaseMana => _baseMana;
        public bool UsesMagic => _baseMana > 0f;

        public event Action Changed;

        private void Awake() => CacheComponents();

        private void CacheComponents()
        {
            if (_health == null) _health = GetComponent<Health>();
            if (_stamina == null) _stamina = GetComponent<PlayerStamina>();
            if (_mana == null) _mana = GetComponent<PlayerMana>();
            if (_controller == null) _controller = GetComponent<PlayerController>();
        }

        /// <summary>Valores base da personagem (já com bônus fixos das características).</summary>
        public void SetBase(float health, float stamina, float mana, float speedMultiplier)
        {
            _baseHealth = health;
            _baseStamina = stamina;
            _baseMana = mana;
            _baseSpeed = speedMultiplier;
            Recalculate(true);
        }

        /// <summary>Define (ou troca) os modificadores de uma fonte: "nível", equipamento, buff...</summary>
        public void SetModifiers(object source, IEnumerable<StatModifier> modifiers, bool refill = false)
        {
            if (modifiers == null) _layers.Remove(source);
            else _layers[source] = new List<StatModifier>(modifiers).ToArray();
            Recalculate(refill);
        }

        public float Get(StatType stat)
        {
            var total = 0f;
            foreach (var layer in _layers.Values)
                foreach (var m in layer)
                    if (m.stat == stat) total += m.value;
            return total;
        }

        public float MaxHealth => Mathf.Max(1f, _baseHealth + Get(StatType.MaxHealth));
        public float MaxStamina => Mathf.Max(1f, _baseStamina + Get(StatType.MaxStamina));
        /// <summary>Personagens sem magia (base 0) não ganham mana por equipamento ou nível.</summary>
        public float MaxMana => UsesMagic ? Mathf.Max(0f, _baseMana + Get(StatType.MaxMana)) : 0f;
        public float Armor => Mathf.Max(0f, Get(StatType.Armor));
        public float SpeedMultiplier => Mathf.Max(0.2f, _baseSpeed * (1f + Get(StatType.MoveSpeedPercent) / 100f));
        /// <summary>Fração do dano físico absorvida pela armadura (100 de armadura = 50%).</summary>
        public float ArmorReduction => Armor / (Armor + 100f);

        public float Resistance(DamageType type)
        {
            var stat = type switch
            {
                DamageType.Fire => StatType.ResistFire,
                DamageType.Frost => StatType.ResistFrost,
                DamageType.Lightning => StatType.ResistLightning,
                DamageType.Poison => StatType.ResistPoison,
                DamageType.Arcane => StatType.ResistArcane,
                _ => StatType.ResistPhysical
            };
            return Mathf.Clamp(Get(stat), -100f, resistanceCap);
        }

        public float OutgoingDamageMultiplier(DamageType type)
        {
            var percent = type == DamageType.Physical ? Get(StatType.PhysicalDamagePercent) : Get(StatType.MagicDamagePercent);
            return Mathf.Max(0f, 1f + percent / 100f);
        }

        public void Recalculate(bool refill)
        {
            CacheComponents();
            if (_health != null) _health.SetMax(MaxHealth, refill);
            if (_stamina != null) _stamina.SetMax(MaxStamina, refill);
            if (_mana != null) _mana.SetMax(MaxMana, refill);
            if (_controller != null) _controller.BaseSpeedMultiplier = SpeedMultiplier;
            Changed?.Invoke();
            EventBus.Publish(new PlayerStatsChanged());
        }

        /// <summary>Armadura reduz dano físico; resistências reduzem o tipo correspondente.</summary>
        public bool ModifyIncomingDamage(ref DamageInfo info)
        {
            var multiplier = 1f - Resistance(info.Type) / 100f;
            if (info.Type == DamageType.Physical) multiplier *= 1f - ArmorReduction;
            info.Amount *= Mathf.Max(0f, multiplier);
            return true;
        }
    }
}
