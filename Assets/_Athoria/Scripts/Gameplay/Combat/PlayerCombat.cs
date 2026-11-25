using System.Collections.Generic;
using Athoria.Core;
using Athoria.Core.Events;
using Athoria.Gameplay.Items;
using Athoria.Gameplay.Player;
using Athoria.Gameplay.Stats;
using UnityEngine;

namespace Athoria.Gameplay.Combat
{
    /// <summary>
    /// Combate corpo a corpo do jogador.
    /// - Clique: ataque leve (clicar de novo na recuperação faz combo, alternando o lado).
    /// - Segurar: ataque pesado (mais stamina, mais dano e atordoamento).
    /// - Botão direito: bloquear (escudo absorve tudo; arma absorve parte).
    /// - Alt: esquiva com invulnerabilidade curta.
    /// Tempo dos golpes vem da velocidade de ataque da arma.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCombat : MonoBehaviour, IDamageModifier
    {
        public enum State { Idle, WindUp, HeavyWindUp, Active, Recovery, Dodging }

        [Header("Referências")]
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerStamina stamina;
        [SerializeField] private PlayerMana mana;
        [SerializeField] private Characters.PlayerTraits traits;
        [SerializeField] private PlayerStats stats;
        [SerializeField] private PlayerWeaponView weaponView;
        [SerializeField] private PlayerCameraEffects cameraEffects;
        [SerializeField] private Health health;
        [SerializeField] private Transform aimOrigin;
        [SerializeField] private CombatFeedback feedback;
        [SerializeField, Tooltip("Dados usados quando nenhuma arma está empunhada.")]
        private WeaponData unarmed;

        [Header("Ataque")]
        [SerializeField, Tooltip("Segurar por este tempo transforma o golpe em pesado.")]
        private float heavyHoldThreshold = 0.25f;
        [SerializeField, Tooltip("Tempo máximo segurando o pesado antes de soltar sozinho.")]
        private float heavyMaxHold = 1.5f;
        [SerializeField] private float inputBuffer = 0.2f;
        [SerializeField, Range(0f, 1f), Tooltip("A partir de que fração da recuperação o combo é aceito.")]
        private float comboWindowStart = 0.3f;
        [SerializeField] private float hitRadius = 0.18f;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private float attackMoveMultiplier = 0.6f;

        [Header("Fases (fração do tempo do golpe)")]
        [SerializeField] private float windupFraction = 0.3f;
        [SerializeField] private float activeFraction = 0.18f;
        [SerializeField] private float recoveryFraction = 0.52f;
        [SerializeField] private float heavyDurationMultiplier = 1.5f;

        [Header("Impacto")]
        [SerializeField] private float hitStopLight = 0.045f;
        [SerializeField] private float hitStopHeavy = 0.09f;
        [SerializeField] private float hitStopTimeScale = 0.05f;
        [SerializeField] private float shakeLight = 0.25f;
        [SerializeField] private float shakeHeavy = 0.45f;

        [Header("Bloqueio")]
        [SerializeField, Tooltip("Meio-ângulo frontal protegido.")] private float blockAngle = 75f;
        [SerializeField, Range(0f, 1f)] private float shieldAbsorb = 1f;
        [SerializeField, Range(0f, 1f)] private float weaponAbsorb = 0.7f;
        [SerializeField] private float shieldStaminaPerDamage = 1f;
        [SerializeField] private float weaponStaminaPerDamage = 1.5f;
        [SerializeField] private float guardBreakDuration = 1.2f;
        [SerializeField] private float blockMoveMultiplier = 0.5f;
        [SerializeField] private float blockRaiseSpeed = 9f;

        [Header("Esquiva")]
        [SerializeField] private float dodgeStamina = 20f;
        [SerializeField] private float dodgeSpeed = 8.5f;
        [SerializeField] private float dodgeDuration = 0.22f;
        [SerializeField] private float dodgeInvulnerability = 0.28f;
        [SerializeField] private float dodgeCooldown = 0.45f;

        private State _state = State.Idle;
        private float _stateTime;
        private float _stateDuration;
        private WeaponData _weapon;
        private bool _heavy;
        private bool _mirrored;
        private bool _bounced;
        private float _holdTime;
        private float _lastAttackEnd = float.NegativeInfinity;
        private AttackMotion.Swing _swing;
        private int _comboStep;
        private WeaponData _lastComboWeapon;
        private bool _ranged;                       // disparo/feitiço: anima o golpe, mas sem varredura corpo a corpo
        private Vector3 _fromPos, _fromRot;         // pose em que o golpe começou (combo emenda sem salto)
        private Vector3 _lastPos, _lastRot;         // última pose aplicada
        private float _sweepProgress;
        private readonly HashSet<IDamageable> _hitThisSwing = new();
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private readonly List<RaycastHit> _sorted = new();

        private float _blockBlend;
        private float _guardBrokenUntil;
        private float _invulnerableUntil;
        private float _nextDodgeTime;
        private float _hitStopUntil;
        private bool _inHitStop;

        public State CurrentState => _state;
        public bool IsBlocking => _blockBlend > 0.6f && _state == State.Idle && input.BlockHeld && !IsGuardBroken;
        public bool IsGuardBroken => Time.time < _guardBrokenUntil;
        public bool IsInvulnerable => Time.time < _invulnerableUntil;
        public WeaponData CurrentWeapon => weaponView.ShownMainHand != null ? weaponView.ShownMainHand : unarmed;

        private void Reset()
        {
            input = GetComponent<PlayerInputReader>();
            controller = GetComponent<PlayerController>();
            stamina = GetComponent<PlayerStamina>();
            mana = GetComponent<PlayerMana>();
            traits = GetComponent<Characters.PlayerTraits>();
            stats = GetComponent<PlayerStats>();
            weaponView = GetComponent<PlayerWeaponView>();
            cameraEffects = GetComponent<PlayerCameraEffects>();
            health = GetComponent<Health>();
        }

        private void Awake()
        {
            if (stats == null) stats = GetComponent<PlayerStats>();
            if (traits == null) traits = GetComponent<Characters.PlayerTraits>();
            if (mana == null) mana = GetComponent<PlayerMana>();
            if (input == null || controller == null || stamina == null || weaponView == null || aimOrigin == null)
            {
                Debug.LogError("[Athoria] PlayerCombat com referências faltando.", this);
                enabled = false;
            }
        }

        private void OnEnable() { if (health != null) health.Damaged += OnDamaged; }

        private void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
            EndHitStop();
            if (weaponView != null)
            {
                weaponView.ClearAction();
                weaponView.SetBlockBlend(0f);
            }
            if (controller != null)
            {
                controller.SpeedMultiplier = 1f;
                controller.SprintAllowed = true;
            }
        }

