using System.Collections.Generic;
using Athoria.Gameplay.Items;
using Athoria.Gameplay.Loot;
using Athoria.Gameplay.World;
using UnityEngine;
using static Athoria.Editor.WorldBuildKit;
using G = Athoria.Editor.FloodedTerrainGenerator;
using K = Athoria.Editor.FloodedMeshKit;

namespace Athoria.Editor
{
    /// <summary>
    /// A Capital (noroeste, terra alta): quarteirões de sobrados vitorianos de 2 a 4 andares,
    /// tijolo e reboco descascando, telhados íngremes, janelas em arco, grades de ferro,
    /// andaimes em paredes ruindo, ruas de paralelepípedo com lampiões a gás, praça com
    /// chafariz seco, portão ao sul e o cemitério com a entrada da Cripta ao norte.
    /// </summary>
    internal static class FloodedCapital
    {
        private const float StoryHeight = 3.2f;

        public static void Build(List<Transform> content, FloodedMaterials m, LootTable knightLoot)
        {
            var rng = new System.Random(2024);
            var root = Group("A Capital", G.P(G.Capital.x, G.Capital.y));
            content.Add(root);

            for (var bx = 0; bx < 5; bx++)
            for (var bz = 0; bz < 5; bz++)
            {
                var x0 = -G.CityHalf + bx * G.StreetSpacing + G.StreetHalfWidth;
                var z0 = -G.CityHalf + bz * G.StreetSpacing + G.StreetHalfWidth;
                var size = G.StreetSpacing - G.StreetHalfWidth * 2f;            // 36 m
                var center = new Vector2(G.Capital.x + x0 + size * 0.5f, G.Capital.y + z0 + size * 0.5f);
                if (bx == 2 && bz == 2) { Plaza(content, m, center, knightLoot); continue; }
                if (bx == 2 && bz == 4) { Churchyard(content, m, center, size); continue; }
                var ruined = (bx == 0 && bz == 3) || (bx == 4 && bz == 1) || (bx == 3 && bz == 0);
                Block(content, m, center, size, ruined, rng);
            }

            Lamps(content, m);
            Gate(content, m);
            // Locais
            Discoverable(root, "ta_capital", "A Capital", false, 40, G.P(G.Capital.x, G.Capital.y, 5f), new Vector3(220f, 12f, 220f));
        }

        // ------------------------------------------------------------------ quarteirão

        private static void Block(List<Transform> content, FloodedMaterials m, Vector2 center, float size, bool ruined, System.Random rng)
        {
            const float depth = 10f;
            var half = size * 0.5f;
            // Lados: (direção da frente, eixo ao longo da frente, comprimento disponível, deslocamento)
            var sides = new (Vector2 front, Vector2 along, float length)[]
            {
                (new Vector2(0, -1), new Vector2(1, 0), size),                  // sul (comprimento total)
                (new Vector2(0, 1), new Vector2(-1, 0), size),                  // norte
                (new Vector2(-1, 0), new Vector2(0, -1), size - depth * 2f),    // oeste (entre os cantos)
                (new Vector2(1, 0), new Vector2(0, 1), size - depth * 2f),      // leste
            };
            foreach (var (front, along, length) in sides)
            {
                var start = -length * 0.5f;
                while (start < length * 0.5f - 4f)
                {
                    var width = Mathf.Min(7.5f + (float)rng.NextDouble() * 5f, length * 0.5f - start);
                    if (length * 0.5f - (start + width) < 5f) width = length * 0.5f - start;   // não deixa sobra estreita
                    var mid = start + width * 0.5f;
                    var pos2 = center + front * (half - depth * 0.5f) + along * mid;
                    var yaw = Mathf.Atan2(front.x, front.y) * Mathf.Rad2Deg;
                    var stories = 2 + rng.Next(3);
                    if (ruined && rng.NextDouble() < 0.6) Ruin(content, m, pos2, yaw, width - 0.3f, depth, stories, rng);
                    else Townhouse(content, m, pos2, yaw, width - 0.3f, depth, stories, rng);
                    start += width;
                }
            }
            // Quintal no meio do quarteirão: entulho, varal, barris
            var yard = Group("Quintal", G.P(center.x, center.y), (float)rng.NextDouble() * 360f);
            content.Add(yard);
            for (var i = 0; i < 4; i++)
            {
                var p = new Vector3((float)rng.NextDouble() * 10f - 5f, 0.3f, (float)rng.NextDouble() * 10f - 5f);
                K.Box(yard, "Entulho", p, new Vector3(0.6f + (float)rng.NextDouble(), 0.5f, 0.6f + (float)rng.NextDouble()), rng.Next(2) == 0 ? m.Brick : m.Stone,
                    new Vector3(0, (float)rng.NextDouble() * 90f, 12f), true, 1f);
            }
            K.Box(yard, "Varal Poste A", new Vector3(-3f, 1.1f, 0f), new Vector3(0.12f, 2.2f, 0.12f), m.WoodDark, default, true, 1f);
            K.Box(yard, "Varal Poste B", new Vector3(3f, 1.1f, 0f), new Vector3(0.12f, 2.2f, 0.12f), m.WoodDark, default, true, 1f);
            K.Box(yard, "Varal", new Vector3(0f, 2.1f, 0f), new Vector3(6f, 0.02f, 0.02f), m.Rope, default, false, 1f);
            for (var i = 0; i < 3; i++)
                K.Box(yard, "Lençol", new Vector3(-1.8f + i * 1.8f, 1.6f, 0f), new Vector3(1.2f, 1f, 0.02f), m.Cloth, new Vector3(0, 0, (i - 1) * 3f), false, 1f);
        }

