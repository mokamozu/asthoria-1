using Athoria.Gameplay.AI;
using UnityEngine;

namespace Athoria.Gameplay.Summons
{
    /// <summary>
    /// Escolha de alvo dos espíritos: só inimigos (EnemyAI) vivos, perto do espírito
    /// e sem se afastar demais da invocadora. Aliados nunca entram na lista.
    /// </summary>
    public static class SpiritTargeting
    {
        public static bool IsValid(EnemyAI enemy, Vector3 spirit, Vector3 owner, SpiritData data, float slack = 1f)
        {
            if (enemy == null || !enemy.IsAlive || enemy.CurrentState == EnemyAI.State.Dead) return false;
            var p = enemy.transform.position;
            return Flat(p - spirit) <= data.DetectionRange * slack && Flat(p - owner) <= data.LeashRange * slack;
        }

        public static EnemyAI FindNearest(Vector3 spirit, Vector3 owner, SpiritData data)
        {
            EnemyAI best = null;
            var bestDist = float.MaxValue;
            foreach (var enemy in EnemyAI.All)
            {
                if (!IsValid(enemy, spirit, owner, data)) continue;
                var d = Flat(enemy.transform.position - spirit);
                if (d >= bestDist) continue;
                bestDist = d;
                best = enemy;
            }
            return best;
        }

        public static float Flat(Vector3 v)
        {
            v.y = 0f;
            return v.magnitude;
        }
    }
}
