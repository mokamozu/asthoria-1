using Athoria.Core.Events;
using Athoria.Gameplay.Characters;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace Athoria.UI.HUD
{
    /// <summary>
    /// Barra central inferior: [1][2] armas  [Q] habilidade  [3][4] consumíveis,
    /// e o nome da arma em uso. Só escuta eventos.
    /// </summary>
    public sealed class HotbarView : MonoBehaviour
    {
        [SerializeField] private HotbarSlotView weapon1;
        [SerializeField] private HotbarSlotView weapon2;
        [SerializeField] private HotbarSlotView ability;
        [SerializeField] private HotbarSlotView item3;
        [SerializeField] private HotbarSlotView item4;
        [SerializeField] private Text weaponName;
        [SerializeField] private Text abilityName;

        private float _readyAt;
        private float _cooldown;
        private bool _hasAbility;

        private void OnEnable()
        {
            EventBus.Subscribe<WeaponLoadoutChanged>(OnWeapons);
            EventBus.Subscribe<QuickItemsChanged>(OnItems);
            EventBus.Subscribe<AbilityStateChanged>(OnAbility);
            EventBus.Subscribe<Athoria.Gameplay.Summons.MantrasChanged>(OnMantras);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<WeaponLoadoutChanged>(OnWeapons);
            EventBus.Unsubscribe<QuickItemsChanged>(OnItems);
            EventBus.Unsubscribe<AbilityStateChanged>(OnAbility);
            EventBus.Unsubscribe<Athoria.Gameplay.Summons.MantrasChanged>(OnMantras);
        }

        private void OnWeapons(WeaponLoadoutChanged e)
        {
            weapon1.Set(e.Slot1 != null ? e.Slot1.Icon : null, null, e.ActiveSlot == 0);
            weapon2.Set(e.Slot2 != null ? e.Slot2.Icon : null, null, e.ActiveSlot == 1);
            _weaponLabel = e.MainHand != null ? e.MainHand.DisplayName : "Desarmada";
            if (weaponName != null) weaponName.text = _weaponLabel;
            _shownAmmo = -2;
        }

        private string _weaponLabel = string.Empty;
        private int _shownAmmo = -2;
        private bool _shownReloading;
        private FirearmAmmo _ammo;

        /// <summary>Tiros no carregador ao lado do nome da arma de fogo (ex.: "Carabina de Tambor  4/6").</summary>
        private void UpdateAmmo()
        {
            if (weaponName == null) return;
            if (_ammo == null) _ammo = FindAnyObjectByType<FirearmAmmo>();
            if (_ammo == null) return;
            var w = _ammo.Current;
            var cap = FirearmAmmo.Capacity(w);
            var n = cap > 0 ? _ammo.Rounds(w) : -1;
            if (n == _shownAmmo && _ammo.IsReloading == _shownReloading) return;
            _shownAmmo = n;
            _shownReloading = _ammo.IsReloading;
            weaponName.text = cap <= 0 ? _weaponLabel
                : _ammo.IsReloading ? $"{_weaponLabel}  recarregando…"
                : $"{_weaponLabel}  {n}/{cap}";
        }

        private void OnItems(QuickItemsChanged e)
        {
            item3.Set(e.Slot3 != null ? e.Slot3.Icon : null, e.Slot3 != null ? e.Count3.ToString() : null, false, e.Slot3 != null && e.Count3 == 0);
            item4.Set(e.Slot4 != null ? e.Slot4.Icon : null, e.Slot4 != null ? e.Count4.ToString() : null, false, e.Slot4 != null && e.Count4 == 0);
        }

        private void OnAbility(AbilityStateChanged e)
        {
            _summonActive = e.SummonActive;
            _hasAbility = e.Ability != null;
            _readyAt = e.ReadyAt;
            _cooldown = e.Cooldown;
            _abilityIcon = e.Ability != null ? e.Ability.Icon : null;
            ability.Set(_abilityIcon, _mantraLabel, false);
            _abilityLabel = e.Ability != null ? e.Ability.DisplayName : string.Empty;
            RefreshAbilityName();
        }

        private bool _summonActive;
        private string _abilityLabel = string.Empty;
        private string _mantraLabel;
        private Sprite _abilityIcon;

        /// <summary>Mantras Ativos (só para quem invoca): contador "usados/limite" no canto do espaço da habilidade.</summary>
        private void OnMantras(Athoria.Gameplay.Summons.MantrasChanged e)
        {
            _mantraLabel = e.Capacity > 0 ? $"{e.Used}/{e.Capacity}" : null;
            ability.Set(_abilityIcon, _mantraLabel, false);
        }

        private void RefreshAbilityName()
        {
            if (abilityName != null) abilityName.text = _abilityLabel;
        }

        private void Update()
        {
            UpdateAmmo();
            if (_hasAbility && _summonActive)
            {
                ability.SetCooldown(1f, 0f, "ATIVO");   // invocação manifestada: indisponível, sem contar recarga
                return;
            }
            if (!_hasAbility || _cooldown <= 0f)
            {
                ability.SetCooldown(0f, 0f);
                return;
            }
            var left = Mathf.Max(0f, _readyAt - Time.time);
            ability.SetCooldown(left / _cooldown, left);
        }
    }
}
