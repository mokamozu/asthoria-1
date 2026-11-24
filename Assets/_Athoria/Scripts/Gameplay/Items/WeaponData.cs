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
        [SerializeField, Tooltip("Posição/rotação do modelo dentro da mão.")]
        private ViewPose modelPose;
        [SerializeField, Tooltip("Pose do braço que segura a arma (direito; esquerdo para escudos).")]
        private ViewPose holdingArmPose;
        [SerializeField, Tooltip("Pose do braço esquerdo em armas de duas mãos.")]
        private ViewPose supportArmPose;

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

        public WeaponCategory Category => category;
        public WeaponHandling Handling => handling;
        public GameObject ViewModelPrefab => viewModelPrefab;
        public ViewPose ModelPose => modelPose;
        public ViewPose HoldingArmPose => holdingArmPose;
        public ViewPose SupportArmPose => supportArmPose;
        public float Damage => damage;
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
                var line = $"Dano {damage:0}   •   {attackSpeed:0.0} ataques/s";
                if (!IsFirearm) line += $"   •   Alcance {range:0.0} m";
                var extra = HasSpell && !IsFirearm ? $"\nAtaque pesado: projétil arcano ({spellManaCost:0} de mana)" : string.Empty;
                return $"{hands}\n{line}{extra}";
            }
        }
        public bool IsTwoHanded => handling == WeaponHandling.TwoHanded;

#if UNITY_EDITOR
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
            modelPose = model;
            holdingArmPose = holdingArm;
            supportArmPose = supportArm;
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
