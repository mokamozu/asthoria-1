using System;
using UnityEngine;

namespace Athoria.Gameplay.Items
{
    public enum WeaponCategory { Sword, Axe, Dagger, Hammer, Spear, Shield, Magic, Unarmed, Firearm }

    public enum WeaponHandling
    {
        /// <summary>Mão direita; a esquerda fica livre para escudo.</summary>
        OneHanded,
        /// <summary>Ocupa as duas mãos; escudo não é usado.</summary>
        TwoHanded,
        /// <summary>Só mão esquerda (escudos).</summary>
        OffHand
    }

    /// <summary>Posição e rotação locais (graus) usadas pelo view model.</summary>
    [Serializable]
    public struct ViewPose
    {
        public Vector3 position;
        public Vector3 rotation;

        public ViewPose(Vector3 position, Vector3 rotation)
        {
            this.position = position;
            this.rotation = rotation;
        }

        public Quaternion Rotation => Quaternion.Euler(rotation);
    }

    /// <summary>Dados de uma arma ou escudo: visual em primeira pessoa e atributos de combate.</summary>
    [CreateAssetMenu(menuName = "Athoria/Itens/Arma", fileName = "NovaArma")]
    public sealed class WeaponData : ItemData
    {
        [Header("Tipo")]
        [SerializeField] private WeaponCategory category;
        [SerializeField] private WeaponHandling handling;

        [Header("Visual em primeira pessoa")]
        [SerializeField] private GameObject viewModelPrefab;
        [SerializeField, Tooltip("Posição/rotação do modelo dentro da mão. Pode mudar com o jogo rodando: a arma se atualiza na hora.")]
        private ViewPose modelPose;
        [SerializeField, Tooltip("Pose do braço que segura a arma (direito; esquerdo para escudos). Posição = cotovelo, em metros, a partir da câmera (X direita, Y cima, Z frente).")]
        private ViewPose holdingArmPose;
        [SerializeField, Tooltip("Pose do braço esquerdo em armas de duas mãos.")]
        private ViewPose supportArmPose;

        // Poses que a geração automática escreveu. Se as de cima forem diferentes,
        // foram ajustadas à mão e a geração (Reconstruir tudo / Gerar armas) não as sobrescreve.
        [Header("Pegada (mão de apoio)")]
        [SerializeField, Tooltip("Distância (m) da mão de apoio ao longo do cabo, a partir da mão principal (+ à frente, - atrás). " +
                                 "Use -99 para o padrão da categoria (lança 0,42 · arma de fogo 0,30 · cajado 0,36 · martelo -0,20 · espadas -0,12).")]
        private float supportGripDistance = -99f;
        [SerializeField, UnityEngine.Range(-1f, 1f), Tooltip("Quanto o pulso de apoio acompanha a inclinação do cabo (0-1). -1 = padrão da categoria.")]
        private float supportWristFollow = -1f;

        [SerializeField, Tooltip("Ações dos braços desta arma (sacar, guardar, inspecionar…). Vazio = conjunto padrão.")]
        private Athoria.Gameplay.Combat.ViewModelActionSet viewModelActions;

        [SerializeField, Tooltip("Usa os tempos abaixo em vez dos da tabela da categoria.")] private bool overrideTiming;
        [SerializeField] private Athoria.Gameplay.Combat.WeaponTiming timing = new();

        [SerializeField, HideInInspector] private ViewPose generatedModelPose, generatedHoldingPose, generatedSupportPose;
        [SerializeField, HideInInspector] private bool hasGeneratedPoses;

        [Header("Combate (usado na Etapa 4)")]
        [SerializeField, Min(0f)] private float damage = 10f;
        [SerializeField, Min(0.1f), Tooltip("Ataques leves por segundo.")] private float attackSpeed = 1.2f;
        [SerializeField, Min(0.5f), Tooltip("Alcance em metros.")] private float range = 2f;
        [SerializeField, Min(0f)] private float lightAttackStamina = 10f;
        [SerializeField, Min(0f)] private float heavyAttackStamina = 22f;
        [SerializeField, Min(1f)] private float heavyDamageMultiplier = 1.8f;

        [Header("Projétil (magia ou arma de fogo)")]
        [SerializeField, Tooltip("Magia: o ataque pesado lança o projétil gastando mana. Arma de fogo: todo ataque dispara.")]
        private GameObject spellProjectile;
        [SerializeField, Min(0f)] private float spellManaCost = 12f;
        [SerializeField, Min(1f)] private float spellSpeed = 18f;
        [SerializeField] private AudioClip projectileSound;
        [SerializeField, Min(1), Tooltip("Projéteis por disparo (escopeta: vários chumbos).")] private int projectileCount = 1;
        [SerializeField, UnityEngine.Range(0f, 30f), Tooltip("Abertura do cone dos projéteis, em graus.")] private float projectileSpread;

        [Header("Carregador (armas de fogo)")]
        [SerializeField, Min(0), Tooltip("Tiros antes de recarregar (R). 0 = sem limite.")] private int magazineSize;
        [SerializeField, Min(0.2f), Tooltip("Tempo da recarga completa, em segundos.")] private float reloadTime = 2f;

        [Header("Animação dos ataques")]
        [SerializeField, Tooltip("Conjunto de golpes (combo de 3 leves + pesado) em primeira pessoa.")]
        private Combat.AttackSet attackSet;

