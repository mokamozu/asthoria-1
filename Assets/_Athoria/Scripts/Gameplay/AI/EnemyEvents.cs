using Athoria.Core.Events;

namespace Athoria.Gameplay.AI
{
    /// <summary>Um inimigo morreu (para missões, estatísticas, HUD).</summary>
    public readonly struct EnemyKilled : IGameEvent
    {
        public readonly EnemyAI Enemy;
        public readonly EnemyData Data;

        public EnemyKilled(EnemyAI enemy, EnemyData data)
        {
            Enemy = enemy;
            Data = data;
        }
    }

    /// <summary>Um inimigo avistou o jogador e entrou em combate.</summary>
    public readonly struct EnemyEngaged : IGameEvent
    {
        public readonly EnemyAI Enemy;
        public EnemyEngaged(EnemyAI enemy) => Enemy = enemy;
    }
}