        /// <summary>Sobrado: base de pedra, corpo de tijolo ou reboco, cornija, telhado de duas águas, janelas em arco.</summary>
        private static void Townhouse(List<Transform> content, FloodedMaterials m, Vector2 at, float yaw, float width, float depth, int stories, System.Random rng)
        {
            var house = Group("Sobrado", G.P(at.x, at.y), yaw);
            content.Add(house);
            var bodyH = stories * StoryHeight;
            var wall = rng.Next(5) switch { 0 => m.Brick, 1 => m.BrickSoot, 2 => m.Plaster, 3 => m.PlasterDirty, _ => m.PlasterGreen };

            K.Box(house, "Embasamento", new Vector3(0, 0.25f, 0), new Vector3(width + 0.2f, 1.5f, depth + 0.2f), m.Stone, default, true, 2f);
            K.Box(house, "Corpo", new Vector3(0, bodyH * 0.5f, 0), new Vector3(width, bodyH, depth), wall, default, true, 2.5f);
            K.Box(house, "Cornija", new Vector3(0, bodyH + 0.12f, 0), new Vector3(width + 0.35f, 0.25f, depth + 0.35f), m.WoodDark, default, false, 1f);
            for (var s = 1; s < stories; s++)   // frisos entre andares
                K.Box(house, "Friso", new Vector3(0, s * StoryHeight, depth * 0.5f + 0.06f), new Vector3(width, 0.14f, 0.12f), m.Stone, default, false, 1f);

            // Telhado: cumeeira paralela à rua (águas para a rua) ou empena virada para a rua.
            var gableFront = rng.NextDouble() < 0.35;
            var roofH = (gableFront ? width : depth) * 0.48f;
            if (gableFront) K.Gable(house, "Telhado", new Vector3(0, bodyH + 0.24f, 0), 90f, depth, width, roofH, m.Slate, wall);
            else K.Gable(house, "Telhado", new Vector3(0, bodyH + 0.24f, 0), 0f, width, depth, roofH, m.Slate, wall);
            var chimneys = 1 + rng.Next(2);
            for (var c = 0; c < chimneys; c++)
            {
                var cx = (c == 0 ? -1f : 1f) * (width * 0.3f);
                K.Box(house, "Chaminé", new Vector3(cx, bodyH + roofH * 0.7f, -depth * 0.15f), new Vector3(0.7f, roofH * 1.2f + 0.8f, 0.7f), m.BrickSoot, default, false, 1.5f);
            }

            // Janelas em arco: frente e fundos; algumas acesas (gente ainda mora ali).
            var perFloor = Mathf.Max(1, Mathf.FloorToInt(width / 2.6f));
            var dark = new List<(Vector3, Vector2)>();
            var lit = new List<(Vector3, Vector2)>();
            var darkBack = new List<(Vector3, Vector2)>();
            var doorX = perFloor % 2 == 1 ? 0f : -width / perFloor * 0.5f;
            for (var s = 0; s < stories; s++)
            for (var i = 0; i < perFloor; i++)
            {
                var x = -width * 0.5f + width / perFloor * (i + 0.5f);
                var y = s * StoryHeight + (s == 0 ? 1.55f : 1.75f);
                if (s == 0 && Mathf.Abs(x - doorX) < 0.6f) continue;   // porta
                var size = s == 0 ? new Vector2(1.2f, 2.1f) : new Vector2(1f, 1.9f);
                (rng.NextDouble() < 0.12 ? lit : dark).Add((new Vector3(x, y, 0f), size));
                darkBack.Add((new Vector3(-x, y, 0f), size));
            }
            if (dark.Count > 0) K.PanelObject(house, "Janelas", K.Panels("janelas", dark), m.Window, new Vector3(0, 0, depth * 0.5f + 0.03f), 0f);
            if (lit.Count > 0) K.PanelObject(house, "Janelas Acesas", K.Panels("janelas_acesas", lit), m.WindowLit, new Vector3(0, 0, depth * 0.5f + 0.03f), 0f);
            if (darkBack.Count > 0) K.PanelObject(house, "Janelas Fundos", K.Panels("janelas_fundos", darkBack), m.Window, new Vector3(0, 0, -depth * 0.5f - 0.03f), 180f);

            // Porta, degraus, verga
            K.Box(house, "Porta", new Vector3(doorX, 1.3f, depth * 0.5f + 0.04f), new Vector3(1.3f, 2.5f, 0.08f), m.WoodDark, default, false, 1f);
            K.Box(house, "Verga", new Vector3(doorX, 2.7f, depth * 0.5f + 0.1f), new Vector3(1.7f, 0.25f, 0.2f), m.Stone, default, false, 1f);
            K.Box(house, "Degrau", new Vector3(doorX, 0.1f, depth * 0.5f + 0.5f), new Vector3(1.8f, 0.3f, 0.8f), m.Stone, default, true, 1f);

            // Grade de ferro na frente (pátio inglês), com abertura na porta.
            if (rng.NextDouble() < 0.3) Railing(house, m, new Vector3(0, 0, depth * 0.5f + 1.4f), width, doorX);

            // Andaime numa parede que está ruindo
            if (rng.NextDouble() < 0.15) Scaffolding(house, m, width, depth, bodyH, stories);
        }

