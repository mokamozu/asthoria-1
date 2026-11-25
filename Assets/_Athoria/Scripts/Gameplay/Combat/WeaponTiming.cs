using System;
using Athoria.Gameplay.Items;
using UnityEngine;

namespace Athoria.Gameplay.Combat
{
    /// <summary>
    /// Tempo de um golpe, em fases: antecipação (com uma pausa no alto, para o golpe ser lido),
    /// execução (o único trecho que causa dano), acompanhamento (o corpo segue o movimento e
    /// não dá para cancelar) e recuperação (volta à guarda; o combo entra no meio dela).
    /// As frações são proporções: a soma vira o tempo de um golpe leve (1 / ataques por segundo).
    /// </summary>
    [Serializable]
    public sealed class WeaponTiming
    {
        public WeaponCategory category;

        [Header("Golpe leve (proporções)")]
        [Min(0.01f)] public float anticipation = 0.28f;
        [Range(0f, 0.6f), Tooltip("Parte final da antecipação parada no alto do golpe.")] public float anticipationHold = 0.12f;
        [Min(0.01f), Tooltip("Execução: só aqui o golpe acerta.")] public float strike = 0.16f;
        [Min(0f), Tooltip("Acompanhamento: sem dano, sem esquiva nem combo (compromisso).")] public float followThrough = 0.14f;
        [Min(0.01f)] public float recovery = 0.42f;
        [Range(0f, 1f), Tooltip("A partir de que fração da recuperação o próximo golpe do combo é aceito.")] public float comboWindow = 0.35f;
        [Tooltip("Progresso da execução no tempo (0-1 → 0-1). O ponto mais rápido fica por volta de 60–70%.")]
        public AnimationCurve strikeCurve = Curve(0.6f, 0.6f, 1.8f);

        [Header("Golpe pesado")]
        [Min(1f)] public float heavyDurationMultiplier = 1.5f;
        [Min(0f), Tooltip("Preparação extra do pesado (fração do golpe leve).")] public float heavyAnticipation = 0.4f;

        [Header("Impacto")]
        [Min(0f)] public float hitStopLight = 0.045f;
        [Min(0f)] public float hitStopHeavy = 0.09f;
        [Min(0f), Tooltip("Pausa extra quando o golpe mata.")] public float hitStopKill = 0.03f;
        [Min(0f)] public float shakeLight = 0.25f;
        [Min(0f)] public float shakeHeavy = 0.45f;

        [Header("Rastro")]
        public bool trail = true;
        public Color trailColor = new(0.85f, 0.9f, 1f, 0.35f);
        [Range(0f, 1f), Tooltip("Onde o rastro começa na lâmina (0 = cabo, 1 = ponta).")] public float trailStart = 0.35f;
        [Min(0.02f)] public float trailLifetime = 0.12f;

        public float LightTotal => anticipation + strike + followThrough + recovery;

        /// <summary>Progresso da execução com a curva (sempre 0-1, crescente).</summary>
        public float StrikeProgress(float t)
        {
            t = Mathf.Clamp01(t);
            if (strikeCurve == null || strikeCurve.length < 2) return t * t * (3f - 2f * t);
            return Mathf.Clamp01(strikeCurve.Evaluate(t));
        }

        /// <summary>Curva com aceleração até o pico (peakT) e desaceleração depois.</summary>
        public static AnimationCurve Curve(float peakT, float peakValue, float peakSlope, float edgeSlope = 0.3f) =>
            new(new Keyframe(0f, 0f, edgeSlope, edgeSlope),
                new Keyframe(peakT, peakValue, peakSlope, peakSlope),
                new Keyframe(1f, 1f, edgeSlope, edgeSlope));
    }

    /// <summary>Tabela de tempos por categoria de arma; uma arma pode sobrescrever a sua.</summary>
    [CreateAssetMenu(menuName = "Athoria/Combate/Tabela de tempos das armas", fileName = "TemposDasArmas")]
    public sealed class WeaponTimingTable : ScriptableObject
    {
        public WeaponTiming[] entries = Array.Empty<WeaponTiming>();

        public WeaponTiming Get(WeaponCategory category)
        {
            foreach (var e in entries) if (e != null && e.category == category) return e;
            return null;
        }
    }
}
