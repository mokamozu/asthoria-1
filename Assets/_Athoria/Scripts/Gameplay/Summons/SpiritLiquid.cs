using System.Collections.Generic;
using UnityEngine;

namespace Athoria.Gameplay.Summons
{
    /// <summary>
    /// Líquido espiritual (lágrimas do Lobo Espiritual): a textura do fio rola para baixo
    /// para parecer escorrer, em passos curtos como nos jogos de PS1, e gotas pingam das
    /// pontas. Os ossos das lágrimas ainda balançam pelas animações do modelo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpiritLiquid : MonoBehaviour
    {
        [SerializeField, Tooltip("Renderers que têm o material do líquido.")] private Renderer[] renderers = System.Array.Empty<Renderer>();
        [SerializeField, Tooltip("Parte do nome do material do líquido.")] private string materialName = "Lagrima";
        [SerializeField, Tooltip("Velocidade do escorrer (repetições da textura por segundo).")] private float flowSpeed = 0.8f;
        [SerializeField, Min(0f), Tooltip("Passos por segundo da rolagem (0 = contínua).")] private float stepsPerSecond = 20f;
        [SerializeField, Tooltip("Gotas que pingam das pontas.")] private ParticleSystem[] drips = System.Array.Empty<ParticleSystem>();

        private static readonly int StId = Shader.PropertyToID("_BaseMap_ST");
        private readonly List<(Renderer renderer, int index, Vector4 st)> _slots = new();
        private MaterialPropertyBlock _block;
        private float _offset;
        private float _pending;
        private float _intensity = 1f;
        private float[] _baseRates = System.Array.Empty<float>();

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                    if (mats[i] != null && mats[i].name.Contains(materialName) && mats[i].HasProperty(StId))
                        _slots.Add((r, i, mats[i].GetVector(StId)));
            }
            _baseRates = new float[drips.Length];
            for (var i = 0; i < drips.Length; i++)
                if (drips[i] != null) _baseRates[i] = drips[i].emission.rateOverTimeMultiplier;
        }

        private void Update()
        {
            _pending += Time.deltaTime;
            if (stepsPerSecond > 0f && _pending < 1f / stepsPerSecond) return;
            _offset = Mathf.Repeat(_offset + flowSpeed * _intensity * _pending, 1f);
            _pending = 0f;
            foreach (var (renderer, index, st) in _slots)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(_block, index);
                _block.SetVector(StId, new Vector4(st.x, st.y, st.z, st.w + _offset));
                renderer.SetPropertyBlock(_block, index);
            }
        }

        /// <summary>1 = escorrendo normal, 0 = parado e sem gotas (espírito se dissipando).</summary>
        public void SetIntensity(float amount)
        {
            _intensity = Mathf.Clamp01(amount);
            for (var i = 0; i < drips.Length; i++)
            {
                if (drips[i] == null) continue;
                var emission = drips[i].emission;
                emission.rateOverTimeMultiplier = _baseRates[i] * _intensity;
            }
        }
    }
}
