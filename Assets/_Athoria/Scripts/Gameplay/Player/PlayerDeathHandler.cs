using System.Collections;
using Athoria.Gameplay.Combat;
using UnityEngine;

namespace Athoria.Gameplay.Player
{
    /// <summary>
    /// Morte do jogador: desliga os controles, a câmera "cai" e, depois de
    /// alguns segundos, revive no ponto de partida. O sistema de salvamento
    /// (Etapa 14) passará a decidir o ponto de retorno.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class PlayerDeathHandler : MonoBehaviour
    {
        [SerializeField] private Behaviour[] disableOnDeath;
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float respawnDelay = 3f;

        private Health _health;
        private PlayerController _controller;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;

        public bool IsDead { get; private set; }

        private void Awake()
        {
            _health = GetComponent<Health>();
            _controller = GetComponent<PlayerController>();
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
        }

        private void OnEnable() => _health.Died += OnDied;
        private void OnDisable() => _health.Died -= OnDied;

        /// <summary>Define onde o jogador volta ao morrer.</summary>
        public void SetRespawnPoint(Vector3 position, Quaternion rotation)
        {
            _spawnPosition = position;
            _spawnRotation = rotation;
        }

        private void OnDied(DamageInfo info) => StartCoroutine(DeathSequence());

        private IEnumerator DeathSequence()
        {
            IsDead = true;
            foreach (var b in disableOnDeath) if (b != null) b.enabled = false;

            var startPos = cameraPivot.localPosition;
            var startRot = cameraPivot.localRotation;
            var endPos = new Vector3(startPos.x, 0.3f, startPos.z);
            var endRot = startRot * Quaternion.Euler(-10f, 0f, 70f);
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.8f)
            {
                var e = t * t;
                cameraPivot.localPosition = Vector3.Lerp(startPos, endPos, e);
                cameraPivot.localRotation = Quaternion.Slerp(startRot, endRot, e);
                yield return null;
            }

            yield return new WaitForSeconds(respawnDelay);

            cameraPivot.localRotation = Quaternion.identity;
            if (_controller != null) _controller.Teleport(_spawnPosition, _spawnRotation);
            _health.Revive();
            foreach (var b in disableOnDeath) if (b != null) b.enabled = true;
            IsDead = false;
        }
    }
}
