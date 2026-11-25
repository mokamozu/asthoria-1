using System.Collections;
using System.Collections.Generic;
using Athoria.Core.Events;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Player;
using Athoria.Gameplay.World;
using UnityEngine;
using UnityEngine.AI;

namespace Athoria.Gameplay.Bosses
{
    /// <summary>
    /// Inteligência do Ancião do Brejo (chefe tanque), em máquina de estados:
    /// Adormecido (pedra) → Despertando → Observando ⇄ Aproximando → ataques
    /// (Soco pesado, Salto, Arroto venenoso, Golpe rápido) → Recuperação; Cambaleando
    /// após impactos fortes; Morte. Escolhe o ataque pela distância, recargas e o último
    /// ataque usado (nunca puramente aleatório). Hitboxes em BossHitbox, efeitos em
    /// BossEffects, poses em MossAncientAnimator, câmera do encontro em BossEncounterCinematic.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(NavMeshAgent))]
    public sealed class MossAncientBrain : MonoBehaviour, IDamageModifier
    {
        public enum State { Dormant, Awakening, Idle, Approach, HeavySlam, JumpAttack, PoisonBelch, QuickStrike, Recovery, Stagger, Death }

        [SerializeField] private BossData data;
        [SerializeField, Tooltip("Identidade no save: derrotado uma vez, não desperta mais.")] private string uniqueId = "anciao_do_brejo";
        [SerializeField] private MossAncientAnimator anim;
        [SerializeField] private BossEffects fx;
        [SerializeField] private BossEncounterCinematic cinematic;
        [SerializeField, Tooltip("Boca (de onde sai o arroto).")] private Transform mouth;
        [SerializeField, Tooltip("Altura do jogo de cena do salto (m).")] private float jumpHeight = 3.2f;

        private Health _health;
        private NavMeshAgent _agent;
        private AudioSource _voice;
        private State _state = State.Dormant;
        private float _stateTime, _stateDuration;
        private readonly Dictionary<BossAttackData, float> _readyAt = new();
        private readonly Dictionary<BossAttackData, float> _lastUsed = new();
        private BossAttackData _lastAttack;
        private float _closeTime;
        private float _poise, _staggerReadyAt;
        private float _nextThink;
        private bool _interruptible;
        private Coroutine _routine;
        private GroundMarker _marker;
        private PlayerController _player;
        private Health _playerHealth;

        public State Current => _state;
        public BossData Data => data;
        public Health Health => _health;
        public bool IsDormant => _state == State.Dormant;
        public string DisplayName => data != null ? data.DisplayName : name;
        public string UniqueId => uniqueId;

        // ================================================================= ciclo de vida

        private void Awake()
        {
            _health = GetComponent<Health>();
            _agent = GetComponent<NavMeshAgent>();
            if (anim == null) anim = GetComponentInChildren<MossAncientAnimator>();
            if (fx == null) fx = GetComponent<BossEffects>();
            if (cinematic == null) cinematic = GetComponent<BossEncounterCinematic>();
            _voice = gameObject.AddComponent<AudioSource>();
            _voice.spatialBlend = 1f; _voice.minDistance = 6f; _voice.maxDistance = 60f; _voice.rolloffMode = AudioRolloffMode.Linear;
            _agent.enabled = false;
            if (data == null) { Debug.LogError($"[Athoria] {name}: chefe sem BossData.", this); enabled = false; return; }
            _health.SetMax(data.maxHealth, true);
            _agent.speed = data.walkSpeed;
            _agent.angularSpeed = data.turnSpeed;
            _agent.acceleration = 6f;
            _agent.updateRotation = false;
            _agent.stoppingDistance = 0.5f;
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
            if (anim != null) anim.Step += OnStep;
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
            if (anim != null) anim.Step -= OnStep;
        }

