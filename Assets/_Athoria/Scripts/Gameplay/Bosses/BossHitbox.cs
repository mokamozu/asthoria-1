using System.Collections.Generic;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Player;
using UnityEngine;

namespace Athoria.Gameplay.Bosses
{
    /// <summary>
    /// Hitboxes próprias dos golpes do chefe: área circular (soco, salto) e cone (golpe rápido).
    /// Só acerta a jogadora e os aliados dela (espíritos), nunca outros inimigos nem o próprio chefe.
    /// </summary>
    public static class BossHitbox
    {
        private static readonly Collider[] Buffer = new Collider[32];
        private static readonly HashSet<Health> Hit = new();

        public static bool IsHostile(Collider c, out Health health)
        {
            health = c.GetComponentInParent<Health>();
            if (health == null || !health.IsAlive) return false;
            return health.IsPlayer || c.GetComponentInParent<PlayerAlly>() != null;
        }

        /// <summary>Dano em área ao redor de um ponto (altura limitada: pular por cima não salva, mas estar longe sim).</summary>
        public static int Area(GameObject source, Vector3 center, float radius, float damage, DamageType type, float stagger, float maxHeight = 2.5f)
        {
            Hit.Clear();
            var count = Physics.OverlapSphereNonAlloc(center, radius, Buffer, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var c = Buffer[i];
                if (c.transform.IsChildOf(source.transform) || !IsHostile(c, out var health) || !Hit.Add(health)) continue;
                var p = health.transform.position;
                if (Mathf.Abs(p.y - center.y) > maxHeight) continue;
                var flat = p - center; flat.y = 0f;
                var dir = flat.sqrMagnitude > 0.01f ? flat.normalized : source.transform.forward;
                health.ApplyDamage(new DamageInfo
                {
                    Amount = damage, Type = type, Source = source,
                    Point = c.ClosestPoint(center), Normal = -dir, Direction = dir,
                    StaggerPower = stagger, IsHeavy = true
                });
            }
            return Hit.Count;
        }

        /// <summary>Dano em cone à frente (golpe rápido).</summary>
        public static int Cone(GameObject source, Vector3 origin, Vector3 forward, float range, float arcDegrees, float damage, DamageType type, float stagger)
        {
            Hit.Clear();
            forward.y = 0f; forward.Normalize();
            var count = Physics.OverlapSphereNonAlloc(origin, range, Buffer, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var c = Buffer[i];
                if (c.transform.IsChildOf(source.transform) || !IsHostile(c, out var health) || !Hit.Add(health)) continue;
                var to = health.transform.position - origin; to.y = 0f;
                if (to.sqrMagnitude > 0.01f && Vector3.Angle(forward, to) > arcDegrees * 0.5f) continue;
                health.ApplyDamage(new DamageInfo
                {
                    Amount = damage, Type = type, Source = source,
                    Point = c.ClosestPoint(origin), Normal = -forward, Direction = forward,
                    StaggerPower = stagger, IsHeavy = true
                });
            }
            return Hit.Count;
        }

        /// <summary>Tremor de câmera na jogadora, mais forte quanto mais perto do impacto.</summary>
        public static void ShakePlayer(Vector3 at, float strength, float falloff = 18f)
        {
            var fx = Object.FindAnyObjectByType<PlayerCameraEffects>();
            if (fx == null) return;
            var d = Vector3.Distance(fx.transform.position, at);
            fx.AddShake(strength * Mathf.Clamp01(1f - d / falloff));
        }

        /// <summary>Altura do chão abaixo de um ponto, ignorando o próprio chefe.</summary>
        public static Vector3 Ground(Vector3 point, Transform ignore)
        {
            var hits = Physics.RaycastAll(point + Vector3.up * 4f, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore);
            var best = float.MaxValue;
            var result = point;
            foreach (var h in hits)
            {
                if (ignore != null && h.transform.IsChildOf(ignore)) continue;
                if (h.collider.GetComponentInParent<PlayerController>() != null) continue;
                if (h.distance < best) { best = h.distance; result = h.point; }
            }
            return result;
        }
    }
}
