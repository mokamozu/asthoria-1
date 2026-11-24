using Athoria.Core.Events;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Stats;
using UnityEngine;

namespace Athoria.UI.HUD
{
    /// <summary>
    /// HUD do jogador. Só escuta eventos — não conhece os componentes do
    /// jogador. Fica discreta (semitransparente) quando tudo está cheio e
    /// nada acontece por alguns segundos.
    /// </summary>
    public sealed class HUDController : MonoBehaviour
    {
        [Header("Barras")]
        [SerializeField] private ResourceBar healthBar;
        [SerializeField] private ResourceBar staminaBar;
        [SerializeField] private ResourceBar manaBar;
        [SerializeField, Range(0f, 1f)] private float lowHealthFraction = 0.25f;

        [Header("Efeitos")]
        [SerializeField] private DamageVignette vignette;

        [Header("Modo discreto")]
        [SerializeField] private CanvasGroup resourcesGroup;
        [SerializeField] private float idleDelay = 4f;
        [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.35f;
        [SerializeField] private float fadeSpeed = 3f;

        private float _lastActivity;
        private bool _healthFull = true, _staminaFull = true, _manaFull = true;

        private void OnEnable()
        {
            EventBus.Subscribe<HealthChanged>(OnHealth);
            EventBus.Subscribe<StaminaChanged>(OnStamina);
            EventBus.Subscribe<ManaChanged>(OnMana);
            EventBus.Subscribe<DamageApplied>(OnDamage);
            _lastActivity = Time.unscaledTime;
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<HealthChanged>(OnHealth);
            EventBus.Unsubscribe<StaminaChanged>(OnStamina);
            EventBus.Unsubscribe<ManaChanged>(OnMana);
            EventBus.Unsubscribe<DamageApplied>(OnDamage);
        }

        private void OnHealth(HealthChanged e)
        {
            if (!e.Target.IsPlayer) return;
            healthBar.Set(e.Current, e.Max);
            healthBar.SetAlert(e.Current > 0f && e.Current / e.Max <= lowHealthFraction);
            _healthFull = e.Current >= e.Max;
            MarkActivity(_healthFull);
        }

        private void OnStamina(StaminaChanged e)
        {
            staminaBar.Set(e.Current, e.Max);
            staminaBar.SetAlert(e.IsExhausted);
            _staminaFull = e.Current >= e.Max;
            MarkActivity(_staminaFull);
        }

        private void OnMana(ManaChanged e)
        {
            manaBar.gameObject.SetActive(e.Max > 0f);   // sem magia: sem barra
            manaBar.Set(e.Current, e.Max);
            _manaFull = e.Max <= 0f || e.Current >= e.Max;
            MarkActivity(_manaFull);
        }

        private void OnDamage(DamageApplied e)
        {
            if (!e.Target.IsPlayer || vignette == null) return;
            if (e.Result == DamageResult.Damaged || e.Result == DamageResult.Killed)
                vignette.Flash(Mathf.Clamp01(e.Info.Amount / Mathf.Max(1f, e.Target.Max) * 3f + 0.25f));
        }

        private void MarkActivity(bool full)
        {
            if (!full) _lastActivity = Time.unscaledTime;
        }

        private void Update()
        {
            if (resourcesGroup == null) return;
            var allFull = _healthFull && _staminaFull && _manaFull;
            var idle = allFull && Time.unscaledTime - _lastActivity > idleDelay;
            var target = idle ? idleAlpha : 1f;
            resourcesGroup.alpha = Mathf.MoveTowards(resourcesGroup.alpha, target, fadeSpeed * Time.unscaledDeltaTime);
        }
    }
}
