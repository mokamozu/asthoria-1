using Athoria.Core.Events;
using UnityEngine;

namespace Athoria.Gameplay.Combat
{
    public enum DamageType { Physical, Fire, Frost, Lightning, Poison, Arcane }

    public enum DamageResult
    {
        /// <summary>Alvo morto, imune ou dano zero.</summary>
        Ignored,
        /// <summary>Totalmente bloqueado/esquivado.</summary>
        Blocked,
        Damaged,
        Killed
    }

    /// <summary>Tudo o que um golpe ou projétil carrega até o alvo.</summary>
    public struct DamageInfo
    {
        public float Amount;
        public DamageType Type;
        public GameObject Source;
        public Vector3 Point;
        public Vector3 Normal;
        /// <summary>Direção em que o golpe viaja (do atacante para o alvo).</summary>
        public Vector3 Direction;
        /// <summary>Força de atordoamento: 1 = leve, 2 = pesado.</summary>
        public float StaggerPower;
        public bool IsHeavy;
        /// <summary>Preenchido por modificadores (bloqueio parcial etc.).</summary>
        public bool WasBlocked;
    }

    /// <summary>Qualquer coisa que pode receber dano.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        DamageResult ApplyDamage(DamageInfo info);
    }

    /// <summary>
    /// Componentes no mesmo objeto do Health podem alterar o dano antes de
    /// ser aplicado (bloqueio, esquiva, resistências). Retornar false anula o golpe.
    /// </summary>
    public interface IDamageModifier
    {
        bool ModifyIncomingDamage(ref DamageInfo info);
    }

    /// <summary>Vida de algum Health mudou (dano, cura, reviver, novo máximo).</summary>
    public readonly struct HealthChanged : IGameEvent
    {
        public readonly Health Target;
        public readonly float Current;
        public readonly float Max;

        public HealthChanged(Health target, float current, float max)
        {
            Target = target;
            Current = current;
            Max = max;
        }
    }

    /// <summary>Publicado sempre que um Health recebe dano (inclusive bloqueado).</summary>
    public readonly struct DamageApplied : IGameEvent
    {
        public readonly Health Target;
        public readonly DamageInfo Info;
        public readonly DamageResult Result;

        public DamageApplied(Health target, DamageInfo info, DamageResult result)
        {
            Target = target;
            Info = info;
            Result = result;
        }
    }
}
