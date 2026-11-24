using Athoria.Core.Events;
using Athoria.Gameplay.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace Athoria.UI.HUD
{
    /// <summary>Mostra "[E] Abrir porta", "[E] Pegar Poção"... quando o jogador mira algo interagível.</summary>
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [SerializeField] private Text text;
        [SerializeField] private string keyLabel = "E";

        private void Awake() => text.text = string.Empty;
        private void OnEnable() => EventBus.Subscribe<InteractionTargetChanged>(OnTarget);
        private void OnDisable() => EventBus.Unsubscribe<InteractionTargetChanged>(OnTarget);

        private void OnTarget(InteractionTargetChanged e) =>
            text.text = e.HasTarget ? $"[{keyLabel}]  {e.Prompt}" : string.Empty;
    }
}
