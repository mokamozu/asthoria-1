using Athoria.Core.Events;

namespace Athoria.Gameplay.Interaction
{
    /// <summary>Mudou o objeto que o jogador está mirando. Prompt nulo = nada interagível.</summary>
    public readonly struct InteractionTargetChanged : IGameEvent
    {
        public readonly string Prompt;
        public bool HasTarget => Prompt != null;
        public InteractionTargetChanged(string prompt) => Prompt = prompt;
    }
}
