using Athoria.Gameplay.Combat;
using UnityEngine;

namespace Athoria.Gameplay.World
{
    /// <summary>
    /// Armadilha que dispara dardos no jogador quando ele está no alcance e
    /// à vista. Serve para testar bloqueio/esquiva e como armadilha de masmorra.
    /// </summary>
    public sealed class DartTrap : MonoBehaviour
    {
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform muzzle;
        [SerializeField] private float range = 14f;
        [SerializeField] private float interval = 2.5f;
        [SerializeField] private float damage = 12f;
        [SerializeField] private float aimHeight = 1.3f;
        [SerializeField] private AudioClip fireSound;

        private Transform _target;
        private float _nextShot;

        private void Start()
        {
            if (muzzle == null) muzzle = transform;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _target = player.transform;
            _nextShot = Time.time + interval;
        }

        private void Update()
        {
            if (_target == null || projectilePrefab == null || Time.time < _nextShot) return;

            var aim = _target.position + Vector3.up * aimHeight;
            var toTarget = aim - muzzle.position;
            if (toTarget.magnitude > range) return;

            // Só atira com linha de visão livre.
            if (Physics.Raycast(muzzle.position, toTarget.normalized, out var hit, toTarget.magnitude, ~0, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(_target))
                return;

            _nextShot = Time.time + interval;
            var dart = Instantiate(projectilePrefab, muzzle.position, Quaternion.LookRotation(toTarget));
            dart.Launch(gameObject, damage, DamageType.Physical);
            CombatFeedback.Play(fireSound, muzzle.position, 0.8f);
        }
    }
}