        /// <summary>Casa em ruínas: paredes quebradas sem telhado, entulho dentro.</summary>
        private static void Ruin(List<Transform> content, FloodedMaterials m, Vector2 at, float yaw, float width, float depth, int stories, System.Random rng)
        {
            var ruin = Group("Ruína", G.P(at.x, at.y), yaw);
            content.Add(ruin);
            var wall = rng.Next(2) == 0 ? m.BrickSoot : m.PlasterDirty;
            var h = stories * StoryHeight;
            float H(float k) => Mathf.Max(1.2f, h * (0.35f + 0.65f * k));
            var hf = H((float)rng.NextDouble()); var hb = H((float)rng.NextDouble());
            K.Box(ruin, "Frente E", new Vector3(-width * 0.3f, hf * 0.5f, depth * 0.5f - 0.2f), new Vector3(width * 0.4f, hf, 0.4f), wall, default, true, 2.5f);
            K.Box(ruin, "Frente D", new Vector3(width * 0.33f, hf * 0.35f, depth * 0.5f - 0.2f), new Vector3(width * 0.34f, hf * 0.7f, 0.4f), wall, default, true, 2.5f);
            K.Box(ruin, "Fundo", new Vector3(0, hb * 0.5f, -depth * 0.5f + 0.2f), new Vector3(width, hb, 0.4f), wall, default, true, 2.5f);
            K.Box(ruin, "Lado E", new Vector3(-width * 0.5f + 0.2f, h * 0.4f, 0), new Vector3(0.4f, h * 0.8f, depth), wall, default, true, 2.5f);
            K.Box(ruin, "Lado D", new Vector3(width * 0.5f - 0.2f, hb * 0.45f, 0), new Vector3(0.4f, hb * 0.9f, depth), wall, default, true, 2.5f);
            for (var i = 0; i < 6; i++)
                K.Box(ruin, "Entulho", new Vector3((float)rng.NextDouble() * width * 0.7f - width * 0.35f, 0.3f, (float)rng.NextDouble() * depth * 0.6f - depth * 0.3f),
                    new Vector3(0.8f + (float)rng.NextDouble() * 1.2f, 0.5f + (float)rng.NextDouble() * 0.5f, 0.8f + (float)rng.NextDouble()), rng.Next(2) == 0 ? m.Brick : m.Stone,
                    new Vector3((float)rng.NextDouble() * 20f, (float)rng.NextDouble() * 90f, (float)rng.NextDouble() * 20f), true, 1f);
            K.Box(ruin, "Viga Caída", new Vector3(0f, 1.1f, 0f), new Vector3(0.25f, 0.25f, depth * 0.9f), m.WoodDark, new Vector3(18f, 20f, 0f), true, 1f);
            if (rng.NextDouble() < 0.5) Scaffolding(ruin, m, width, depth, hf, Mathf.Max(1, Mathf.FloorToInt(hf / StoryHeight)));
        }

