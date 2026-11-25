using System;
using Athoria.Core.Data;
using Athoria.Gameplay.Combat;
using UnityEngine;

namespace Athoria.Gameplay.AI
{
    /// <summary>Pose usada pelo animador procedural durante um ataque.</summary>
    public enum EnemyAttackPose { Slash, Overhead, Thrust, Bite, Shoot }

    /// <summary>Um ataque do inimigo: alcance, tempos (preparação/golpe/recuperação) e dano.</summary>
    [Serializable]
    public sealed class EnemyAttack
    {
        public string name = "Golpe";
        public EnemyAttackPose pose = EnemyAttackPose.Slash;
        [Min(0f)] public float minRange;
        [Min(0.1f)] public float maxRange = 2f;
        [Min(0f)] public float damage = 10f;
        public DamageType damageType = DamageType.Physical;
        [Tooltip("Preparação (o aviso que o jogador lê para bloquear/esquivar).")]
        [Min(0.05f)] public float windup = 0.5f;
        [Min(0.02f)] public float activeTime = 0.15f;
        [Min(0.05f)] public float recovery = 0.6f;
        [Tooltip("Abertura do golpe em graus (corpo a corpo).")]
        [Range(10f, 360f)] public float arc = 80f;
        [Tooltip("1 = leve, 2 = pesado.")]
        [Min(0f)] public float stagger = 1f;
        public bool heavy;
        [Tooltip("Metros que o inimigo avança durante o golpe.")]
        [Min(0f)] public float lunge;
        [Tooltip("Se preenchido, o ataque dispara este projétil em vez de golpear.")]
        public Projectile projectile;
        [Min(1f)] public float projectileSpeed = 22f;
        [Tooltip("Segundos até este ataque poder ser usado de novo.")]
        [Min(0f)] public float cooldown;
        [Min(0.01f)] public float weight = 1f;
        [Tooltip("Chance de emendar outro ataque logo depois (combo).")]
        [Range(0f, 1f)] public float chainChance;
        [Tooltip("Índice do ataque emendado (-1 = sorteia).")]
        public int chainTo = -1;
        [Tooltip("Som do golpe. Vazio = sons de ataque do inimigo.")]
        public AudioClip sound;
    }

    /// <summary>
    /// Dados de um tipo de inimigo. Toda a afinação de comportamento fica
    /// aqui para que lobos, esqueletos e cavaleiros usem o mesmo EnemyAI.
    /// </summary>
    [CreateAssetMenu(menuName = "Athoria/Inimigo", fileName = "Inimigo_")]
    public sealed class EnemyData : GameData
    {
        [Header("Vida e defesa")]
        [Min(1f)] public float maxHealth = 60f;
        [Tooltip("Redução de dano físico (0 a 0.9).")]
        [Range(0f, 0.9f)] public float physicalReduction;
        [Tooltip("Atordoamento acumulado necessário para interromper o inimigo.")]
        [Min(0.1f)] public float poise = 1f;
        [Min(0.1f)] public float staggerDuration = 0.6f;

        [Header("Bloqueio")]
        public bool canBlock;
        [Tooltip("Chance de levantar a guarda quando o jogador começa um ataque perto.")]
        [Range(0f, 1f)] public float blockChance = 0.4f;
        [Range(10f, 180f)] public float blockAngle = 70f;
        [Min(0.1f)] public float blockDuration = 1.2f;
        [Range(0f, 1f)] public float blockAbsorb = 1f;
        [Tooltip("Golpes bloqueados antes da guarda quebrar.")]
        [Min(1)] public int guardHits = 3;
        [Range(0f, 1f)] public float riposteChance = 0.5f;

        [Header("Movimento")]
        [Min(0.1f)] public float walkSpeed = 1.6f;
        [Min(0.1f)] public float runSpeed = 4f;
        [Min(30f)] public float turnSpeed = 360f;
        [Min(0.1f)] public float agentRadius = 0.4f;
        [Min(0.2f)] public float agentHeight = 1.8f;

        [Header("Percepção")]
        [Min(1f)] public float sightRange = 16f;
        [Range(20f, 360f)] public float sightAngle = 120f;
        [Tooltip("Distância em que percebe o jogador mesmo de costas.")]
        [Min(0f)] public float senseRadius = 2f;
        [Tooltip("Ouve passos de corrida e golpes até esta distância.")]
        [Min(0f)] public float hearingRange = 9f;
        [Tooltip("Segundos que lembra do jogador depois de perdê-lo de vista.")]
        [Min(0.5f)] public float memoryTime = 4f;
        [Tooltip("Chama aliados nesse raio ao avistar o jogador.")]
        [Min(0f)] public float alertRadius = 10f;

        [Header("Comportamento")]
        [Tooltip("Distância máxima do posto antes de desistir e voltar.")]
        [Min(2f)] public float leashRange = 28f;
        [Tooltip("0 = fica parado no posto.")]
        [Min(0f)] public float patrolRadius = 4f;
        public Vector2 patrolWait = new(2f, 5f);
        [Tooltip("Pausa entre ataques (mín, máx).")]
        public Vector2 attackInterval = new(0.8f, 1.8f);
        [Tooltip("Rodeia o jogador enquanto espera para atacar.")]
        public bool circlesTarget;
        [Tooltip("Distância que tenta manter (arqueiros). 0 = corpo a corpo.")]
        [Min(0f)] public float preferredDistance;
        [Tooltip("Foge ao ficar com essa fração de vida (0 = nunca).")]
        [Range(0f, 1f)] public float retreatHealthFraction;
        [Min(0.5f)] public float retreatDuration = 3f;
        [Tooltip("Vida recuperada por segundo ao voltar para o posto.")]
        [Min(0f)] public float returnRegenPerSecond = 15f;
        [Tooltip("Multiplicador de dano de ataques furtivos (inimigo desatento).")]
        [Min(1f)] public float sneakAttackMultiplier = 2f;

        [Header("Ataques")]
        public EnemyAttack[] attacks = { new() };

        [Header("Recompensa")]
        [Min(0)] public int experience = 20;
        [Tooltip("O que cai ao morrer (ouro, itens). Fica no LootDropper do Prefab.")]
        public Loot.LootTable loot;

        [Header("Sons")]
        public AudioClip alertSound;
        public AudioClip[] attackSounds;
        public AudioClip hurtSound;
        public AudioClip deathSound;

        public float MaxAttackRange
        {
            get
            {
                var max = 1f;
                if (attacks != null) foreach (var a in attacks) if (a != null) max = Mathf.Max(max, a.maxRange);
                return max;
            }
        }

        public bool IsRanged => preferredDistance > 0f;
    }
}
