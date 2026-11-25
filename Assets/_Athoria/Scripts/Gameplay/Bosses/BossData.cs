using Athoria.Core.Data;
using Athoria.Gameplay.Items;
using UnityEngine;

namespace Athoria.Gameplay.Bosses
{
    /// <summary>
    /// Atributos do chefe tanque (Ancião do Brejo) e o que é preciso para despertá-lo.
    /// Todos os números são editáveis no Inspector; os atuais são provisórios de teste.
    /// </summary>
    [CreateAssetMenu(menuName = "Athoria/Chefes/Chefe", fileName = "NovoChefe")]
    public sealed class BossData : GameData
    {
        [Header("Vida e defesa")]
        [Min(1f)] public float maxHealth = 1500f;
        [Min(0f), Tooltip("Dano subtraído de cada golpe.")] public float defense = 6f;
        [Range(0f, 0.9f), Tooltip("Fração do dano ignorada depois da defesa.")] public float damageResistance = 0.25f;
        [Range(0.05f, 1f), Tooltip("Mínimo de dano que sempre passa (fração do golpe).")] public float minDamageFraction = 0.15f;

        [Header("Atordoamento")]
        [Min(0f), Tooltip("Dano acumulado que faz o chefe cambalear (golpes pesados contam em dobro).")] public float staggerThreshold = 120f;
        [Min(0f)] public float staggerDuration = 1.4f;
        [Min(0f), Tooltip("Tempo sem poder cambalear de novo.")] public float staggerImmunity = 8f;

        [Header("Movimento")]
        [Min(0.1f)] public float walkSpeed = 1.6f;
        [Min(10f)] public float turnSpeed = 70f;
        [Min(0f), Tooltip("Pausa para 'pensar' entre ações (s).")] public float thinkTime = 0.6f;
        [Min(0f), Tooltip("Segundos com o jogador colado nele antes de usar o golpe rápido.")] public float closePressureTime = 1.2f;
        [Min(0.5f)] public float closeRange = 3.2f;

        [Header("Despertar")]
        [Tooltip("Item que desperta o chefe quando jogado perto dele.")] public ItemData wakeItem;
        [Min(1)] public int wakeItemAmount = 40;
        [Min(0.5f), Tooltip("Distância do chefe em que o item precisa cair.")] public float activationRadius = 6f;
        [Min(0.5f)] public float awakenDuration = 5.2f;

        [Header("Ataques")]
        public BossAttackData heavySlam;
        public BossAttackData jumpAttack;
        public BossAttackData poisonBelch;
        public BossAttackData quickStrike;

        [Header("Recompensa")]
        [Min(0)] public int experience = 600;

        [Header("Sons")]
        public AudioClip roarSound;
        public AudioClip rocksSound;
        public AudioClip stepSound;
        public AudioClip hurtSound;
        public AudioClip deathSound;

        public BossAttackData[] Attacks => new[] { heavySlam, jumpAttack, poisonBelch, quickStrike };
    }
}
