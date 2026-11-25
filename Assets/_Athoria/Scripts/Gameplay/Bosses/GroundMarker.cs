using UnityEngine;

namespace Athoria.Gameplay.Bosses
{
    /// <summary>
    /// Marca no chão: aviso antes do golpe (círculo que se fecha e pisca) ou onda de choque
    /// (anel que se expande e some). Sem dano — só a indicação visual.
    /// </summary>
    public sealed class GroundMarker : MonoBehaviour
    {
        private Material _mat;
        private float _radius, _duration, _time;
        private bool _shockwave;
        private Color _color;
        private static Mesh s_quad;
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        public static GroundMarker Warning(Material material, Vector3 center, float radius, float duration, Color color) =>
            Create(material, center, radius, duration, color, false);

        public static GroundMarker Shockwave(Material material, Vector3 center, float radius, float duration, Color color) =>
            Create(material, center, radius, duration, color, true);

        private static GroundMarker Create(Material material, Vector3 center, float radius, float duration, Color color, bool shockwave)
        {
            if (material == null) return null;
            var go = new GameObject(shockwave ? "Onda de choque" : "Aviso no chão");
            go.transform.position = center + Vector3.up * 0.06f;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = Quad();
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var marker = go.AddComponent<GroundMarker>();
            marker._mat = new Material(material);
            r.sharedMaterial = marker._mat;
            marker._radius = radius; marker._duration = Mathf.Max(0.05f, duration);
            marker._shockwave = shockwave; marker._color = color;
            marker.Apply(0f);
            return marker;
        }

        public void Hide() { if (this != null) Destroy(gameObject); }

        private void Update()
        {
            _time += Time.deltaTime;
            var t = Mathf.Clamp01(_time / _duration);
            Apply(t);
            if (_time >= _duration) Destroy(gameObject);
        }

        private void Apply(float t)
        {
            float size, alpha;
            if (_shockwave)
            {
                size = Mathf.Lerp(0.4f, 1.6f, 1f - (1f - t) * (1f - t)) * _radius;
                alpha = 1f - t;
            }
            else
            {
                size = Mathf.Lerp(1.25f, 1f, t) * _radius;                        // fecha até o tamanho exato da área
                alpha = 0.35f + 0.45f * t + 0.2f * Mathf.Sin(_time * Mathf.Lerp(8f, 28f, t));   // pisca mais rápido perto do impacto
            }
            transform.localScale = new Vector3(size * 2f, size * 2f, 1f);
            var c = _color; c.a *= Mathf.Clamp01(alpha);
            if (_mat != null) _mat.SetColor(ColorId, c);
        }

        private void OnDestroy() { if (_mat != null) Destroy(_mat); }

        private static Mesh Quad()
        {
            if (s_quad != null) return s_quad;
            s_quad = new Mesh { name = "Marca" };
            s_quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            s_quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            s_quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            s_quad.RecalculateNormals();
            return s_quad;
        }
    }
}
