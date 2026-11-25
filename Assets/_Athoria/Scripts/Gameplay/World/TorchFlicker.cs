using UnityEngine;

namespace Athoria.Gameplay.World
{
    /// <summary>Faz uma luz tremeluzir como fogo (tochas, fogueiras, lanternas).</summary>
    [RequireComponent(typeof(Light))]
    public sealed class TorchFlicker : MonoBehaviour
    {
        [SerializeField] private float amount = 0.25f;
        [SerializeField] private float speed = 7f;

        private Light _light;
        private float _base;
        private float _seed;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _base = _light.intensity;
            _seed = Random.value * 100f;
        }

        private void Update()
        {
            var n = Mathf.PerlinNoise(_seed, Time.time * speed) * 2f - 1f;
            _light.intensity = _base * (1f + n * amount);
        }
    }
}
