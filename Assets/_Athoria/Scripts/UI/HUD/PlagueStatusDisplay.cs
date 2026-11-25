using Athoria.Core.Events;
using Athoria.Gameplay.World;
using UnityEngine;

namespace Athoria.UI.HUD
{
    /// <summary>
    /// Barra da contaminação da peste: aparece suavemente quando a jogadora se contamina e
    /// some quando zera. Pulsa enquanto ela está na água negra e quando chega em 100%.
    /// </summary>
    public sealed class PlagueStatusDisplay : MonoBehaviour
    {
        [SerializeField] private ResourceBar bar;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private float fadeSpeed = 2.5f;

        private float _value;
        private bool _inWater;

        private void OnEnable()
        {
            EventBus.Subscribe<PlagueChanged>(OnPlague);
            if (group != null) group.alpha = 0f;
        }

        private void OnDisable() => EventBus.Unsubscribe<PlagueChanged>(OnPlague);

        private void OnPlague(PlagueChanged e)
        {
            _value = e.Value;
            _inWater = e.InPlagueWater;
            if (bar != null) bar.Set(e.Value * 100f, 100f);
            if (bar != null) bar.SetAlert(e.Value >= 1f || e.InPlagueWater);
        }

        private void Update()
        {
            if (group == null) return;
            var target = _value > 0.001f || _inWater ? 1f : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * fadeSpeed);
        }
    }
}
