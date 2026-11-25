using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Athoria.Gameplay.AI
{
    /// <summary>
    /// Espírito do Musgo: criatura medrosa que não ataca. Fica escondida como uma moita;
    /// quando a jogadora chega perto (ou bate nela), sai correndo para longe e, depois de
    /// um tempo em segurança, se esconde de novo. Ao morrer solta Musgo (LootDropper),
    /// o item que desperta o Ancião do Brejo.
    /// Animação no estilo PS1: o Animator avança em passos de 15 quadros por segundo.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(NavMeshAgent))]
    public sealed class MossSpiritAI : MonoBehaviour
    {
        public enum State { Hiding, Watching, Fleeing, Calm, Dead }

        [SerializeField] private Animator animator;
        [SerializeField] private string displayName = "Espírito do Musgo";
        [Header("Percepção")]
        [SerializeField, Tooltip("Escondido (moita), só percebe a jogadora bem de perto.")] private float hiddenAlertRadius = 4f;
        [SerializeField, Tooltip("Fora do esconderijo, percebe de mais longe.")] private float alertRadius = 9f;
        [SerializeField, Tooltip("Distância em que se sente seguro.")] private float safeRadius = 16f;
        [SerializeField, Tooltip("Tempo encarando antes de fugir (s).")] private float watchTime = 0.5f;
        [Header("Fuga")]
        [SerializeField] private float runSpeed = 5.5f;
        [SerializeField, Tooltip("Distância de cada trecho da fuga (m).")] private float fleeStep = 9f;
        [SerializeField, Tooltip("Segundos em segurança até se esconder de novo.")] private float hideDelay = 3f;
        [SerializeField, Tooltip("Segundos até o corpo sumir depois de morto.")] private float corpseTime = 25f;
        [SerializeField] private float animationFps = 15f;

        private Health _health;
        private NavMeshAgent _agent;
        private Transform _player;
        private State _state = State.Hiding;
        private float _stateTime, _animClock;
        private Vector3 _fleeTarget;
        private float _repath;
        private string _clip;

        public State Current => _state;
        public string DisplayName => displayName;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.enabled = false;           // avançado à mão, em passos
            _agent.speed = runSpeed;
            _agent.acceleration = 30f;
            _agent.angularSpeed = 720f;
            _agent.autoBraking = true;
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
        }

        private void Start()
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, 3f, NavMesh.AllAreas)) _agent.Warp(hit.position);
            else _agent.enabled = false;
            Enter(State.Hiding);
            _animClock = Random.value;                                 // moitas não respiram em sincronia
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            StepAnimation(dt);
            if (_state == State.Dead || dt <= 0f) return;
            _stateTime += dt;
            if (_player == null)
            {
                var pc = FindAnyObjectByType<PlayerController>();
                if (pc == null) return;
                _player = pc.transform;
            }
            var dist = FlatDistance(_player.position);

            switch (_state)
            {
                case State.Hiding:
                    if (dist <= hiddenAlertRadius) Enter(State.Watching);
                    break;
                case State.Calm:
                    if (dist <= alertRadius) Enter(State.Watching);
                    else if (_stateTime >= hideDelay) Enter(State.Hiding);
                    break;
                case State.Watching:
                    Face(_player.position);
                    if (_stateTime >= watchTime) Enter(State.Fleeing);
                    break;
                case State.Fleeing:
                    TickFlee(dist);
                    break;
            }
        }

        private void TickFlee(float dist)
        {
            if (!_agent.enabled || !_agent.isOnNavMesh) { Enter(State.Calm); return; }
            _repath -= Time.deltaTime;
            var arrived = !_agent.pathPending && _agent.remainingDistance <= 0.6f;
            if (dist >= safeRadius && arrived) { _agent.ResetPath(); Enter(State.Calm); return; }
            if (arrived || _repath <= 0f) PickFleeTarget();
        }

        /// <summary>Escolhe um ponto para longe da jogadora; se a direção estiver bloqueada, tenta outras.</summary>
        private void PickFleeTarget()
        {
            _repath = 1.2f;
            var away = transform.position - _player.position; away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            away.Normalize();
            foreach (var angle in new[] { 0f, 35f, -35f, 70f, -70f, 110f, -110f })
            {
                var dir = Quaternion.Euler(0f, angle + Random.Range(-15f, 15f), 0f) * away;
                var candidate = transform.position + dir * fleeStep;
                if (!NavMesh.SamplePosition(candidate, out var hit, 2.5f, NavMesh.AllAreas)) continue;
                if (FlatDistance(hit.position) < 1.5f) continue;
                _fleeTarget = hit.position;
                _agent.isStopped = false;
                _agent.SetDestination(_fleeTarget);
                return;
            }
        }

        private void OnDamaged(DamageInfo info, DamageResult result)
        {
            if (_state == State.Dead || result != DamageResult.Damaged) return;
            if (_state != State.Fleeing) Enter(State.Fleeing);
            else _repath = 0f;                                        // apanhou correndo: muda de rumo
        }

        private void OnDied(DamageInfo info)
        {
            Enter(State.Dead);
            if (_agent.enabled && _agent.isOnNavMesh) { _agent.isStopped = true; _agent.ResetPath(); }
            _agent.enabled = false;
            foreach (var c in GetComponents<Collider>()) c.enabled = false;
            Destroy(gameObject, corpseTime);
        }

        private void Enter(State s)
        {
            _state = s;
            _stateTime = 0f;
            if (_agent.enabled && _agent.isOnNavMesh) _agent.isStopped = s != State.Fleeing;
            switch (s)
            {
                case State.Hiding: Play("Hiding"); break;
                case State.Watching:
                case State.Calm: Play("Idle"); break;
                case State.Fleeing: Play("Running"); PickFleeTarget(); break;
                case State.Dead: Play("Death"); break;
            }
        }

        private void Play(string clip)
        {
            if (animator == null || _clip == clip) return;
            _clip = clip;
            animator.Play(clip, 0, 0f);
            animator.Update(0f);
        }

        private void StepAnimation(float dt)
        {
            if (animator == null || animationFps <= 0f) return;
            _animClock += dt;
            var step = 1f / animationFps;
            if (_animClock < step) return;
            var advance = Mathf.Floor(_animClock / step) * step;
            _animClock -= advance;
            animator.Update(advance);
        }

        private void Face(Vector3 point)
        {
            var to = point - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 360f * Time.deltaTime);
        }

        private float FlatDistance(Vector3 p)
        {
            var d = p - transform.position; d.y = 0f;
            return d.magnitude;
        }
    }
}
