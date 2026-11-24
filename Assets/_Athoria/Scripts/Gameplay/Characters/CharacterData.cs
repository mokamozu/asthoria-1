using System;
using Athoria.Core.Data;
using Athoria.Gameplay.Items;
using UnityEngine;

namespace Athoria.Gameplay.Characters
{
    public enum TraitEffect
    {
        [InspectorName("Vida máxima (+valor)")] MaxHealth,
        [InspectorName("Vigor máximo (+valor)")] MaxStamina,
        [InspectorName("Mana máxima (+valor)")] MaxMana,
        [InspectorName("Regenera vida fora de combate (valor/s)")] HealthRegenOutOfCombat,
        [InspectorName("Velocidade de movimento (+%)")] MoveSpeedPercent,
        [InspectorName("Dano com categoria de arma (+%)")] DamagePercentWithCategory,
        [InspectorName("Custo de vigor da esquiva (-%)")] DodgeStaminaReductionPercent
    }

    /// <summary>Característica especial da personagem. Todo efeito listado aqui funciona no jogo.</summary>
    [Serializable]
    public struct CharacterTrait
    {
        public string name;
        [TextArea(1, 3)] public string description;
        public TraitEffect effect;
        public float value;
        [Tooltip("Só para 'Dano com categoria de arma'.")]
        public WeaponCategory category;
    }

    /// <summary>
    /// Personagem jogável. Para adicionar uma nova: criar um asset deste tipo
    /// e incluí-lo no CharacterDatabase — nenhum sistema central muda.
    /// </summary>
    [CreateAssetMenu(menuName = "Athoria/Personagens/Personagem", fileName = "NovaPersonagem")]
    public sealed class CharacterData : GameData
    {
        [Header("Identidade")]
        [SerializeField] private string title;
        [SerializeField] private string characterClass;
        [SerializeField] private string region;
        [SerializeField] private Color accentColor = new(0.8f, 0.6f, 0.3f);

        [Header("Arte")]
        [SerializeField] private Sprite portrait;
        [SerializeField] private Sprite portraitSmall;
        [SerializeField] private Sprite fullBody;
        [SerializeField] private Sprite regionEmblem;

        [Header("Valores iniciais")]
        [SerializeField, Min(1f)] private float baseHealth = 100f;
        [SerializeField, Min(1f)] private float baseStamina = 100f;
        [SerializeField, Min(0f), Tooltip("0 = personagem não usa magia.")] private float baseMana = 50f;

        [Header("Características")]
        [SerializeField] private CharacterTrait[] traits = Array.Empty<CharacterTrait>();

        [Header("Equipamento inicial")]
        [SerializeField, Tooltip("Até 4 armas nos atalhos 1-4.")] private WeaponData[] startingWeapons = Array.Empty<WeaponData>();
        [SerializeField] private WeaponData startingOffHand;

        public string Title => title;
        public string CharacterClass => characterClass;
        public string Region => region;
        public Color AccentColor => accentColor;
        public Sprite Portrait => portrait;
        public Sprite PortraitSmall => portraitSmall != null ? portraitSmall : portrait;
        public Sprite FullBody => fullBody;
        public Sprite RegionEmblem => regionEmblem;
        public float BaseHealth => baseHealth;
        public float BaseStamina => baseStamina;
        public float BaseMana => baseMana;
        public CharacterTrait[] Traits => traits;
        public WeaponData[] StartingWeapons => startingWeapons;
        public WeaponData StartingOffHand => startingOffHand;

#if UNITY_EDITOR
        public void EditorConfigure(string newTitle, string newClass, string newRegion, Color accent,
            Sprite newPortrait, Sprite newSmall, Sprite newFullBody, Sprite emblem,
            float health, float stamina, float mana, CharacterTrait[] newTraits,
            WeaponData[] weapons, WeaponData offHand)
        {
            title = newTitle; characterClass = newClass; region = newRegion; accentColor = accent;
            portrait = newPortrait; portraitSmall = newSmall; fullBody = newFullBody; regionEmblem = emblem;
            baseHealth = health; baseStamina = stamina; baseMana = mana;
            traits = newTraits; startingWeapons = weapons; startingOffHand = offHand;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
