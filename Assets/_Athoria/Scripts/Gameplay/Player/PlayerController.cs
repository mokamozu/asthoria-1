using System;
using Athoria.Core;
using Athoria.Gameplay.Stats;
using UnityEngine;

namespace Athoria.Gameplay.Player
{
    /// <summary>
    /// Movimentação em primeira pessoa com CharacterController: andar, correr,
    /// agachar, pular e gravidade. Aceleração e desaceleração dão sensação de
    /// peso; pulo tem "coyote time" e buffer para não parecer travado.
    /// O pivô do Player fica nos pés.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerStamina stamina;

        [Header("Velocidades (m/s)")]
        [SerializeField] private float walkSpeed = 4.2f;
        [SerializeField] private float sprintSpeed = 7.0f;
        [SerializeField] private float crouchSpeed = 2.1f;

        [Header("Aceleração (m/s²)")]
        [SerializeField] private float groundAcceleration = 32f;
        [SerializeField] private float groundDeceleration = 40f;
        [SerializeField] private float airAcceleration = 8f;

        [Header("Pulo e gravidade")]
        [SerializeField] private float jumpHeight = 1.15f;
        [SerializeField] private float gravity = -22f;
        [SerializeField, Tooltip("Gravidade extra na queda (pulo menos flutuante).")]
        private float fallGravityMultiplier = 1.6f;
        [SerializeField, Tooltip("Gravidade extra ao soltar o pulo cedo (pulo curto).")]
        private float shortJumpGravityMultiplier = 2f;
        [SerializeField] private float terminalVelocity = -40f;
        [SerializeField] private float coyoteTime = 0.12f;
        [SerializeField] private float jumpBufferTime = 0.15f;
        [SerializeField, Tooltip("Força para manter o jogador colado no chão em rampas.")]
        private float groundStickVelocity = -6f;

        [Header("Agachar")]
        [SerializeField] private float standingHeight = 1.8f;
        [SerializeField] private float crouchHeight = 1.1f;
        [SerializeField] private float standingEyeHeight = 1.65f;
        [SerializeField] private float crouchEyeHeight = 0.95f;
        [SerializeField, Tooltip("Metros por segundo de mudança de altura.")]
        private float crouchTransitionSpeed = 6f;
        [SerializeField] private LayerMask ceilingMask = ~0;

        [Header("Custo de stamina")]
        [SerializeField] private float sprintCostPerSecond = 18f;
        [SerializeField] private float jumpCost = 12f;

        private CharacterController _cc;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _lastGroundedTime = float.NegativeInfinity;
        private float _currentHeight;
        private bool _wasGrounded;
        private readonly Collider[] _overlap = new Collider[8];
        private Vector3 _dashVelocity;
        private float _dashUntil;

        /// <summary>Multiplicador de velocidade (ataques e bloqueio deixam o jogador mais lento).</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        /// <summary>Multiplicador permanente da personagem (características).</summary>
        public float BaseSpeedMultiplier { get; set; } = 1f;
        /// <summary>Peso carregado (inventário acima da capacidade deixa mais lento).</summary>
        public float EncumbranceMultiplier { get; set; } = 1f;
        /// <summary>Terreno/ambiente (ex.: água do pântano). 1 = normal.</summary>
        public float EnvironmentMultiplier { get; set; } = 1f;
        /// <summary>Multiplica a altura do pulo (água funda, lama).</summary>
        public float JumpMultiplier { get; set; } = 1f;
        /// <summary>Combate pode impedir a corrida (atacando, bloqueando).</summary>
        public bool SprintAllowed { get; set; } = true;
        public bool IsDashing => Time.time < _dashUntil;
        public bool IsGrounded { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        public Vector3 HorizontalVelocity => _horizontalVelocity;
        public float VerticalVelocity => _verticalVelocity;
        public float WalkSpeed => walkSpeed;
        public float SprintSpeed => sprintSpeed;
        /// <summary>Altura atual dos olhos (interpolada ao agachar).</summary>
        public float EyeHeight =>
            Mathf.Lerp(crouchEyeHeight, standingEyeHeight, Mathf.InverseLerp(crouchHeight, standingHeight, _currentHeight));

        /// <summary>Disparado ao tocar o chão, com a velocidade de queda (positiva).</summary>
        public event Action<float> Landed;
        public event Action Jumped;

        private void Reset()
        {
            input = GetComponent<PlayerInputReader>();
            stamina = GetComponent<PlayerStamina>();
        }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (stamina == null) stamina = GetComponent<PlayerStamina>();

            _currentHeight = standingHeight;
            ApplyHeight(_currentHeight);
        }

        private void Update()
        {
            if (GameBootstrap.Instance != null && GameBootstrap.Instance.IsPaused) return;

            var dt = Time.deltaTime;
            IsGrounded = _cc.isGrounded;
            if (IsGrounded) _lastGroundedTime = Time.time;

            // Saiu de uma borda sem pular: não carrega a força de "colar no chão".
            if (!IsGrounded && _wasGrounded && _verticalVelocity < 0f) _verticalVelocity = 0f;

            UpdateCrouch(dt);
            UpdateHorizontal(dt);
            UpdateJump();
            UpdateGravity(dt);

            var fallSpeed = -_verticalVelocity;
            var flags = _cc.Move((_horizontalVelocity + Vector3.up * _verticalVelocity) * dt);

            if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f) _verticalVelocity = 0f;

