using UnityEngine;

namespace Athoria.Gameplay.Combat
{
    /// <summary>
    /// Reação visual a dano: o corpo inclina para longe do golpe (mola) e
    /// pisca. Serve para inimigos e bonecos de treino.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class HitReaction : MonoBehaviour
    {
        [SerializeField, Tooltip("Parte que inclina (ex.: corpo do boneco). Vazio = este objeto.")]
        private Transform pivot;
        [SerializeField] private float tiltPerStagger = 14f;
        [SerializeField] private float stiffness = 120f;
        [SerializeField] private float damping = 9f;
        [SerializeField] private Color flashColor = new(1f, 0.85f, 0.75f);
        [SerializeField] private float flashTime = 0.1f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private Health _health;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
        private Quaternion _restRotation;
        private Vector3 _tilt;       // eixo * ângulo
        private Vector3 _tiltVel;
        private float _flashUntil;
        private bool _flashing;

        private void Awake()
        {
            _health = GetComponent<Health>();
            if (pivot == null) pivot = transform;
            _restRotation = pivot.localRotation;
            _renderers = GetComponentsInChildren<Renderer>();
            _block = new MaterialPropertyBlock();
        }

        private void OnEnable() => _health.Damaged += OnDamaged;
        private void OnDisable() => _health.Damaged -= OnDamaged;

        private void OnDamaged(DamageInfo info, DamageResult result)
        {
            if (result == DamageResult.Ignored || result == DamageResult.Blocked) return;

            var dir = info.Direction;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = -transform.forward;
            var axis = Vector3.Cross(Vector3.up, dir.normalized);   // inclina na direção do golpe
            _tiltVel += pivot.parent != null ? pivot.parent.InverseTransformDirection(axis) * tiltPerStagger * info.StaggerPower * 10f
                                             : axis * tiltPerStagger * info.StaggerPower * 10f;
            _flashUntil = Time.time + flashTime;
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            // Mola amortecida de volta à posição de repouso.
            var accel = -stiffness * _tilt - damping * _tiltVel;
            _tiltVel += accel * dt;
            _tilt += _tiltVel * dt;
            _tilt = Vector3.ClampMagnitude(_tilt, 45f);
            pivot.localRotation = Quaternion.AngleAxis(_tilt.magnitude, _tilt.sqrMagnitude > 1e-6f ? _tilt.normalized : Vector3.up) * _restRotation;

            var shouldFlash = Time.time < _flashUntil;
            if (shouldFlash == _flashing) return;
            _flashing = shouldFlash;
            foreach (var r in _renderers)
            {
                if (shouldFlash)
                {
                    r.GetPropertyBlock(_block);
                    _block.SetColor(BaseColorId, flashColor);
                    _block.SetColor(EmissionId, flashColor * 0.6f);
                    r.SetPropertyBlock(_block);
                }
                else
                {
                    r.SetPropertyBlock(null);
                }
            }
        }
    }
}
