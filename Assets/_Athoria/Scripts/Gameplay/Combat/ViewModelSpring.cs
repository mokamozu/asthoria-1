using System;
using UnityEngine;

namespace Athoria.Gameplay.Combat
{
    /// <summary>
    /// Mola amortecida para os movimentos dos braços/arma (como no Overwatch): segue um alvo
    /// com tensão, passa um pouco do ponto e assenta — em vez de interpolar em linha reta.
    /// frequency = quão rápida (Hz); damping = 0 oscila sem parar, 1 assenta sem passar do ponto.
    /// </summary>
    [Serializable]
    public struct SpringSettings
    {
        [Min(0.1f)] public float frequency;
        [Range(0f, 1.5f)] public float damping;
        public SpringSettings(float frequency, float damping) { this.frequency = frequency; this.damping = damping; }
    }

    public struct Spring3
    {
        public Vector3 Value;
        public Vector3 Velocity;

        public void Step(Vector3 target, SpringSettings s, float dt)
        {
            // subpassos para continuar estável com quadros longos
            var steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.008f), 1, 8);
            var h = dt / steps;
            var w = 2f * Mathf.PI * s.frequency;
            for (var i = 0; i < steps; i++)
            {
                var accel = w * w * (target - Value) - 2f * s.damping * w * Velocity;
                Velocity += accel * h;
                Value += Velocity * h;
            }
        }

        public void Kick(Vector3 impulse) => Velocity += impulse;
        public void Reset() { Value = Velocity = Vector3.zero; }
    }
}