            var groundedNow = _cc.isGrounded;
            if (groundedNow && !IsGrounded && fallSpeed > 1f) Landed?.Invoke(fallSpeed);

            // Bater numa parede reduz a velocidade guardada (evita "grudar" acelerando).
            var actual = _cc.velocity;
            actual.y = 0f;
            if (actual.sqrMagnitude < _horizontalVelocity.sqrMagnitude)
                _horizontalVelocity = Vector3.Lerp(_horizontalVelocity, actual, 0.5f);

            _wasGrounded = groundedNow;
        }

        private void UpdateHorizontal(float dt)
        {
            var move = input.Move;
            var wishDir = transform.right * move.x + transform.forward * move.y;
            var hasInput = wishDir.sqrMagnitude > 0.01f;

            if (IsDashing)
            {
                _horizontalVelocity = _dashVelocity;
                IsSprinting = false;
                return;
            }

            var wantsSprint = input.SprintHeld && move.y > 0.1f && !IsCrouching && SprintAllowed;
            IsSprinting = wantsSprint && !stamina.IsExhausted && stamina.Current > 0f && (IsGrounded || IsSprinting);
            if (IsSprinting && IsGrounded && hasInput) stamina.Drain(sprintCostPerSecond * dt);

            var speed = IsCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed;
            var target = wishDir * (speed * SpeedMultiplier * BaseSpeedMultiplier * EncumbranceMultiplier * EnvironmentMultiplier);

            float accel;
            if (!IsGrounded) accel = airAcceleration;
            else accel = target.sqrMagnitude >= _horizontalVelocity.sqrMagnitude ? groundAcceleration : groundDeceleration;

            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, target, accel * dt);
        }

        /// <summary>Impulso horizontal por um tempo (esquiva).</summary>
        /// <summary>
        /// Puxão (arpão): voa até o ponto em linha reta, compensando a gravidade durante o trajeto.
        /// </summary>
        public void PullTo(Vector3 target, float speed)
        {
            var delta = target - transform.position;
            var time = Mathf.Clamp(delta.magnitude / Mathf.Max(1f, speed), 0.12f, 1.6f);
            var horizontal = new Vector3(delta.x, 0f, delta.z) / time;
            _dashVelocity = horizontal;
            _dashUntil = Time.time + time;
            // sobe o suficiente para chegar à altura do alvo apesar da gravidade
            _verticalVelocity = delta.y / time - 0.5f * gravity * time;
        }

        public void Dash(Vector3 velocity, float duration)
        {
            velocity.y = 0f;
            _dashVelocity = velocity;
            _dashUntil = Time.time + duration;
        }

        /// <summary>Teleporta (respawn). Desliga o CharacterController para mover com segurança.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _cc.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _cc.enabled = true;
            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _dashUntil = 0f;
        }

        private void UpdateJump()
        {
            var buffered = Time.time - input.LastJumpPressedTime <= jumpBufferTime;
            if (!buffered) return;

            var canCoyote = Time.time - _lastGroundedTime <= coyoteTime && _verticalVelocity <= 0.01f;
            if (!canCoyote) return;

            if (IsCrouching)
            {
                if (!CanStand()) return;
                IsCrouching = false;
            }

            input.ConsumeJump();
            if (!stamina.TrySpend(jumpCost)) return;

            _verticalVelocity = Mathf.Sqrt(2f * jumpHeight * Mathf.Max(0.05f, JumpMultiplier) * -gravity);
            _lastGroundedTime = float.NegativeInfinity;
            IsGrounded = false;
            Jumped?.Invoke();
        }

        private void UpdateGravity(float dt)
        {
            if (IsGrounded && _verticalVelocity <= 0f)
            {
                _verticalVelocity = groundStickVelocity;
                return;
            }

            var g = gravity;
            if (_verticalVelocity < 0f) g *= fallGravityMultiplier;
            else if (!input.JumpHeld) g *= shortJumpGravityMultiplier;

            _verticalVelocity = Mathf.Max(terminalVelocity, _verticalVelocity + g * dt);
        }

        private void UpdateCrouch(float dt)
        {
            if (input.CrouchHeld) IsCrouching = true;
            else if (IsCrouching && CanStand()) IsCrouching = false;

            var target = IsCrouching ? crouchHeight : standingHeight;
            if (Mathf.Approximately(_currentHeight, target)) return;

            _currentHeight = Mathf.MoveTowards(_currentHeight, target, crouchTransitionSpeed * dt);
            ApplyHeight(_currentHeight);
        }

        private void ApplyHeight(float height)
        {
            _cc.height = height;
            _cc.center = new Vector3(0f, height * 0.5f, 0f);
        }

        /// <summary>Verifica se há espaço acima para ficar de pé.</summary>
        public bool CanStand()
        {
            var radius = _cc.radius * 0.95f;
            var pos = transform.position;
            var bottom = pos + Vector3.up * (_cc.radius + 0.05f);
            var top = pos + Vector3.up * (standingHeight - _cc.radius);
            var count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, _overlap, ceilingMask, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
                if (_overlap[i] != _cc) return false;
            return true;
        }
    }
}
