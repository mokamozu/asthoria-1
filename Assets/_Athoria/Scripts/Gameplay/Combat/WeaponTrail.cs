using System.Collections.Generic;
using UnityEngine;

namespace Athoria.Gameplay.Combat
{
    /// <summary>
    /// Rastro da lâmina na primeira pessoa: uma fita entre a base e a ponta da arma, guardada
    /// no espaço do view model (acompanha a câmera) e que some em poucos décimos de segundo.
    /// Usa Time.time, então congela junto com a pausa de impacto.
    /// </summary>
    [DefaultExecutionOrder(300)]   // depois do IK e das poses: a arma já está no lugar final
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class WeaponTrail : MonoBehaviour
    {
        private struct Sample { public Vector3 a, b; public float time; }

        private const int MaxSamples = 32;
        private readonly List<Sample> _samples = new();
        private readonly List<Vector3> _verts = new();
        private readonly List<Color> _colors = new();
        private readonly List<Vector2> _uvs = new();
        private readonly List<int> _tris = new();
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private Transform _weapon;
        private Vector3 _base, _tip;
        private bool _emit;
        private Color _color = Color.white;
        private float _life = 0.12f;

        private void Awake()
        {
            _mesh = new Mesh { name = "RastroArma" };
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = GetComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;
        }

        public void SetMaterial(Material material) { if (material != null) GetComponent<MeshRenderer>().sharedMaterial = material; }

        /// <summary>Arma atual (base e ponta no espaço local do modelo). Nula = sem rastro.</summary>
        public void SetWeapon(Transform weapon, Vector3 baseLocal, Vector3 tipLocal)
        {
            _weapon = weapon;
            _base = baseLocal;
            _tip = tipLocal;
            _samples.Clear();
        }

        /// <param name="start">Onde o rastro começa na lâmina (0 = cabo, 1 = ponta).</param>
        public void SetEmitting(bool on, Color color, float lifetime, float start = 0.35f)
        {
            if (on && !_emit) _samples.Clear();
            _emit = on;
            if (on) { _color = color; _life = Mathf.Max(0.02f, lifetime); _base = _tip * Mathf.Clamp01(start); }
        }

        private void LateUpdate()
        {
            var now = Time.time;
            if (_emit && _weapon != null)
            {
                _samples.Add(new Sample
                {
                    a = transform.InverseTransformPoint(_weapon.TransformPoint(_base)),
                    b = transform.InverseTransformPoint(_weapon.TransformPoint(_tip)),
                    time = now,
                });
                if (_samples.Count > MaxSamples) _samples.RemoveAt(0);
            }
            while (_samples.Count > 0 && now - _samples[0].time > _life) _samples.RemoveAt(0);
            if (_samples.Count < 2) { _renderer.enabled = false; return; }
            Build(now);
            _renderer.enabled = true;
        }

        private void Build(float now)
        {
            _verts.Clear(); _colors.Clear(); _uvs.Clear(); _tris.Clear();
            var n = _samples.Count;
            for (var i = 0; i < n; i++)
            {
                var s = _samples[i];
                var age = Mathf.Clamp01((now - s.time) / _life);
                var fade = (1f - age) * (i / (float)(n - 1));    // mais forte perto da arma
                var tipColor = _color; tipColor.a *= fade;
                var baseColor = _color; baseColor.a *= fade * 0.15f;
                _verts.Add(s.a); _colors.Add(baseColor); _uvs.Add(new Vector2(i / (float)(n - 1), 0f));
                _verts.Add(s.b); _colors.Add(tipColor); _uvs.Add(new Vector2(i / (float)(n - 1), 1f));
                if (i == 0) continue;
                var v = i * 2;
                _tris.Add(v - 2); _tris.Add(v - 1); _tris.Add(v);
                _tris.Add(v - 1); _tris.Add(v + 1); _tris.Add(v);
                // dupla face: a fita é vista dos dois lados
                _tris.Add(v); _tris.Add(v - 1); _tris.Add(v - 2);
                _tris.Add(v); _tris.Add(v + 1); _tris.Add(v - 1);
            }
            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.SetColors(_colors);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetTriangles(_tris, 0);
            _mesh.RecalculateBounds();
        }

        private void OnDestroy() { if (_mesh != null) Destroy(_mesh); }
    }
}