        private void Start()
        {
            if (!string.IsNullOrEmpty(uniqueId) && WorldState.Has(WorldState.BossKey(uniqueId)))
            {
                // Já derrotado neste save: fica como um monte de pedra sem vida, sem despertar de novo.
                _state = State.Death;
                _health.Invulnerable = true;
                if (anim != null) anim.Snap(BossPose.Death);
                var awakener = GetComponent<MossAwakener>();
                if (awakener != null) awakener.enabled = false;
                return;
            }
            EnterDormant();
        }

        private void EnterDormant()
        {
            Enter(State.Dormant);
            _health.Invulnerable = true;          // sem dano, sem reação, sem barra de vida
            if (anim != null) anim.Snap(BossPose.Dormant);
        }

        // ================================================================= despertar

        public bool CanAwaken => _state == State.Dormant && enabled;

        public void Awaken()
        {
            if (!CanAwaken) return;
            _routine = StartCoroutine(AwakenRoutine());
        }

        private IEnumerator AwakenRoutine()
        {
            Enter(State.Awakening);
            var duration = data.awakenDuration;
            FindPlayer();
            if (_player != null) FaceInstant(_player.transform.position);
            if (anim != null) anim.Play(BossPose.Awakening, duration);
            if (cinematic != null) cinematic.Play(duration, 0.55f);
            if (fx != null) fx.MossFall(true);
            Voice(data.rocksSound, 0.9f);
            yield return new WaitForSeconds(duration * 0.5f);
            if (fx != null) fx.MossFall(false);
            yield return new WaitForSeconds(duration * 0.05f);
            // O grito
            Voice(data.roarSound, 1f);
            BossHitbox.ShakePlayer(transform.position, 0.7f, 40f);
            yield return new WaitForSeconds(duration * 0.45f);

            _health.Invulnerable = false;
            PlaceOnNavMesh();
            EventBus.Publish(new BossEncounterStarted(_health, DisplayName));
            _nextThink = Time.time + data.thinkTime;
            EnterIdle();
        }

        // ================================================================= atualização

        private void Update()
        {
            if (_state is State.Dormant or State.Awakening or State.Death) return;
            var dt = Time.deltaTime;
            if (dt <= 0f) return;
            _stateTime += dt;
            if (!FindPlayer()) return;

            var dist = FlatDistance(_player.transform.position);
            _closeTime = dist <= data.closeRange ? _closeTime + dt : Mathf.Max(0f, _closeTime - dt * 2f);

            switch (_state)
            {
                case State.Idle:
                    Face(_player.transform.position, 1f);
                    if (Time.time >= _nextThink) Think(dist);
                    break;
                case State.Approach:
                    TickApproach(dist);
                    break;
                case State.Recovery:
                case State.Stagger:
                    if (_stateTime >= _stateDuration) EnterIdle();
                    break;
            }
        }

        private void Think(float dist)
        {
            _nextThink = Time.time + data.thinkTime;
            if (_playerHealth == null || !_playerHealth.IsAlive) return;
            var attack = ChooseAttack(dist);
            if (attack != null) { StartAttack(attack); return; }
            var slam = data.heavySlam;
            var wantRange = slam != null ? slam.maxRange * 0.85f : data.closeRange;
            if (dist > wantRange) Enter(State.Approach);
        }

        /// <summary>
        /// Escolha previsível: só ataques prontos e no alcance; o golpe rápido só quando a
        /// jogadora insiste em ficar colada; peso maior para quem não é usado há mais tempo
        /// e penalidade para repetir o último ataque.
        /// </summary>
        private BossAttackData ChooseAttack(float dist)
        {
            BossAttackData best = null;
            var bestScore = 0f;
            foreach (var a in data.Attacks)
            {
                if (a == null || dist < a.minRange || dist > a.maxRange) continue;
                if (_readyAt.TryGetValue(a, out var ready) && Time.time < ready) continue;
                if (a.kind == BossAttackKind.QuickStrike && _closeTime < data.closePressureTime) continue;
                var since = _lastUsed.TryGetValue(a, out var used) ? Time.time - used : 30f;
                var score = a.weight * (1f + Mathf.Min(since, 30f) * 0.04f) * (a == _lastAttack ? 0.45f : 1f);
                if (a.kind == BossAttackKind.QuickStrike) score *= 3f;   // pressão de perto tem prioridade
                if (score > bestScore) { bestScore = score; best = a; }
            }
            return best;
        }

