using Athoria.Core.Events;
using UnityEngine;

namespace Athoria.Gameplay.Combat
{
    /// <summary>
    /// O jogador começou um ataque. Inimigos usam para "ler" o golpe
    /// (levantar a guarda) e para ouvir o barulho do combate.
    /// </summary>
    public readonly struct PlayerAttackStarted : IGameEvent
    {
        public readonly GameObject Attacker;
        public readonly Vector3 Origin;
        public readonly Vector3 Forward;

        public PlayerAttackStarted(GameObject attacker, Vector3 origin, Vector3 forward)
        {
            Attacker = attacker;
            Origin = origin;
            Forward = forward;
        }
    }
}
