using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Athoria.Editor
{
    /// <summary>
    /// Texturas procedurais das Terras Alagadas (ruído de Perlin/valor periódico e células de
    /// Worley), todas repetíveis sem emenda, com mapa de normais tirado da altura. Saem como PNG
    /// em Art/Mundo/Texturas/TerrasAlagadas e só são refeitas quando a versão muda.
    /// Resolução baixa (PS1) e filtro de ponto: o detalhe vem do desenho e do relevo.
    /// </summary>
    public static class ProceduralTextures
    {
        public const string Folder = "Assets/_Athoria/Art/Mundo/Texturas/TerrasAlagadas";
        public const string MaterialFolder = "Assets/_Athoria/Materials/TerrasAlagadas";
        private const int Version = 1;

        // ------------------------------------------------------------------ ruído periódico

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        private static int Wrap(int v, int p) => ((v % p) + p) % p;

        /// <summary>Ruído de valor suave e periódico (u,v em 0..1).</summary>
        private static float Value(float u, float v, int period, int seed)
        {
            var x = u * period; var y = v * period;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            int xa = Wrap(x0, period), xb = Wrap(x0 + 1, period), ya = Wrap(y0, period), yb = Wrap(y0 + 1, period);
            var a = Mathf.Lerp(Hash(xa, ya, seed), Hash(xb, ya, seed), fx);
            var b = Mathf.Lerp(Hash(xa, yb, seed), Hash(xb, yb, seed), fx);
            return Mathf.Lerp(a, b, fy);
        }

        private static float Fbm(float u, float v, int period, int octaves, int seed)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (var i = 0; i < octaves; i++)
            {
                sum += Value(u, v, period, seed + i * 101) * amp;
                norm += amp; amp *= 0.5f; period *= 2;
            }
            return sum / norm;
        }

        /// <summary>Células de Worley periódicas: F1, F2 (distâncias em células) e o id da célula mais próxima.</summary>
        private static void Worley(float u, float v, int cells, int seed, out float f1, out float f2, out float id)
        {
            var x = u * cells; var y = v * cells;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            f1 = f2 = 9f; id = 0f;
            for (var j = -1; j <= 1; j++)
            for (var i = -1; i <= 1; i++)
            {
                int gx = cx + i, gy = cy + j;
                int wx = Wrap(gx, cells), wy = Wrap(gy, cells);
                var px = gx + 0.15f + 0.7f * Hash(wx, wy, seed);
                var py = gy + 0.15f + 0.7f * Hash(wx, wy, seed + 7);
                var d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                if (d < f1) { f2 = f1; f1 = d; id = Hash(wx, wy, seed + 13); }
                else if (d < f2) f2 = d;
            }
        }

        private static Color C(float r, float g, float b, float a = 1f) => new(r, g, b, a);
        private static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, Mathf.Clamp01(t));
        private static float S(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        // ------------------------------------------------------------------ geração

        private delegate void Painter(float u, float v, out Color color, out float height);

        private sealed class Spec
        {
            public string Name;
            public int W, H;
            public Painter Paint;
            public float NormalStrength = 2f;
            public bool Alpha;
            public bool NoNormal;
        }

        private static readonly Dictionary<string, Spec> Specs = new();

        private static void Add(string name, int size, float normal, Painter p, bool alpha = false, bool noNormal = false, int height = 0)
            => Specs[name] = new Spec { Name = name, W = size, H = height > 0 ? height : size, Paint = p, NormalStrength = normal, Alpha = alpha, NoNormal = noNormal };

        static ProceduralTextures()
        {
            // ---------------- chão ----------------
            Add("LamaRachada", 256, 3f, (float u, float v, out Color c, out float h) =>
            {
                var n = Fbm(u, v, 4, 5, 11);
                Worley(u, v, 7, 3, out var f1, out var f2, out var id);
                var edge = f2 - f1;
                var crack = 1f - S(0f, 0.07f, edge + (Fbm(u, v, 16, 2, 5) - 0.5f) * 0.05f);
                c = Mix(C(0.31f, 0.24f, 0.16f), C(0.19f, 0.15f, 0.1f), n + id * 0.3f);
                c *= 1f - 0.75f * crack;
                h = 0.55f * n + 0.25f * (1f - f1 * 0.6f) - crack * 0.6f;
            });
            Add("LamaPantano", 256, 2f, (float u, float v, out Color c, out float h) =>
            {
                var n = Fbm(u, v, 4, 5, 21);
                var algae = S(0.5f, 0.75f, Fbm(u, v, 3, 3, 22));
                Worley(u, v, 18, 23, out var f1, out _, out var id);
                var pebble = id > 0.82f ? 1f - S(0.1f, 0.28f, f1) : 0f;
                c = Mix(C(0.16f, 0.14f, 0.09f), C(0.09f, 0.09f, 0.05f), n);
                c = Mix(c, C(0.13f, 0.17f, 0.07f), algae * 0.7f);
                c = Mix(c, C(0.3f, 0.28f, 0.24f), pebble);
                h = n * 0.6f + pebble * 0.5f;
            });
            Add("GramaPantano", 256, 1.6f, (float u, float v, out Color c, out float h) =>
            {
                var blades = Fbm(u * 1f, v * 0.25f, 32, 3, 31) * 0.6f + Fbm(u, v, 64, 2, 32) * 0.4f;
                var dry = S(0.55f, 0.75f, Fbm(u, v, 3, 3, 33));
                var mud = S(0.62f, 0.8f, Fbm(u, v, 4, 3, 34));
                c = Mix(C(0.13f, 0.16f, 0.07f), C(0.27f, 0.3f, 0.13f), blades);
                c = Mix(c, C(0.38f, 0.33f, 0.18f), dry * 0.8f);
                c = Mix(c, C(0.18f, 0.14f, 0.09f), mud * 0.7f);
                h = blades;
            });
            Add("Paralelepipedo", 256, 3.5f, (float u, float v, out Color c, out float h) =>
            {
                Worley(u, v, 9, 41, out var f1, out var f2, out var id);
                var edge = f2 - f1;
                var stone = S(0.04f, 0.16f, edge);
                var n = Fbm(u, v, 16, 3, 42);
                var grass = S(0.45f, 0.7f, Fbm(u, v, 5, 2, 43));
                var stoneColor = Mix(C(0.4f, 0.38f, 0.34f), C(0.25f, 0.24f, 0.22f), id * 0.8f + n * 0.4f);
                var grout = Mix(C(0.11f, 0.09f, 0.06f), C(0.17f, 0.21f, 0.08f), grass);
                c = Mix(grout, stoneColor, stone);
                c *= 0.85f + 0.15f * (1f - f1);   // pedra molhada escurece no centro gasto
                h = stone * (0.75f + 0.25f * (1f - f1 * 0.8f)) + n * 0.1f;
            });
            Add("TerraEstrada", 256, 2f, (float u, float v, out Color c, out float h) =>
            {
                var n = Fbm(u, v, 4, 5, 51);
                Worley(u, v, 22, 52, out var f1, out _, out var id);
                var pebble = id > 0.7f ? 1f - S(0.08f, 0.25f, f1) : 0f;
                var puddle = S(0.62f, 0.7f, Fbm(u, v, 3, 3, 53));
                c = Mix(C(0.32f, 0.26f, 0.18f), C(0.2f, 0.16f, 0.11f), n);
                c = Mix(c, C(0.42f, 0.4f, 0.35f), pebble * 0.8f);
                c = Mix(c, C(0.12f, 0.11f, 0.08f), puddle * 0.6f);
                h = n * 0.5f + pebble * 0.4f - puddle * 0.3f;
            });
            Add("Cal", 256, 1.5f, (float u, float v, out Color c, out float h) =>
            {
                var n = Fbm(u, v, 4, 5, 61);
                var dirt = S(0.55f, 0.72f, Fbm(u, v, 8, 3, 62));
                c = Mix(C(0.74f, 0.73f, 0.66f), C(0.55f, 0.54f, 0.48f), n);
                c = Mix(c, C(0.3f, 0.25f, 0.17f), dirt * 0.8f);
                h = n * 0.4f - dirt * 0.2f;
            });

            // ---------------- construção ----------------
            Add("Tijolo", 256, 3f, (float u, float v, out Color c, out float h) =>
            {
                Brick(u, v, 8, 4, out var mortar, out var id, out var bevel);
                var n = Fbm(u, v, 16, 3, 71);
                var soot = S(0.45f, 0.8f, Fbm(u, v, 3, 4, 72));
                var damp = S(0.55f, 0.8f, Fbm(u, v * 0.5f, 4, 3, 73));
                var brick = Mix(C(0.45f, 0.21f, 0.14f), C(0.3f, 0.15f, 0.11f), id + n * 0.3f);
                brick = Mix(brick, C(0.14f, 0.12f, 0.1f), soot * 0.6f);
                brick = Mix(brick, C(0.15f, 0.2f, 0.11f), damp * 0.5f);
                c = Mix(C(0.42f, 0.4f, 0.36f) * (0.7f + n * 0.3f), brick, mortar);
                h = mortar * (0.7f + bevel * 0.3f) + n * 0.15f;
            });
            Add("Reboco", 256, 2.5f, (float u, float v, out Color c, out float h) =>
            {
                var n = Fbm(u, v, 8, 4, 81);
                var streak = Fbm(u * 1f, v * 0.15f, 24, 3, 82);
                var stain = S(0.5f, 0.85f, Fbm(u, v, 3, 4, 83));
                var fallen = S(0.66f, 0.7f, Fbm(u, v, 4, 3, 84));        // reboco caído mostra o tijolo
                Worley(u, v, 6, 85, out var f1, out var f2, out _);
                var crack = (1f - S(0f, 0.025f, f2 - f1)) * S(0.45f, 0.6f, Fbm(u, v, 6, 2, 86));
                var plaster = Mix(C(0.66f, 0.62f, 0.52f), C(0.5f, 0.47f, 0.4f), n);
                plaster = Mix(plaster, C(0.3f, 0.29f, 0.22f), streak * 0.5f + stain * 0.4f);
                plaster *= 1f - crack * 0.7f;
                Brick(u, v, 8, 4, out var mortar, out var id, out _);
                var brick = Mix(C(0.4f, 0.2f, 0.14f), C(0.28f, 0.14f, 0.1f), id) * Mathf.Lerp(0.6f, 1f, mortar);
                c = Mix(plaster, brick, fallen);
                h = Mathf.Lerp(0.7f + n * 0.2f - crack * 0.4f, 0.25f + mortar * 0.2f, fallen);
            });
            Add("MadeiraPodre", 256, 2.5f, (float u, float v, out Color c, out float h) =>
            {
                const int planks = 4;
                var px = u * planks;
                var plank = Mathf.FloorToInt(px);
                var fx = px - plank;
                var gap = 1f - S(0f, 0.05f, Mathf.Min(fx, 1f - fx));
                var shift = Hash(plank, 0, 91);
                var grain = Fbm(u * 1f, (v + shift) * 0.1f, 64, 3, 92 + plank);
                var rot = S(0.6f, 0.8f, Fbm(u, v, 4, 4, 93));
                var grey = Hash(plank, 1, 94);
                c = Mix(C(0.33f, 0.26f, 0.18f), C(0.18f, 0.15f, 0.11f), grain * 0.8f + grey * 0.3f);
                c = Mix(c, C(0.32f, 0.31f, 0.28f), grey * 0.35f);                // tábuas desbotadas
                c = Mix(c, C(0.08f, 0.1f, 0.05f), rot * 0.75f);
                c *= 1f - gap * 0.85f;
                h = (1f - gap) * (0.6f + grain * 0.4f) - rot * 0.2f;
            });
            Add("Palha", 256, 2.5f, (float u, float v, out Color c, out float h) =>
            {
                var straw = Fbm(u * 1f, v * 0.12f, 64, 3, 101);
                var band = (v * 6f) % 1f;
                var rows = S(0f, 0.25f, band);
                var rotten = S(0.55f, 0.8f, Fbm(u, v, 4, 3, 102));
                c = Mix(C(0.42f, 0.35f, 0.2f), C(0.2f, 0.16f, 0.09f), straw);
                c = Mix(c, C(0.12f, 0.13f, 0.07f), rotten * 0.6f);
                c *= 0.55f + 0.45f * rows;
                h = rows * 0.6f + straw * 0.4f;
            });
            Add("Telha", 256, 3f, (float u, float v, out Color c, out float h) =>
            {
                const int rows = 8, cols = 8;
                var y = v * rows;
                var row = Mathf.FloorToInt(y);
                var fy = y - row;
                var x = u * cols + (row % 2) * 0.5f;
                var col = Mathf.FloorToInt(x);
                var fx = x - col;
                var round = Mathf.Abs(fx - 0.5f) * 2f;
                var bottom = fy - 0.2f * (1f - round * round);   // borda de baixo arredondada (escama)
                var edge = S(0f, 0.08f, bottom) * S(0f, 0.06f, Mathf.Min(fx, 1f - fx));
                var id = Hash(Wrap(col, cols), row, 111);
                var moss = S(0.58f, 0.8f, Fbm(u, v, 4, 4, 112));
                c = Mix(C(0.24f, 0.25f, 0.28f), C(0.13f, 0.14f, 0.16f), id * 0.8f + fy * 0.3f);
                c = Mix(c, C(0.17f, 0.22f, 0.1f), moss * 0.7f);
                c *= 0.35f + 0.65f * edge;
                h = edge * (0.3f + fy * 0.7f);
            });
            Add("Ferro", 128, 1.5f, (float u, float v, out Color c, out float h) =>
            {
                var rust = S(0.45f, 0.75f, Fbm(u, v, 4, 5, 121));
                var n = Fbm(u, v, 16, 3, 122);
                c = Mix(C(0.1f, 0.1f, 0.11f), C(0.34f, 0.17f, 0.08f), rust);
                c *= 0.8f + n * 0.3f;
                h = rust * 0.4f + n * 0.3f;
            });
            Add("PedraAntiga", 256, 3f, (float u, float v, out Color c, out float h) =>
            {
                const int rows = 4;
                var y = v * rows;
                var row = Mathf.FloorToInt(y);
                var fy = y - row;
                var cols = 2 + (int)(Hash(row, 0, 131) * 2f);
                var x = u * cols + Hash(row, 1, 131);
                var col = Mathf.FloorToInt(x);
                var fx = x - col;
                var joint = S(0f, 0.06f, Mathf.Min(Mathf.Min(fx, 1f - fx) * 2f / cols * rows, Mathf.Min(fy, 1f - fy)));
                var id = Hash(Wrap(col, cols), row, 132);
                var n = Fbm(u, v, 8, 4, 133);
                var moss = S(0.5f, 0.75f, Fbm(u, v, 4, 4, 134) + (1f - fy) * 0.15f);
                c = Mix(C(0.38f, 0.38f, 0.34f), C(0.22f, 0.22f, 0.2f), id * 0.7f + n * 0.4f);
                c = Mix(c, C(0.16f, 0.22f, 0.09f), moss * 0.8f);
                c *= 0.3f + 0.7f * joint;
                h = joint * (0.6f + n * 0.4f);
            });
            Add("Casca", 256, 3f, (float u, float v, out Color c, out float h) =>
            {
                var fibers = Fbm(u * 1f, v * 0.08f, 48, 3, 141);
                var ridge = 1f - Mathf.Abs(fibers - 0.5f) * 2f;
                var moss = S(0.6f, 0.8f, Fbm(u, v, 4, 3, 142));
                c = Mix(C(0.2f, 0.16f, 0.12f), C(0.09f, 0.08f, 0.06f), 1f - ridge);
                c = Mix(c, C(0.15f, 0.19f, 0.08f), moss * 0.6f);
                h = ridge;
            });
            Add("Juta", 128, 1.5f, (float u, float v, out Color c, out float h) =>
            {
                var weaveU = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 32f));
                var weaveV = Mathf.Abs(Mathf.Sin(v * Mathf.PI * 32f));
                var weave = ((int)(u * 32f) + (int)(v * 32f)) % 2 == 0 ? weaveU : weaveV;
                var stain = S(0.5f, 0.8f, Fbm(u, v, 4, 4, 151));
                c = Mix(C(0.45f, 0.38f, 0.26f), C(0.3f, 0.24f, 0.15f), 1f - weave);
                c = Mix(c, C(0.2f, 0.12f, 0.08f), stain * 0.75f);
                h = weave;
            });
            Add("Pano", 128, 1f, (float u, float v, out Color c, out float h) =>
            {
                var n = Fbm(u, v, 8, 3, 161);
                c = Mix(C(0.3f, 0.27f, 0.22f), C(0.17f, 0.15f, 0.12f), n);
                h = n;
            });

            // ---------------- água ----------------
            Add("AguaTurva", 256, 0f, (float u, float v, out Color c, out float h) =>
            {
                var n = Fbm(u, v, 4, 5, 171);
                var silt = S(0.5f, 0.8f, Fbm(u, v, 8, 3, 172));
                var g = 0.75f + n * 0.4f + silt * 0.15f;
                c = C(g, g, g * 0.9f);
                h = 0f;
            }, noNormal: true);
            Add("AguaOndas", 256, 4f, (float u, float v, out Color c, out float h) =>
            {
                Worley(u, v, 6, 181, out var f1, out _, out _);
                h = Fbm(u, v, 8, 4, 182) * 0.7f + Mathf.Sin(f1 * 14f) * 0.08f;
                c = Color.gray;
            });

            // ---------------- janelas (recorte em arco) ----------------
            Add("JanelaArco", 64, 1.5f, (float u, float v, out Color c, out float h) => Window(u, v, false, out c, out h), alpha: true, height: 128);
            Add("JanelaAcesa", 64, 1.5f, (float u, float v, out Color c, out float h) => Window(u, v, true, out c, out h), alpha: true, height: 128);

            // ---------------- folhagem (recorte) ----------------
            Add("FolhaGrama", 128, 0f, (float u, float v, out Color c, out float h) => Blades(u, v, 16, 0.85f, 0.05f, 201,
                C(0.1f, 0.13f, 0.05f), C(0.38f, 0.42f, 0.2f), out c, out h), alpha: true, noNormal: true);
            Add("FolhaErvaSeca", 128, 0f, (float u, float v, out Color c, out float h) => Blades(u, v, 11, 0.95f, 0.02f, 211,
                C(0.2f, 0.16f, 0.08f), C(0.6f, 0.52f, 0.3f), out c, out h), alpha: true, noNormal: true);
            Add("FolhaJunco", 64, 0f, (float u, float v, out Color c, out float h) => Reeds(u, v, out c, out h), alpha: true, noNormal: true, height: 256);
        }

        /// <summary>Tijolos em fiadas alternadas: 1 = tijolo, 0 = argamassa.</summary>
        private static void Brick(float u, float v, int rows, int cols, out float brick, out float id, out float bevel)
        {
            var y = v * rows;
            var row = Mathf.FloorToInt(y);
            var fy = y - row;
            var x = u * cols + (row % 2) * 0.5f;
            var col = Mathf.FloorToInt(x);
            var fx = x - col;
            var ex = Mathf.Min(fx, 1f - fx) / rows * cols;   // em unidades de altura de fiada
            var ey = Mathf.Min(fy, 1f - fy);
            var e = Mathf.Min(ex, ey);
            brick = S(0.04f, 0.07f, e);
            bevel = S(0.07f, 0.2f, e);
            id = Hash(Wrap(col, cols), row, 77);
        }

        private static void Window(float u, float v, bool lit, out Color c, out float h)
        {
            // 64×128: moldura, vidro com caixilho em cruz, topo em arco. Fora do arco = transparente.
            var x = (u - 0.5f) * 2f;          // -1..1
            var y = v * 2f;                   // 0..2
            var archCenter = 1.5f;
            var inside = y < archCenter ? Mathf.Abs(x) < 1f : x * x + (y - archCenter) * (y - archCenter) * 4f < 1f;
            if (!inside) { c = new Color(0, 0, 0, 0); h = 0f; return; }
            var border = y < archCenter ? Mathf.Min(1f - Mathf.Abs(x), y) : 1f - Mathf.Sqrt(x * x + (y - archCenter) * (y - archCenter) * 4f);
            var frame = border < 0.16f;
            var mullion = Mathf.Abs(x) < 0.06f || Mathf.Abs(y - 0.95f) < 0.05f;
            var grime = Fbm(u, v, 4, 3, lit ? 301 : 302);
            if (frame || mullion)
            {
                c = Mix(C(0.16f, 0.12f, 0.08f), C(0.08f, 0.06f, 0.04f), grime);
                h = 1f;
            }
            else
            {
                c = lit ? Mix(C(0.95f, 0.7f, 0.3f), C(0.55f, 0.35f, 0.12f), grime + (y / 2f) * 0.3f)
                        : Mix(C(0.1f, 0.12f, 0.13f), C(0.2f, 0.22f, 0.2f), grime * 0.8f + (1f - y / 2f) * 0.3f);
                h = 0.2f;
            }
            c.a = 1f;
        }

        private static void Blades(float u, float v, int count, float maxHeight, float curve, int seed, Color baseColor, Color tipColor, out Color c, out float h)
        {
            c = new Color(0, 0, 0, 0); h = 0f;
            for (var i = 0; i < count; i++)
            {
                var x0 = 0.08f + 0.84f * Hash(i, 0, seed);
                var height = maxHeight * (0.45f + 0.55f * Hash(i, 1, seed));
                if (v > height) continue;
                var t = v / height;
                var bendDir = Hash(i, 2, seed) > 0.5f ? 1f : -1f;
                var cx = x0 + bendDir * (curve + 0.12f * Hash(i, 3, seed)) * t * t;
                var halfWidth = (0.018f + 0.02f * Hash(i, 4, seed)) * (1f - t);
                if (Mathf.Abs(u - cx) > halfWidth) continue;
                var shade = 0.8f + 0.2f * Hash(i, 5, seed);
                c = Mix(baseColor, tipColor, t) * shade;
                c.a = 1f; h = t;
                return;
            }
        }

        private static void Reeds(float u, float v, out Color c, out float h)
        {
            c = new Color(0, 0, 0, 0); h = 0f;
            for (var i = 0; i < 6; i++)
            {
                var x0 = 0.1f + 0.8f * Hash(i, 0, 401);
                var height = 0.6f + 0.38f * Hash(i, 1, 401);
                var cx = x0 + (Hash(i, 2, 401) - 0.5f) * 0.15f * v * v;
                var head = Hash(i, 3, 401) > 0.45f;
                // espiga de taboa (marrom) no alto de alguns juncos
                if (head && v > height - 0.16f && v < height - 0.02f && Mathf.Abs(u - cx) < 0.05f)
                {
                    c = Mix(C(0.3f, 0.18f, 0.09f), C(0.18f, 0.1f, 0.05f), Fbm(u, v, 16, 2, 402)); c.a = 1f; h = 1f; return;
                }
                if (v > height) continue;
                var t = v / height;
                var half = 0.022f * (1f - t * 0.7f);
                if (Mathf.Abs(u - cx) > half) continue;
                c = Mix(C(0.12f, 0.14f, 0.06f), C(0.48f, 0.46f, 0.24f), t);
                c.a = 1f; h = t;
                return;
            }
        }

        // ------------------------------------------------------------------ arquivos

        public static void EnsureAll(bool force = false)
        {
            Directory.CreateDirectory(Folder);
            var marker = Path.Combine(Folder, "versao.txt");
            var current = File.Exists(marker) ? File.ReadAllText(marker).Trim() : "";
            var stale = force || current != Version.ToString();
            var created = new List<(string path, bool normal, bool alpha)>();
            try
            {
                var index = 0;
                foreach (var spec in Specs.Values)
                {
                    EditorUtility.DisplayProgressBar("Texturas das Terras Alagadas", spec.Name, index++ / (float)Specs.Count);
                    var albedoPath = $"{Folder}/{spec.Name}.png";
                    var normalPath = $"{Folder}/{spec.Name}_Normal.png";
                    if (!stale && File.Exists(albedoPath) && (spec.NoNormal || File.Exists(normalPath))) continue;
                    Generate(spec, albedoPath, normalPath);
                    created.Add((albedoPath, false, spec.Alpha));
                    if (!spec.NoNormal) created.Add((normalPath, true, false));
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            if (created.Count == 0) return;
            File.WriteAllText(marker, Version.ToString());
            AssetDatabase.Refresh();
            foreach (var (path, normal, alpha) in created)
            {
                if (AssetImporter.GetAtPath(path) is not TextureImporter imp) continue;
                imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                imp.sRGBTexture = !normal;
                imp.filterMode = FilterMode.Point;
                imp.wrapMode = alpha ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                imp.mipmapEnabled = true;
                imp.alphaIsTransparency = alpha;
                imp.mipMapsPreserveCoverage = alpha;
                imp.alphaTestReferenceValue = 0.5f;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }
        }

        private static void Generate(Spec spec, string albedoPath, string normalPath)
        {
            int w = spec.W, h = spec.H;
            var colors = new Color[w * h];
            var heights = new float[w * h];
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                spec.Paint((x + 0.5f) / w, (y + 0.5f) / h, out var c, out var hv);
                if (!spec.Alpha) c.a = 1f;
                colors[y * w + x] = c;
                heights[y * w + x] = hv;
            }
            Save(albedoPath, w, h, colors);
            if (spec.NoNormal) return;

            var normals = new Color[w * h];
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                float H(int dx, int dy) => heights[Wrap(y + dy, h) * w + Wrap(x + dx, w)];
                var sx = (H(1, -1) + 2f * H(1, 0) + H(1, 1)) - (H(-1, -1) + 2f * H(-1, 0) + H(-1, 1));
                var sy = (H(-1, 1) + 2f * H(0, 1) + H(1, 1)) - (H(-1, -1) + 2f * H(0, -1) + H(1, -1));
                var n = new Vector3(-sx * spec.NormalStrength, -sy * spec.NormalStrength, 1f).normalized;
                normals[y * w + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
            Save(normalPath, w, h, normals);
        }

        private static void Save(string path, int w, int h, Color[] pixels)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        public static Texture2D Albedo(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{name}.png");
        public static Texture2D Normal(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{name}_Normal.png");

        // ------------------------------------------------------------------ materiais

        private static readonly Dictionary<string, Material> Cache = new();

        private static Material Asset(string name, Shader shader)
        {
            if (Cache.TryGetValue(name, out var m) && m != null) return m;
            Directory.CreateDirectory(MaterialFolder);
            var path = $"{MaterialFolder}/{name}.mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != shader) m.shader = shader;
            Cache[name] = m;
            return m;
        }

        /// <summary>Material de superfície com relevo (PSX). A textura é repetida por metro pelas malhas do kit.</summary>
        public static Material Surface(string name, string texture, Color tint, float bump = 1f, float wetness = 0f, bool alphaClip = false, Color? emission = null)
        {
            var shader = Shader.Find("Athoria/PSX Lit Relevo") ?? Shader.Find("Universal Render Pipeline/Simple Lit");
            var m = Asset(name, shader);
            m.SetTexture("_BaseMap", Albedo(texture));
            m.SetColor("_BaseColor", tint);
            var normal = Normal(texture);
            if (normal != null) m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", bump);
            m.SetFloat("_Wetness", wetness);
            m.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
            if (alphaClip) m.EnableKeyword("_ALPHATEST_ON"); else m.DisableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", alphaClip ? (float)CullMode.Off : (float)CullMode.Back);
            m.SetColor("_EmissionColor", emission ?? Color.black);
            m.renderQueue = alphaClip ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Water(string name, Color color, float iridescence, float specular, float flow, Color reflect)
        {
            var m = Asset(name, Shader.Find("Athoria/Agua") ?? Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetTexture("_BaseMap", Albedo("AguaTurva"));
            m.SetTexture("_BumpMap", Normal("AguaOndas"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Iridescence", iridescence);
            m.SetFloat("_Specular", specular);
            m.SetFloat("_Flow", flow);
            m.SetColor("_ReflectColor", reflect);
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Foliage(string name, string texture, Color tint, Color dry, float wind)
        {
            var m = Asset(name, Shader.Find("Athoria/Folhagem Vento") ?? Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetTexture("_BaseMap", Albedo(texture));
            m.SetColor("_BaseColor", tint);
            m.SetColor("_DryColor", dry);
            m.SetFloat("_WindStrength", wind);
            m.enableInstancing = true;
            m.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
