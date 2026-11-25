using Athoria.Gameplay.Player;
using UnityEngine;

namespace Athoria.Gameplay.Combat
{
    /// <summary>
    /// Camadas procedurais dos braços e da arma em primeira pessoa, todas com molas e com
    /// parâmetros separados por situação: olhar (lead/lag + inclinação), andar, correr,
    /// pulo/queda, aterrissagem e respiração parada. Não mexe na câmera do mundo — só no
    /// conjunto braços+arma —, e cada camada tem limite para nunca tirar a arma da tela.
    /// O PlayerWeaponView soma Offset/Rotation às poses e aos golpes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FirstPersonMovementAnimation : MonoBehaviour
    {
        [System.Serializable]
        public struct Bob
        {
            [Tooltip("Passos por segundo na velocidade de referência.")] public float frequency;
            [Tooltip("Balanço para os lados (m).")] public float horizontal;
            [Tooltip("Balanço para cima/baixo (m).")] public float vertical;
            [Tooltip("Inclinação lateral a cada passo (graus).")] public float roll;
        }

        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerInputReader input;

        [Header("Olhar (atraso ao virar a câmera)")]
        [SerializeField, Tooltip("Graus de atraso por pixel do mouse.")] private float lookPerPixel = 0.03f;
        [SerializeField, Tooltip("Graus de atraso para o analógico no máximo.")] private float lookGamepad = 2.5f;
        [SerializeField] private float lookMaxAngle = 5f;
        [SerializeField, Tooltip("Deslocamento (m) por grau de atraso.")] private float lookShiftPerDegree = 0.0022f;
        [SerializeField, Tooltip("Inclinação (graus) por grau de giro para o lado.")] private float lookRollFactor = 0.7f;
        [SerializeField] private SpringSettings lookSpring = new(4.5f, 0.55f);

        [Header("Andar / correr")]
        [SerializeField] private Bob walk = new() { frequency = 1.9f, horizontal = 0.012f, vertical = 0.010f, roll = 0.8f };
        [SerializeField] private Bob run = new() { frequency = 2.6f, horizontal = 0.022f, vertical = 0.018f, roll = 1.6f };
        [SerializeField, Tooltip("Inclinação (graus) ao andar de lado, na velocidade máxima.")] private float strafeRoll = 3f;
        [SerializeField, Tooltip("Recuo (m) da arma andando para a frente, na velocidade máxima.")] private float forwardPull = 0.012f;
        [SerializeField] private SpringSettings moveSpring = new(5f, 0.75f);

        [Header("Pulo e queda")]
        [SerializeField, Tooltip("Tranco para baixo ao pular (m/s).")] private float jumpKick = 0.18f;
        [SerializeField, Tooltip("A arma sobe um pouco caindo (m por m/s de queda).")] private float fallLift = 0.004f;
        [SerializeField] private float fallLiftMax = 0.03f;
        [SerializeField] private SpringSettings airSpring = new(3.5f, 0.6f);

        [Header("Aterrissagem")]
        [SerializeField, Tooltip("Tranco para baixo por m/s de queda.")] private float landingKickPerSpeed = 0.045f;
        [SerializeField] private float landingKickMax = 0.5f;
        [SerializeField, Tooltip("Inclinação para a frente (graus) por m/s de queda.")] private float landingPitchPerSpeed = 0.6f;
        [SerializeField] private SpringSettings landingSpring = new(6f, 0.45f);

        [Header("Parado (respiração)")]
        [SerializeField] private float breathAmplitude = 0.0035f;
        [SerializeField] private float breathFrequency = 0.25f;
        [SerializeField, Tooltip("Graus de oscilação lenta parado.")] private float idleSwayAngle = 0.35f;

        [Header("Limites (nunca tira a arma da tela)")]
        [SerializeField] private float maxOffset = 0.06f;
        [SerializeField] private float maxAngle = 9f;

        private Spring3 _look, _move, _air, _landPos, _landRot;
        private float _phase, _runBlend, _moveBlend;

        public Vector3 Offset { get; private set; }
        public Quaternion Rotation { get; private set; } = Quaternion.identity;

        private void Reset()
        {
            controller = GetComponent<PlayerController>();
            input = GetComponent<PlayerInputReader>();
        }

        private void Awake()
        {
            if (controller == null) controller = GetComponent<PlayerController>();
            if (input == null) input = GetComponent<PlayerInputReader>();
        }

        private void OnEnable()
        {
            if (controller == null) return;
            controller.Landed += OnLanded;
            controller.Jumped += OnJumped;
        }

        private void OnDisable()
        {
            if (controller == null) return;
            controller.Landed -= OnLanded;
            controller.Jumped -= OnJumped;
        }

        private void OnJumped() => _air.Kick(Vector3.down * jumpKick);

        private void OnLanded(float fallSpeed)
        {
            var k = Mathf.Min(fallSpeed * landingKickPerSpeed, landingKickMax);
            _landPos.Kick(Vector3.down * k);
            _landRot.Kick(new Vector3(fallSpeed * landingPitchPerSpeed * 6f, 0f, 0f));
        }

        /// <summary>Atualiza as camadas. Chamado pelo PlayerWeaponView antes de montar a pose final.</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f || controller == null) return;

            // 1) Olhar: a arma atrasa em relação à câmera e inclina para o lado do giro
            var look = input != null ? input.LookDelta : Vector2.zero;
            var lag = input != null && input.LookIsPointer ? look * lookPerPixel : look * lookGamepad;
            lag = Vector2.ClampMagnitude(lag, lookMaxAngle);
            _look.Step(new Vector3(lag.y, -lag.x, -lag.x * lookRollFactor), lookSpring, dt);

            // 2) Andar / correr: passo em figura de 8 + inclinação de lado + recuo para a frente
            var local = transform.InverseTransformDirection(controller.HorizontalVelocity);
            var speed = new Vector2(local.x, local.z).magnitude;
            var maxSpeed = Mathf.Max(0.1f, controller.SprintSpeed);
            var moving = controller.IsGrounded && speed > 0.2f;
            var sprinting = controller.IsSprinting && speed > controller.WalkSpeed * 1.05f;
            _runBlend = Mathf.MoveTowards(_runBlend, sprinting ? 1f : 0f, dt * 4f);
            _moveBlend = Mathf.MoveTowards(_moveBlend, moving ? Mathf.Clamp01(speed / Mathf.Max(0.1f, controller.WalkSpeed)) : 0f, dt * 5f);
            var bob = Blend(walk, run, _runBlend);
            if (moving) _phase += dt * bob.frequency * Mathf.Clamp(speed / Mathf.Max(0.1f, controller.WalkSpeed), 0.6f, 1.6f) * Mathf.PI * 2f;
            var bobPos = new Vector3(Mathf.Cos(_phase) * bob.horizontal, -Mathf.Abs(Mathf.Sin(_phase)) * bob.vertical, 0f) * _moveBlend;
            var bobRoll = Mathf.Cos(_phase) * bob.roll * _moveBlend;
            var strafe = Mathf.Clamp(local.x / maxSpeed, -1f, 1f);
            var forward = Mathf.Clamp(local.z / maxSpeed, -1f, 1f);
            var moveTarget = bobPos + new Vector3(0f, 0f, -forward * forwardPull);
            _move.Step(moveTarget, moveSpring, dt);
            var moveRoll = bobRoll - strafe * strafeRoll;

            // 3) Pulo/queda: sobe um pouco caindo
            var fall = !controller.IsGrounded ? Mathf.Clamp(-controller.VerticalVelocity * fallLift, -fallLiftMax, fallLiftMax) : 0f;
            _air.Step(new Vector3(0f, fall, 0f), airSpring, dt);

            // 4) Aterrissagem (molas soltas por impulso)
            _landPos.Step(Vector3.zero, landingSpring, dt);
            _landRot.Step(Vector3.zero, landingSpring, dt);

            // 5) Respiração parada (some andando)
            var still = 1f - _moveBlend;
            var t = Time.time * breathFrequency * Mathf.PI * 2f;
            var breath = new Vector3(0f, Mathf.Sin(t) * breathAmplitude, 0f) * still;
            var idle = new Vector3(Mathf.Sin(t * 0.5f) * idleSwayAngle, Mathf.Cos(t * 0.37f) * idleSwayAngle, 0f) * still;

            var lookShift = new Vector3(_look.Value.y, -_look.Value.x, 0f) * lookShiftPerDegree;
            var pos = _move.Value + _air.Value + _landPos.Value + breath + lookShift;
            Offset = Vector3.ClampMagnitude(pos, maxOffset);

            var euler = _look.Value + _landRot.Value + idle + new Vector3(0f, 0f, moveRoll);
            euler = Vector3.ClampMagnitude(euler, maxAngle);
            Rotation = Quaternion.Euler(euler);
        }

        private static Bob Blend(Bob a, Bob b, float t) => new()
        {
            frequency = Mathf.Lerp(a.frequency, b.frequency, t),
            horizontal = Mathf.Lerp(a.horizontal, b.horizontal, t),
            vertical = Mathf.Lerp(a.vertical, b.vertical, t),
            roll = Mathf.Lerp(a.roll, b.roll, t),
        };
    }
}
