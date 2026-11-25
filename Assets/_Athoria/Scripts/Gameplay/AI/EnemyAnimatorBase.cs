using UnityEngine;

namespace Athoria.Gameplay.AI
{
    /// <summary>
    /// O que a IA dos inimigos pede para a animação: velocidade de andar e poses de combate.
    /// Implementado pelo EnemyAnimator (procedural, modelos de primitivas) e pelo
    /// HumanoidEnemyAnimator (clipes da Universal Animation Library num esqueleto humanoide).
    /// </summary>
    public abstract class EnemyAnimatorBase : MonoBehaviour
    {
        public enum Pose { None, Windup, Strike, Recover, Block, Stagger, Dead }

        /// <summary>Velocidade horizontal atual em m/s (controla o passo).</summary>
        public abstract void SetSpeed(float metersPerSecond);

        /// <summary>Troca a pose. duration = quanto tempo a IA vai ficar nela (0 = desconhecido).</summary>
        public abstract void SetPose(Pose pose, EnemyAttackPose attackPose = EnemyAttackPose.Slash, float duration = 0f);

        /// <summary>Volta tudo ao repouso na hora (reaparecer).</summary>
        public abstract void ResetPose();
    }
}