        private void Update()
        {
            UpdateHitStop();
            if (GameBootstrap.Instance != null && GameBootstrap.Instance.IsPaused) return;

            var dt = Time.deltaTime;
            _stateTime += dt;

            // Trocou de arma no meio do golpe: cancela.
            if (_state != State.Idle && _state != State.Dodging && _weapon != CurrentWeapon) GoIdle();

            switch (_state)
            {
                case State.Idle: TickIdle(); break;
                case State.WindUp: TickWindUp(); break;
                case State.HeavyWindUp: TickHeavyWindUp(); break;
                case State.Active: TickActive(); break;
                case State.Recovery: TickRecovery(); break;
                case State.Dodging: if (!controller.IsDashing) GoIdle(); break;
            }

            UpdateBlock(dt);
            UpdateMovementRules();
            ApplyMotion();
        }

        // ---------------- Estados ----------------

        private void TickIdle()
        {
            if (TryDodge()) return;
            if (!input.BlockHeld || IsGuardBroken) TryStartAttack(Time.time - _lastAttackEnd < 0.6f);
        }

        private void TickWindUp()
        {
            if (TryDodge()) return;
            if (input.AttackHeld) _holdTime += Time.deltaTime;
            if (_stateTime < _stateDuration) return;

            // Fim da preparação leve. Ainda segurando: espera o limite e vira pesado.
            if (input.AttackHeld)
            {
                if (_holdTime < heavyHoldThreshold) return;

                var extra = Mathf.Max(0f, _weapon.HeavyAttackStamina - _weapon.LightAttackStamina);
                if (stamina.TrySpend(extra))
                {
                    _heavy = true;
                    _swing = AttackMotion.Get(_weapon, true, 0);
                    Enter(State.HeavyWindUp, 0.4f * AttackDuration(false));
                    return;
                }
            }
            EnterActive();
        }

