using Athoria.Core.Events;
using Athoria.Gameplay.Stats;
using UnityEngine;
using UnityEngine.UI;

namespace Athoria.UI.HUD
{
    /// <summary>Nível atual, barra de progresso de XP e "+XP" ao ganhar experiência.</summary>
    public sealed class ExperienceDisplay : MonoBehaviour
    {
        [SerializeField] private ResourceBar xpBar;
        [SerializeField] private Text levelText;
        [SerializeField] private Text gainText;
        [SerializeField] private float gainVisibleTime = 1.6f;
        [SerializeField] private float gainRise = 14f;

        private float _gainShownAt = float.NegativeInfinity;
        private int _gainAccumulated;
        private Vector2 _gainBasePos;

        private void Awake()
        {
            if (gainText != null)
            {
                _gainBasePos = gainText.rectTransform.anchoredPosition;
                SetGainAlpha(0f);
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ExperienceChanged>(OnChanged);
            EventBus.Subscribe<ExperienceGained>(OnGained);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ExperienceChanged>(OnChanged);
            EventBus.Unsubscribe<ExperienceGained>(OnGained);
        }

        private void OnChanged(ExperienceChanged e)
        {
            if (levelText != null) levelText.text = e.Level.ToString();
            if (e.Required <= 0) xpBar.Set(1f, 1f);
            else xpBar.Set(e.Current, e.Required);
        }

        private void OnGained(ExperienceGained e)
        {
            if (gainText == null) return;
            // Ganhos em sequência rápida somam no mesmo texto.
            var visible = Time.unscaledTime - _gainShownAt < gainVisibleTime * 0.6f;
            _gainAccumulated = visible ? _gainAccumulated + e.Amount : e.Amount;
            gainText.text = $"+{_gainAccumulated} XP";
            _gainShownAt = Time.unscaledTime;
        }

        private void Update()
        {
            if (gainText == null) return;
            var t = (Time.unscaledTime - _gainShownAt) / gainVisibleTime;
            if (t > 1f)
            {
                SetGainAlpha(0f);
                return;
            }
            SetGainAlpha(t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f);
            gainText.rectTransform.anchoredPosition = _gainBasePos + Vector2.up * (gainRise * t);
        }

        private void SetGainAlpha(float a)
        {
            var c = gainText.color;
            c.a = a;
            gainText.color = c;
        }
    }
}