        private static void Railing(Transform parent, FloodedMaterials m, Vector3 local, float width, float gapX)
        {
            var bars = new List<(Vector3, Vector3)>();
            var gapA = Mathf.Clamp(gapX - 0.75f, -width * 0.5f, width * 0.5f);
            var gapB = Mathf.Clamp(gapX + 0.75f, -width * 0.5f, width * 0.5f);
            for (var x = -width * 0.5f; x <= width * 0.5f; x += 0.16f)
                if (x < gapA || x > gapB) bars.Add((new Vector3(x, 0.65f, 0f), new Vector3(0.03f, 1.3f, 0.03f)));
            void Rail(float a, float b)
            {
                if (b - a < 0.1f) return;
                bars.Add((new Vector3((a + b) * 0.5f, 1.25f, 0f), new Vector3(b - a, 0.05f, 0.05f)));
                bars.Add((new Vector3((a + b) * 0.5f, 0.15f, 0f), new Vector3(b - a, 0.05f, 0.05f)));
            }
            Rail(-width * 0.5f, gapA);
            Rail(gapB, width * 0.5f);
            var go = K.Combined(parent, "Grade de Ferro", K.Bars("grade", bars, 0.5f), m.Iron, local, 0f);
            // colisão só nos trechos de grade (a abertura do portão fica livre)
            foreach (var (a, b) in new[] { (-width * 0.5f, gapA), (gapB, width * 0.5f) })
            {
                if (b - a < 0.1f) continue;
                var bc = go.AddComponent<BoxCollider>();
                bc.size = new Vector3(b - a, 1.3f, 0.12f);
                bc.center = new Vector3((a + b) * 0.5f, 0.65f, 0f);
            }
        }

        private static void Scaffolding(Transform parent, FloodedMaterials m, float width, float depth, float height, int floors)
        {
            var z = depth * 0.5f + 1.1f;
            var poles = new List<(Vector3, Vector3)>();
            for (var x = -width * 0.5f + 0.3f; x <= width * 0.5f; x += Mathf.Max(2f, width / 3f))
            {
                poles.Add((new Vector3(x, height * 0.5f, z), new Vector3(0.12f, height + 0.6f, 0.12f)));
                poles.Add((new Vector3(x, height * 0.5f, z - 0.9f), new Vector3(0.12f, height + 0.6f, 0.12f)));
            }
            K.Combined(parent, "Andaime (postes)", K.Bars("andaime", poles, 1f), m.WoodDark, Vector3.zero, 0f);
            for (var f = 1; f <= floors; f++)
                K.Box(parent, "Tábua do Andaime", new Vector3(0, f * StoryHeight - 0.4f, z - 0.45f), new Vector3(width, 0.08f, 1f), m.Wood, default, true, 1.5f);
            K.Box(parent, "Escora", new Vector3(0, height * 0.45f, z + 0.05f), new Vector3(width * 1.05f, 0.1f, 0.1f), m.WoodDark, new Vector3(0, 0, Mathf.Atan2(height * 0.9f, width) * Mathf.Rad2Deg), false, 1f);
        }

        // ------------------------------------------------------------------ praça e cemitério