        private void TickHeavyWindUp()
        {
            if (TryDodge()) return;
            _holdTime += Time.deltaTime;
            var wound = _stateTime >= _stateDuration;
            if (wound && (!input.AttackHeld || _holdTime >= heavyMaxHold)) EnterActive();
        }

        private void TickActive()
        {
            var previous = _sweepProgress;
            _sweepProgress = Mathf.Clamp01(_stateTime / _stateDuration);
            Sweep(previous, _sweepProgress);
            if (_bounced || _stateTime >= _stateDuration) Enter(State.Recovery, AttackDuration(_heavy) * recoveryFraction);
        }

        private void TickRecovery()
        {
            if (TryDodge()) return;
            var progress = _stateTime / _stateDuration;
            if (!_heavy && progress >= comboWindowStart && TryStartAttack(true)) return;
            if (_stateTime >= _stateDuration)
            {
                _lastAttackEnd = Time.time;
                GoIdle();
            }
        }

        // ---------------- Ataque ----------------

        private float AttackDuration(bool heavy)
        {
            var d = 1f / Mathf.Max(0.1f, _weapon.AttackSpeed);
            return heavy ? d * heavyDurationMultiplier : d;
        }

        private FirearmAmmo _ammo;
        private FirearmAmmo Ammo => _ammo != null ? _ammo : (_ammo = GetComponent<FirearmAmmo>());

        /// <param name="chain">Emenda no golpe anterior: avança no combo (1 → 2 → 3 → 1…).</param>
        private bool TryStartAttack(bool chain)
        {
            if (Time.time - input.LastAttackPressedTime > inputBuffer) return false;
            if (!weaponView.IsReady || controller.IsDashing) return false;

            var weapon = CurrentWeapon;
            if (weapon == null || weapon.IsShield) return false;
            // Arma de fogo com carregador: não atira recarregando nem sem bala (só o clique).
            if (weapon.IsFirearm && Ammo != null)
            {
                if (Ammo.IsReloading) { input.ConsumeAttack(); return false; }
                if (!Ammo.HasRounds(weapon)) { input.ConsumeAttack(); Ammo.DryFire(weapon); return false; }
            }
            if (!stamina.TrySpend(weapon.LightAttackStamina))
            {
                input.ConsumeAttack();
                return false;
            }

            input.ConsumeAttack();
            _comboStep = chain && weapon == _lastComboWeapon ? (_comboStep + 1) % AttackMotion.ComboLength(weapon) : 0;
            _lastComboWeapon = weapon;
            _weapon = weapon;
            _heavy = false;
            _mirrored = _comboStep % 2 == 1;
            _bounced = false;
            _ranged = false;
            _holdTime = 0f;
            _swing = AttackMotion.Get(weapon, false, _comboStep);
            _fromPos = _lastPos;
            _fromRot = _lastRot;
            Enter(State.WindUp, AttackDuration(false) * windupFraction);
            EventBus.Publish(new PlayerAttackStarted(gameObject, aimOrigin.position, aimOrigin.forward));
            return true;
        }

        private void EnterActive()
        {
            _sweepProgress = 0f;
            _hitThisSwing.Clear();
            Enter(State.Active, AttackDuration(_heavy) * activeFraction);

            // Arma de fogo: todo ataque dispara. Arma mágica: o pesado lança o feitiço (se houver mana).
            if (_weapon.HasSpell && (_weapon.IsFirearm || _heavy) && (_weapon.SpellManaCost <= 0f || mana != null && mana.TrySpend(_weapon.SpellManaCost)))
            {
                if (_weapon.IsFirearm && Ammo != null) Ammo.Consume(_weapon);
                CastSpell();
                _ranged = true;    // sem varredura corpo a corpo, mas o coice/recuo é animado
                return;
            }
            if (feedback != null)
                CombatFeedback.Play(_heavy ? feedback.swingHeavy : CombatFeedback.Pick(feedback.swingLight), aimOrigin.position, 0.7f, 0.1f, false);
            Sweep(0f, 0f);
        }

