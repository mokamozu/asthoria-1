using Athoria.Core.Events;
using UnityEngine;

namespace Athoria.Gameplay.World
{
    /// <summary>Nome da área desta cena. Ao abrir, mostra o banner e põe o jogador no ponto de chegada.</summary>
    public sealed class AreaInfo : MonoBehaviour
    {
        [SerializeField] private string id = "area";
        [SerializeField] private string displayName = "Área";
        [SerializeField] private string subtitle;

        public string Id => id;
        public string DisplayName => displayName;

        private void Start()
        {
            // Abrindo a cena direto (sem viagem): usa o ponto padrão.
            if (!WorldManager.IsTravelling) WorldManager.PlaceAtSpawn(null);
            EventBus.Publish(new AreaEntered(id, displayName, subtitle));
        }
    }
}
