using UnityEngine;
using UnityEngine.UI;

namespace Athoria.UI.HUD
{
    /// <summary>
    /// Barra de recurso (vida, stamina, mana). O preenchimento segue o valor na
    /// hora; uma "trilha" clara fica para trás mostrando quanto foi perdido.
    /// </summary>
    public sealed class ResourceBar : MonoBehaviour
    {
        [SerializeField] private RectTransform fill;
        [SerializeField] private RectTransform trail;
        [SerializeField] private Image fillImage;
        [SerializeField] private Text label;
        [SerializeField] private bool showNumbers = true;

        [Header("Trilha")]
        [SerializeField] private float trailDelay = 0.45f;
        [SerializeField] private float trailSpeed = 0.9f;

        [Header("Alerta (ex.: exaustão, vida baixa)")]
        [SerializeField] private Color alertColor = new(1f, 0.35f, 0.25f);
        [SerializeField] private float alertPulseSpeed = 8f;

        private float _value = 1f;
        private float _trailValue = 1f;
        private float _trailHoldUntil;
        private Color _baseColor;
        private bool _alert;

        public float Value => _value;

        private void Awake()
        {
            if (fillImage != null) _baseColor = fillImage.color;
            Apply(fill, _value);
            ApplyTrail();
        }

        public void Set(float current, float max)
        {
            var v = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            if (v < _value) _trailHoldUntil = Time.unscaledTime + trailDelay;
            else _trailValue = Mathf.Max(_trailValue, v);   // ganho: trilha acompanha
            _value = v;
            Apply(fill, _value);
            if (label != null) label.text = showNumbers ? $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}" : string.Empty;
        }

        public void SetAlert(bool alert)
        {
            _alert = alert;
            if (!alert && fillImage != null) fillImage.color = _baseColor;
        }

        private void Update()
        {
            if (_trailValue > _value && Time.unscaledTime >= _trailHoldUntil)
                _trailValue = Mathf.MoveTowards(_trailValue, _value, trailSpeed * Time.unscaledDeltaTime);
            if (_trailValue < _value) _trailValue = _value;
            ApplyTrail();

            if (_alert && fillImage != null)
            {
                var t = (Mathf.Sin(Time.unscaledTime * alertPulseSpeed) + 1f) * 0.5f;
                fillImage.color = Color.Lerp(_baseColor, alertColor, t);
            }
        }

        /// <summary>A trilha cobre só o trecho perdido (entre o valor atual e o antigo).</summary>
        private void ApplyTrail()
        {
            if (trail == null) return;
            trail.anchorMin = new Vector2(_value, trail.anchorMin.y);
            trail.anchorMax = new Vector2(Mathf.Max(_value, _trailValue), trail.anchorMax.y);
        }

        private static void Apply(RectTransform rect, float v)
        {
            if (rect == null) return;
            rect.anchorMax = new Vector2(v, rect.anchorMax.y);
        }
    }
}
