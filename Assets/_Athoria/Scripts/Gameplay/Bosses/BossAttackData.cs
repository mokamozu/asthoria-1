using Athoria.Core.Data;
using Athoria.Gameplay.Combat;
using UnityEngine;

namespace Athoria.Gameplay.Bosses
{
    public enum BossAttackKind
    {
        [InspectorName("Soco pesado no chão")] HeavySlam,
        [InspectorName("Salto sobre o jogador")] JumpAttack,
        [InspectorName("Arroto venenoso")] PoisonBelch,
        [InspectorName("Golpe rápido")] QuickStrike,
    }

    /// <summary>
    /// Um ataque do chefe: tempos (preparação, golpe, recuperação), recarga, alcance,
    /// área e dano. Tudo ajustável no Inspector; os valores atuais são provisórios.
    /// </summary>
    [CreateAssetMenu(menuName = "Athoria/Chefes/Ataque de chefe", fileName = "NovoAtaqueDeChefe")]
    public sealed class BossAttackData : GameData
    {
        public BossAttackKind kind;

        [Header("Distância em que o chefe escolhe este ataque (m)")]
        [Min(0f)] public float minRange;
        [Min(0.5f)] public float maxRange = 4f;
        [Tooltip("Prioridade quando mais de um ataque serve (maior = preferido).")] [Min(0f)] public float weight = 1f;

        [Header("Tempos (s)")]
        [Min(0f), Tooltip("Preparação (o jogador vê e pode fugir).")] public float windup = 1.5f;
        [Min(0f), Tooltip("Tempo do golpe em si (no salto: tempo no ar).")] public float active = 0.3f;
        [Min(0f), Tooltip("Recuperação depois do golpe (janela para o jogador atacar).")] public float recovery = 1.5f;
        [Min(0f), Tooltip("Tempo até poder usar este ataque de novo.")] public float cooldown = 6f;
        [Min(0f), Tooltip("Quanto antes do impacto o aviso no chão aparece.")] public float warningTime = 0.8f;

        [Header("Dano")]
        [Min(0f)] public float damage = 30f;
        public DamageType damageType = DamageType.Physical;
        [Min(0f)] public float stagger = 2f;
        [Min(0.1f), Tooltip("Raio da área de dano (soco, salto, nuvem).")] public float radius = 3f;
        [Min(0f), Tooltip("Distância à frente do chefe onde o soco acerta.")] public float reach = 2.6f;
        [Range(10f, 360f), Tooltip("Abertura do golpe rápido (graus).")] public float arc = 100f;

        [Header("Veneno (arroto)")]
        [Min(0f)] public float cloudDuration = 8f;
        [Min(0f), Tooltip("Dano por segundo dentro da nuvem.")] public float poisonDps = 8f;
        [Min(0.1f)] public float poisonTick = 0.5f;

        [Header("Sons")]
        public AudioClip windupSound;
        public AudioClip impactSound;

#if UNITY_EDITOR
        public void EditorConfigure(BossAttackKind newKind, float min, float max, float newWeight, float newWindup, float newActive,
            float newRecovery, float newCooldown, float newWarning, float newDamage, float newStagger, float newRadius, float newReach)
        {
            kind = newKind; minRange = min; maxRange = max; weight = newWeight; windup = newWindup; active = newActive;
            recovery = newRecovery; cooldown = newCooldown; warningTime = newWarning; damage = newDamage; stagger = newStagger;
            radius = newRadius; reach = newReach;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
