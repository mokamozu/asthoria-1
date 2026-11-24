using UnityEngine;

namespace Athoria.Gameplay.Combat
{
    /// <summary>Projétil simples (dardo, flecha): voa reto, causa dano no primeiro acerto.</summary>
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField] private float speed = 14f;
        [SerializeField] private float lifetime = 5f;
        [SerializeField] private float radius = 0.06f;
        [SerializeField] private LayerMask mask = ~0;
        [SerializeField] private CombatFeedback feedback;

        private DamageInfo _damage;
        private GameObject _owner;
        private float _dieAt;

        public void Launch(GameObject owner, float damage, DamageType type, float stagger = 1f, float overrideSpeed = 0f)
        {
            if (overrideSpeed > 0f) speed = overrideSpeed;
            _owner = owner;
            _damage = new DamageInfo { Amount = damage, Type = type, Source = owner, StaggerPower = stagger };
            _dieAt = Time.time + lifetime;
        }

        private void Update()
        {
            if (Time.time >= _dieAt)
            {
                Destroy(gameObject);
                return;
            }

            var step = speed * Time.deltaTime;
            var dir = transform.forward;
            if (Physics.SphereCast(transform.position, radius, dir, out var hit, step, mask, QueryTriggerInteraction.Ignore)
                && (_owner == null || !hit.collider.transform.IsChildOf(_owner.transform)))
            {
                var target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null)
                {
                    var info = _damage;
                    info.Point = hit.point;
                    info.Normal = hit.normal;
                    info.Direction = dir;
                    var result = target.ApplyDamage(info);
                    if (result != DamageResult.Blocked && feedback != null)
                        feedback.PlayImpact(SurfaceMaterial.Of(hit.collider), hit.point, hit.normal, 0.7f);
                }
                else if (feedback != null)
                {
                    feedback.PlayImpact(SurfaceMaterial.Of(hit.collider), hit.point, hit.normal, 0.6f);
                }
                Destroy(gameObject);
                return;
            }

            transform.position += dir * step;
        }
    }
}
