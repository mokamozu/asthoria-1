using System.Collections;
using Athoria.Gameplay.Combat;
using UnityEngine;

namespace Athoria.Gameplay.World
{
    /// <summary>Boneco de treino: ao "morrer" tomba e volta inteiro depois de alguns segundos.</summary>
    [RequireComponent(typeof(Health))]
    public sealed class TrainingDummy : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private float respawnDelay = 3f;
        [SerializeField] private float fallTime = 0.35f;

        private Health _health;
        private Quaternion _bodyRest;
        private HitReaction _reaction;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _reaction = GetComponent<HitReaction>();
            if (body == null) body = transform;
            _bodyRest = body.localRotation;
        }

        private void OnEnable() => _health.Died += OnDied;
        private void OnDisable() => _health.Died -= OnDied;

        private void OnDied(DamageInfo info) => StartCoroutine(FallAndRespawn(info.Direction));

        private IEnumerator FallAndRespawn(Vector3 hitDirection)
        {
            if (_reaction != null) _reaction.enabled = false;

            hitDirection.y = 0f;
            if (hitDirection.sqrMagnitude < 0.01f) hitDirection = -transform.forward;
            var axis = body.parent != null
                ? body.parent.InverseTransformDirection(Vector3.Cross(Vector3.up, hitDirection.normalized))
                : Vector3.Cross(Vector3.up, hitDirection.normalized);
            var start = body.localRotation;
            var fallen = Quaternion.AngleAxis(80f, axis) * _bodyRest;

            for (var t = 0f; t < 1f; t += Time.deltaTime / fallTime)
            {
                body.localRotation = Quaternion.Slerp(start, fallen, t * t);
                yield return null;
            }
            body.localRotation = fallen;

            yield return new WaitForSeconds(respawnDelay);

            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.5f)
            {
                body.localRotation = Quaternion.Slerp(fallen, _bodyRest, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            body.localRotation = _bodyRest;
            _health.Revive();
            if (_reaction != null) _reaction.enabled = true;
        }
    }
}