        public WeaponCategory Category => category;
        public Combat.AttackSet AttackSet => attackSet;
        public WeaponHandling Handling => handling;
        public GameObject ViewModelPrefab => viewModelPrefab;
        public ViewPose ModelPose => modelPose;
        public Athoria.Gameplay.Combat.ViewModelActionSet ViewModelActions => viewModelActions;
        /// <summary>Tempos próprios desta arma, ou nulo para usar a tabela da categoria.</summary>
        public Athoria.Gameplay.Combat.WeaponTiming TimingOverride => overrideTiming ? timing : null;
        /// <summary>Distância da mão de apoio no cabo (null = padrão da categoria).</summary>
        public float? SupportGripDistance => supportGripDistance > -50f ? supportGripDistance : null;
        /// <summary>Quanto o pulso de apoio segue o cabo (null = padrão da categoria).</summary>
        public float? SupportWristFollowOverride => supportWristFollow >= 0f ? supportWristFollow : null;
        public ViewPose HoldingArmPose => holdingArmPose;
        public ViewPose SupportArmPose => supportArmPose;
        public float Damage => damage * Quality;
        public float AttackSpeed => attackSpeed;
        public float Range => range;
        public float LightAttackStamina => lightAttackStamina;
        public float HeavyAttackStamina => heavyAttackStamina;
        public float HeavyDamageMultiplier => heavyDamageMultiplier;
        public GameObject SpellProjectile => spellProjectile;
        public float SpellManaCost => spellManaCost;
        public float SpellSpeed => spellSpeed;
        public bool HasSpell => spellProjectile != null;
        public AudioClip ProjectileSound => projectileSound;
        public int ProjectileCount => Mathf.Max(1, projectileCount);
        public int MagazineSize => magazineSize;
        public float ReloadTime => reloadTime;
        public float ProjectileSpread => projectileSpread;
        public bool IsFirearm => category == WeaponCategory.Firearm;

        public bool IsShield => handling == WeaponHandling.OffHand;

        public override ItemCategory ItemType => IsShield ? ItemCategory.Shield : ItemCategory.Weapon;
        public override GameObject WorldModel => base.WorldModel != null ? base.WorldModel : viewModelPrefab;

        public override string TooltipStats
        {
            get
            {
                var hands = handling switch
                {
                    WeaponHandling.TwoHanded => "Duas mãos",
                    WeaponHandling.OffHand => "Mão secundária",
                    _ => "Uma mão"
                };
                if (IsShield) return $"{hands}\nBloqueio total de ataques frontais";
                var line = $"Dano {Damage:0}   •   {attackSpeed:0.0} ataques/s";
                if (!IsFirearm) line += $"   •   Alcance {range:0.0} m";
                var extra = HasSpell && !IsFirearm ? $"\nAtaque pesado: projétil arcano ({spellManaCost:0} de mana)" : string.Empty;
                return $"{hands}\n{line}{extra}";
            }
        }
        public bool IsTwoHanded => handling == WeaponHandling.TwoHanded;

#if UNITY_EDITOR
        /// <summary>As poses foram mudadas à mão no Inspector (a geração automática as preserva).</summary>
        public bool PosesEditedByHand => hasGeneratedPoses &&
            (!SamePose(modelPose, generatedModelPose) || !SamePose(holdingArmPose, generatedHoldingPose) || !SamePose(supportArmPose, generatedSupportPose));

        private static bool SamePose(ViewPose a, ViewPose b) =>
            (a.position - b.position).sqrMagnitude < 1e-8f && (a.rotation - b.rotation).sqrMagnitude < 1e-6f;

        [ContextMenu("Voltar às poses padrão (geradas)")]
        private void RestoreGeneratedPoses()
        {
            if (!hasGeneratedPoses) return;
            UnityEditor.Undo.RecordObject(this, "Voltar às poses padrão");
            modelPose = generatedModelPose;
            holdingArmPose = generatedHoldingPose;
            supportArmPose = generatedSupportPose;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void EditorSetAttackSet(Combat.AttackSet set)
        {
            attackSet = set;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void EditorSetMagazine(int size, float reload)
        {
            magazineSize = Mathf.Max(0, size);
            reloadTime = Mathf.Max(0.2f, reload);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void EditorSetSpread(int count, float spread)
        {
            projectileCount = Mathf.Max(1, count);
            projectileSpread = spread;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void EditorSetSpell(GameObject projectile, float manaCost, float speed, AudioClip sound = null)
        {
            projectileSound = sound;
            spellProjectile = projectile;
            spellManaCost = manaCost;
            spellSpeed = speed;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void EditorConfigure(WeaponCategory newCategory, WeaponHandling newHandling, GameObject prefab,
            ViewPose model, ViewPose holdingArm, ViewPose supportArm,
            float newDamage, float newAttackSpeed, float newRange, float lightStamina, float heavyStamina, float heavyMultiplier)
        {
            category = newCategory;
            handling = newHandling;
            viewModelPrefab = prefab;
            if (!PosesEditedByHand)
            {
                modelPose = model;
                holdingArmPose = holdingArm;
                supportArmPose = supportArm;
            }
            generatedModelPose = model;
            generatedHoldingPose = holdingArm;
            generatedSupportPose = supportArm;
            hasGeneratedPoses = true;
            damage = newDamage;
            attackSpeed = newAttackSpeed;
            range = newRange;
            lightAttackStamina = lightStamina;
            heavyAttackStamina = heavyStamina;
            heavyDamageMultiplier = heavyMultiplier;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
