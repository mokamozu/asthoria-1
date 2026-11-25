using System.Collections;
using System.Collections.Generic;
using Athoria.Core;
using Athoria.Core.Events;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Player;
using Athoria.Gameplay.Stats;
using UnityEngine;

namespace Athoria.Gameplay.Characters
{
    public readonly struct AbilityStateChanged : IGameEvent
    {
        public readonly AbilityData Ability;
        /// <summary>Time.time em que fica pronta de novo.</summary>
        public readonly float ReadyAt;
        public readonly float Cooldown;
        /// <summary>Invocação ainda manifestada: indisponível e sem contar recarga.</summary>
        public readonly bool SummonActive;

        public AbilityStateChanged(AbilityData ability, float readyAt, float cooldown, bool summonActive = false)
        {
            Ability = ability;
            ReadyAt = readyAt;
            Cooldown = cooldown;
            SummonActive = summonActive;
        }
    }

    /// <summary>Executa a habilidade especial da personagem (tecla Q).</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAbility : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private float dashDuration = 0.22f;
        [SerializeField] private float dashHitRadius = 1.1f;
        [SerializeField] private LayerMask hitMask = ~0;

        private AbilityData _ability;
        private float _readyAt;
        private InputBridge _bridge;
        private readonly HashSet<IDamageable> _dashHits = new();
        private readonly Collider[] _overlap = new Collider[16];

        private Summons.SpiritCompanion _summon;

