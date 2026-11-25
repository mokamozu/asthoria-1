using System.Collections.Generic;
using UnityEngine;

namespace Athoria.Gameplay.World
{
    /// <summary>
    /// Poça da peste: água negra e oleosa. Quem pisa dentro dela (na água) vai acumulando
    /// contaminação. A área é um círculo no plano (centro = este objeto).
    /// </summary>
    public sealed class PlagueWater : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float radius = 20f;

        private static readonly List<PlagueWater> All = new();

        public float Radius => radius;

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        /// <summary>O ponto está dentro de alguma poça da peste (no plano)?</summary>
        public static bool Contains(Vector3 point)
        {
            foreach (var p in All)
            {
                var d = point - p.transform.position;
                d.y = 0f;
                if (d.sqrMagnitude <= p.radius * p.radius) return true;
            }
            return false;
        }

#if UNITY_EDITOR
        public void EditorSetRadius(float r) => radius = r;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.1f, 0.5f, 0.8f);
            UnityEditor.Handles.color = Gizmos.color;
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, radius);
        }
#endif
    }
}