        private static void Plaza(List<Transform> content, FloodedMaterials m, Vector2 c, LootTable knightLoot)
        {
            var plaza = Group("Praça", G.P(c.x, c.y));
            content.Add(plaza);
            // Chafariz seco
            for (var i = 0; i < 8; i++)
            {
                var a = i * 45f;
                var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                K.Box(plaza, "Borda do Chafariz", dir * 4f + Vector3.up * 0.45f, new Vector3(3.4f, 0.9f, 0.5f), m.Stone, new Vector3(0, a, 0), true, 1.5f);
            }
            K.Box(plaza, "Fundo Seco", new Vector3(0, 0.08f, 0), new Vector3(7.6f, 0.16f, 7.6f), m.Dirt, default, false, 2f);
            K.Box(plaza, "Coluna", new Vector3(0, 1.8f, 0), new Vector3(0.9f, 3.6f, 0.9f), m.Stone, default, true, 1.5f);
            K.Box(plaza, "Bacia Alta", new Vector3(0, 3.7f, 0), new Vector3(2.2f, 0.35f, 2.2f), m.Stone, new Vector3(0, 45, 0), false, 1.5f);
            // Carroça da peste com corpos embrulhados
            var cart = Group("Carroça da Peste", Vector3.zero, 30f, plaza);
            cart.localPosition = new Vector3(9f, 0f, -6f);
            Cart(cart, m);
            // Quadro de avisos e bancos
            K.Box(plaza, "Quadro de Avisos", new Vector3(-9f, 1.4f, 8f), new Vector3(2.2f, 1.4f, 0.12f), m.Wood, new Vector3(0, 20, 0), true, 1f);
            K.Box(plaza, "Poste do Quadro", new Vector3(-9f, 0.8f, 8f), new Vector3(0.15f, 1.6f, 0.15f), m.WoodDark, new Vector3(0, 20, 0), false, 1f);
            for (var i = 0; i < 4; i++)
            {
                var a = 20f + i * 90f;
                var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                K.Box(plaza, "Banco", dir * 10f + Vector3.up * 0.45f, new Vector3(2f, 0.1f, 0.5f), m.Wood, new Vector3(0, a, 0), true, 1f);
                K.Box(plaza, "Pé do Banco", dir * 10f + Vector3.up * 0.2f, new Vector3(1.8f, 0.4f, 0.12f), m.Iron, new Vector3(0, a, 0), false, 1f);
            }
            // O Cavaleiro Corrompido guarda a chave da Cripta (antes ficava na capela).
            content.Add(Enemy(EnemyContentFactory.Knight, null, G.P(c.x + 2f, c.y + 6f), 180f, knightLoot, null, "ta_cavaleiro_capital")?.transform);
            Discoverable(plaza, "ta_praca", "Praça do Chafariz Seco", false, 20, G.P(c.x, c.y, 2f), new Vector3(30f, 4f, 30f));
        }

        public static void Cart(Transform cart, FloodedMaterials m)
        {
            K.Box(cart, "Caixa", new Vector3(0, 0.85f, 0), new Vector3(1.6f, 0.5f, 2.6f), m.Wood, default, true, 1f);
            K.Box(cart, "Eixo", new Vector3(0, 0.45f, 0.2f), new Vector3(1.9f, 0.1f, 0.1f), m.WoodDark, default, false, 1f);
            for (var s = -1; s <= 1; s += 2)
            {
                var wheel = Part(cart, "Roda", PrimitiveType.Cylinder, new Vector3(s * 0.95f, 0.45f, 0.2f), new Vector3(0.9f, 0.05f, 0.9f), m.WoodDark, new Vector3(0, 0, 90), false);
                wheel.isStatic = true;
                K.Box(cart, "Varal", new Vector3(s * 0.4f, 0.75f, 2f), new Vector3(0.08f, 0.08f, 1.8f), m.WoodDark, new Vector3(-8f, 0, 0), false, 1f);
            }
            for (var i = 0; i < 3; i++)
            {
                var b = Part(cart, "Corpo Embrulhado", PrimitiveType.Capsule, new Vector3(-0.4f + i * 0.4f, 1.25f, 0f), new Vector3(0.42f, 0.9f, 0.36f), m.Burlap, new Vector3(90f, 0f, (i - 1) * 6f), false);
                b.isStatic = true;
            }
        }