        private void CastSpell()
        {
            var origin = aimOrigin.position + aimOrigin.forward * 0.6f - aimOrigin.up * 0.1f;
            var count = _weapon.ProjectileCount;
            var damage = _weapon.Damage * (_heavy ? _weapon.HeavyDamageMultiplier : 1f) * DamageMultiplier();
            var type = _weapon.IsFirearm ? DamageType.Physical : DamageType.Arcane;
            for (var i = 0; i < count; i++)
            {
                // Escopeta: cada chumbo sai num ponto aleatório do cone.
                var rotation = aimOrigin.rotation;
                if (count > 1 && _weapon.ProjectileSpread > 0f)
                {
                    var p = Random.insideUnitCircle * _weapon.ProjectileSpread;
                    rotation *= Quaternion.Euler(p.y, p.x, 0f);
                }
                var go = Instantiate(_weapon.SpellProjectile, origin, rotation);
                var projectile = go.GetComponent<Projectile>();
                if (projectile != null) projectile.Launch(gameObject, damage, type, (_heavy ? 2f : 1f) / Mathf.Sqrt(count), _weapon.SpellSpeed);
            }
            var sound = _weapon.ProjectileSound != null ? _weapon.ProjectileSound : feedback != null ? feedback.swingHeavy : null;
            CombatFeedback.Play(sound, aimOrigin.position, 0.8f, 0.08f, false);
            weaponView.Recoil(new Vector3(0f, 0.4f, -1.2f));
            if (cameraEffects != null) cameraEffects.AddShake(0.15f);
        }

        private void Sweep(float from, float to)
        {
            var range = _weapon.Range;
            var steps = Mathf.Max(1, Mathf.CeilToInt((to - from) * 8f));
            for (var i = 1; i <= steps && !_bounced && !_ranged; i++)
            {
                var p = Mathf.Lerp(from, to, i / (float)steps);
                var angles = Vector2.Lerp(_swing.SweepStart, _swing.SweepEnd, p);
                var dir = aimOrigin.rotation * Quaternion.Euler(angles.y, angles.x, 0f) * Vector3.forward;
                CastAndHit(dir, range);
            }
        }

        private void CastAndHit(Vector3 dir, float range)
        {
            var origin = aimOrigin.position;
            var count = Physics.SphereCastNonAlloc(origin, hitRadius, dir, _hits, range, hitMask, QueryTriggerInteraction.Ignore);
            _sorted.Clear();
            for (var i = 0; i < count; i++)
            {
                if (_hits[i].collider.transform.IsChildOf(transform)) continue;
                _sorted.Add(_hits[i]);
            }
            _sorted.Sort((a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in _sorted)
            {
                // Esfera já sobrepondo no início: distância 0 e ponto inválido.
                var point = hit.distance <= 0f ? origin + dir * 0.4f : hit.point;
                var normal = hit.distance <= 0f ? -dir : hit.normal;

                var target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null)
                {
                    if (!target.IsAlive || !_hitThisSwing.Add(target)) continue;
                    HitTarget(target, hit.collider, point, normal, dir);
                    continue;
                }

                // Mundo: a arma para na parede (não atravessa para acertar o que está atrás).
                if (hit.distance <= range * 0.85f) HitWorld(hit.collider, point, normal);
                return;
            }
        }

        private void HitTarget(IDamageable target, Collider col, Vector3 point, Vector3 normal, Vector3 dir)
        {
            var multiplier = _heavy ? _weapon.HeavyDamageMultiplier : 1f;
            var info = new DamageInfo
            {
                Amount = _weapon.Damage * multiplier * DamageMultiplier(),
                Type = _weapon.Category == WeaponCategory.Magic ? DamageType.Arcane : DamageType.Physical,
                Source = gameObject,
                Point = point,
                Normal = normal,
                Direction = dir,
                StaggerPower = _heavy ? 2f : 1f,
                IsHeavy = _heavy
            };

            var result = target.ApplyDamage(info);
            if (result == DamageResult.Ignored) return;

            if (result == DamageResult.Blocked)
            {
                if (feedback != null)
                {
                    CombatFeedback.Play(feedback.block, point);
                    CombatFeedback.SpawnParticles(feedback.blockParticles, point, normal);
                }
                weaponView.Recoil(new Vector3(0.6f, 0.4f, -1.5f));
                _bounced = true;
                return;
            }

            if (feedback != null) feedback.PlayImpact(SurfaceMaterial.Of(col), point, normal);
            var killed = result == DamageResult.Killed;
            StartHitStop((_heavy ? hitStopHeavy : hitStopLight) + (killed ? 0.03f : 0f));
            if (cameraEffects != null) cameraEffects.AddShake(_heavy ? shakeHeavy : shakeLight);
        }

