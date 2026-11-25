using System.Collections.Generic;
using Athoria.Gameplay.Items;
using UnityEngine;

namespace Athoria.Gameplay.Combat
{
    public enum AttackStyle { Slash, Thrust, Overhead, Shot }

    /// <summary>Conjunto de golpes de uma arma (cada cavaleira tem o seu).</summary>
    public enum AttackSet
    {
        [InspectorName("Genérico (pela categoria)")] Generic,
        [InspectorName("Katana (Akane)")] Katana,
        [InspectorName("Khopesh (Anaíra)")] Khopesh,
        [InspectorName("Alabarda (Layla)")] Halberd,
        [InspectorName("Arpão (Anne)")] Harpoon,
        [InspectorName("Escopeta (Anne)")] Shotgun,
        [InspectorName("Carabina de alavanca (Evelyn)")] LeverCarbine,
        [InspectorName("Faca de campo (Evelyn)")] FieldKnife,
        [InspectorName("Montante (Elena)")] Greatsword,
        [InspectorName("Presa da Serpente (Emily)")] SerpentFang,
        [InspectorName("Kanabō (Yuna)")] Kanabo,
    }

    /// <summary>
    /// Animações dos ataques em primeira pessoa, feitas por quadros-chave (posição e
    /// rotação somadas à pose do braço, no espaço da câmera) e interpoladas em curva
    /// (Catmull-Rom), em três fases: preparação → golpe (arco completo) → recuperação
    /// (com continuação do movimento antes de voltar à guarda).
    /// Cada cavaleira tem um combo de 3 golpes leves e um golpe pesado próprios.
    /// Os tempos de cada fase continuam vindo da arma (velocidade de ataque).
    /// </summary>
    public static class AttackMotion
    {
        /// <summary>Quadro-chave: t (0–1 dentro da fase), posição (m) e rotação (graus).</summary>
        public readonly struct Key
        {
            public readonly float T;
            public readonly Vector3 Pos, Rot;
            public Key(float t, Vector3 pos, Vector3 rot) { T = t; Pos = pos; Rot = rot; }
        }

        public sealed class Swing
        {
            /// <summary>Do repouso até a pose armada (o último quadro é a pose de preparação).</summary>
            public Key[] Windup;
            /// <summary>Da pose armada, passando pelo arco, até o fim do golpe.</summary>
            public Key[] Strike;
            /// <summary>Do fim do golpe até a guarda (o último quadro deve ser zero).</summary>
            public Key[] Recover;
            /// <summary>Varredura de acerto em graus (x = guinada, y = arfagem), do início ao fim do golpe.</summary>
            public Vector2 SweepStart, SweepEnd;

            public Vector3 WindupPos => Windup[Windup.Length - 1].Pos;
            public Vector3 WindupRot => Windup[Windup.Length - 1].Rot;
            public Vector3 EndPos => Strike[Strike.Length - 1].Pos;
            public Vector3 EndRot => Strike[Strike.Length - 1].Rot;

            public void SampleWindup(float t, out Vector3 pos, out Vector3 rot) => Sample(Windup, Vector3.zero, Vector3.zero, t, out pos, out rot);
            public void SampleStrike(float t, out Vector3 pos, out Vector3 rot) => Sample(Strike, WindupPos, WindupRot, t, out pos, out rot);
            public void SampleRecover(float t, out Vector3 pos, out Vector3 rot) => Sample(Recover, EndPos, EndRot, t, out pos, out rot);
        }

        // ---------------- interpolação ----------------

        private static readonly List<Key> Scratch = new(8);

