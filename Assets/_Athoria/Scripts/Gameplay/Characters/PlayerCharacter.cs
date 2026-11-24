using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Player;
using Athoria.Gameplay.Stats;
using UnityEngine;

namespace Athoria.Gameplay.Characters
{
    /// <summary>
    /// Monta o jogador com a personagem escolhida no menu (ou a padrão, ao dar
    /// Play direto na cena): valores base, características e equipamento inicial.
    /// Roda antes dos outros componentes para que eles já iniciem configurados.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class PlayerCharacter : MonoBehaviour
    {
        [SerializeField, Tooltip("Usada quando a cena é iniciada sem passar pelo menu.")]
        private CharacterData fallback;

        public CharacterData Current { get; private set; }

        private void Awake()
        {
            Current = PlayerSession.SelectedCharacter != null ? PlayerSession.SelectedCharacter : fallback;
            if (Current == null)
            {
                Debug.LogWarning("[Athoria] Nenhuma personagem definida para o jogador.", this);
                return;
            }
            Apply(Current);
        }

        private void Apply(CharacterData c)
        {
            var traits = GetComponent<PlayerTraits>();
            if (traits != null) traits.SetTraits(c.Traits);
            var bonus = traits != null ? (System.Func<TraitEffect, float>)traits.FlatBonus : _ => 0f;

            if (TryGetComponent<PlayerStats>(out var stats))
            {
                stats.SetBase(
                    c.BaseHealth + bonus(TraitEffect.MaxHealth),
                    c.BaseStamina + bonus(TraitEffect.MaxStamina),
                    c.BaseMana > 0f ? c.BaseMana + bonus(TraitEffect.MaxMana) : 0f,
                    traits != null ? traits.MoveSpeedMultiplier : 1f);
            }

            if (TryGetComponent<WeaponLoadout>(out var loadout))
            {
                for (var i = 0; i < WeaponLoadout.QuickSlotCount; i++)
                    loadout.SetQuickSlot(i, c.StartingWeapons != null && i < c.StartingWeapons.Length ? c.StartingWeapons[i] : null);
                loadout.SetOffHand(c.StartingOffHand);
            }
        }
    }
}