        private static void Churchyard(List<Transform> content, FloodedMaterials m, Vector2 c, float size)
        {
            var yard = Group("Cemitério da Capital", G.P(c.x, c.y));
            content.Add(yard);
            var half = size * 0.5f - 0.5f;
            // Grade de ferro no perímetro, com portão ao sul
            for (var side = 0; side < 4; side++)
            {
                var yaw = side * 90f;
                var holder = Group("Grade", Vector3.zero, yaw, yard);
                holder.localPosition = Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, -half);
                Railing(holder, m, Vector3.zero, size - 1f, side == 0 ? 0f : 999f);
            }
            // Lápides
            var rng = new System.Random(7);
            for (var gx = -3; gx <= 3; gx++)
            for (var gz = -2; gz <= 1; gz++)
            {
                if (gx == 0) continue;   // caminho central
                var p = new Vector3(gx * 3.4f + (float)rng.NextDouble() * 0.5f, 0f, gz * 4f - 2f);
                var tilt = new Vector3(((float)rng.NextDouble() - 0.5f) * 18f, ((float)rng.NextDouble() - 0.5f) * 20f, ((float)rng.NextDouble() - 0.5f) * 14f);
                K.Box(yard, "Lápide", p + Vector3.up * 0.5f, new Vector3(0.7f, 1f + (float)rng.NextDouble() * 0.5f, 0.18f), m.Stone, tilt, true, 1f);
                K.Box(yard, "Cova", p + new Vector3(0, 0.08f, -1.1f), new Vector3(0.9f, 0.18f, 1.8f), m.Dirt, default, false, 1f);
            }
            // Mausoléu com a entrada da Cripta (ao norte, porta para o sul)
            var crypt = Group("Entrada da Cripta", Vector3.zero, 0f, yard);
            crypt.localPosition = new Vector3(0f, 0f, half - 6f);
            K.Box(crypt, "Corpo", new Vector3(0, 2.8f, 1.5f), new Vector3(8f, 5.6f, 6f), m.Stone, default, true, 2f);
            K.Gable(crypt, "Telhado", new Vector3(0, 5.6f, 1.5f), 90f, 6f, 8f, 2.4f, m.Slate, m.Stone);
            K.Box(crypt, "Pilar E", new Vector3(-2f, 2.2f, -1.7f), new Vector3(0.8f, 4.4f, 0.8f), m.Stone, default, true, 1f);
            K.Box(crypt, "Pilar D", new Vector3(2f, 2.2f, -1.7f), new Vector3(0.8f, 4.4f, 0.8f), m.Stone, default, true, 1f);
            K.Box(crypt, "Lintel", new Vector3(0f, 4.6f, -1.7f), new Vector3(5f, 0.6f, 1f), m.Stone, default, true, 1f);
            K.Box(crypt, "Degrau", new Vector3(0f, 0.15f, -2.6f), new Vector3(4f, 0.3f, 1.2f), m.Stone, default, true, 1f);
            PortalDoor(crypt, new Vector3(0f, 1.8f, -1.45f), new Vector3(3.1f, 3.6f, 0.2f), CriptaBuilder.SceneName, "entrada", "Entrar na Cripta", Item("ChaveCripta"));
            Torch(crypt, new Vector3(-2.8f, 2.6f, -2f));
            Torch(crypt, new Vector3(2.8f, 2.6f, -2f));
            var chest = PlaceChest(null, false, "ta_bau_cemiterio", yard.TransformPoint(new Vector3(-12f, 0f, 10f)), 90f);
            if (chest != null) content.Add(chest.transform);
            content.Add(Enemy(EnemyContentFactory.Skeleton, null, yard.TransformPoint(new Vector3(-6f, 0f, 0f)), 180f)?.transform);
            content.Add(Enemy(EnemyContentFactory.Skeleton, null, yard.TransformPoint(new Vector3(6f, 0f, 4f)), 200f)?.transform);
            Discoverable(yard, "ta_cemiterio", "Cemitério da Capital", false, 30, yard.position + Vector3.up * 2f, new Vector3(size, 5f, size));
        }

