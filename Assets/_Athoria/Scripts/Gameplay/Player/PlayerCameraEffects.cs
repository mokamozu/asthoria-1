using UnityEngine;

namespace Athoria.Gameplay.Player
{
    /// <summary>
    /// Efeitos de câmera que dão peso ao personagem: altura dos olhos ao
    /// agachar, balanço ao andar (head bob), afundar ao aterrissar e leve
    /// aumento de campo de visão ao correr.
    /// Hierarquia esperada: Player > CameraPivot > CameraOffset > Camera.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCameraEffects : MonoBehaviour
    {
        [SerializeField] private PlayerController controller;
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Transform cameraOffset;
        [SerializeField] private Camera targetCamera;

        [Header("Altura dos olhos")]
        [SerializeField] private float eyeHeightSmoothing = 14f;

        [Header("Balanço ao andar")]
        [SerializeField] private float bobFrequency = 1.9f;
        [SerializeField] private float bobVertical = 0.045f;
        [SerializeField] private float bobHorizontal = 0.025f;
        [SerializeField] private float sprintBobMultiplier = 1.4f;

        [Header("Aterrissagem")]
        [SerializeField] private float landingDipPerSpeed = 0.018f;
        [SerializeField] private float maxLandingDip = 0.28f;
        [SerializeField] private float landingRecoverTime = 0.18f;

        [Header("Campo de visão")]
        [SerializeField] private float baseFov = 75f;
        [SerializeField] private float sprintFovBonus = 6f;
        [SerializeField] private float fovSmoothing = 6f;

        private float _bobPhase;
        private Vector3 _bobOffset;
        private float _landingOffset;
        private float _landingVelocity;
        private float _eyeHeight;
        private float _shake;

        [Header("Tremor")]
        [SerializeField] private float shakeDecay = 6f;
        [SerializeField] private float shakeMaxAngle = 2.5f;
        [SerializeField] private float shakeFrequency = 28f;

        public float BaseFov
        {
            get => baseFov;
            set => baseFov = value;
        }

        private void Reset() => controller = GetComponent<PlayerController>();

        private void Awake()
        {
            if (controller == null) controller = GetComponent<PlayerController>();
            if (cameraPivot == null || cameraOffset == null || targetCamera == null)
            {
                Debug.LogError("[Athoria] PlayerCameraEffects com referências faltando.", this);
                enabled = false;
                return;
            }
            _eyeHeight = controller.EyeHeight;
            targetCamera.fieldOfView = baseFov;
        }

        private void OnEnable() { if (controller != null) controller.Landed += OnLanded; }
        private void OnDisable() { if (controller != null) controller.Landed -= OnLanded; }

        private void LateUpdate()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f) return;

            _eyeHeight = Mathf.Lerp(_eyeHeight, controller.EyeHeight, 1f - Mathf.Exp(-eyeHeightSmoothing * dt));
            var pivotPos = cameraPivot.localPosition;
            pivotPos.y = _eyeHeight;
            cameraPivot.localPosition = pivotPos;

            UpdateBob(dt);
            _landingOffset = Mathf.SmoothDamp(_landingOffset, 0f, ref _landingVelocity, landingRecoverTime);
            cameraOffset.localPosition = _bobOffset + Vector3.up * _landingOffset;

            _shake = Mathf.MoveTowards(_shake, 0f, shakeDecay * Time.unscaledDeltaTime);
            var s = _shake * _shake * shakeMaxAngle;
            var t = Time.unscaledTime * shakeFrequency;
            cameraOffset.localRotation = Quaternion.Euler(
                (Mathf.PerlinNoise(t, 0.1f) - 0.5f) * 2f * s,
                (Mathf.PerlinNoise(0.3f, t) - 0.5f) * 2f * s,
                (Mathf.PerlinNoise(t, t * 0.5f) - 0.5f) * 2f * s * 0.5f);

            var targetFov = baseFov + (controller.IsSprinting && controller.HorizontalVelocity.magnitude > controller.WalkSpeed ? sprintFovBonus : 0f);
            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFov, 1f - Mathf.Exp(-fovSmoothing * dt));
        }

        private void UpdateBob(float dt)
        {
            var speed = controller.HorizontalVelocity.magnitude;
            Vector3 target;

            if (controller.IsGrounded && speed > 0.2f)
            {
                var speedFactor = speed / controller.WalkSpeed;
                _bobPhase += dt * bobFrequency * speedFactor * Mathf.PI * 2f;
                var amp = controller.IsSprinting ? sprintBobMultiplier : 1f;
                amp *= Mathf.Clamp01(speedFactor);
                target = new Vector3(
                    Mathf.Cos(_bobPhase) * bobHorizontal * amp,
                    -Mathf.Abs(Mathf.Sin(_bobPhase)) * bobVertical * amp,
                    0f);
            }
            else
            {
                target = Vector3.zero;
            }

            _bobOffset = Vector3.Lerp(_bobOffset, target, 1f - Mathf.Exp(-12f * dt));
        }

        /// <summary>Tremor de câmera, 0..1 (acumula até 1).</summary>
        public void AddShake(float amount) => _shake = Mathf.Clamp01(_shake + amount);

        private void OnLanded(float fallSpeed)
        {
            _landingOffset = -Mathf.Min(fallSpeed * landingDipPerSpeed, maxLandingDip);
            _landingVelocity = 0f;
        }
    }
}
