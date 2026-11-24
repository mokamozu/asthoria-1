using UnityEngine;
using UnityEngine.UI;

namespace Athoria.UI.HUD
{
    /// <summary>Bordas da tela ficam vermelhas ao levar dano. A textura é gerada em código.</summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class DamageVignette : MonoBehaviour
    {
        [SerializeField] private Color color = new(0.55f, 0f, 0f, 1f);
        [SerializeField] private float fadeSpeed = 1.6f;
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.75f;

        private RawImage _image;
        private float _intensity;

        private void Awake()
        {
            _image = GetComponent<RawImage>();
            _image.texture = BuildTexture(128);
            _image.raycastTarget = false;
            Apply();
        }

        public void Flash(float intensity) => _intensity = Mathf.Clamp01(Mathf.Max(_intensity, intensity));

        private void Update()
        {
            if (_intensity <= 0f) return;
            _intensity = Mathf.MoveTowards(_intensity, 0f, fadeSpeed * Time.unscaledDeltaTime);
            Apply();
        }

        private void Apply()
        {
            var c = color;
            c.a = _intensity * maxAlpha;
            _image.color = c;
            _image.enabled = _intensity > 0f;
        }

        private static Texture2D BuildTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Vinheta" };
            var px = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var u = (x + 0.5f) / size * 2f - 1f;
                var v = (y + 0.5f) / size * 2f - 1f;
                var d = Mathf.Sqrt(u * u + v * v) / 1.4142f;
                var a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