        /// <summary>Posição de chegada ao sair da Cripta (diante do mausoléu).</summary>
        public static Vector3 CryptExit()
        {
            var c = new Vector2(G.Capital.x, G.Capital.y - G.CityHalf + 4f * G.StreetSpacing + G.StreetHalfWidth + (G.StreetSpacing - G.StreetHalfWidth * 2f) * 0.5f);
            var half = (G.StreetSpacing - G.StreetHalfWidth * 2f) * 0.5f - 0.5f;
            return G.P(c.x, c.y + half - 6f - 6f, 0.1f);
        }

        // ------------------------------------------------------------------ lampiões e portão

        private static void Lamps(List<Transform> content, FloodedMaterials m)
        {
            var lamps = 0;
            for (var i = 0; i < 6; i++)
            for (var j = 0; j < 6; j++)
            {
                if ((i + j) % 2 == 1) continue;               // um cruzamento sim, outro não (menos luzes)
                var x = G.Capital.x - G.CityHalf + i * G.StreetSpacing + 3.2f;
                var z = G.Capital.y - G.CityHalf + j * G.StreetSpacing + 3.2f;
                var lamp = Group("Lampião a Gás", G.P(x, z));
                content.Add(lamp);
                K.Box(lamp, "Poste", new Vector3(0, 1.7f, 0), new Vector3(0.14f, 3.4f, 0.14f), m.Iron, default, true, 1f);
                K.Box(lamp, "Base", new Vector3(0, 0.2f, 0), new Vector3(0.35f, 0.4f, 0.35f), m.Iron, default, false, 1f);
                K.Box(lamp, "Lanterna", new Vector3(0, 3.6f, 0), new Vector3(0.38f, 0.5f, 0.38f), m.LampGlow, default, false, 1f);
                K.Box(lamp, "Chapéu", new Vector3(0, 3.92f, 0), new Vector3(0.5f, 0.12f, 0.5f), m.Iron, new Vector3(0, 45, 0), false, 1f);
                var light = new GameObject("Luz").AddComponent<Light>();
                light.transform.SetParent(lamp, false);
                light.transform.localPosition = new Vector3(0, 3.5f, 0);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.72f, 0.42f);
                light.range = 11f;
                light.intensity = 1.6f;
                light.shadows = LightShadows.None;
                light.gameObject.AddComponent<TorchFlicker>();
                lamps++;
            }
        }

        private static void Gate(List<Transform> content, FloodedMaterials m)
        {
            var x = G.Capital.x - 22f;
            var z = G.Capital.y - G.CityHalf - 4f;
            var gate = Group("Portão da Capital", G.P(x, z));
            content.Add(gate);
            K.Box(gate, "Torre O", new Vector3(-6.5f, 4f, 0), new Vector3(3f, 8f, 3f), m.Stone, default, true, 2f);
            K.Box(gate, "Torre L", new Vector3(6.5f, 4f, 0), new Vector3(3f, 8f, 3f), m.Stone, default, true, 2f);
            K.Box(gate, "Arco", new Vector3(0, 7.2f, 0), new Vector3(10f, 1.6f, 2.4f), m.Stone, default, true, 2f);
            K.Gable(gate, "Telhado O", new Vector3(-6.5f, 8f, 0), 0f, 3f, 3f, 2f, m.Slate, m.Stone);
            K.Gable(gate, "Telhado L", new Vector3(6.5f, 8f, 0), 0f, 3f, 3f, 2f, m.Slate, m.Stone);
            Torch(gate, new Vector3(-4.8f, 3f, -1.6f), 10f, 2f);
            Torch(gate, new Vector3(4.8f, 3f, -1.6f), 10f, 2f);
            // muro baixo e quebrado dos lados do portão
            for (var s = -1; s <= 1; s += 2)
                for (var i = 0; i < 4; i++)
                    K.Box(gate, "Muro", new Vector3(s * (10f + i * 4f), 1.2f - i * 0.2f, 0.5f), new Vector3(4f, 2.4f - i * 0.4f, 0.8f), m.Stone, new Vector3(0, 0, s * i * 2f), true, 2f);
            content.Add(Enemy(EnemyContentFactory.Archer, null, G.P(x - 5f, z + 10f), 180f)?.transform);
            content.Add(Enemy(EnemyContentFactory.Skeleton, null, G.P(x + 4f, z + 14f), 180f)?.transform);
        }
    }
}