        private void TickApproach(float dist)
        {
            var slam = data.heavySlam;
            if (slam != null && dist <= slam.maxRange * 0.8f || Time.time >= _nextThink && ChooseAttack(dist) != null)
            {
                StopAgent();
                EnterIdle();
                return;
            }
            if (_agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                if (_stateTime < 0.05f || Time.frameCount % 10 == 0) _agent.SetDestination(_player.transform.position);
                var v = _agent.desiredVelocity; v.y = 0f;
                if (v.sqrMagnitude > 0.01f) Face(transform.position + v, 1f);
            }
            else Face(_player.transform.position, 1f);
            if (anim != null && anim.Current != BossPose.Walk) anim.Play(BossPose.Walk);
        }

        // ================================================================= ataques

        private void StartAttack(BossAttackData a)
        {
            StopAgent();
            _lastAttack = a;
            _lastUsed[a] = Time.time;
            _readyAt[a] = Time.time + a.windup + a.active + a.cooldown;
            switch (a.kind)
            {
                case BossAttackKind.HeavySlam: Enter(State.HeavySlam); _routine = StartCoroutine(SlamRoutine(a)); break;
                case BossAttackKind.JumpAttack: Enter(State.JumpAttack); _routine = StartCoroutine(JumpRoutine(a)); break;
                case BossAttackKind.PoisonBelch: Enter(State.PoisonBelch); _routine = StartCoroutine(BelchRoutine(a)); break;
                case BossAttackKind.QuickStrike: Enter(State.QuickStrike); _routine = StartCoroutine(QuickRoutine(a)); break;
            }
        }

        // Ataque 1 — soco pesado no chão: ergue o braço devagar, aviso no chão, impacto em área e onda de choque.
        private IEnumerator SlamRoutine(BossAttackData a)
        {
            _interruptible = true;
            anim?.Play(BossPose.SlamWindup, a.windup);
            Voice(a.windupSound, 0.8f);
            var t = 0f;
            Vector3 point = default;
            var warned = false;
            while (t < a.windup)
            {
                t += Time.deltaTime;
                if (t < a.windup * 0.6f) Face(_player.transform.position, 0.8f);           // mira e depois trava
                point = BossHitbox.Ground(transform.position + transform.forward * a.reach, transform);
                if (!warned && t >= a.windup - a.warningTime) { warned = true; _marker = fx?.Warning(point, a.radius, a.warningTime + a.active); }
                yield return null;
            }
            _interruptible = false;
            anim?.Play(BossPose.SlamHit, a.active);
            yield return new WaitForSeconds(a.active);
            ClearMarker();
            BossHitbox.Area(gameObject, point, a.radius, a.damage, a.damageType, a.stagger);
            fx?.Dust(point, a.radius / 3f);
            fx?.Shockwave(point, a.radius);
            Voice(a.impactSound, 1f);
            BossHitbox.ShakePlayer(point, 0.55f);
            EnterRecovery(a);
        }

