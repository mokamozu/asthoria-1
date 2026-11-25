using Athoria.Core.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Athoria.Gameplay.Save
{
    /// <summary>F5 salva rápido, F9 carrega o save rápido.</summary>
    public sealed class SaveHotkeys : MonoBehaviour
    {
        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f5Key.wasPressedThisFrame) SaveSystem.Save(SaveSystem.QuickSlot);
            if (kb.f9Key.wasPressedThisFrame)
            {
                if (SaveSystem.Exists(SaveSystem.QuickSlot)) SaveSystem.Load(SaveSystem.QuickSlot);
                else EventBus.Publish(new GameMessage("Nenhum save rápido (F5 para salvar)"));
            }
        }
    }
}
