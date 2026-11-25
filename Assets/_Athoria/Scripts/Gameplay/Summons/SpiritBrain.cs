using System.Collections;
using Athoria.Gameplay.AI;
using Athoria.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Athoria.Gameplay.Summons
{
    /// <summary>
    /// IA do espírito companheiro: acompanha a invocadora, procura inimigos por perto,
    /// persegue, ataca sozinho (pelo Health/IDamageable de sempre) e volta a acompanhar
    /// quando não há alvo. Se ficar preso ou longe demais, reaparece ao lado dela.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class SpiritBrain : MonoBehaviour
    {
        public enum State { Inactive, Manifesting, Follow, Chase, Attack, Hurt, Dead }

        private NavMeshAgent _agent;
        private Health _health;
        private SpiritAnimator _anim;
        private SpiritCompanion _spirit;
        private SpiritData _data;
        private State _state;
        private float _stateTime;
        private EnemyAI _target;
        private float _nextRetarget;
        private float _nextAttack;
        private Coroutine _attackRoutine;
        private Vector3 _lastPos;
        private float _stuckTime;

        public State Current => _state;
        public EnemyAI Target => _target;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _anim = GetComponent<SpiritAnimator>();
            _agent.enabled = false;
        }

        private void OnEnable() => _health.Damaged += OnDamaged;
        private void OnDisable() => _health.Damaged -= OnDamaged;

        public void Begin(SpiritCompanion spirit)
        {
            _spirit = spirit;
            _data = spirit.Data;
            _agent.speed = _data.WalkSpeed;
            _agent.angularSpeed = _data.TurnSpeed;
            _agent.acceleration = 24f;
            _agent.stoppingDistance = 0.2f;
            _agent.updateRotation = false;
            if (NavMesh.SamplePosition(transform.position, out var hit, 3f, NavMesh.AllAreas))
            {
                _agent.enabled = true;
                _agent.Warp(hit.position);
            }
            _lastPos = transform.position;
            Enter(State.Manifesting);
        }

        public void Stop()
        {
            CancelAttack();
            _target = null;
            if (AgentReady) _agent.ResetPath();
            _agent.enabled = false;
            Enter(State.Dead);
        }

        private bool AgentReady => _agent.enabled && _agent.isOnNavMesh;
        private Vector3 OwnerPos => _spirit != null && _spirit.Owner != null ? _spirit.Owner.position : transform.position;

        private void Enter(State s)
        {
            _state = s;
            _stateTime = 0f;
        }

        private void Update()
        {
            if (_state is State.Inactive or State.Dead || _data == null) return;
            var dt = Time.deltaTime;
            if (dt <= 0f) return;
            _stateTime += dt;

            var moved = transform.position - _lastPos;
            moved.y = 0f;
            var speed = moved.magnitude / dt;
            _lastPos = transform.position;
            if (_anim != null) _anim.SetSpeed(speed);

            if (_state == State.Manifesting)
            {
                if (_stateTime >= _data.SummonDuration) Enter(State.Follow);
                return;
            }
            if (_state == State.Hurt)
            {
                if (_stateTime >= _data.HurtDuration) Enter(_target != null ? State.Chase : State.Follow);
                return;
            }

            // Longe demais da invocadora: reaparece ao lado dela
            if (SpiritTargeting.Flat(OwnerPos - transform.position) > _data.TeleportDistance)
            {
                WarpNearOwner();
                return;
            }

            if (Time.time >= _nextRetarget)
            {
                _nextRetarget = Time.time + 0.25f;
                if (!SpiritTargeting.IsValid(_target, transform.position, OwnerPos, _data, 1.25f))
                    _target = SpiritTargeting.FindNearest(transform.position, OwnerPos, _data);
            }

            switch (_state)
            {
                case State.Follow: TickFollow(speed, dt); break;
                case State.Chase: TickChase(speed, dt); break;
                case State.Attack: TickAttack(); break;
            }
        }

        private void TickFollow(float speed, float dt)
        {
            if (_target != null)
            {
                Enter(State.Chase);
                return;
            }
            var owner = OwnerPos;
            var dist = SpiritTargeting.Flat(owner - transform.position);
            if (dist > _data.FollowDistance)
            {
                _agent.speed = dist > _data.FollowDistance * 2.5f ? _data.RunSpeed : _data.WalkSpeed;
                var to = owner - transform.position;
                to.y = 0f;
                // fica um pouco antes da invocadora, do lado de onde vem
                var goal = owner - to.normalized * (_data.FollowDistance * 0.8f);
                MoveTo(goal);
                FaceMovement(dt);
                CheckStuck(speed, dt);
            }
            else
            {
                StopMoving();
                _stuckTime = 0f;
                if (_spirit.Owner != null) Face(owner + _spirit.Owner.forward * 3f, dt * 0.5f);
            }
        }

        private void TickChase(float speed, float dt)
        {
            if (_target == null)
            {
                Enter(State.Follow);
                return;
            }
            var targetPos = _target.transform.position;
            var dist = SpiritTargeting.Flat(targetPos - transform.position);
            if (dist <= _data.AttackRange)
            {
                StopMoving();
                Enter(State.Attack);
                return;
            }
            _agent.speed = _data.RunSpeed;
            MoveTo(targetPos);
            FaceMovement(dt);
            CheckStuck(speed, dt);
        }

        private void TickAttack()
        {
            if (_target == null)
            {
                CancelAttack();
                Enter(State.Follow);
                return;
            }
            var targetPos = _target.transform.position;
            Face(targetPos, Time.deltaTime);
            if (_attackRoutine != null) return;
            if (SpiritTargeting.Flat(targetPos - transform.position) > _data.AttackRange + 0.3f)
            {
                Enter(State.Chase);
                return;
            }
            if (Time.time >= _nextAttack) _attackRoutine = StartCoroutine(AttackRoutine(_target));
        }

        private IEnumerator AttackRoutine(EnemyAI target)
        {
            _nextAttack = Time.time + _data.AttackInterval;
            if (_anim != null) _anim.PlayOneShot(SpiritAnimator.Clip.Attack, Mathf.Max(_data.AttackWindup + 0.25f, 0.4f));
            yield return new WaitForSeconds(_data.AttackWindup);
            _attackRoutine = null;
            if (_state != State.Attack || target == null || !target.IsAlive) yield break;
            var to = target.transform.position - transform.position;
            to.y = 0f;
            if (to.magnitude > _data.AttackRange + 0.5f) yield break;
            var dir = to.sqrMagnitude > 0.001f ? to.normalized : transform.forward;
            var point = target.transform.position + Vector3.up * 0.8f;
            target.Health.ApplyDamage(new DamageInfo
            {
                Amount = _data.Damage,
                Type = _data.DamageType,
                Source = gameObject,
                Point = point - dir * 0.3f,
                Normal = -dir,
                Direction = dir,
                StaggerPower = _data.Stagger
            });
        }

        private void CancelAttack()
        {
            if (_attackRoutine == null) return;
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }

        private void OnDamaged(DamageInfo info, DamageResult result)
        {
            if (_state is State.Dead or State.Inactive or State.Manifesting) return;
            if (result != DamageResult.Damaged) return;
            CancelAttack();
            StopMoving();
            if (_anim != null) _anim.PlayOneShot(SpiritAnimator.Clip.Hurt, _data.HurtDuration);
            // Quem o feriu vira alvo, se for um inimigo válido
            var attacker = info.Source != null ? info.Source.GetComponentInParent<EnemyAI>() : null;
            if (attacker != null && SpiritTargeting.IsValid(attacker, transform.position, OwnerPos, _data, 1.25f)) _target = attacker;
            Enter(State.Hurt);
        }

        // ---------- movimento ----------
        private void MoveTo(Vector3 goal)
        {
            if (!AgentReady) return;
            if ((_agent.destination - goal).sqrMagnitude > 0.25f || !_agent.hasPath) _agent.SetDestination(goal);
            _agent.isStopped = false;
        }

        private void StopMoving()
        {
            if (AgentReady) _agent.isStopped = true;
        }

        private void FaceMovement(float dt)
        {
            var v = _agent.enabled ? _agent.desiredVelocity : Vector3.zero;
            v.y = 0f;
            if (v.sqrMagnitude > 0.05f) Face(transform.position + v, dt);
        }

        private void Face(Vector3 point, float dt)
        {
            var to = point - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), _data.TurnSpeed * dt);
        }

        /// <summary>Andando sem sair do lugar por 1,5 s: reaparece ao lado da invocadora.</summary>
        private void CheckStuck(float speed, float dt)
        {
            var wantsToMove = AgentReady && _agent.hasPath && _agent.remainingDistance > 0.6f;
            _stuckTime = wantsToMove && speed < 0.25f ? _stuckTime + dt : 0f;
            if (_stuckTime > 1.5f) WarpNearOwner();
        }

        private void WarpNearOwner()
        {
            _stuckTime = 0f;
            var owner = _spirit.Owner;
            if (owner == null) return;
            var pos = owner.position - owner.forward * 1.5f + owner.right * 0.8f;
            if (!NavMesh.SamplePosition(pos, out var hit, 3f, NavMesh.AllAreas)) return;
            if (!_agent.enabled) _agent.enabled = true;
            _agent.Warp(hit.position);
            _lastPos = hit.position;
            _target = null;
            CancelAttack();
            Enter(State.Follow);
        }
    }
}