        private void HitWorld(Collider col, Vector3 point, Vector3 normal)
        {
            if (feedback != null) feedback.PlayImpact(SurfaceMaterial.Of(col), point, normal, 0.8f);
            weaponView.Recoil(new Vector3(0.5f, 0.6f, -1.8f));
            if (cameraEffects != null) cameraEffects.AddShake(0.2f);
            StartHitStop(0.03f);
            _bounced = true;
        }

        // ---------------- Defesa ----------------

        private void UpdateBlock(float dt)
        {
            var wants = input.BlockHeld && _state == State.Idle && !IsGuardBroken;
            _blockBlend = Mathf.MoveTowards(_blockBlend, wants ? 1f : 0f, blockRaiseSpeed * dt);
            weaponView.SetBlockBlend(_blockBlend);
        }

        private bool TryDodge()
        {
            if (Time.time - input.LastDodgePressedTime > inputBuffer) return false;
            if (Time.time < _nextDodgeTime || controller.IsDashing || !controller.IsGrounded) return false;
            if (_state == State.Active) return false;

            input.ConsumeDodge();
            var cost = dodgeStamina * (traits != null ? traits.DodgeStaminaMultiplier : 1f);
            if (!stamina.TrySpend(cost)) return false;

            var move = input.Move;
            var dir = transform.right * move.x + transform.forward * move.y;
            if (dir.sqrMagnitude < 0.01f) dir = -transform.forward;     // sem direção: recua
            controller.Dash(dir.normalized * dodgeSpeed, dodgeDuration);
            _invulnerableUntil = Time.time + dodgeInvulnerability;
            _nextDodgeTime = Time.time + dodgeDuration + dodgeCooldown;
            if (feedback != null) CombatFeedback.Play(feedback.dodge, transform.position, 0.6f, 0.1f, false);
            Enter(State.Dodging, dodgeDuration);
            return true;
        }

        /// <summary>Chamado pelo Health do jogador antes de aplicar dano.</summary>
        public bool ModifyIncomingDamage(ref DamageInfo info)
        {
            if (IsInvulnerable) return false;   // esquiva
            if (!IsBlocking) return true;

            var incoming = -info.Direction;
            incoming.y = 0f;
            var facing = aimOrigin.forward;
            facing.y = 0f;
            if (incoming.sqrMagnitude < 0.001f || Vector3.Angle(incoming, facing) > blockAngle) return true;

            var hasShield = weaponView.HasShield;
            var absorb = hasShield ? shieldAbsorb : weaponAbsorb;
            var cost = info.Amount * (hasShield ? shieldStaminaPerDamage : weaponStaminaPerDamage);
            info.WasBlocked = true;

            if (!stamina.IsExhausted && stamina.Current >= cost)
            {
                stamina.Drain(cost);
                info.Amount *= 1f - absorb;
                if (feedback != null)
                {
                    CombatFeedback.Play(feedback.block, info.Point);
                    CombatFeedback.SpawnParticles(feedback.blockParticles, info.Point, -info.Direction);
                }
                weaponView.Recoil(new Vector3(0f, 0.3f, -1.4f));
                if (cameraEffects != null) cameraEffects.AddShake(0.2f);
                return info.Amount > 0f;
            }

            // Guarda quebrada: stamina acabou, metade do dano passa.
            stamina.Drain(stamina.Current + 1f);
            info.Amount *= 0.5f;
            _guardBrokenUntil = Time.time + guardBreakDuration;
            if (feedback != null) CombatFeedback.Play(feedback.guardBreak, info.Point);
            weaponView.Recoil(new Vector3(-0.4f, -0.8f, -2.2f));
            if (cameraEffects != null) cameraEffects.AddShake(0.6f);
            return true;
        }

        private void OnDamaged(DamageInfo info, DamageResult result)
        {
            if (result != DamageResult.Damaged && result != DamageResult.Killed) return;
            if (feedback != null) CombatFeedback.Play(feedback.playerHurt, transform.position, 0.9f, 0.1f, false);
            if (cameraEffects != null) cameraEffects.AddShake(0.5f);
            // Golpes pesados interrompem a preparação do ataque.
            if (info.StaggerPower >= 2f && (_state == State.WindUp || _state == State.HeavyWindUp)) GoIdle();
        }

