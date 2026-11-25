using Athoria.Core.Events;
using Athoria.Gameplay.Combat;

namespace Athoria.Gameplay.Bosses
{
    /// <summary>O chefe acordou e a luta começou (a HUD mostra a barra de vida).</summary>
    public readonly struct BossEncounterStarted : IGameEvent
    {
        public readonly Health Health;
        public readonly string Name;
        public BossEncounterStarted(Health health, string name) { Health = health; Name = name; }
    }

    public readonly struct BossDefeated : IGameEvent
    {
        public readonly Health Health;
        public readonly string Name;
        public BossDefeated(Health health, string name) { Health = health; Name = name; }
    }

    /// <summary>Uma cena de câmera começou/terminou (a HUD se esconde durante a cena).</summary>
    public readonly struct CinematicStateChanged : IGameEvent
    {
        public readonly bool Playing;
        public CinematicStateChanged(bool playing) => Playing = playing;
    }
}