        public AbilityData Ability => _ability;
        public bool SummonActive => _summon != null;
        public bool IsReady => _ability != null && Time.time >= _readyAt;

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            var character = GetComponent<PlayerCharacter>();
            if (character != null && character.Current != null) _ability = character.Current.Ability;
        }

        private void OnEnable()
        {
            if (input != null && input.Actions != null)
            {
                var action = input.Actions.FindAction("Player/SpecialAbility");
                if (action != null)
                {
                    _bridge = new InputBridge(action, TryActivate);
                    _bridge.Enable();
                }
            }
        }

        private void OnDisable() => _bridge?.Disable();

        private void Start() => Publish();

        public void SetAbility(AbilityData ability)
        {
            if (_summon != null)
            {
                _summon.Ended -= OnSummonEnded;
                _summon.Dismiss();
                _summon = null;
            }
            _ability = ability;
            _readyAt = 0f;
            Publish();
        }

        private void Publish() => EventBus.Publish(new AbilityStateChanged(_ability, _readyAt, _ability != null ? _ability.Cooldown : 0f, SummonActive));

        public void TryActivate()
        {
            if (_ability == null || GameBootstrap.Instance != null && GameBootstrap.Instance.IsPaused) return;
            if (SummonActive)
            {
                EventBus.Publish(new GameMessage($"{_ability.Spirit.DisplayName} ainda está manifestado"));
                return;
            }
            if (!IsReady)
            {
                EventBus.Publish(new GameMessage("Habilidade em recarga"));
                return;
            }

            var health = GetComponent<Health>();
            if (health != null && !health.IsAlive) return;

            var mana = GetComponent<PlayerMana>();
            var stamina = GetComponent<PlayerStamina>();
            if (_ability.ManaCost > 0f && (mana == null || mana.Current < _ability.ManaCost))
            {
                EventBus.Publish(new GameMessage("Mana insuficiente"));
                return;
            }
            if (_ability.StaminaCost > 0f && (stamina == null || !stamina.CanSpend(_ability.StaminaCost)))
            {
                EventBus.Publish(new GameMessage("Vigor insuficiente"));
                return;
            }
            var grappleTarget = Vector3.zero;
            if (_ability.Effect == AbilityEffect.Grapple && !TryGrappleTarget(_ability.Magnitude, out grappleTarget))
            {
                EventBus.Publish(new GameMessage("Nada ao alcance para prender o arpão"));
                return;   // não gasta mana, vigor nem a recarga
            }

            var summoner = GetComponent<Summons.SummonController>();
            if (_ability.Effect == AbilityEffect.SummonSpirit)
            {
                if (summoner == null)
                {
                    EventBus.Publish(new GameMessage("Esta personagem não pode invocar espíritos"));
                    return;
                }
                if (!summoner.CanSummon(_ability.Spirit, out var reason))
                {
                    EventBus.Publish(new GameMessage(reason));
                    return;   // não gasta mana nem a recarga
                }
            }
            if (_ability.ManaCost > 0f) mana.TrySpend(_ability.ManaCost);
            if (_ability.StaminaCost > 0f) stamina.TrySpend(_ability.StaminaCost);

            switch (_ability.Effect)
            {
                case AbilityEffect.Heal:
                    if (health != null) health.Heal(_ability.Amount);
                    break;
                case AbilityEffect.HealOverTime:
                    StartCoroutine(HealOverTime(health, _ability.Amount, _ability.Duration));
                    break;
                case AbilityEffect.SpeedBoost:
                    StartCoroutine(SpeedBoost(_ability.Magnitude, _ability.Duration));
                    if (stamina != null) stamina.Restore(_ability.Amount);
                    break;
                case AbilityEffect.DashStrike:
                    StartCoroutine(DashStrike(_ability.Amount, _ability.Magnitude));
                    break;
                case AbilityEffect.Grapple:
                    GetComponent<PlayerController>().PullTo(grappleTarget, 24f);
                    break;
                case AbilityEffect.SummonSpirit:
                    _summon = summoner.Summon(_ability.Spirit, _ability);
                    if (_summon != null) _summon.Ended += OnSummonEnded;
                    break;
            }

            if (_ability.CastFx != null)
                Effects.SpriteFx.SpawnOnGround(_ability.CastFx, transform.position + transform.forward * _ability.CastFxDistance);

            // Invocação: a recarga só começa quando o espírito deixa o mundo físico (OnSummonEnded).
            _readyAt = _summon != null ? float.PositiveInfinity : Time.time + _ability.Cooldown;
            EventBus.Publish(new GameMessage(_ability.DisplayName));
            Publish();
        }

        private void OnSummonEnded(Summons.SpiritCompanion spirit)
        {
            spirit.Ended -= OnSummonEnded;
            if (!this || spirit != _summon) return;
            _summon = null;
            _readyAt = Time.time + (_ability != null ? _ability.Cooldown : 0f);
            if (_ability != null && _ability.Spirit != null)
                EventBus.Publish(new GameMessage($"{_ability.Spirit.DisplayName} se dissipou"));
            Publish();
        }

        private IEnumerator HealOverTime(Health health, float total, float duration)
        {
            if (health == null) yield break;
            var perSecond = total / Mathf.Max(0.1f, duration);
            for (var t = 0f; t < duration && health.IsAlive; t += Time.deltaTime)
            {
                health.Heal(perSecond * Time.deltaTime);
                yield return null;
            }
        }

        private IEnumerator SpeedBoost(float percent, float duration)
        {
            var stats = GetComponent<PlayerStats>();
            if (stats == null) yield break;
            var key = new object();
            stats.SetModifiers(key, new[] { new StatModifier(StatType.MoveSpeedPercent, percent) });
            yield return new WaitForSeconds(duration);
            stats.SetModifiers(key, null);
        }

        /// <summary>
        /// Arpão de abordagem: mira pelo centro da câmera; se acertar algo sólido dentro do
        /// alcance, puxa a jogadora até lá (um pouco antes, para não entrar na parede).
        /// </summary>
        private bool TryGrappleTarget(float range, out Vector3 target)
        {
            target = Vector3.zero;
            var cam = Camera.main;
            if (GetComponent<PlayerController>() == null || cam == null) return false;
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            var hits = Physics.RaycastAll(ray, range, hitMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.distance < 2f) return false;
                target = hit.point + hit.normal * 0.6f - Vector3.up * 0.9f;
                // em parede: para logo abaixo do ponto; em chão/telhado: pousa em cima
                if (hit.normal.y > 0.5f) target = hit.point + Vector3.up * 0.1f;
                return true;
            }
            return false;
        }

        private IEnumerator DashStrike(float damage, float distance)
        {
            var controller = GetComponent<PlayerController>();
            if (controller == null) yield break;
            var dir = transform.forward;
            controller.Dash(dir * (distance / dashDuration), dashDuration);
            _dashHits.Clear();

            var stats = GetComponent<PlayerStats>();
            var multiplier = stats != null ? stats.OutgoingDamageMultiplier(DamageType.Physical) : 1f;
            var feedback = GetComponent<PlayerCombat>();
            while (controller.IsDashing)
            {
                var center = transform.position + Vector3.up * 1f;
                var count = Physics.OverlapSphereNonAlloc(center, dashHitRadius, _overlap, hitMask, QueryTriggerInteraction.Ignore);
                for (var i = 0; i < count; i++)
                {
                    var col = _overlap[i];
                    if (col.transform.IsChildOf(transform)) continue;
                    var target = col.GetComponentInParent<IDamageable>();
                    if (target == null || !target.IsAlive || !_dashHits.Add(target)) continue;
                    if (_ability != null && _ability.HitFx != null)
                        Effects.SpriteFx.SpawnOnGround(_ability.HitFx, col.bounds.center);
                    target.ApplyDamage(new DamageInfo
                    {
                        Amount = damage * multiplier,
                        Type = DamageType.Physical,
                        Source = gameObject,
                        Point = col.ClosestPoint(center),
                        Normal = -dir,
                        Direction = dir,
                        StaggerPower = 2f,
                        IsHeavy = true
                    });
                }
                yield return null;
            }
        }

        /// <summary>Liga uma ação do Input System a um callback simples.</summary>
        private sealed class InputBridge
        {
            private readonly UnityEngine.InputSystem.InputAction _action;
            private readonly System.Action _callback;

            public InputBridge(UnityEngine.InputSystem.InputAction action, System.Action callback)
            {
                _action = action;
                _callback = callback;
            }

            public void Enable() => _action.performed += OnPerformed;
            public void Disable() => _action.performed -= OnPerformed;
            private void OnPerformed(UnityEngine.InputSystem.InputAction.CallbackContext _) => _callback();
        }
    }
}
