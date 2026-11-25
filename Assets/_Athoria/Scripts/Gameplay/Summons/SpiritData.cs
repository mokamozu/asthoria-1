using Athoria.Core.Data;
using Athoria.Gameplay.Combat;
using UnityEngine;

namespace Athoria.Gameplay.Summons
{
    /// <summary>
    /// Ficha de um espírito invocável (ex.: Lobo Espiritual da Akane).
    /// Os números vêm do asset, nunca do código — os valores atuais são provisórios de teste.
    /// O custo de mana e a recarga ficam na AbilityData que invoca o espírito.
    /// </summary>
    [CreateAssetMenu(menuName = "Athoria/Invocações/Espírito", fileName = "NovoEspirito")]
    public sealed class SpiritData : GameData
    {
        [Header("Manifestação")]
        [SerializeField, Tooltip("Prefab com SpiritCompanion, SpiritBrain, Health e NavMeshAgent.")] private GameObject prefab;
        [SerializeField, Min(0), Tooltip("Quantos Mantras este espírito ocupa enquanto está manifestado.")] private int mantraCost = 1;
        [SerializeField, Tooltip("Efeito no chão ao surgir (opcional).")] private Effects.SpriteFxAnimation manifestFx;
        [SerializeField, Tooltip("Efeito no chão ao desaparecer (opcional).")] private Effects.SpriteFxAnimation vanishFx;

        [Header("Vida")]
        [SerializeField, Min(1f)] private float maxHealth = 1f;

        [Header("Ataque")]
        [SerializeField, Min(0f)] private float damage;
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [SerializeField, Min(0.1f), Tooltip("Segundos entre um ataque e o próximo.")] private float attackInterval = 1f;
        [SerializeField, Min(0.3f), Tooltip("Distância (m) a partir da qual morde.")] private float attackRange = 1.5f;
        [SerializeField, Min(0f), Tooltip("Segundos do início do ataque até o dano (bate com a animação).")] private float attackWindup = 0.3f;
        [SerializeField, Min(0f)] private float stagger = 1f;

        [Header("Movimento")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 2f;
        [SerializeField, Min(0.1f)] private float runSpeed = 6f;
        [SerializeField, Min(30f)] private float turnSpeed = 540f;

        [Header("Percepção e acompanhamento")]
        [SerializeField, Min(1f), Tooltip("Raio (m) em que procura inimigos.")] private float detectionRange = 10f;
        [SerializeField, Min(1f), Tooltip("Não persegue inimigos mais longe que isso da invocadora.")] private float leashRange = 18f;
        [SerializeField, Min(0.5f), Tooltip("Distância (m) que mantém da invocadora ao acompanhar.")] private float followDistance = 2.5f;
        [SerializeField, Min(2f), Tooltip("Mais longe que isso (ou preso), reaparece ao lado da invocadora.")] private float teleportDistance = 25f;

        [Header("Tempos de animação (s)")]
        [SerializeField, Min(0f)] private float summonDuration = 1.2f;
        [SerializeField, Min(0f)] private float hurtDuration = 0.4f;
        [SerializeField, Min(0f)] private float deathDuration = 1.4f;

        public GameObject Prefab => prefab;
        public int MantraCost => mantraCost;
        public Effects.SpriteFxAnimation ManifestFx => manifestFx;
        public Effects.SpriteFxAnimation VanishFx => vanishFx;
        public float MaxHealth => maxHealth;
        public float Damage => damage;
        public DamageType DamageType => damageType;
        public float AttackInterval => attackInterval;
        public float AttackRange => attackRange;
        public float AttackWindup => attackWindup;
        public float Stagger => stagger;
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public float TurnSpeed => turnSpeed;
        public float DetectionRange => detectionRange;
        public float LeashRange => leashRange;
        public float FollowDistance => followDistance;
        public float TeleportDistance => teleportDistance;
        public float SummonDuration => summonDuration;
        public float HurtDuration => hurtDuration;
        public float DeathDuration => deathDuration;

#if UNITY_EDITOR
        public void EditorSetPrefab(GameObject newPrefab, Effects.SpriteFxAnimation manifest, Effects.SpriteFxAnimation vanish)
        {
            prefab = newPrefab; manifestFx = manifest; vanishFx = vanish;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void EditorConfigure(int mantras, float health, float newDamage, float interval, float range, float windup, float newStagger,
            float walk, float run, float detection, float leash, float follow, float teleport, float summonTime, float hurtTime, float deathTime)
        {
            mantraCost = mantras; maxHealth = health; damage = newDamage; attackInterval = interval; attackRange = range;
            attackWindup = windup; stagger = newStagger; walkSpeed = walk; runSpeed = run; detectionRange = detection;
            leashRange = leash; followDistance = follow; teleportDistance = teleport;
            summonDuration = summonTime; hurtDuration = hurtTime; deathDuration = deathTime;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
