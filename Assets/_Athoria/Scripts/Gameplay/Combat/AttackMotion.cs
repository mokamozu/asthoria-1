using Athoria.Gameplay.Items;
using UnityEngine;

namespace Athoria.Gameplay.Combat
{
    public enum AttackStyle { Slash, Thrust, Overhead, Shot }

    /// <summary>
    /// Trajetórias procedurais dos golpes: pose de preparação (windup), pose
    /// final e a varredura usada para detectar acertos. Valores no espaço da
    /// câmera, somados à pose do braço.
    /// </summary>
    public static class AttackMotion
    {
        public readonly struct Keyframes
        {
            public readonly Vector3 WindupPos, WindupRot, EndPos, EndRot;
            /// <summary>Varredura em graus (x = yaw, y = pitch) do início ao fim.</summary>
            public readonly Vector2 SweepStart, SweepEnd;

            public Keyframes(Vector3 wp, Vector3 wr, Vector3 ep, Vector3 er, Vector2 ss, Vector2 se)
            {
                WindupPos = wp; WindupRot = wr; EndPos = ep; EndRot = er; SweepStart = ss; SweepEnd = se;
            }
        }

        public static AttackStyle StyleFor(WeaponCategory category, bool heavy)
        {
            switch (category)
            {
                case WeaponCategory.Firearm:
                    return AttackStyle.Shot;
                case WeaponCategory.Dagger:
                case WeaponCategory.Spear:
                case WeaponCategory.Unarmed:
                    return AttackStyle.Thrust;
                default:
                    return heavy ? AttackStyle.Overhead : AttackStyle.Slash;
            }
        }

        /// <param name="mirrored">Golpe de volta (esquerda → direita), usado no combo.</param>
        public static Keyframes Get(AttackStyle style, bool mirrored)
        {
            switch (style)
            {
                case AttackStyle.Shot:
                    // Mira (leve recuo para trás) e coice para cima ao disparar.
                    return new Keyframes(
                        new Vector3(0f, 0.01f, -0.02f), new Vector3(-2f, 0f, 0f),
                        new Vector3(0f, 0.04f, -0.12f), new Vector3(-14f, 2f, 0f),
                        Vector2.zero, Vector2.zero);
                case AttackStyle.Thrust:
                    return new Keyframes(
                        new Vector3(0f, -0.02f, -0.12f), new Vector3(6f, 0f, 0f),
                        new Vector3(-0.04f, 0.03f, 0.22f), new Vector3(-6f, -6f, 0f),
                        Vector2.zero, Vector2.zero);
                case AttackStyle.Overhead:
                    return new Keyframes(
                        new Vector3(0.04f, 0.12f, -0.06f), new Vector3(-42f, 12f, -12f),
                        new Vector3(-0.06f, -0.12f, 0.12f), new Vector3(40f, -20f, 10f),
                        new Vector2(0f, -35f), new Vector2(0f, 35f));
                default:
                    return mirrored
                        ? new Keyframes(
                            new Vector3(-0.16f, 0.02f, 0.04f), new Vector3(-10f, -50f, 30f),
                            new Vector3(0.1f, -0.06f, 0.04f), new Vector3(10f, 40f, -30f),
                            new Vector2(-55f, -5f), new Vector2(55f, 10f))
                        : new Keyframes(
                            new Vector3(0.08f, 0.06f, -0.05f), new Vector3(-20f, 35f, -30f),
                            new Vector3(-0.2f, -0.04f, 0.08f), new Vector3(10f, -60f, 35f),
                            new Vector2(55f, -5f), new Vector2(-55f, 10f));
            }
        }
    }
}
