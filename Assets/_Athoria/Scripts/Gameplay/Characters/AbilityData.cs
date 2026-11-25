using Athoria.Core.Data;
using UnityEngine;

namespace Athoria.Gameplay.Characters
{
    public enum AbilityEffect
    {
        [InspectorName("Cura instantânea")] Heal,
        [InspectorName("Cura ao longo do tempo")] HealOverTime,
        [InspectorName("Impulso de velocidade (+ vigor)")] SpeedBoost,
        [InspectorName("Investida cortante")] DashStrike,
        [InspectorName("Arpão de abordagem (puxa até o ponto mirado)")] Grapple,
        [InspectorName("Invocar espírito (recarga começa quando ele desaparece)")] SummonSpirit
    }

    /// <summary>Habilidade especial (tecla Q). Cada efeito listado aqui funciona no jogo.</summary>
    [CreateAssetMenu(menuName = "Athoria/Personagens/Habilidade", fileName = "NovaHabilidade")]
    public sealed class AbilityData : GameData
    {
        [SerializeField] private AbilityEffect effect;
        [SerializeField, Min(0.5f)] private float cooldown = 20f;
        [SerializeField, Min(0f)] private float manaCost;
        [SerializeField, Min(0f)] private float staminaCost;

        [Header("Valores do efeito")]
        [SerializeField, Tooltip("Cura, dano da investida ou vigor recuperado.")] private float amount = 30f;
        [SerializeField, Tooltip("Duração (cura ao longo do tempo, impulso).")] private float duration = 4f;
        [SerializeField, Tooltip("% de velocidade (impulso) ou distância em metros (investida, alcance do arpão).")] private float magnitude = 30f;

        [Header("Invocação")]
        [SerializeField, Tooltip("Espírito invocado (efeito Invocar espírito).")] private Summons.SpiritData spirit;

        [Header("Efeitos visuais (opcional)")]
        [SerializeField, Tooltip("Tocado ao usar, no chão à frente do jogador.")] private Effects.SpriteFxAnimation castFx;
        [SerializeField, Min(0f)] private float castFxDistance = 2.5f;
        [SerializeField, Tooltip("Tocado em cada alvo atingido (investida).")] private Effects.SpriteFxAnimation hitFx;

        public AbilityEffect Effect => effect;
        public float Cooldown => cooldown;
        public float ManaCost => manaCost;
        public float StaminaCost => staminaCost;
        public float Amount => amount;
        public float Duration => duration;
        public float Magnitude => magnitude;
        public Effects.SpriteFxAnimation CastFx => castFx;
        public float CastFxDistance => castFxDistance;
        public Effects.SpriteFxAnimation HitFx => hitFx;
        public Summons.SpiritData Spirit => spirit;

#if UNITY_EDITOR
        public void EditorSetSpirit(Summons.SpiritData newSpirit)
        {
            spirit = newSpirit;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void EditorSetFx(Effects.SpriteFxAnimation cast, float distance, Effects.SpriteFxAnimation hit)
        {
            castFx = cast;
            castFxDistance = distance;
            hitFx = hit;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void EditorConfigure(AbilityEffect newEffect, float newCooldown, float mana, float stamina, float newAmount, float newDuration, float newMagnitude)
        {
            effect = newEffect; cooldown = newCooldown; manaCost = mana; staminaCost = stamina;
            amount = newAmount; duration = newDuration; magnitude = newMagnitude;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
