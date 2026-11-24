namespace Athoria.Core.Events
{
    /// <summary>Disparado quando o jogo é pausado ou retomado.</summary>
    public readonly struct GamePausedChanged : IGameEvent
    {
        public readonly bool IsPaused;
        public GamePausedChanged(bool isPaused) => IsPaused = isPaused;
    }
}

namespace Athoria.Core.Events
{
    /// <summary>Mensagem curta para o jogador (ex.: "Inventário cheio").</summary>
    public readonly struct GameMessage : IGameEvent
    {
        public readonly string Text;
        public GameMessage(string text) => Text = text;
    }
}