        // ---------------- Movimento e animação ----------------

        private void UpdateMovementRules()
        {
            var attacking = _state == State.WindUp || _state == State.HeavyWindUp || _state == State.Active;
            float mult = 1f;
            if (attacking) mult = attackMoveMultiplier;
            else if (_state == State.Recovery) mult = Mathf.Lerp(attackMoveMultiplier, 1f, 0.5f);
            else if (_blockBlend > 0.01f) mult = Mathf.Lerp(1f, blockMoveMultiplier, _blockBlend);
            controller.SpeedMultiplier = mult;
            controller.SprintAllowed = _state == State.Idle && _blockBlend < 0.01f;
        }

        private void ApplyMotion()
        {
            Vector3 pos, rot;
            var t = _stateDuration > 0f ? Mathf.Clamp01(_stateTime / _stateDuration) : 1f;
            switch (_state)
            {
                case State.WindUp:
                case State.HeavyWindUp:
                {
                    var e = 1f - (1f - t) * (1f - t);           // ease-out: prepara rápido e segura
                    if (_state == State.HeavyWindUp)
                    {
                        // Sai da pose armada do golpe leve e vai para a preparação pesada; treme ao segurar.
                        var light = AttackMotion.Get(_weapon, false, _comboStep);
                        _swing.SampleWindup(e, out var hp, out var hr);
                        pos = Vector3.Lerp(light.WindupPos, hp, e);
                        rot = Vector3.Lerp(light.WindupRot, hr, e);
                        if (t >= 1f) pos += Random.insideUnitSphere * 0.003f;
                    }
                    else
                    {
                        // Emenda a partir de onde o golpe anterior parou
                        _swing.SampleWindup(e, out pos, out rot);
                        pos += _fromPos * (1f - e);
                        rot += _fromRot * (1f - e);
                    }
                    break;
                }
                case State.Active:
                {
                    var e = t * t * (3f - 2f * t);
                    _swing.SampleStrike(e, out pos, out rot);
                    break;
                }
                case State.Recovery:
                {
                    if (_bounced)
                    {
                        // Bateu e voltou: recua a partir do ponto do impacto, sem a continuação do golpe.
                        var e = 1f - (1f - t) * (1f - t);
                        _swing.SampleStrike(_sweepProgress, out var sp, out var sr);
                        pos = Vector3.Lerp(sp, Vector3.zero, e);
                        rot = Vector3.Lerp(sr, Vector3.zero, e);
                    }
                    else _swing.SampleRecover(t, out pos, out rot);
                    break;
                }
                default:
                    _lastPos = _lastRot = Vector3.zero;
                    weaponView.ClearAction();
                    return;
            }

            _lastPos = pos;
            _lastRot = rot;
            var bothArms = _weapon != null && _weapon.IsTwoHanded;
            weaponView.SetAction(pos, rot, bothArms);
        }

        private float DamageMultiplier()
        {
            if (_weapon == null) return 1f;
            var m = traits != null ? traits.DamageMultiplierFor(_weapon.Category) : 1f;
            if (stats != null)
            {
                var type = _weapon.Category == WeaponCategory.Magic ? DamageType.Arcane : DamageType.Physical;
                m *= stats.OutgoingDamageMultiplier(type);
            }
            return m;
        }

        // ---------------- Utilitários ----------------

        private void Enter(State state, float duration)
        {
            _state = state;
            _stateTime = 0f;
            _stateDuration = Mathf.Max(0.01f, duration);
        }

        private void GoIdle()
        {
            _state = State.Idle;
            _stateTime = 0f;
            _stateDuration = 0f;
            _weapon = null;
            _heavy = false;
        }

        private void StartHitStop(float duration)
        {
            if (GameBootstrap.Instance != null && GameBootstrap.Instance.IsPaused) return;
            _hitStopUntil = Mathf.Max(_hitStopUntil, Time.unscaledTime + duration);
            _inHitStop = true;
            Time.timeScale = hitStopTimeScale;
        }

        private void UpdateHitStop()
        {
            if (_inHitStop && Time.unscaledTime >= _hitStopUntil) EndHitStop();
        }

        private void EndHitStop()
        {
            if (!_inHitStop) return;
            _inHitStop = false;
            if (GameBootstrap.Instance == null || !GameBootstrap.Instance.IsPaused) Time.timeScale = 1f;
        }
    }
}
