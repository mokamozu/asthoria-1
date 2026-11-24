using UnityEngine;

namespace Athoria.Gameplay.Interaction
{
    /// <summary>Qualquer coisa com que o jogador pode interagir (portas, baús, NPCs, itens no chão...).</summary>
    public interface IInteractable
    {
        /// <summary>Texto mostrado ao jogador, ex.: "Abrir porta".</summary>
        string InteractionPrompt { get; }
        bool CanInteract(GameObject interactor);
        void Interact(GameObject interactor);
    }
}