        // Ataque 2 — salto: agacha mirando onde a jogadora está, voa até lá e esmaga a área.
        private IEnumerator JumpRoutine(BossAttackData a)
        {
            _interruptible = true;
            anim?.Play(BossPose.JumpCrouch, a.windup);
            Voice(a.windupSound, 0.8f);
            var target = BossHitbox.Ground(_player.transform.position, transform);      // posição do momento da preparação
            if (NavMesh.SamplePosition(target, out var nav, 3f, NavMesh.AllAreas)) target = nav.position;
            _marker = fx?.Warning(target, a.radius, a.windup + a.active);
            var t = 0f;
            while (t < a.windup) { t += Time.deltaTime; Face(target, 1.2f); yield return null; }

            _interruptible = false;
            var start = transform.position;
            _agent.enabled = false;
            anim?.Play(BossPose.JumpAir, a.active);
            fx?.Dust(start, 1.2f);
            for (t = 0f; t < a.active; t += Time.deltaTime)
            {
                var k = Mathf.Clamp01(t / a.active);
                transform.position = Vector3.Lerp(start, target, k) + Vector3.up * (4f * jumpHeight * k * (1f - k));
                yield return null;
            }
            transform.position = target;
            ClearMarker();
            anim?.Play(BossPose.JumpLand, 0.4f);
            BossHitbox.Area(gameObject, target, a.radius, a.damage, a.damageType, a.stagger);
            fx?.Dust(target, a.radius / 2.5f);
            fx?.Debris(target, a.radius / 3f);
            fx?.Shockwave(target, a.radius * 1.2f);
            Voice(a.impactSound, 1f);
            BossHitbox.ShakePlayer(target, 0.8f);
            PlaceOnNavMesh();
            EnterRecovery(a);
        }

        // Ataque 3 — arroto venenoso: puxa o ar (peito estufa), arrota e a nuvem fica no lugar.
        private IEnumerator BelchRoutine(BossAttackData a)
        {
            _interruptible = true;
            anim?.Play(BossPose.BelchInhale, a.windup);
            Voice(a.windupSound, 0.9f);
            var t = 0f;
            Vector3 at = default;
            var warned = false;
            while (t < a.windup)
            {
                t += Time.deltaTime;
                if (t < a.windup * 0.6f) Face(_player.transform.position, 0.9f);   // depois trava a direção
                var from = mouth != null ? mouth.position : transform.position + Vector3.up * 2f;
                at = BossHitbox.Ground(from + transform.forward * (a.radius * 0.9f), transform);
                if (!warned && t >= a.windup - Mathf.Max(a.warningTime, 0.3f))
                {
                    warned = true;
                    _marker = fx?.Warning(at, a.radius, a.windup - t + 0.2f, true);   // aviso verde: veneno
                }
                yield return null;
            }
            ClearMarker();
            _interruptible = false;
            anim?.Play(BossPose.Belch, a.active);
            Voice(a.impactSound, 1f);
            fx?.Cloud(gameObject, at, a.radius, a.cloudDuration, a.poisonDps, a.poisonTick);
            yield return new WaitForSeconds(a.active);
            EnterRecovery(a);
        }

        // Ataque 4 — golpe rápido: quase sem aviso, varre de perto com o braço esquerdo.
        private IEnumerator QuickRoutine(BossAttackData a)
        {
            _interruptible = false;
            _closeTime = 0f;
            anim?.Play(BossPose.QuickWindup, a.windup);
            Voice(a.windupSound, 0.9f);                       // rosnado curto + marca rápida na frente
            _marker = fx?.Warning(BossHitbox.Ground(transform.position + transform.forward * (a.maxRange * 0.55f), transform),
                a.maxRange * 0.6f, a.windup + a.active * 0.4f);
            var t = 0f;
            while (t < a.windup) { t += Time.deltaTime; Face(_player.transform.position, 3f); yield return null; }
            anim?.Play(BossPose.QuickStrike, a.active);
            Voice(a.impactSound, 0.9f);
            yield return new WaitForSeconds(a.active * 0.4f);
            ClearMarker();
            BossHitbox.Cone(gameObject, transform.position + Vector3.up * 1.2f, transform.forward, a.maxRange + 0.6f, a.arc, a.damage, a.damageType, a.stagger);
            yield return new WaitForSeconds(a.active * 0.6f);
            EnterRecovery(a);
        }

