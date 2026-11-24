using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Stats;
using UnityEngine;

namespace Athoria.Gameplay.Items
{
    public enum ConsumableEffect
    {
        [InspectorName("Restaura vida")] RestoreHealth,
        [InspectorName("Restaura vigor")] RestoreStamina,
        [InspectorName("Restaura mana")] RestoreMana
    }

    /// <summary>Poções e outros itens de uso único.</summary>
    [CreateAssetMenu(menuName = "Athoria/Itens/Consumível", fileName = "NovoConsumivel")]
    public sealed class ConsumableData : ItemData
    {
        [Header("Efeito")]
        [SerializeField] private ConsumableEffect effect;
        [SerializeField, Min(0f)] private float amount = 30f;

        public override ItemCategory ItemType => ItemCategory.Consumable;
        public ConsumableEffect Effect => effect;
        public float Amount => amount;

        public override string TooltipStats => effect switch
        {
            ConsumableEffect.RestoreHealth => $"Restaura {amount:0} de vida",
            ConsumableEffect.RestoreStamina => $"Restaura {amount:0} de vigor",
            _ => $"Restaura {amount:0} de mana"
        };

        /// <summary>Aplica o efeito. Retorna false se não teve efeito (ex.: vida cheia) — o item não é gasto.</summary>
        public bool Use(GameObject user)
        {
            switch (effect)
            {
                case ConsumableEffect.RestoreHealth:
                    if (!user.TryGetComponent<Health>(out var health) || !health.IsAlive || health.Current >= health.Max) return false;
                    health.Heal(amount);
                    return true;
                case ConsumableEffect.RestoreStamina:
                    if (!user.TryGetComponent<PlayerStamina>(out var stamina) || stamina.Current >= stamina.Max) return false;
                    stamina.Restore(amount);
                    return true;
                default:
                    if (!user.TryGetComponent<PlayerMana>(out var mana) || mana.Max <= 0f || mana.Current >= mana.Max) return false;
                    mana.Restore(amount);
                    return true;
            }
        }

#if UNITY_EDITOR
        public void EditorSetEffect(ConsumableEffect newEffect, float newAmount)
        {
            effect = newEffect;
            amount = newAmount;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