        private static void Sample(Key[] keys, Vector3 startPos, Vector3 startRot, float t, out Vector3 pos, out Vector3 rot)
        {
            Scratch.Clear();
            Scratch.Add(new Key(0f, startPos, startRot));
            foreach (var k in keys) if (k.T > 0f) Scratch.Add(k);
            t = Mathf.Clamp01(t);
            var i = 1;
            while (i < Scratch.Count - 1 && Scratch[i].T < t) i++;
            var a = Scratch[i - 1];
            var b = Scratch[i];
            var p0 = Scratch[Mathf.Max(0, i - 2)];
            var p3 = Scratch[Mathf.Min(Scratch.Count - 1, i + 1)];
            var u = b.T > a.T ? (t - a.T) / (b.T - a.T) : 1f;
            pos = CatmullRom(p0.Pos, a.Pos, b.Pos, p3.Pos, u);
            rot = CatmullRom(p0.Rot, a.Rot, b.Rot, p3.Rot, u);
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            var u2 = u * u;
            var u3 = u2 * u;
            return 0.5f * (2f * p1 + (p2 - p0) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (3f * p1 - p0 - 3f * p2 + p3) * u3);
        }

        // ---------------- escolha do golpe ----------------

        public static int ComboLength(WeaponData weapon) =>
            weapon == null || weapon.IsFirearm ? 1 : Library.TryGetValue(weapon.AttackSet, out var set) ? set.Light.Length : 2;

        /// <summary>Golpe leve do passo do combo (0, 1, 2…) ou o golpe pesado.</summary>
        public static Swing Get(WeaponData weapon, bool heavy, int comboStep)
        {
            if (weapon != null && Library.TryGetValue(weapon.AttackSet, out var set))
                return heavy ? set.Heavy : set.Light[Mathf.Abs(comboStep) % set.Light.Length];
            var style = StyleFor(weapon != null ? weapon.Category : WeaponCategory.Unarmed, heavy);
            return Generic(style, comboStep % 2 == 1);
        }

        public static AttackStyle StyleFor(WeaponCategory category, bool heavy)
        {
            switch (category)
            {
                case WeaponCategory.Firearm: return AttackStyle.Shot;
                case WeaponCategory.Dagger:
                case WeaponCategory.Spear:
                case WeaponCategory.Unarmed: return AttackStyle.Thrust;
                default: return heavy ? AttackStyle.Overhead : AttackStyle.Slash;
            }
        }

        // ---------------- atalhos para escrever os golpes ----------------

        private static Key K(float t, float x, float y, float z, float rx, float ry, float rz) => new(t, new Vector3(x, y, z), new Vector3(rx, ry, rz));
        private static Key Zero(float t = 1f) => new(t, Vector3.zero, Vector3.zero);
        private static Swing S(Key[] windup, Key[] strike, Key[] recover, Vector2 from, Vector2 to) =>
            new() { Windup = windup, Strike = strike, Recover = recover, SweepStart = from, SweepEnd = to };
        private static Vector2 V(float yaw, float pitch) => new(yaw, pitch);

        private sealed class Set
        {
            public Swing[] Light;
            public Swing Heavy;
        }

        // Golpes antigos (armas sem conjunto próprio, como as de teste).
        private static Swing Generic(AttackStyle style, bool mirrored)
        {
            switch (style)
            {
                case AttackStyle.Shot:
                    return S(new[] { K(1, 0, .01f, -.02f, -2, 0, 0) }, new[] { K(1, 0, .04f, -.12f, -14, 2, 0) }, new[] { Zero() }, V(0, 0), V(0, 0));
                case AttackStyle.Thrust:
                    return S(new[] { K(1, 0, -.02f, -.12f, 6, 0, 0) }, new[] { K(1, -.04f, .03f, .22f, -6, -6, 0) }, new[] { Zero() }, V(0, 0), V(0, 0));
                case AttackStyle.Overhead:
                    return S(new[] { K(1, .04f, .12f, -.06f, -42, 12, -12) }, new[] { K(1, -.06f, -.12f, .12f, 40, -20, 10) }, new[] { Zero() }, V(0, -35), V(0, 35));
                default:
                    return mirrored
                        ? S(new[] { K(1, -.16f, .02f, .04f, -10, -50, 30) }, new[] { K(1, .1f, -.06f, .04f, 10, 40, -30) }, new[] { Zero() }, V(-55, -5), V(55, 10))
                        : S(new[] { K(1, .08f, .06f, -.05f, -20, 35, -30) }, new[] { K(1, -.2f, -.04f, .08f, 10, -60, 35) }, new[] { Zero() }, V(55, -5), V(-55, 10));
            }
        }

