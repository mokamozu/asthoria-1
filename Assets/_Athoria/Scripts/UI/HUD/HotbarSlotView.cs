using UnityEngine;
using UnityEngine.UI;

namespace Athoria.UI.HUD
{
    /// <summary>Um espaço da barra de atalhos: moldura, ícone, tecla e contador.</summary>
    public sealed class HotbarSlotView : MonoBehaviour
    {
        [SerializeField] private Image frame;
        [SerializeField] private Image icon;
        [SerializeField] private Text count;
        [SerializeField] private Image cooldownFill;
        [SerializeField] private Text cooldownText;

        private static readonly Color Normal = new(0.4f, 0.29f, 0.15f);
        private static readonly Color Active = new(0.96f, 0.78f, 0.4f);

        public void Set(Sprite sprite, string counter, bool active, bool dimmed = false)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.color = dimmed ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
            if (count != null) count.text = counter ?? string.Empty;
            frame.color = active ? Active : Normal;
        }

        /// <summary>0 = pronta, 1 = recarga cheia.</summary>
        public void SetCooldown(float fraction, float secondsLeft, string label = null)
        {
            if (cooldownFill == null) return;
            cooldownFill.fillAmount = Mathf.Clamp01(fraction);
            cooldownFill.enabled = fraction > 0f;
            if (cooldownText != null)
                cooldownText.text = label ?? (fraction > 0f ? Mathf.CeilToInt(secondsLeft).ToString() : string.Empty);
        }
    }
}
