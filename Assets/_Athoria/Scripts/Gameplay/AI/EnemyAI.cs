using System;
using System.Collections;
using System.Collections.Generic;
using Athoria.Core.Events;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Player;
using Athoria.Gameplay.World;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace Athoria.Gameplay.AI
{
    /// <summary>
    /// Inteligência dos inimigos, em máquina de estados:
    /// Parado/Patrulha → (vê ou ouve) → Investigar / Perseguir → Atacar
    /// (preparação, golpe, recuperação) com Bloqueio, Atordoamento e Recuo;
    /// perdeu o jogador ou foi longe demais do posto → Voltar (recupera vida).
    /// Tudo é afinado pelo EnemyData.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(NavMeshAgent))]
    [DefaultExecutionOrder(50)]
    public sealed class EnemyAI : MonoBehaviour, IDamageModifier
    {
        public enum State { Idle, Patrol, Investigate, Chase, Attack, Block, Stagger, Retreat, Return, Dead }
        private enum AttackPhase { Windup, Active, Recovery }

        [SerializeField] private EnemyData data;
        [SerializeField] private EnemyAnimatorBase animator;
        [SerializeField, Tooltip("Ponto dos olhos (visão). Vazio = 85% da altura.")] private Transform eyes;
        [SerializeField, Tooltip("Saída dos projéteis. Vazio = olhos.")] private Transform muzzle;
        [SerializeField] private CombatFeedback feedback;
        [SerializeField, Tooltip("Pontos de patrulha. Vazio = pontos aleatórios em volta do posto.")]
        private Transform[] patrolPoints = Array.Empty<Transform>();
        [SerializeField, Tooltip("Segundos para reaparecer no posto depois de morrer (0 = não reaparece).")]
        private float respawnDelay;
        [SerializeField, Tooltip("Segundos que o corpo fica no chão (quando não reaparece).")]
        private float corpseTime = 12f;
        [SerializeField] private LayerMask sightMask = ~0;
        [SerializeField, Tooltip("Preencha só em inimigos únicos (chefes): morto uma vez, não volta mais (fica no save).")]
        private string uniqueId;

        private static readonly List<EnemyAI> s_all = new();
        public static IReadOnlyList<EnemyAI> All => s_all;

        private static PlayerController s_player;
        private static Health s_playerHealth;

        private Health _health;
        private NavMeshAgent _agent;
        private Collider[] _colliders;
        private Renderer[] _renderers;
        private readonly RaycastHit[] _rayHits = new RaycastHit[12];

        private Vector3 _post;
        private Quaternion _postRotation;
        private State _state;
        private float _stateTime;
        private float _stateDuration;

        // Percepção
        private bool _hasTarget;
        private bool _canSeeTarget;
        private Vector3 _lastKnown;
        private float _lastSeenTime;
        private float _nextPerception;

        // Combate
        private EnemyAttack _attack;
        private int _attackIndex;
        private AttackPhase _phase;
        private float _phaseTime;
        private bool _attackHit;
        private float[] _attackReadyAt = Array.Empty<float>();
        private float _nextAttackTime;
        private float _poiseDamage;
        private float _lastPoiseHit;
        private int _guardLeft;
        private bool _guardBroken;
        private bool _riposteQueued;
        private float _nextBlockTime;
        private bool _retreatedThisFight;
        private bool _backpedal;
        private float _circleSign = 1f;
        private float _nextCircleChange;

        // Movimento
        private bool _patrolMoving;
        private float _patrolWaitUntil;
        private int _patrolIndex;
        private float _nextRepath;
        private Vector3 _lastDestination;
        private Vector3 _lastPosition;
        private Coroutine _deathRoutine;

        public EnemyData Data => data;
        public State CurrentState => _state;
        public bool IsAlive => _health != null && _health.IsAlive;
        public bool IsAware => _hasTarget;
        public Health Health => _health;
        public string DisplayName => data != null ? data.DisplayName : name;
        public string UniqueId => uniqueId;
        public event Action<State> StateChanged;

        // ---------------- Ciclo de vida ----------------

        private void Awake()
        {
            _health = GetComponent<Health>();
            _agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponentInChildren<EnemyAnimatorBase>();
            _colliders = GetComponentsInChildren<Collider>();
            _renderers = GetComponentsInChildren<Renderer>();
            _post = transform.position;
            _postRotation = transform.rotation;
            _agent.enabled = false;   // liga no Start, depois que o NavMesh existir

            if (data == null)
            {
                Debug.LogError($"[Athoria] {name}: EnemyAI sem EnemyData.", this);
                enabled = false;
                return;
            }

            _health.SetMax(data.maxHealth, true);
            _attackReadyAt = new float[data.attacks?.Length ?? 0];
            _guardLeft = data.guardHits;
            _agent.radius = data.agentRadius;
            _agent.height = data.agentHeight;
            _agent.speed = data.walkSpeed;
            _agent.angularSpeed = data.turnSpeed;
            _agent.acceleration = 24f;
            _agent.stoppingDistance = 0.15f;
            _agent.autoBraking = true;
            _agent.updateRotation = false;   // a rotação é nossa (encarar o alvo, mirar golpes)
        }

        private void OnEnable()
        {
            s_all.Add(this);
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
            EventBus.Subscribe<PlayerAttackStarted>(OnPlayerAttack);
        }

        private void OnDisable()
        {
            s_all.Remove(this);
            if (_health != null)
            {
                _health.Damaged -= OnDamaged;
                _health.Died -= OnDied;
            }
            EventBus.Unsubscribe<PlayerAttackStarted>(OnPlayerAttack);
        }

        private void Start()
        {
            if (!string.IsNullOrEmpty(uniqueId) && WorldState.Has(WorldState.BossKey(uniqueId)))
            {
                Destroy(gameObject);   // chefe já derrotado neste save
                return;
            }
            PlaceOnNavMesh();
            _lastPosition = transform.position;
            EnterRest();
        }

        private void PlaceOnNavMesh()
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, 3f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                _post = hit.position;
                _agent.enabled = true;
                _agent.Warp(hit.position);
            }
            else
            {
                Debug.LogWarning($"[Athoria] {name}: sem NavMesh por perto; o inimigo vai ficar parado.", this);
            }
        }

        private bool AgentReady => _agent.enabled && _agent.isOnNavMesh;
        private bool HasPatrol => patrolPoints.Length > 0 || data.patrolRadius > 0.1f;

        // ---------------- Atualização ----------------

        private void Update()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_state != State.Dead)
            {
                _stateTime += dt;
                if (Time.time - _lastPoiseHit > 2.5f) _poiseDamage = 0f;
                if (Time.time >= _nextPerception)
                {
                    _nextPerception = Time.time + 0.12f;
                    Perceive();
                }

                switch (_state)
                {
                    case State.Idle: TickIdle(); break;
                    case State.Patrol: TickPatrol(); break;
                    case State.Investigate: TickInvestigate(); break;
                    case State.Chase: TickChase(); break;
                    case State.Attack: TickAttack(dt); break;
                    case State.Block: TickBlock(); break;
                    case State.Stagger: TickStagger(); break;
                    case State.Retreat: TickRetreat(); break;
                    case State.Return: TickReturn(dt); break;
                }
            }

            var moved = transform.position - _lastPosition;
            moved.y = 0f;
            if (animator != null) animator.SetSpeed(moved.magnitude / dt);
            _lastPosition = transform.position;
        }

        private void Enter(State state, float duration = 0f)
        {
            _state = state;
            _stateTime = 0f;
            _stateDuration = duration;
            if (animator != null)
            {
                switch (state)
                {
                    case State.Block: animator.SetPose(EnemyAnimator.Pose.Block); break;
                    case State.Stagger: animator.SetPose(EnemyAnimator.Pose.Stagger); break;
                    case State.Dead: animator.SetPose(EnemyAnimator.Pose.Dead); break;
                    case State.Attack: break;   // o ataque define a pose
                    default: animator.SetPose(EnemyAnimator.Pose.None); break;
                }
            }
            StateChanged?.Invoke(state);
        }

        private void EnterRest()
        {
            _patrolMoving = false;
            _patrolWaitUntil = Time.time + Random.Range(data.patrolWait.x, data.patrolWait.y);
            Enter(HasPatrol ? State.Patrol : State.Idle);
        }

        // ---------------- Percepção ----------------

        private static bool FindPlayer()
        {
            if (s_player != null) return true;
            s_player = FindAnyObjectByType<PlayerController>();
            s_playerHealth = s_player != null ? s_player.GetComponent<Health>() : null;
            return s_player != null;
        }

        private static bool TargetValid => FindPlayer() && s_playerHealth != null && s_playerHealth.IsAlive;

        private static Vector3 TargetPoint() => s_player.transform.position + Vector3.up * (s_player.EyeHeight * 0.8f);

        private Vector3 EyePosition() => eyes != null ? eyes.position : transform.position + Vector3.up * (data.agentHeight * 0.85f);

        // ---------------- Foco: a jogadora ou um aliado dela (espírito invocado) ----------------
        private PlayerAlly _ally;

        /// <summary>O alvo atual ainda existe e está vivo.</summary>
        private bool FocusValid => _ally != null ? _ally.IsAlive : TargetValid;
        private Transform FocusTransform => _ally != null ? _ally.transform : s_player.transform;
        private Vector3 FocusPosition => FocusTransform.position;
        private Vector3 FocusPoint() => _ally != null ? _ally.AimPoint : TargetPoint();
        private Health FocusHealth => _ally != null ? _ally.Health : s_playerHealth;
        private Vector3 FocusVelocity => _ally != null ? Vector3.zero : s_player.HorizontalVelocity;

        private void Perceive()
        {
            _canSeeTarget = false;
            if (_ally != null && !_ally.IsAlive) _ally = null;
            var playerOk = TargetValid;

            // Voltando para o posto: ignora todos até chegar perto de casa (ou ser atacado).
            if (_state == State.Return && FlatDistance(_post) > 4f) return;

            var eye = EyePosition();
            var seesPlayer = false;
            var playerDist = float.MaxValue;
            var crouching = false;
            if (playerOk)
            {
                var target = TargetPoint();
                var toTarget = target - eye;
                playerDist = toTarget.magnitude;
                crouching = s_player.IsCrouching;
                var range = data.sightRange * (crouching ? 0.55f : 1f);
                var flat = toTarget;
                flat.y = 0f;

                var noticed = playerDist <= data.senseRadius * (crouching ? 0.6f : 1f)
                              || playerDist <= range && Vector3.Angle(transform.forward, flat) <= data.sightAngle * 0.5f
                              || _hasTarget && playerDist <= range;   // em combate acompanha o jogador em volta
                seesPlayer = noticed && HasLineOfSight(eye, target, s_player.transform);
            }

            // Aliados da jogadora (espíritos) também são alvos: fica com o mais próximo que enxergar.
            var ally = SpotAlly(eye, out var allyDist);
            if (ally != null && (!seesPlayer || allyDist < playerDist))
            {
                _ally = ally;
                _canSeeTarget = true;
                _lastKnown = ally.transform.position;
                _lastSeenTime = Time.time;
                if (!_hasTarget) Engage(true);
                return;
            }

            if (seesPlayer)
            {
                _ally = null;
                _canSeeTarget = true;
                _lastKnown = s_player.transform.position;
                _lastSeenTime = Time.time;
                if (!_hasTarget) Engage(true);
                return;
            }

            if (_ally == null && !playerOk)
            {
                if (_hasTarget) GiveUp();
                return;
            }
            if (_ally != null || !playerOk) return;   // segue o aliado pela última posição conhecida

            if (!_hasTarget && s_player.IsSprinting && !crouching && playerDist <= data.hearingRange)
                Hear(s_player.transform.position);
        }

        private PlayerAlly SpotAlly(Vector3 eye, out float bestDist)
        {
            PlayerAlly best = null;
            bestDist = float.MaxValue;
            foreach (var ally in PlayerAlly.All)
            {
                if (!ally.IsAlive) continue;
                var point = ally.AimPoint;
                var to = point - eye;
                var dist = to.magnitude;
                var flat = to;
                flat.y = 0f;
                var noticed = dist <= data.senseRadius
                              || dist <= data.sightRange && Vector3.Angle(transform.forward, flat) <= data.sightAngle * 0.5f
                              || _hasTarget && dist <= data.sightRange;
                if (!noticed || dist >= bestDist || !HasLineOfSight(eye, point, ally.transform)) continue;
                best = ally;
                bestDist = dist;
            }
            return best;
        }

        private bool HasLineOfSight(Vector3 from, Vector3 to) => HasLineOfSight(from, to, null);

        /// <summary>Linha de visão até "to"; "goal" é quem se procura (null = o foco atual).</summary>
        private bool HasLineOfSight(Vector3 from, Vector3 to, Transform goal)
        {
            var dir = to - from;
            var dist = dir.magnitude;
            if (dist < 0.01f) return true;
            var count = Physics.RaycastNonAlloc(from, dir / dist, _rayHits, dist, sightMask, QueryTriggerInteraction.Ignore);
            Array.Sort(_rayHits, 0, count, RayHitComparer.Instance);
            for (var i = 0; i < count; i++)
            {
                var t = _rayHits[i].collider.transform;
                if (t.IsChildOf(transform)) continue;
                var seeking = goal != null ? goal : _ally != null ? _ally.transform : s_player != null ? s_player.transform : null;
                if (seeking != null && t.IsChildOf(seeking)) return true;
                if (t.GetComponentInParent<EnemyAI>() != null) continue;   // aliados não tapam a visão
                return false;
            }
            return true;
        }

        private sealed class RayHitComparer : IComparer<RaycastHit>
        {
            public static readonly RayHitComparer Instance = new();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }

        private void Engage(bool alertAllies)
        {
            var wasAware = _hasTarget;
            _hasTarget = true;
            _lastSeenTime = Time.time;
            if (!wasAware)
            {
                _retreatedThisFight = false;
                _nextAttackTime = Time.time + Random.Range(0.4f, 0.9f);
                CombatFeedback.Play(data.alertSound, EyePosition(), 0.9f);
                EventBus.Publish(new EnemyEngaged(this));
            }
            if (alertAllies) AlertAllies();
            if (_state is State.Idle or State.Patrol or State.Investigate or State.Return) Enter(State.Chase);
        }

        private void AlertAllies()
        {
            if (data.alertRadius <= 0f) return;
            foreach (var other in s_all)
            {
                if (other == this || !other.IsAlive || other._hasTarget || other._state == State.Dead) continue;
                if ((other.transform.position - transform.position).sqrMagnitude > data.alertRadius * data.alertRadius) continue;
                other._lastKnown = _lastKnown;
                other.Engage(false);
            }
        }

        private void Hear(Vector3 position)
        {
            if (_state is not (State.Idle or State.Patrol or State.Investigate)) return;
            _lastKnown = position;
            Enter(State.Investigate);
        }

        /// <summary>Perdeu o jogador de vez: volta para o posto.</summary>
        private void GiveUp()
        {
            _hasTarget = false;
            _ally = null;
            _canSeeTarget = false;
            if (_state != State.Dead) Enter(State.Return);
        }

        private void OnPlayerAttack(PlayerAttackStarted e)
        {
            if (_state == State.Dead || !TargetValid) return;
            var toMe = transform.position - e.Origin;
            toMe.y = 0f;
            var dist = toMe.magnitude;

            if (!_hasTarget)
            {
                if (dist <= data.hearingRange * 0.8f) Hear(e.Origin);
                return;
            }

            // Lê o golpe: levanta a guarda se o jogador está atacando na direção dele.
            if (!data.canBlock || _state != State.Chase || dist > 4f || Time.time < _nextBlockTime) return;
            var forward = e.Forward;
            forward.y = 0f;
            if (Vector3.Angle(forward, toMe) > 50f) return;
            _nextBlockTime = Time.time + data.blockDuration + 1.2f;
            if (Random.value > data.blockChance) return;
            StopAgent();
            Enter(State.Block, data.blockDuration);
        }

        // ---------------- Estados ----------------

        private void TickIdle()
        {
            StopAgent();
            transform.rotation = Quaternion.RotateTowards(transform.rotation, _postRotation, data.turnSpeed * 0.3f * Time.deltaTime);
        }

        private void TickPatrol()
        {
            if (!_patrolMoving)
            {
                StopAgent();
                if (Time.time < _patrolWaitUntil) return;
                if (TryPickPatrolPoint(out var point))
                {
                    _patrolMoving = true;
                    MoveTo(point, data.walkSpeed, true);
                }
                else _patrolWaitUntil = Time.time + 2f;
                return;
            }

            FaceMovement();
            if (Arrived() || _stateTime > 20f)
            {
                _patrolMoving = false;
                _stateTime = 0f;
                _patrolWaitUntil = Time.time + Random.Range(data.patrolWait.x, data.patrolWait.y);
            }
        }

        private bool TryPickPatrolPoint(out Vector3 point)
        {
            if (patrolPoints.Length > 0)
            {
                var p = patrolPoints[_patrolIndex % patrolPoints.Length];
                _patrolIndex++;
                point = p != null ? p.position : _post;
                return true;
            }
            for (var i = 0; i < 6; i++)
            {
                var offset = Random.insideUnitCircle * data.patrolRadius;
                if (NavMesh.SamplePosition(_post + new Vector3(offset.x, 0f, offset.y), out var hit, 1.5f, NavMesh.AllAreas))
                {
                    point = hit.position;
                    return true;
                }
            }
            point = _post;
            return false;
        }

        private void TickInvestigate()
        {
            if (FlatDistance(_lastKnown) > 1.2f && _stateTime < 12f)
            {
                MoveTo(_lastKnown, data.walkSpeed * 1.4f);
                FaceMovement();
                return;
            }

            // Chegou: olha em volta e desiste.
            StopAgent();
            transform.Rotate(0f, Mathf.Sin(_stateTime * 1.6f) * 70f * Time.deltaTime, 0f);
            if (_stateDuration <= 0f) _stateDuration = _stateTime + 3f;   // procura por 3 s
            if (_stateDuration > 0f && _stateTime >= _stateDuration) Enter(State.Return);
        }

        private void TickChase()
        {
            if (!_hasTarget || !FocusValid)
            {
                GiveUp();
                return;
            }

            var targetPos = FocusPosition;
            var dist = FlatDistance(targetPos);

            if (FlatDistance(_post) > data.leashRange && dist > 2.5f)
            {
                GiveUp();
                return;
            }

            if (!_canSeeTarget && Time.time - _lastSeenTime > data.memoryTime)
            {
                _hasTarget = false;
                Enter(State.Investigate);
                return;
            }

            if (data.retreatHealthFraction > 0f && !_retreatedThisFight && _health.Normalized <= data.retreatHealthFraction)
            {
                _retreatedThisFight = true;
                StartRetreat(data.retreatDuration, false);
                return;
            }

            if (data.IsRanged && _canSeeTarget && dist < data.preferredDistance * 0.55f && _stateTime > 0.5f)
            {
                StartRetreat(1.3f, true);
                return;
            }

            if (_canSeeTarget && Time.time >= _nextAttackTime && TryPickAttack(dist, out var index))
            {
                StartAttack(index);
                return;
            }

            var desired = data.IsRanged ? data.preferredDistance : MeleeRange() * 0.8f;
            if (_canSeeTarget && dist <= desired + 0.6f)
            {
                if (data.circlesTarget || data.IsRanged)
                {
                    if (Time.time >= _nextCircleChange)
                    {
                        _circleSign = Random.value < 0.5f ? -1f : 1f;
                        _nextCircleChange = Time.time + Random.Range(1.5f, 3f);
                    }
                    var away = transform.position - targetPos;
                    away.y = 0f;
                    away = away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward;
                    var side = Quaternion.Euler(0f, 40f * _circleSign, 0f) * away;
                    MoveTo(targetPos + side * Mathf.Max(desired, 1.6f), data.walkSpeed);
                }
                else StopAgent();
                Face(targetPos);
                return;
            }

            MoveTo(_canSeeTarget ? targetPos : _lastKnown, data.runSpeed);
            if (_canSeeTarget && dist < 5f) Face(targetPos);
            else FaceMovement();
        }

        private void TickBlock()
        {
            StopAgent();
            if (FocusValid) Face(FocusPosition);
            if (_stateTime >= _stateDuration) Enter(_hasTarget ? State.Chase : State.Return);
        }

        /// <summary>A jogadora aparou o golpe no tempo certo: o ataque é cancelado e o inimigo cambaleia.</summary>
        public void Parried()
        {
            if (_state == State.Dead) return;
            EnterStagger(data.staggerDuration * 1.5f);
        }

        private void EnterStagger(float duration)
        {
            StopAgent();
            _attack = null;
            Enter(State.Stagger, duration);
        }

        private void TickStagger()
        {
            if (_stateTime < _stateDuration) return;
            _nextAttackTime = Mathf.Max(_nextAttackTime, Time.time + 0.3f);
            Enter(_hasTarget ? State.Chase : State.Return);
        }

        private void StartRetreat(float duration, bool backpedal)
        {
            _backpedal = backpedal;
            Enter(State.Retreat, duration);
        }

        private void TickRetreat()
        {
            if (!FocusValid)
            {
                GiveUp();
                return;
            }

            var targetPos = FocusPosition;
            var away = transform.position - targetPos;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward;

            // Tenta fugir em linha reta; se houver parede, tenta pelos lados.
            var found = false;
            var dest = transform.position;
            foreach (var angle in new[] { 0f, 45f, -45f, 90f, -90f })
            {
                var candidate = transform.position + Quaternion.Euler(0f, angle, 0f) * away * 4f;
                if (!NavMesh.SamplePosition(candidate, out var hit, 1f, NavMesh.AllAreas)) continue;
                if (NavMesh.Raycast(transform.position, hit.position, out _, NavMesh.AllAreas)) continue;
                dest = hit.position;
                found = true;
                break;
            }

            if (found) MoveTo(dest, _backpedal ? data.walkSpeed * 1.5f : data.runSpeed);
            else StopAgent();

            if (_backpedal) Face(targetPos);
            else FaceMovement();

            if (_stateTime >= _stateDuration || !found && _stateTime > 0.5f)
            {
                _nextAttackTime = Time.time + (_backpedal ? 0.1f : 0.6f);
                Enter(State.Chase);
            }
        }

        private void TickReturn(float dt)
        {
            if (data.returnRegenPerSecond > 0f) _health.Heal(data.returnRegenPerSecond * dt);

            if (FlatDistance(_post) > 0.6f)
            {
                MoveTo(_post, data.walkSpeed * 1.5f);
                FaceMovement();
                if (_stateTime < 30f) return;
                // Preso no caminho: volta direto (evita inimigos perdidos pelo mapa).
                if (AgentReady) _agent.Warp(_post);
            }

            StopAgent();
            _health.Heal(_health.Max);
            _guardLeft = data.guardHits;
            _poiseDamage = 0f;
            EnterRest();
        }

        // ---------------- Ataque ----------------

        private float MeleeRange()
        {
            var best = float.MaxValue;
            foreach (var a in data.attacks)
                if (a != null && a.projectile == null) best = Mathf.Min(best, a.maxRange);
            return best == float.MaxValue ? 1.5f : best;
        }

        private bool TryPickAttack(float dist, out int index)
        {
            index = -1;
            var total = 0f;
            for (var i = 0; i < data.attacks.Length; i++)
                if (AttackUsable(i, dist)) total += data.attacks[i].weight;
            if (total <= 0f) return false;

            var roll = Random.value * total;
            for (var i = 0; i < data.attacks.Length; i++)
            {
                if (!AttackUsable(i, dist)) continue;
                roll -= data.attacks[i].weight;
                if (roll > 0f) continue;
                index = i;
                return true;
            }
            return false;
        }

        private bool AttackUsable(int i, float dist)
        {
            var a = data.attacks[i];
            return a != null && Time.time >= _attackReadyAt[i] && dist >= a.minRange && dist <= a.maxRange + 0.3f;
        }

        private void StartAttack(int index)
        {
            _attackIndex = index;
            _attack = data.attacks[index];
            _phase = AttackPhase.Windup;
            _phaseTime = 0f;
            _attackHit = false;
            StopAgent();
            Enter(State.Attack);
            if (animator != null) animator.SetPose(EnemyAnimator.Pose.Windup, _attack.pose, _attack.windup);
        }

        private void TickAttack(float dt)
        {
            if (_attack == null)
            {
                Enter(State.Chase);
                return;
            }

            _phaseTime += dt;
            var valid = FocusValid;
            switch (_phase)
            {
                case AttackPhase.Windup:
                    // Acompanha o jogador, mas quase trava no fim da preparação (dá para esquivar para o lado).
                    if (valid) Face(FocusPosition, _phaseTime < _attack.windup * 0.7f ? 1f : 0.2f);
                    if (_phaseTime < _attack.windup) return;
                    _phase = AttackPhase.Active;
                    _phaseTime = 0f;
                    if (animator != null) animator.SetPose(EnemyAnimator.Pose.Strike, _attack.pose, _attack.activeTime);
                    PlayAttackSound();
                    if (_attack.projectile != null)
                    {
                        if (valid) Fire();
                        _attackHit = true;
                    }
                    break;

                case AttackPhase.Active:
                    if (_attack.lunge > 0f && AgentReady)
                        _agent.Move(transform.forward * (_attack.lunge / _attack.activeTime) * dt);
                    if (!_attackHit && valid) TryHit();
                    if (_phaseTime < _attack.activeTime) return;
                    _phase = AttackPhase.Recovery;
                    _phaseTime = 0f;
                    if (animator != null) animator.SetPose(EnemyAnimator.Pose.Recover, _attack.pose, _attack.recovery);
                    break;

                case AttackPhase.Recovery:
                    if (_phaseTime < _attack.recovery) return;
                    _attackReadyAt[_attackIndex] = Time.time + _attack.cooldown;
                    if (valid && Random.value < _attack.chainChance)
                    {
                        var next = _attack.chainTo >= 0 && _attack.chainTo < data.attacks.Length
                            ? _attack.chainTo
                            : Random.Range(0, data.attacks.Length);
                        if (FlatDistance(FocusPosition) <= data.attacks[next].maxRange + 0.8f)
                        {
                            StartAttack(next);
                            return;
                        }
                    }
                    _attack = null;
                    _nextAttackTime = Time.time + Random.Range(data.attackInterval.x, data.attackInterval.y);
                    Enter(_hasTarget ? State.Chase : State.Return);
                    break;
            }
        }

        private void TryHit()
        {
            var chest = FocusPoint();
            var origin = transform.position + Vector3.up * (data.agentHeight * 0.6f);
            var to = chest - origin;
            var flat = to;
            flat.y = 0f;
            if (flat.magnitude > _attack.maxRange + 0.4f || Mathf.Abs(to.y) > 2.2f) return;
            if (Vector3.Angle(transform.forward, flat) > _attack.arc * 0.5f) return;
            if (!HasLineOfSight(origin, chest)) return;

            _attackHit = true;
            var dir = flat.sqrMagnitude > 0.001f ? flat.normalized : transform.forward;
            var info = new DamageInfo
            {
                Amount = _attack.damage,
                Type = _attack.damageType,
                Source = gameObject,
                Point = chest - dir * 0.3f,
                Normal = -dir,
                Direction = dir,
                StaggerPower = _attack.stagger,
                IsHeavy = _attack.heavy
            };
            var result = FocusHealth.ApplyDamage(info);

            // Golpe leve que bate no escudo: o inimigo recua um pouco (abre uma janela para o contra-ataque).
            if (result == DamageResult.Blocked && !_attack.heavy)
            {
                _phase = AttackPhase.Recovery;
                _phaseTime = -0.25f;
                if (animator != null) animator.SetPose(EnemyAnimator.Pose.Stagger);
            }
        }

        private void Fire()
        {
            var origin = muzzle != null ? muzzle.position : EyePosition();
            var aim = FocusPoint();
            // Mira um pouco à frente de quem corre — mas só metade, para dar para desviar.
            var travel = Vector3.Distance(origin, aim) / Mathf.Max(1f, _attack.projectileSpeed);
            aim += FocusVelocity * travel * 0.5f;
            var projectile = Instantiate(_attack.projectile, origin, Quaternion.LookRotation(aim - origin));
            projectile.Launch(gameObject, _attack.damage, _attack.damageType, _attack.stagger, _attack.projectileSpeed);
        }

        private void PlayAttackSound()
        {
            var clip = _attack.sound != null ? _attack.sound : CombatFeedback.Pick(data.attackSounds);
            if (clip == null && feedback != null)
                clip = _attack.heavy ? feedback.swingHeavy : CombatFeedback.Pick(feedback.swingLight);
            CombatFeedback.Play(clip, transform.position + Vector3.up, 0.8f);
        }

        // ---------------- Dano ----------------

        private static bool IsFromPlayer(DamageInfo info) =>
            info.Source != null && info.Source.GetComponentInParent<PlayerController>() != null;

        public bool ModifyIncomingDamage(ref DamageInfo info)
        {
            if (_state == State.Dead || data == null) return true;

            // Ataque furtivo: alvo desatento leva dano extra.
            if (!_hasTarget && IsFromPlayer(info) && _state is State.Idle or State.Patrol or State.Investigate)
            {
                info.Amount *= data.sneakAttackMultiplier;
                EventBus.Publish(new GameMessage("Ataque furtivo!"));
            }

            if (info.Type == DamageType.Physical) info.Amount *= 1f - data.physicalReduction;

            if (_state != State.Block) return true;
            var incoming = -info.Direction;
            incoming.y = 0f;
            if (incoming.sqrMagnitude < 0.001f || Vector3.Angle(incoming, transform.forward) > data.blockAngle) return true;

            _guardLeft -= info.IsHeavy ? 2 : 1;
            if (_guardLeft <= 0)
            {
                // Guarda quebrada: metade do dano passa e ele fica atordoado.
                _guardBroken = true;
                info.Amount *= 0.5f;
                return true;
            }

            info.WasBlocked = true;
            info.Amount *= 1f - data.blockAbsorb;
            if (Random.value < data.riposteChance) _riposteQueued = true;
            return info.Amount > 0.01f;
        }

        private void OnDamaged(DamageInfo info, DamageResult result)
        {
            if (_state == State.Dead || result == DamageResult.Ignored || result == DamageResult.Killed) return;

            if (IsFromPlayer(info) && FindPlayer())
            {
                _ally = null;
                _lastKnown = s_player.transform.position;
                _lastSeenTime = Time.time;
                Engage(!_hasTarget);
            }
            else if (info.Source != null && info.Source.GetComponentInParent<PlayerAlly>() is { IsAlive: true } attacker)
            {
                // Mordido por um aliado da jogadora (espírito): passa a mirar nele.
                _ally = attacker;
                _lastKnown = attacker.transform.position;
                _lastSeenTime = Time.time;
                Engage(!_hasTarget);
            }

            if (result == DamageResult.Blocked)
            {
                if (_riposteQueued)
                {
                    // Contra-ataque logo depois de aparar.
                    _riposteQueued = false;
                    _nextAttackTime = Time.time;
                    Enter(State.Chase);
                }
                return;
            }

            CombatFeedback.Play(data.hurtSound, EyePosition(), 0.8f);

            if (_guardBroken)
            {
                _guardBroken = false;
                _guardLeft = data.guardHits;
                EnterStagger(data.staggerDuration * 1.6f);
                return;
            }

            _poiseDamage += info.StaggerPower;
            _lastPoiseHit = Time.time;
            if (_poiseDamage >= data.poise && _state != State.Stagger)
            {
                _poiseDamage = 0f;
                EnterStagger(data.staggerDuration * (info.IsHeavy ? 1.3f : 1f));
            }
        }

        private void OnDied(DamageInfo info)
        {
            StopAgent();
            _attack = null;
            _hasTarget = false;
            Enter(State.Dead);
            _agent.enabled = false;
            foreach (var c in _colliders) if (c != null) c.enabled = false;
            CombatFeedback.Play(data.deathSound, EyePosition(), 0.9f);

            // O loot (ouro e itens) fica com o LootDropper.
            EventBus.Publish(new EnemyKilled(this, data));
            if (_deathRoutine != null) StopCoroutine(_deathRoutine);
            _deathRoutine = StartCoroutine(respawnDelay > 0f ? Respawn() : Decay());
        }

        private IEnumerator Decay()
        {
            yield return new WaitForSeconds(corpseTime);
            var start = transform.position;
            for (var t = 0f; t < 1f; t += Time.deltaTime / 2f)
            {
                transform.position = start + Vector3.down * (t * 1.2f);
                yield return null;
            }
            Destroy(gameObject);
        }

        private IEnumerator Respawn()
        {
            yield return new WaitForSeconds(respawnDelay);
            foreach (var r in _renderers) if (r != null) r.enabled = false;
            transform.SetPositionAndRotation(_post, _postRotation);
            if (animator != null) animator.ResetPose();
            yield return new WaitForSeconds(0.3f);

            _health.Revive();
            foreach (var c in _colliders) if (c != null) c.enabled = true;
            foreach (var r in _renderers) if (r != null) r.enabled = true;
            for (var i = 0; i < _attackReadyAt.Length; i++) _attackReadyAt[i] = 0f;
            _guardLeft = data.guardHits;
            _poiseDamage = 0f;
            _deathRoutine = null;
            PlaceOnNavMesh();
            EnterRest();
        }

        // ---------------- Movimento ----------------

        private void MoveTo(Vector3 destination, float speed, bool force = false)
        {
            if (!AgentReady) return;
            _agent.isStopped = false;
            _agent.speed = speed;
            if (!force && Time.time < _nextRepath && (destination - _lastDestination).sqrMagnitude < 1f) return;
            _nextRepath = Time.time + 0.2f;
            _lastDestination = destination;
            if (NavMesh.SamplePosition(destination, out var hit, 2.5f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }

        private void StopAgent()
        {
            if (!AgentReady) return;
            _agent.isStopped = true;
            _agent.ResetPath();
            _agent.velocity = Vector3.zero;
        }

        private bool Arrived() =>
            AgentReady && !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.3f;

        private void Face(Vector3 point, float speedMultiplier = 1f)
        {
            var dir = point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir),
                data.turnSpeed * speedMultiplier * Time.deltaTime);
        }

        private void FaceMovement()
        {
            if (!AgentReady) return;
            var v = _agent.desiredVelocity;
            v.y = 0f;
            if (v.sqrMagnitude > 0.05f) Face(transform.position + v);
        }

        private float FlatDistance(Vector3 point)
        {
            var d = point - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        // ---------------- Depuração ----------------

        private void OnDrawGizmosSelected()
        {
            if (data == null) return;
            var post = Application.isPlaying ? _post : transform.position;
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, data.sightRange);
            var half = data.sightAngle * 0.5f;
            Gizmos.DrawRay(transform.position + Vector3.up, Quaternion.Euler(0f, half, 0f) * transform.forward * data.sightRange);
            Gizmos.DrawRay(transform.position + Vector3.up, Quaternion.Euler(0f, -half, 0f) * transform.forward * data.sightRange);
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, data.hearingRange);
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(post, data.leashRange);
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, data.MaxAttackRange);
        }
    }
}
