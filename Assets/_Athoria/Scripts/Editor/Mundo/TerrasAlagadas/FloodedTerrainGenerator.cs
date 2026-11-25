using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Athoria.Editor
{
    /// <summary>
    /// Relevo e pintura do chão das Terras Alagadas (1000 × 1000 m, água em 12 m):
    /// terra alta a noroeste (a Capital), bacia alagada a leste (vila de palafitas), brejo
    /// profundo a sudoeste (arena do chefe), canais sinuosos ligando as águas, poças da peste
    /// em depressões, valas de corpos e estradas elevadas. Tudo determinístico.
    /// </summary>
    internal static class FloodedTerrainGenerator
    {
        public const float Half = 500f;
        public const float WaterLevel = 12f;
        public const float BaseY = 0f;
        public const float HeightRange = 40f;

        // ---------------- marcos ----------------
        public static readonly Vector2 Capital = new(-270f, 250f);
        public const float CityHalf = 110f;           // a cidade ocupa ±110 m em volta do centro
        public const float CityGround = 20f;
        public const float StreetSpacing = 44f;       // ruas em −110, −66, −22, 22, 66, 110
        public const float StreetHalfWidth = 4f;
        public static readonly Vector2 Village = new(230f, 60f);
        public const float VillageDeck = 13.35f;
        public static readonly Vector2 Swamp = new(-250f, -260f);
        public static readonly Vector2 Arena = new(-290f, -300f);
        public const float ArenaRadius = 34f;
        public const float ArenaGround = 11.7f;        // 30 cm de água: arena meio submersa
        public static readonly Vector2 Arrival = new(410f, -40f);

        public struct Pit { public Vector2 C; public Vector2 Size; public float Yaw; }
        public struct Disposal { public Vector2 C; public float R; public float H; public Pit[] Pits; }

        public static readonly Disposal[] Disposals =
        {
            new() { C = new Vector2(-425f, 190f), R = 26f, H = 17f, Pits = new[]
            {
                new Pit { C = new Vector2(-432f, 196f), Size = new Vector2(11f, 4f), Yaw = 15f },
                new Pit { C = new Vector2(-418f, 182f), Size = new Vector2(9f, 3.5f), Yaw = 15f },
                new Pit { C = new Vector2(-428f, 178f), Size = new Vector2(5f, 5f), Yaw = 0f },   // poço de cal
            } },
            new() { C = new Vector2(-135f, 92f), R = 22f, H = 14.5f, Pits = new[]
            {
                new Pit { C = new Vector2(-140f, 96f), Size = new Vector2(10f, 3.5f), Yaw = -30f },
                new Pit { C = new Vector2(-128f, 86f), Size = new Vector2(4.5f, 4.5f), Yaw = 10f },
            } },
            new() { C = new Vector2(-150f, -150f), R = 20f, H = 13.4f, Pits = new[]
            {
                new Pit { C = new Vector2(-154f, -146f), Size = new Vector2(9f, 3.5f), Yaw = 60f },
                new Pit { C = new Vector2(-144f, -156f), Size = new Vector2(4f, 4f), Yaw = 0f },
            } },
        };

        /// <summary>Poças da peste (x, z, raio).</summary>
        public static readonly Vector3[] PlagueZones =
        {
            new(60f, -120f, 30f),
            new(-110f, -320f, 24f),
            new(330f, -210f, 34f),
            new(-55f, 165f, 20f),
        };

        public static readonly Vector2[][] Roads =
        {
            // estrada principal: chegada a leste → vila → encruzilhada → portão da Capital
            new Vector2[] { new(410f, -40f), new(330f, -5f), new(250f, -30f), new(150f, 10f), new(110f, 60f), new(40f, 110f), new(-60f, 125f), new(-150f, 132f), new(-250f, 134f), new(-292f, 136f) },
            // encruzilhada → borda do brejo (valas) → trilha da arena
            new Vector2[] { new(40f, 110f), new(15f, 10f), new(-60f, -80f), new(-140f, -140f), new(-205f, -220f), new(-262f, -272f) },
            // saída oeste da Capital → valas da Capital
            new Vector2[] { new(-380f, 228f), new(-405f, 205f), new(-422f, 192f) },
            // estrada principal → valas do leste da Capital
            new Vector2[] { new(-120f, 130f), new(-132f, 100f) },
            // margem → píer da vila
            new Vector2[] { new(150f, 10f), new(160f, 40f) },
        };

        /// <summary>Canais (polilinhas) por onde a água corre: fundos, 1,5 m de água.</summary>
        public static readonly Vector2[][] Channels =
        {
            new Vector2[] { new(-250f, -260f), new(-170f, -215f), new(-60f, -185f), new(60f, -120f), new(150f, -60f), new(230f, 60f) },
            new Vector2[] { new(230f, 60f), new(300f, 160f), new(360f, 260f), new(420f, 330f) },
            new Vector2[] { new(-250f, -260f), new(-330f, -140f), new(-360f, -20f), new(-330f, 70f) },
        };

        // ---------------- ruído ----------------

        public static float Fbm(float x, float z, int octaves, float freq, float seed)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (var i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x * freq + seed, z * freq + seed * 1.37f) * amp;
                norm += amp; amp *= 0.5f; freq *= 2f;
            }
            return sum / norm;
        }

        private static float Smooth01(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        public static float PolylineDistance(Vector2[][] lines, float x, float z)
        {
            var p = new Vector2(x, z);
            var best = float.MaxValue;
            foreach (var line in lines)
                for (var i = 0; i < line.Length - 1; i++)
                {
                    var a = line[i]; var ab = line[i + 1] - a;
                    var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                    best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
                }
            return best;
        }

        public static float RoadDistance(float x, float z) => PolylineDistance(Roads, x, z);

        // ---------------- máscaras ----------------

        /// <summary>1 dentro da Capital (quadrado), 0 fora, com borda suave.</summary>
        public static float CityMask(float x, float z, float margin = 26f)
        {
            var d = Mathf.Max(Mathf.Abs(x - Capital.x), Mathf.Abs(z - Capital.y));
            return 1f - Smooth01(CityHalf + 6f, CityHalf + 6f + margin, d);
        }

        public static bool InCity(float x, float z) => Mathf.Max(Mathf.Abs(x - Capital.x), Mathf.Abs(z - Capital.y)) <= CityHalf + 6f;

        /// <summary>Distância até o eixo de rua mais próximo da Capital (m).</summary>
        public static float StreetDistance(float x, float z)
        {
            float Axis(float v)
            {
                var m = Mathf.Repeat(v + CityHalf, StreetSpacing);              // ruas em −110 + 44k
                return Mathf.Min(m, StreetSpacing - m);
            }
            return Mathf.Min(Axis(x - Capital.x), Axis(z - Capital.y));
        }

        public static bool IsStreet(float x, float z)
        {
            if (!InCity(x, z)) return false;
            var lx = x - Capital.x; var lz = z - Capital.y;
            if (Mathf.Abs(lx) < 22f - StreetHalfWidth && Mathf.Abs(lz) < 22f - StreetHalfWidth) return true;   // praça central
            return StreetDistance(x, z) <= StreetHalfWidth;
        }

        public static float SwampMask(float x, float z) => 1f - Smooth01(120f, 260f, Vector2.Distance(new Vector2(x, z), Swamp));
        public static float BasinMask(float x, float z) => 1f - Smooth01(65f, 175f, Vector2.Distance(new Vector2(x, z), Village));

        // ---------------- relevo ----------------

        /// <summary>Altura do chão (m, absoluta) em qualquer ponto.</summary>
        public static float Height(float x, float z)
        {
            var h = WaterLevel + 0.4f + (Fbm(x, z, 4, 0.008f, 13.1f) - 0.5f) * 3.4f;
            h += (Fbm(x, z, 2, 0.045f, 3.3f) - 0.5f) * 0.9f;                                       // montinhos

            var north = 1f - Smooth01(130f, 340f, Vector2.Distance(new Vector2(x, z), new Vector2(-300f, 300f)));
            h += north * north * 9f + north * Fbm(x, z, 3, 0.02f, 41f) * 2f;                      // terra alta
            var ne = 1f - Smooth01(60f, 210f, Vector2.Distance(new Vector2(x, z), new Vector2(260f, 360f)));
            h += ne * 4.5f;

            var basin = BasinMask(x, z);
            h = Mathf.Lerp(h, 10.9f + (Fbm(x, z, 3, 0.03f, 7.7f) - 0.5f) * 0.6f, basin);        // bacia da vila (água pela cintura)

            var swamp = SwampMask(x, z);
            h = Mathf.Lerp(h, 11.75f + (Fbm(x, z, 3, 0.05f, 61f) - 0.5f) * 1.7f, swamp);         // brejo: meio seco, meio alagado

            var ch = PolylineDistance(Channels, x, z);
            if (ch < 10f) h = Mathf.Lerp(h, Mathf.Min(h, 10.4f), 1f - Smooth01(3.5f, 10f, ch));

            foreach (var p in PlagueZones)
            {
                var d = Vector2.Distance(new Vector2(x, z), new Vector2(p.x, p.y));
                var rim = Smooth01(p.z * 0.92f, p.z * 1.04f, d) * (1f - Smooth01(p.z * 1.25f, p.z * 1.5f, d));
                h = Mathf.Lerp(h, Mathf.Max(h, 12.35f), rim);                                       // borda seca que segura a água negra
                h = Mathf.Lerp(h, 10.6f + (Fbm(x, z, 2, 0.08f, 9f) - 0.5f) * 0.4f, 1f - Smooth01(p.z * 0.65f, p.z * 0.95f, d));
            }

            var edge = Mathf.InverseLerp(430f, 497f, Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)));
            h += edge * edge * 18f + edge * Fbm(x, z, 2, 0.05f, 91f) * 6f;

            // estradas elevadas (passam por cima do brejo como diques)
            var rd = RoadDistance(x, z);
            if (rd < 8f)
            {
                var k = 1f - Smooth01(3.5f, 8f, rd);
                h = Mathf.Lerp(h, Mathf.Clamp(h, 12.6f, 30f), k);
            }

            // Capital: plana, um pouco abaulada
            var city = CityMask(x, z);
            if (city > 0f) h = Mathf.Lerp(h, CityGround + (Fbm(x, z, 2, 0.03f, 5f) - 0.5f) * 0.25f, city);

            // valas de corpos: terreno seco e plano com covas abertas
            foreach (var s in Disposals)
            {
                var d = Vector2.Distance(new Vector2(x, z), s.C);
                var f = d <= s.R ? 1f : 1f - Smooth01(s.R, s.R * 1.7f, d);
                if (f > 0f) h = Mathf.Lerp(h, s.H, f);
                foreach (var pit in s.Pits)
                {
                    var local = Quaternion.Euler(0f, 0f, pit.Yaw) * new Vector2(x - pit.C.x, z - pit.C.y);
                    var e = Mathf.Max(Mathf.Abs(local.x) / (pit.Size.x * 0.5f), Mathf.Abs(local.y) / (pit.Size.y * 0.5f));
                    if (e < 1.2f) h -= 1.4f * (1f - Smooth01(0.75f, 1.2f, e));
                }
            }

            // arena do chefe: clareira redonda, rasa
            var ad = Vector2.Distance(new Vector2(x, z), Arena);
            if (ad < ArenaRadius + 14f) h = Mathf.Lerp(h, ArenaGround + (Fbm(x, z, 2, 0.1f, 17f) - 0.5f) * 0.15f, 1f - Smooth01(ArenaRadius, ArenaRadius + 14f, ad));

            // chegada: patamar seco
            var arr = Vector2.Distance(new Vector2(x, z), Arrival);
            if (arr < 18f) h = Mathf.Lerp(h, Mathf.Max(h, 13.2f), 1f - Smooth01(8f, 18f, arr));

            return Mathf.Clamp(h, BaseY + 1f, BaseY + HeightRange - 1f);
        }

        public static float Slope(float x, float z)
        {
            var dx = Height(x + 1f, z) - Height(x - 1f, z);
            var dz = Height(x, z + 1f) - Height(x, z - 1f);
            return Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz) * 0.5f) * Mathf.Rad2Deg;
        }

        public static Vector3 P(float x, float z, float up = 0f) => new(x, Height(x, z) + up, z);

        // ---------------- camadas do chão ----------------
        // 0 leito do brejo · 1 lama rachada · 2 grama do pântano · 3 paralelepípedo · 4 terra da estrada · 5 cal · 6 pedra
        public const int LayerSwampBed = 0, LayerMud = 1, LayerGrass = 2, LayerCobble = 3, LayerRoad = 4, LayerLime = 5, LayerRock = 6;

        public static TerrainLayer[] BuildLayers(string folder)
        {
            TerrainLayer Layer(string name, string tex, float tile, float smooth)
            {
                var path = $"{folder}/Camada_{name}.terrainlayer";
                var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
                layer.diffuseTexture = ProceduralTextures.Albedo(tex);
                layer.normalMapTexture = ProceduralTextures.Normal(tex);
                layer.normalScale = 1f;
                layer.tileSize = new Vector2(tile, tile);
                layer.smoothness = smooth;
                layer.metallic = 0f;
                EditorUtility.SetDirty(layer);
                return layer;
            }
            return new[]
            {
                Layer("LeitoBrejo", "LamaPantano", 4f, 0.45f),
                Layer("LamaRachada", "LamaRachada", 4f, 0.1f),
                Layer("GramaPantano", "GramaPantano", 3.5f, 0f),
                Layer("Paralelepipedo", "Paralelepipedo", 3f, 0.25f),
                Layer("TerraEstrada", "TerraEstrada", 4f, 0.1f),
                Layer("Cal", "Cal", 3f, 0f),
                Layer("Pedra", "PedraAntiga", 4f, 0f),
            };
        }

        public static void SplatWeights(float x, float z, float[] w)
        {
            for (var i = 0; i < w.Length; i++) w[i] = 0f;
            var h = Height(x, z);
            var depth = WaterLevel - h;
            var under = Smooth01(-0.2f, 0.2f, depth);
            w[LayerSwampBed] = under * 2f;
            var dry = 1f - under;
            var bank = 1f - Smooth01(0.3f, 1.8f, -depth);                                   // faixa de margem
            var mudNoise = Fbm(x, z, 2, 0.04f, 31f);
            w[LayerMud] = dry * Mathf.Clamp01(bank * 0.9f + (mudNoise - 0.55f) * 1.5f);
            w[LayerGrass] = dry * (1f - w[LayerMud]);

            var slope = Slope(x, z);
            w[LayerRock] = Smooth01(24f, 36f, slope) * 3f;

            var rd = RoadDistance(x, z);
            if (rd < 4.5f) w[LayerRoad] = rd < 3f ? 6f : (4.5f - rd) * 4f;

            if (InCity(x, z))
            {
                if (IsStreet(x, z)) { w[LayerCobble] = 12f; }
                else { w[LayerRoad] += 1.5f; w[LayerGrass] += 0.5f; }                     // quintais: terra batida e mato
            }
            foreach (var s in Disposals)
            {
                if (Vector2.Distance(new Vector2(x, z), s.C) > s.R * 1.2f) continue;
                w[LayerRoad] += 2f;                                                          // terra revirada
                foreach (var pit in s.Pits)
                {
                    var d = Vector2.Distance(new Vector2(x, z), pit.C);
                    if (d < Mathf.Max(pit.Size.x, pit.Size.y) * 0.75f) w[LayerLime] += 4f * (1f - d / (Mathf.Max(pit.Size.x, pit.Size.y) * 0.75f)) + Fbm(x, z, 2, 0.3f, 3f);
                }
            }
            var ad = Vector2.Distance(new Vector2(x, z), Arena);
            if (ad < ArenaRadius) w[LayerMud] += 0.6f;

            var sum = 0f;
            foreach (var v in w) sum += v;
            if (sum <= 0f) { w[LayerGrass] = 1f; return; }
            for (var i = 0; i < w.Length; i++) w[i] /= sum;
        }
    }
}
