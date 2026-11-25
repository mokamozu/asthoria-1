using Athoria.Core.Events;
using Athoria.Gameplay.Interaction;
using Athoria.Gameplay.Inventory;
using Athoria.Gameplay.Items;
using UnityEngine;

namespace Athoria.Gameplay.World
{
    /// <summary>
    /// Passagem para outra cena (entrada de masmorra, saída). Pode exigir um
    /// item-chave, que não é gasto: a passagem fica aberta depois.
    /// </summary>
    public sealed class Portal : MonoBehaviour, IInteractable
    {
        [SerializeField] private string targetScene;
        [SerializeField, Tooltip("Id do SpawnPoint de chegada na cena de destino.")] private string targetSpawn;
        [SerializeField] private string prompt = "Entrar";
        [SerializeField] private ItemData requiredKey;
        [SerializeField] private AudioClip useSound;

        private bool _unlocked;

        public string InteractionPrompt => requiredKey != null && !_unlocked ? $"{prompt} (requer {requiredKey.DisplayName})" : prompt;

        public bool CanInteract(GameObject interactor) => !WorldManager.IsTravelling && !string.IsNullOrEmpty(targetScene);

        public void Interact(GameObject interactor)
        {
            if (requiredKey != null && !_unlocked)
            {
                var inventory = interactor.GetComponentInParent<PlayerInventory>();
                if (inventory == null || inventory.CountOf(requiredKey) <= 0)
                {
                    EventBus.Publish(new GameMessage($"Fechado. Precisa de: {requiredKey.DisplayName}"));
                    return;
                }
                _unlocked = true;
                EventBus.Publish(new GameMessage($"Usou {requiredKey.DisplayName}"));
            }

            if (useSound != null) AudioSource.PlayClipAtPoint(useSound, transform.position);
            WorldManager.TravelTo(targetScene, targetSpawn);
        }
    }
}
