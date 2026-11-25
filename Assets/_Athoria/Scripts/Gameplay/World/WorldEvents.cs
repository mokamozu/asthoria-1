using Athoria.Core.Events;

namespace Athoria.Gameplay.World
{
    /// <summary>O jogador chegou a uma área (região ou interior). Mostrado no banner.</summary>
    public readonly struct AreaEntered : IGameEvent
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string Subtitle;

        public AreaEntered(string id, string name, string subtitle)
        {
            Id = id;
            Name = name;
            Subtitle = subtitle;
        }
    }
}