        private void EnterRecovery(BossAttackData a)
        {
            _routine = null;
            _interruptible = true;
            _readyAt[a] = Time.time + a.cooldown;
            Enter(State.Recovery, a.recovery);
            anim?.Play(BossPose.Recover, a.recovery);
        }

        private void EnterIdle()
        {
            _interruptible = true;
            Enter(State.Idle);
            anim?.Play(BossPose.Idle);
        }

        // ================================================================= dano, atordoamento e morte

        /// <summary>Defesa e resistência de tanque: subtrai a defesa e corta uma fração, mas sempre passa um mínimo.</summary>
        public bool ModifyIncomingDamage(ref DamageInfo info)
        {
            if (data == null) return true;
            var reduced = Mathf.Max(0f, info.Amount - data.defense) * (1f - data.damageResistance);
            info.Amount = Mathf.Max(reduced, info.Amount * data.minDamageFraction);
            return true;
        }

        private void OnDamaged(DamageInfo info, DamageResult result)
        {
            if (_state is State.Death or State.Dormant or State.Awakening || result != DamageResult.Damaged) return;
            if (Random.value < 0.35f) Voice(data.hurtSound, 0.7f);
            _poise += info.Amount * (info.IsHeavy ? 2f : 1f);
            if (_poise < data.staggerThreshold || Time.time < _staggerReadyAt || !_interruptible) return;
            _poise = 0f;
            _staggerReadyAt = Time.time + data.staggerImmunity;
            StopAttack();
            Enter(State.Stagger, data.staggerDuration);
            anim?.Play(BossPose.Hit, data.staggerDuration);
        }

        private void OnDied(DamageInfo info)
        {
            StopAttack();
            StopAgent();
            _agent.enabled = false;
            Enter(State.Death);
            anim?.Play(BossPose.Death, 3.5f);
            Voice(data.deathSound != null ? data.deathSound : data.roarSound, 1f);
            Voice(data.rocksSound, 0.8f);
            BossHitbox.ShakePlayer(transform.position, 0.5f, 40f);
            if (!string.IsNullOrEmpty(uniqueId)) WorldState.Set(WorldState.BossKey(uniqueId));
            EventBus.Publish(new BossDefeated(_health, DisplayName));
        }

        private void StopAttack()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            ClearMarker();
            if (!_agent.enabled) PlaceOnNavMesh();
        }

        private void ClearMarker()
        {
            if (_marker != null) _marker.Hide();
            _marker = null;
        }

        // ================================================================= utilidades

        private void Enter(State s, float duration = 0f)
        {
            _state = s;
            _stateTime = 0f;
            _stateDuration = duration;
        }

        private bool FindPlayer()
        {
            if (_player == null)
            {
                _player = FindAnyObjectByType<PlayerController>();
                _playerHealth = _player != null ? _player.GetComponent<Health>() : null;
            }
            return _player != null;
        }

        private void OnStep()
        {
            Voice(data.stepSound, 0.6f);
            BossHitbox.ShakePlayer(transform.position, 0.08f, 14f);
        }

        private void Voice(AudioClip clip, float volume)
        {
            if (clip != null) _voice.PlayOneShot(clip, volume);
        }

        private float FlatDistance(Vector3 p)
        {
            var d = p - transform.position; d.y = 0f;
            return d.magnitude;
        }

        private void Face(Vector3 point, float speedMultiplier)
        {
            var to = point - transform.position; to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), data.turnSpeed * speedMultiplier * Time.deltaTime);
        }

        private void FaceInstant(Vector3 point)
        {
            var to = point - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to);
        }

        private void StopAgent()
        {
            if (_agent.enabled && _agent.isOnNavMesh) { _agent.isStopped = true; _agent.ResetPath(); }
        }

        private void PlaceOnNavMesh()
        {
            if (!NavMesh.SamplePosition(transform.position, out var hit, 4f, NavMesh.AllAreas)) return;
            _agent.enabled = true;
            _agent.Warp(hit.position);
        }
    }
}
