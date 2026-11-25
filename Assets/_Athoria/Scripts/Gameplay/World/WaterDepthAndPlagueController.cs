using System;
using Athoria.Core.Events;
using Athoria.Gameplay.Bosses;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Player;
using UnityEngine;

namespace Athoria.Gameplay.World
{
    /// <summary>Contaminação da peste mudou (0-1). A HUD mostra a barra.</summary>
    public readonly struct PlagueChanged : IGameEvent
    {
        public readonly float Value;
        public readonly bool InPlagueWater;
        public PlagueChanged(float value, bool inPlagueWater) { Value = value; InPlagueWater = inPlagueWater; }
    }

    /// <summary>
    /// Na jogadora. Compara os pés com o nível da água da região (SwampWater):
    /// • água pela cintura (≥ 0,85 m): metade da velocidade, pulo fraco e passos na água;
    /// • água negra da peste (PlagueWater): a contaminação sobe 20%/s; fora dela cai 8%/s.
    ///   Em 100% a jogadora toma dano de veneno contínuo e a tela fica esverdeada.
    /// Sem região com água (outras cenas), não faz nada.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaterDepthAndPlagueController : MonoBehaviour
    {
        [Header("Água")]
        [SerializeField, Tooltip("Profundidade em que a água começa a atrapalhar (m).")] private float shallowDepth = 0.15f;
        [SerializeField, Tooltip("Água pela cintura (m): lentidão total.")] private float waistDepth = 0.85f;
        [SerializeField, Range(0.1f, 1f)] private float waistSpeedMultiplier = 0.5f;
        [SerializeField, Range(0.05f, 1f)] private float waistJumpMultiplier = 0.4f;
        [SerializeField, Tooltip("Rapidez com que a lentidão entra/sai.")] private float blendSpeed = 4f;

        [Header("Passos na água")]
        [SerializeField] private AudioClip[] wadeSteps = Array.Empty<AudioClip>();
        [SerializeField] private float stepLength = 1.5f;
        [SerializeField, Range(0f, 1f)] private float stepVolume = 0.6f;

        [Header("Peste")]
        [SerializeField, Tooltip("%/s dentro da água da peste.")] private float fillPerSecond = 20f;
        [SerializeField, Tooltip("%/s fora dela.")] private float drainPerSecond = 8f;
        [SerializeField, Tooltip("Dano de veneno por segundo com a contaminação em 100%.")] private float poisonDps = 5f;
        [SerializeField] private float poisonTick = 0.5f;

        private PlayerController _controller;
        private Health _health;
        private AudioSource _audio;
        private float _speedMul = 1f, _jumpMul = 1f;
        private float _stepDistance;
        private float _plague;
        private float _nextTick;
        private float _lastPublished = -1f;
        private bool _lastIn;

        /// <summary>Profundidade atual (m; ≤ 0 = seco).</summary>
        public float Depth { get; private set; }
        public bool IsWading => Depth >= waistDepth;
        public bool InPlagueWater { get; private set; }
        /// <summary>Contaminação da peste (0-1).</summary>
        public float Plague => _plague;
        /// <summary>Um passo dentro d'água (profundidade). Para som/partículas extras.</summary>
        public event Action<float> WadeStep;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _health = GetComponent<Health>();
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
        }

        private void OnDisable()
        {
            if (_controller != null) { _controller.EnvironmentMultiplier = 1f; _controller.JumpMultiplier = 1f; }
        }

        private void Update()
        {
            var water = SwampWater.Current;
            var dt = Time.deltaTime;
            Depth = water != null ? water.DepthAt(transform.position) : -1f;

            // ---- profundidade: lentidão e pulo ----
            var k = Mathf.Clamp01(Mathf.InverseLerp(shallowDepth, waistDepth, Depth));
            var step = blendSpeed * dt;
            _speedMul = Mathf.MoveTowards(_speedMul, Mathf.Lerp(1f, waistSpeedMultiplier, k), step);
            _jumpMul = Mathf.MoveTowards(_jumpMul, Mathf.Lerp(1f, waistJumpMultiplier, k), step);
            if (_controller != null)
            {
                _controller.EnvironmentMultiplier = _speedMul;
                _controller.JumpMultiplier = _jumpMul;
                if (Depth > shallowDepth && _controller.IsGrounded)
                {
                    _stepDistance += _controller.HorizontalVelocity.magnitude * dt;
                    if (_stepDistance >= stepLength) { _stepDistance = 0f; Step(); }
                }
                else _stepDistance = stepLength * 0.6f;   // primeiro passo logo ao entrar
            }

            // ---- peste ----
            var alive = _health == null || _health.IsAlive;
            InPlagueWater = alive && Depth > 0.02f && PlagueWater.Contains(transform.position);
            if (!alive) _plague = 0f;
            else _plague = Mathf.Clamp01(_plague + (InPlagueWater ? fillPerSecond : -drainPerSecond) * 0.01f * dt);

            if (_plague >= 1f && alive)
            {
                PoisonScreenEffect.For(gameObject).Pulse(0.6f);
                if (Time.time >= _nextTick)
                {
                    _nextTick = Time.time + poisonTick;
                    _health?.ApplyDamage(new DamageInfo
                    {
                        Amount = poisonDps * poisonTick, Type = DamageType.Poison, Source = gameObject,
                        Point = transform.position + Vector3.up, Normal = Vector3.up, Direction = Vector3.down, StaggerPower = 0f
                    });
                }
            }
            else _nextTick = Time.time + poisonTick * 0.5f;

            if (Mathf.Abs(_plague - _lastPublished) > 0.004f || InPlagueWater != _lastIn)
            {
                _lastPublished = _plague;
                _lastIn = InPlagueWater;
                EventBus.Publish(new PlagueChanged(_plague, InPlagueWater));
            }
        }

        private void Step()
        {
            WadeStep?.Invoke(Depth);
            if (wadeSteps.Length == 0) return;
            var clip = wadeSteps[UnityEngine.Random.Range(0, wadeSteps.Length)];
            if (clip == null) return;
            _audio.pitch = UnityEngine.Random.Range(0.85f, 1.1f);
            _audio.PlayOneShot(clip, stepVolume * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(Depth / waistDepth)));
        }

#if UNITY_EDITOR
        public void EditorSetSteps(AudioClip[] clips) => wadeSteps = clips;
#endif
    }
}