        // =====================================================================
        // Biblioteca de golpes por cavaleira
        // Convenções: +X direita, +Y cima, +Z frente. Rotação X negativa = arma para cima/trás,
        // positiva = para baixo; Y positivo = para a direita; Z = giro do pulso.
        // =====================================================================
        private static readonly Dictionary<AttackSet, Set> Library = new()
        {
            // ---------- Akane: Katana (kesa-giri, gyaku-kesa, yoko; pesado: kabuto-wari) ----------
            [AttackSet.Katana] = new Set
            {
                Light = new[]
                {
                    // 1) Corte diagonal descendente: alto à direita → baixo à esquerda
                    S(new[] { K(.6f, .10f, .12f, -.04f, -35, 30, -35), K(1, .12f, .14f, -.06f, -40, 34, -40) },
                      new[] { K(.35f, .02f, .06f, .08f, -10, 5, -10), K(.7f, -.14f, -.06f, .10f, 20, -40, 25), K(1, -.20f, -.10f, .06f, 28, -58, 35) },
                      new[] { K(.35f, -.22f, -.12f, .04f, 32, -62, 38), Zero() }, V(55, -25), V(-55, 15)),
                    // 2) Corte diagonal ascendente de volta: baixo à esquerda → alto à direita
                    S(new[] { K(1, -.14f, -.08f, 0f, 18, -45, 30) },
                      new[] { K(.4f, -.04f, -.02f, .10f, 5, -10, 10), K(.75f, .10f, .08f, .10f, -20, 30, -25), K(1, .16f, .12f, .06f, -28, 48, -35) },
                      new[] { K(.3f, .18f, .14f, .04f, -32, 52, -38), Zero() }, V(-55, 15), V(55, -20)),
                    // 3) Corte horizontal largo na altura do peito, lâmina deitada
                    S(new[] { K(.5f, .14f, 0f, -.08f, -5, 55, -70), K(1, .18f, -.02f, -.10f, -5, 62, -80) },
                      new[] { K(.3f, .10f, 0f, .06f, 0, 30, -80), K(.7f, -.12f, 0f, .12f, 0, -35, -80), K(1, -.22f, -.02f, .08f, 0, -65, -80) },
                      new[] { K(.3f, -.24f, -.03f, .06f, 0, -70, -78), Zero() }, V(65, 0), V(-65, 0)),
                },
                // Pesado: corte vertical que racha o elmo
                Heavy = S(new[] { K(.5f, .04f, .16f, -.06f, -55, 8, -8), K(1, .05f, .20f, -.10f, -65, 10, -10) },
                          new[] { K(.4f, .02f, .06f, .06f, -10, 4, -4), K(.8f, -.03f, -.12f, .14f, 40, -6, 4), K(1, -.03f, -.16f, .12f, 48, -8, 6) },
                          new[] { K(.3f, -.03f, -.18f, .10f, 52, -8, 6), Zero() }, V(0, -40), V(0, 35)),
            },

            // ---------- Anaíra: Khopesh (gancho, revés, gancho descendente; pesado: gancho giratório) ----------
            [AttackSet.Khopesh] = new Set
            {
                Light = new[]
                {
                    // 1) Golpe em gancho da direita para a esquerda, puxando a curva de volta
                    S(new[] { K(1, .12f, .10f, -.04f, -25, 40, -20) },
                      new[] { K(.4f, .04f, .04f, .08f, -5, 10, -30), K(.8f, -.12f, -.02f, .06f, 10, -40, -50), K(1, -.16f, -.04f, 0f, 12, -50, -55) },
                      new[] { K(.35f, -.12f, -.03f, -.06f, 5, -45, -40), Zero() }, V(50, -5), V(-50, 10)),
                    // 2) Revés ascendente da esquerda para a direita
                    S(new[] { K(1, -.12f, -.06f, 0f, 15, -40, 35) },
                      new[] { K(.5f, 0f, 0f, .10f, 0, 0, 20), K(1, .14f, .10f, .04f, -25, 45, -15) },
                      new[] { K(.3f, .16f, .12f, 0f, -28, 50, -18), Zero() }, V(-50, 10), V(50, -15)),
                    // 3) Gancho de cima para baixo, puxando o alvo
                    S(new[] { K(1, .06f, .16f, -.06f, -50, 15, -20) },
                      new[] { K(.5f, .02f, .04f, .10f, -5, 5, -10), K(1, -.04f, -.10f, .04f, 35, -15, 10) },
                      new[] { K(.4f, -.02f, -.12f, -.06f, 30, -10, 5), Zero() }, V(10, -35), V(-5, 30)),
                },
                Heavy = S(new[] { K(.5f, .16f, .06f, -.08f, -20, 60, -30), K(1, .20f, .08f, -.12f, -25, 70, -35) },
                          new[] { K(.3f, .10f, .04f, .06f, -10, 30, -30), K(.7f, -.14f, -.02f, .10f, 5, -40, -45), K(1, -.24f, -.06f, .04f, 15, -75, -55) },
                          new[] { K(.3f, -.26f, -.08f, 0f, 18, -80, -58), Zero() }, V(70, -5), V(-70, 10)),
            },

            // ---------- Layla: Alabarda (estocada, varrida com a lâmina, estocada com giro; pesado: machadada) ----------
            [AttackSet.Halberd] = new Set
            {
                Light = new[]
                {
                    S(new[] { K(1, .02f, -.02f, -.14f, 4, 2, 0) },
                      new[] { K(.5f, -.02f, .01f, .14f, -2, -2, 0), K(1, -.04f, .02f, .26f, -4, -4, 0) },
                      new[] { K(.4f, -.04f, .02f, .20f, -3, -3, 0), Zero() }, V(0, 0), V(0, 0)),
                    S(new[] { K(1, .16f, .04f, -.06f, -5, 45, -20) },
                      new[] { K(.5f, .02f, .02f, .08f, 0, 0, -15), K(1, -.18f, 0f, .06f, 5, -50, -10) },
                      new[] { K(.3f, -.20f, 0f, .04f, 5, -55, -8), Zero() }, V(55, 0), V(-55, 5)),
                    S(new[] { K(1, 0f, 0f, -.16f, 6, 0, 0) },
                      new[] { K(.6f, -.03f, .02f, .24f, -4, -3, 0), K(1, -.03f, .02f, .26f, -4, -3, 60) },
                      new[] { K(.4f, -.03f, .02f, .18f, -3, -2, 40), Zero() }, V(0, 0), V(0, 0)),
                },
                Heavy = S(new[] { K(.5f, .02f, .14f, -.08f, -45, 5, 0), K(1, .03f, .18f, -.12f, -55, 6, 0) },
                          new[] { K(.6f, 0f, .02f, .10f, 0, 0, 0), K(1, -.02f, -.12f, .16f, 35, -4, 0) },
                          new[] { K(.3f, -.02f, -.14f, .14f, 38, -4, 0), Zero() }, V(0, -35), V(0, 30)),
            },

            // ---------- Anne: Arpão (estocadas rápidas e investida; pesado: arremetida com giro) ----------
            [AttackSet.Harpoon] = new Set
            {
                Light = new[]
                {
                    S(new[] { K(1, .02f, -.01f, -.10f, 4, 2, 0) },
                      new[] { K(1, -.02f, .01f, .20f, -3, -3, 0) },
                      new[] { K(.3f, -.02f, .01f, .16f, -2, -2, 0), Zero() }, V(0, 0), V(0, 0)),
                    S(new[] { K(1, .03f, -.04f, -.10f, 8, 4, 0) },
                      new[] { K(1, -.03f, -.03f, .20f, 4, -5, 0) },
                      new[] { K(.3f, -.03f, -.03f, .16f, 3, -4, 0), Zero() }, V(-4, 6), V(-4, 6)),
                    S(new[] { K(.6f, .03f, 0f, -.16f, 6, 4, 0), K(1, .04f, 0f, -.18f, 8, 4, 0) },
                      new[] { K(.5f, -.01f, .02f, .16f, -3, -2, 0), K(1, -.04f, .03f, .30f, -5, -5, 0) },
                      new[] { K(.4f, -.04f, .03f, .24f, -4, -4, 0), Zero() }, V(0, 0), V(0, 0)),
                },
                Heavy = S(new[] { K(.6f, .04f, .02f, -.18f, 6, 6, -20), K(1, .05f, .02f, -.20f, 8, 6, -25) },
                          new[] { K(.5f, -.01f, .02f, .18f, -3, -2, 10), K(1, -.04f, .03f, .32f, -5, -5, 70) },
                          new[] { K(.4f, -.04f, .03f, .26f, -4, -4, 50), Zero() }, V(0, 0), V(0, 0)),
            },

            // ---------- Anne: Escopeta de dois canos (coice forte, cano sobe e volta) ----------
            [AttackSet.Shotgun] = new Set
            {
                Light = new[]
                {
                    S(new[] { K(1, 0f, .01f, -.02f, -2, 0, 0) },
                      new[] { K(.2f, 0f, .08f, -.16f, -24, 4, 4), K(1, 0f, .05f, -.12f, -14, 2, 2) },
                      new[] { K(.5f, 0f, .01f, -.04f, -4, 0, 0), Zero() }, V(0, 0), V(0, 0)),
                },
                Heavy = S(new[] { K(1, -.01f, .02f, -.03f, -3, 1, 0) },
                          new[] { K(.2f, 0f, .09f, -.18f, -26, 4, 4), K(1, 0f, .05f, -.12f, -14, 2, 2) },
                          new[] { K(.5f, 0f, .01f, -.04f, -4, 0, 0), Zero() }, V(0, 0), V(0, 0)),
            },

            // ---------- Evelyn: Carabina de alavanca (coice e alavanca acionada na recuperação) ----------
            [AttackSet.LeverCarbine] = new Set
            {
                Light = new[]
                {
                    S(new[] { K(1, -.02f, .02f, -.02f, -2, 1, 0) },
                      new[] { K(.25f, -.02f, .06f, -.10f, -16, 3, 0), K(1, -.02f, .04f, -.08f, -10, 2, 0) },
                      new[] { K(.25f, -.02f, -.02f, -.06f, 10, 4, -25), K(.5f, -.02f, -.03f, -.05f, 14, 4, -28), K(.75f, -.02f, 0f, -.04f, 0, 2, -8), Zero() },
                      V(0, 0), V(0, 0)),
                },
                // Tiro mirado: encosta mais no rosto antes de disparar
                Heavy = S(new[] { K(.6f, -.05f, .04f, -.05f, -3, 3, 0), K(1, -.06f, .05f, -.06f, -3, 3, 0) },
                          new[] { K(.25f, -.06f, .09f, -.14f, -18, 4, 0), K(1, -.05f, .06f, -.10f, -10, 3, 0) },
                          new[] { K(.25f, -.03f, -.02f, -.06f, 10, 4, -25), K(.5f, -.02f, -.03f, -.05f, 14, 4, -28), K(.75f, -.02f, 0f, -.04f, 0, 2, -8), Zero() },
                          V(0, 0), V(0, 0)),
            },

            // ---------- Evelyn: Faca de campo (estocada, corte de revés, golpe de cima com a faca invertida) ----------
            [AttackSet.FieldKnife] = new Set
            {
                Light = new[]
                {
                    S(new[] { K(1, .03f, -.02f, -.10f, 6, 4, 0) },
                      new[] { K(1, -.03f, .02f, .22f, -5, -5, 0) },
                      new[] { K(.3f, -.03f, .02f, .18f, -4, -4, 0), Zero() }, V(0, 0), V(0, 0)),
                    S(new[] { K(1, -.12f, .0f, -.02f, -5, -40, 30) },
                      new[] { K(.5f, 0f, .02f, .08f, 0, 0, 0), K(1, .14f, .02f, .04f, 5, 45, -20) },
                      new[] { K(.3f, .16f, .02f, .02f, 6, 48, -22), Zero() }, V(-45, 0), V(45, 5)),
                    S(new[] { K(1, .04f, .14f, -.04f, -45, 6, 0) },
                      new[] { K(.6f, 0f, .02f, .10f, 0, 0, 0), K(1, -.02f, -.08f, .14f, 40, -4, 0) },
                      new[] { K(.3f, -.02f, -.10f, .12f, 42, -4, 0), Zero() }, V(0, -30), V(0, 25)),
                },
                Heavy = S(new[] { K(1, .04f, 0f, -.16f, 10, 6, 0) },
                          new[] { K(.5f, 0f, .02f, .16f, -5, -3, 0), K(1, -.04f, .02f, .28f, -8, -6, 0) },
                          new[] { K(.4f, -.04f, .02f, .22f, -6, -5, 0), Zero() }, V(0, 0), V(0, 0)),
            },

            // ---------- Elena: Montante (diagonal pesada, subida, horizontal larga; pesado: golpe de cima que crava) ----------
            [AttackSet.Greatsword] = new Set
            {
                Light = new[]
                {
                    S(new[] { K(.6f, .12f, .16f, -.06f, -45, 35, -30), K(1, .14f, .18f, -.08f, -50, 38, -32) },
                      new[] { K(.4f, .03f, .06f, .08f, -12, 8, -12), K(.8f, -.15f, -.10f, .10f, 25, -40, 25), K(1, -.20f, -.14f, .06f, 32, -52, 32) },
                      new[] { K(.4f, -.22f, -.16f, .04f, 35, -55, 34), Zero() }, V(55, -30), V(-50, 20)),
                    S(new[] { K(1, -.14f, -.10f, 0f, 20, -45, 35) },
                      new[] { K(.5f, -.02f, -.02f, .10f, 0, -5, 10), K(1, .16f, .14f, .06f, -30, 45, -30) },
                      new[] { K(.3f, .18f, .16f, .04f, -34, 48, -33), Zero() }, V(-50, 20), V(50, -25)),
                    S(new[] { K(1, .18f, 0f, -.10f, -5, 65, -75) },
                      new[] { K(.5f, 0f, 0f, .10f, 0, 0, -80), K(1, -.24f, -.02f, .06f, 0, -70, -80) },
                      new[] { K(.3f, -.26f, -.03f, .04f, 0, -74, -78), Zero() }, V(65, 0), V(-65, 0)),
                },
                Heavy = S(new[] { K(.5f, .04f, .18f, -.08f, -60, 8, -8), K(1, .05f, .22f, -.12f, -70, 10, -10) },
                          new[] { K(.4f, .02f, .06f, .06f, -10, 4, -4), K(.8f, -.03f, -.16f, .14f, 48, -6, 4), K(1, -.03f, -.20f, .12f, 55, -8, 6) },
                          new[] { K(.5f, -.03f, -.20f, .12f, 55, -8, 6), Zero() }, V(0, -40), V(0, 38)),
            },

            // ---------- Emily: Presa da Serpente (botes rápidos e corte na garganta; pesado: bote duplo) ----------
            [AttackSet.SerpentFang] = new Set
            {
                Light = new[]
                {
                    S(new[] { K(1, .03f, -.02f, -.12f, 6, 4, 0) },
                      new[] { K(.6f, -.02f, .02f, .20f, -4, -4, 0), K(1, -.03f, .03f, .24f, -6, -6, 0) },
                      new[] { K(.3f, -.03f, .03f, .20f, -5, -5, 0), Zero() }, V(0, 0), V(0, 0)),
                    S(new[] { K(1, 0f, -.05f, -.10f, 10, -6, 10) },
                      new[] { K(1, -.10f, -.04f, .24f, 4, -14, 10) },
                      new[] { K(.3f, -.10f, -.04f, .20f, 4, -12, 10), Zero() }, V(-8, 5), V(-8, 5)),
                    S(new[] { K(1, .14f, .04f, -.02f, -15, 45, -60) },
                      new[] { K(.5f, 0f, .03f, .10f, -5, 0, -70), K(1, -.18f, 0f, .06f, 0, -50, -70) },
                      new[] { K(.3f, -.20f, 0f, .04f, 0, -55, -68), Zero() }, V(50, -5), V(-50, -5)),
                },
                Heavy = S(new[] { K(1, .04f, 0f, -.16f, 10, 6, 0) },
                          new[] { K(.4f, -.02f, .02f, .22f, -6, -4, 0), K(.55f, 0f, 0f, .10f, 0, 0, 0), K(1, -.04f, .02f, .28f, -8, -6, 0) },
                          new[] { K(.3f, -.04f, .02f, .24f, -7, -5, 0), Zero() }, V(0, 0), V(0, 0)),
            },

            // ---------- Yuna: Kanabō (balanço horizontal, revés, esmagada; pesado: esmagada carregada) ----------
            [AttackSet.Kanabo] = new Set
            {
                Light = new[]
                {
                    S(new[] { K(.6f, .16f, .08f, -.08f, -20, 55, -25), K(1, .18f, .10f, -.10f, -22, 60, -28) },
                      new[] { K(.4f, .06f, .04f, .08f, -8, 20, -25), K(.8f, -.14f, -.02f, .10f, 8, -45, -20), K(1, -.20f, -.04f, .06f, 12, -60, -18) },
                      new[] { K(.4f, -.22f, -.06f, .04f, 15, -64, -17), Zero() }, V(60, -5), V(-60, 5)),
                    S(new[] { K(1, -.14f, .04f, -.04f, -15, -45, 25) },
                      new[] { K(.5f, 0f, .02f, .10f, 0, 0, 10), K(1, .16f, -.02f, .06f, 10, 50, -15) },
                      new[] { K(.3f, .18f, -.03f, .04f, 12, 54, -16), Zero() }, V(-55, 0), V(55, 5)),
                    S(new[] { K(1, .04f, .20f, -.10f, -62, 8, -8) },
                      new[] { K(.5f, .02f, .05f, .08f, -10, 4, -4), K(1, -.03f, -.16f, .14f, 50, -6, 6) },
                      new[] { K(.5f, -.03f, -.17f, .12f, 50, -6, 6), Zero() }, V(0, -40), V(0, 35)),
                },
                Heavy = S(new[] { K(.5f, .05f, .20f, -.10f, -65, 10, -10), K(1, .06f, .24f, -.14f, -75, 12, -12) },
                          new[] { K(.4f, .02f, .06f, .08f, -10, 4, -4), K(.85f, -.03f, -.18f, .16f, 55, -6, 6), K(1, -.03f, -.20f, .14f, 58, -6, 6) },
                          new[] { K(.5f, -.03f, -.21f, .13f, 58, -6, 6), Zero() }, V(0, -45), V(0, 38)),
            },
        };
    }
}
