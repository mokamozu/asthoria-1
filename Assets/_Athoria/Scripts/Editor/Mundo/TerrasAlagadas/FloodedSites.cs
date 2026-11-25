using System.Collections.Generic;
using System.Linq;
using Athoria.Gameplay.World;
using UnityEngine;
using UnityEngine.Rendering;
using static Athoria.Editor.WorldBuildKit;
using G = Athoria.Editor.FloodedTerrainGenerator;
using K = Athoria.Editor.FloodedMeshKit;

namespace Athoria.Editor
{
    /// <summary>
    /// Vila de palafitas (bacia alagada a leste), brejo profundo com a arena do Ancião do Brejo
    /// (sudoeste), valas de corpos, poças da peste e os inimigos espalhados pela região.
    /// </summary>
    internal static class FloodedSites
    {
        // ================================================================== vila de palafitas

        private static readonly Vector2[] Shacks =
        {
            new(-40f, -10f), new(-28f, 12f), new(-15f, -22f), new(-5f, 8f), new(8f, -12f), new(14f, 20f), new(26f, -4f),
            new(30f, 24f), new(40f, -20f), new(46f, 8f), new(-20f, 30f), new(2f, 36f), new(52f, 30f), new(18f, -36f),
        };
        private static readonly Vector2 Landing = new(160f, 40f);

        public static void StiltVillage(List<Transform> content, FloodedMaterials m)
        {
            var rng = new System.Random(55);
            var deck = G.VillageDeck;
            var nodes = new List<Vector2> { Landing };
            nodes.AddRange(Shacks.Select(s => G.Village + s));

            // Árvore mínima ligando o píer de chegada e as cabanas (passarelas); trechos longos viram pontes de corda.
            var inTree = new HashSet<int> { 0 };
            var edges = new List<(int a, int b)>();
            while (inTree.Count < nodes.Count)
            {
                var best = (a: -1, b: -1, d: float.MaxValue);
                foreach (var a in inTree)
                    for (var b = 0; b < nodes.Count; b++)
                    {
                        if (inTree.Contains(b)) continue;
                        var d = Vector2.Distance(nodes[a], nodes[b]);
                        if (d < best.d) best = (a, b, d);
                    }
                inTree.Add(best.b);
                edges.Add((best.a, best.b));
            }
            // algumas ligações extras (laços) para a vila não ser um labirinto de becos sem saída
            edges.Add((4, 6)); edges.Add((8, 10)); edges.Add((12, 13));

            var village = Group("Vila das Palafitas", new Vector3(G.Village.x, deck, G.Village.y));
            content.Add(village);
            foreach (var (a, b) in edges)
            {
                var pa = nodes[a]; var pb = nodes[b];
                var dir = (pb - pa).normalized;
                // começa/termina na borda da plataforma (3,2 m do centro) e não no meio da cabana
                var from = a == 0 ? pa : pa + dir * 3.2f;
                var to = pb - dir * 3.2f;
                var len = Vector2.Distance(from, to);
                if (len < 0.5f) continue;
                if (len > 19f) RopeBridge(content, m, from, to, deck);
                else Boardwalk(content, m, from, to, deck, a == 0);
            }
            for (var i = 0; i < Shacks.Length; i++)
            {
                var p = G.Village + Shacks[i];
                var yaw = rng.Next(4) * 90f + (float)(rng.NextDouble() * 10 - 5);
                Shack(content, m, p, yaw, deck, i, rng);
            }
            // Píeres com barcos amarrados (nas cabanas da borda)
            foreach (var i in new[] { 0, 8, 12, 13 })
            {
                var p = G.Village + Shacks[i];
                var outward = (Shacks[i]).normalized;
                Dock(content, m, p + outward * 3.2f, outward, deck, rng);
            }
            Discoverable(village, "ta_palafitas", "Vila das Palafitas", false, 35, new Vector3(G.Village.x, deck + 2f, G.Village.y), new Vector3(120f, 8f, 110f));

            // Moradores hostis
            content.Add(Enemy(EnemyContentFactory.Archer, null, new Vector3(G.Village.x + 26f, deck + 0.15f, G.Village.y - 4f), 270f)?.transform);
            content.Add(Enemy(EnemyContentFactory.Archer, null, new Vector3(G.Village.x - 15f, deck + 0.15f, G.Village.y - 22f), 300f)?.transform);
            content.Add(Enemy(EnemyContentFactory.Skeleton, null, new Vector3(G.Village.x + 8f, deck + 0.15f, G.Village.y - 12f), 200f)?.transform);
            content.Add(Enemy(EnemyContentFactory.Skeleton, null, new Vector3(G.Village.x + 46f, deck + 0.15f, G.Village.y + 8f), 90f)?.transform);
        }

        private static void Posts(Transform parent, FloodedMaterials m, Vector3 worldTop, float size = 0.22f)
        {
            var ground = G.Height(worldTop.x, worldTop.z) - 0.6f;
            var h = worldTop.y - ground;
            if (h <= 0.2f) return;
            var post = K.Box(parent, "Estaca", parent.InverseTransformPoint(new Vector3(worldTop.x, ground + h * 0.5f, worldTop.z)), new Vector3(size, h, size), m.WoodDark, default, true, 1f);
            post.transform.rotation = Quaternion.identity;
        }

        private static void Boardwalk(List<Transform> content, FloodedMaterials m, Vector2 from, Vector2 to, float deck, bool fromShore)
        {
            var mid = (from + to) * 0.5f;
            var len = Vector2.Distance(from, to);
            var yaw = Mathf.Atan2(to.x - from.x, to.y - from.y) * Mathf.Rad2Deg;
            var startY = fromShore ? Mathf.Max(G.Height(from.x, from.y) + 0.1f, deck - 0.6f) : deck;
            var walk = Group("Passarela", new Vector3(mid.x, (startY + deck) * 0.5f, mid.y), yaw);
            content.Add(walk);
            var pitch = -Mathf.Atan2(deck - startY, len) * Mathf.Rad2Deg;
            K.Box(walk, "Tábuas", new Vector3(0, -0.06f, 0), new Vector3(1.9f, 0.12f, len + 0.2f), m.Wood, new Vector3(pitch, 0, 0), true, 1.5f);
            var rail = len > 6f;
            for (var t = 0f; t <= len; t += 3f)
            {
                var local = new Vector3(0, 0, -len * 0.5f + t);
                foreach (var side in new[] { -0.85f, 0.85f })
                {
                    var p = walk.TransformPoint(local + Vector3.right * side);
                    Posts(walk, m, new Vector3(p.x, Mathf.Lerp(startY, deck, t / len) - 0.1f, p.z), 0.18f);
                }
            }
            if (rail)
            {
                K.Box(walk, "Corrimão", new Vector3(0.95f, 0.85f, 0), new Vector3(0.07f, 0.07f, len), m.WoodDark, new Vector3(pitch, 0, 0), false, 1f);
                for (var t = 0f; t <= len; t += 2.5f)
                    K.Box(walk, "Pilarete", new Vector3(0.95f, 0.42f, -len * 0.5f + t), new Vector3(0.08f, 0.85f, 0.08f), m.WoodDark, default, false, 1f);
            }
        }

        private static void RopeBridge(List<Transform> content, FloodedMaterials m, Vector2 from, Vector2 to, float deck)
        {
            var mid = (from + to) * 0.5f;
            var len = Vector2.Distance(from, to);
            var yaw = Mathf.Atan2(to.x - from.x, to.y - from.y) * Mathf.Rad2Deg;
            var bridge = Group("Ponte de Corda", new Vector3(mid.x, deck, mid.y), yaw);
            content.Add(bridge);
            const float sag = 0.7f;
            float Y(float t) => -sag * 4f * t * (1f - t);   // parábola
            var planks = Mathf.CeilToInt(len / 0.55f);
            for (var i = 0; i < planks; i++)
            {
                var t = (i + 0.5f) / planks;
                var z = -len * 0.5f + len * t;
                var slope = Mathf.Atan(-sag * 4f * (1f - 2f * t) / len) * Mathf.Rad2Deg;
                K.Box(bridge, "Tábua", new Vector3(((i * 7) % 3 - 1) * 0.03f, Y(t) - 0.04f, z), new Vector3(1.4f, 0.07f, 0.42f), m.Wood,
                    new Vector3(-slope, (i % 5 - 2) * 2f, 0), true, 1f);
            }
            // cordas (corrimão) em segmentos
            const int seg = 10;
            for (var s = -1; s <= 1; s += 2)
                for (var i = 0; i < seg; i++)
                {
                    float t0 = i / (float)seg, t1 = (i + 1f) / seg;
                    var a = new Vector3(s * 0.8f, Y(t0) * 0.6f + 0.9f, -len * 0.5f + len * t0);
                    var b = new Vector3(s * 0.8f, Y(t1) * 0.6f + 0.9f, -len * 0.5f + len * t1);
                    var c = (a + b) * 0.5f;
                    var d = b - a;
                    var rope = K.Box(bridge, "Corda", c, new Vector3(0.04f, 0.04f, d.magnitude), m.Rope, default, false, 1f);
                    rope.transform.localRotation = Quaternion.LookRotation(d);
                }
            // postes nas pontas
            foreach (var end in new[] { -len * 0.5f, len * 0.5f })
                for (var s = -1; s <= 1; s += 2)
                    K.Box(bridge, "Poste", new Vector3(s * 0.8f, 0.5f, end), new Vector3(0.14f, 1.2f, 0.14f), m.WoodDark, default, false, 1f);
        }

        private static void Shack(List<Transform> content, FloodedMaterials m, Vector2 at, float yaw, float deck, int index, System.Random rng)
        {
            var shack = Group("Cabana", new Vector3(at.x, deck, at.y), yaw);
            content.Add(shack);
            K.Box(shack, "Plataforma", new Vector3(0, -0.12f, 0), new Vector3(6.6f, 0.24f, 6.6f), m.Wood, default, true, 1.5f);
            for (var ix = -1; ix <= 1; ix++)
            for (var iz = -1; iz <= 1; iz += 2)
            {
                var p = shack.TransformPoint(new Vector3(ix * 3f, -0.2f, iz * 3f));
                Posts(shack, m, p, 0.26f);
            }
            const float w = 4.6f, d = 4f, h = 2.5f;
            var front = d * 0.5f;
            K.Box(shack, "Parede Fundo", new Vector3(0, h * 0.5f, -front), new Vector3(w, h, 0.14f), m.Wood, default, true, 1.5f);
            K.Box(shack, "Parede E", new Vector3(-w * 0.5f, h * 0.5f, 0), new Vector3(0.14f, h, d), m.Wood, default, true, 1.5f);
            K.Box(shack, "Parede D", new Vector3(w * 0.5f, h * 0.5f, 0), new Vector3(0.14f, h, d), m.Wood, default, true, 1.5f);
            K.Box(shack, "Frente E", new Vector3(-1.55f, h * 0.5f, front), new Vector3(1.5f, h, 0.14f), m.Wood, default, true, 1.5f);
            K.Box(shack, "Frente D", new Vector3(1.55f, h * 0.5f, front), new Vector3(1.5f, h, 0.14f), m.Wood, default, true, 1.5f);
            K.Box(shack, "Verga", new Vector3(0, h - 0.3f, front), new Vector3(1.6f, 0.6f, 0.14f), m.Wood, default, true, 1.5f);
            K.Box(shack, "Janela", new Vector3(w * 0.5f + 0.08f, 1.5f, 0), new Vector3(0.04f, 0.6f, 0.9f), m.WoodDark, default, false, 1f);
            K.Gable(shack, "Telhado de Palha", new Vector3(0, h, 0), 0f, w, d, 1.7f, m.Thatch, m.Wood, 0.6f);
            // Rede de pesca secando e barril
            if (rng.NextDouble() < 0.6)
            {
                K.Box(shack, "Rede", new Vector3(-w * 0.5f - 0.6f, 1.2f, 1.2f), new Vector3(0.03f, 1.6f, 2.2f), m.Rope, new Vector3(0, 0, 8f), false, 0.5f);
                K.Box(shack, "Vara", new Vector3(-w * 0.5f - 0.6f, 2.05f, 1.2f), new Vector3(0.06f, 0.06f, 2.6f), m.WoodDark, default, false, 1f);
            }
            var barrel = PsxAssets.Kit("ModularMedieval/Market/barrel.fbx", 1f, true);
            if (barrel != null) PsxAssets.Place(barrel, shack, shack.TransformPoint(new Vector3(2.6f, 0f, 2.6f)), (float)rng.NextDouble() * 360f);
            // Lanterna pendurada (luz quente) em metade das cabanas
            if (index % 2 == 0)
            {
                K.Box(shack, "Braço da Lanterna", new Vector3(1.8f, 2.6f, front + 0.6f), new Vector3(0.08f, 0.08f, 1.3f), m.WoodDark, default, false, 1f);
                K.Box(shack, "Lanterna", new Vector3(1.8f, 2.25f, front + 1.15f), new Vector3(0.28f, 0.38f, 0.28f), m.LampGlow, default, false, 1f);
                var light = new GameObject("Luz").AddComponent<Light>();
                light.transform.SetParent(shack, false);
                light.transform.localPosition = new Vector3(1.8f, 2.2f, front + 1.15f);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.66f, 0.35f);
                light.range = 9f;
                light.intensity = 1.7f;
                light.shadows = LightShadows.None;
                light.gameObject.AddComponent<TorchFlicker>();
            }
            if (index == 7)
            {
                var chest = PlaceChest(null, false, "ta_bau_palafitas", shack.TransformPoint(new Vector3(0f, 0f, -1.2f)), yaw);
                if (chest != null) content.Add(chest.transform);
            }
        }

        private static void Dock(List<Transform> content, FloodedMaterials m, Vector2 from, Vector2 dir, float deck, System.Random rng)
        {
            const float len = 8f;
            var mid = from + dir * (len * 0.5f);
            var yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            var dock = Group("Píer", new Vector3(mid.x, deck - 0.3f, mid.y), yaw);
            content.Add(dock);
            K.Box(dock, "Tábuas", new Vector3(0, -0.06f, 0), new Vector3(1.6f, 0.12f, len), m.Wood, default, true, 1.5f);
            for (var t = -len * 0.5f; t <= len * 0.5f; t += 2.5f)
                foreach (var s in new[] { -0.7f, 0.7f })
                    Posts(dock, m, dock.TransformPoint(new Vector3(s, -0.1f, t)), 0.16f);
            // Barco a remo amarrado ao lado
            var boat = Group("Barco a Remo", Vector3.zero, 0f, dock);
            boat.localPosition = new Vector3(1.9f, G.WaterLevel - (deck - 0.3f) + 0.05f, (float)(rng.NextDouble() * 2 - 1));
            boat.localRotation = Quaternion.Euler(0, (float)(rng.NextDouble() * 16 - 8), (float)(rng.NextDouble() * 4 - 2));
            K.Box(boat, "Fundo", new Vector3(0, -0.15f, 0), new Vector3(1f, 0.08f, 3.2f), m.WoodDark, default, true, 1f);
            K.Box(boat, "Costado E", new Vector3(-0.55f, 0.05f, 0), new Vector3(0.07f, 0.45f, 3.3f), m.Wood, new Vector3(0, 0, -14f), true, 1f);
            K.Box(boat, "Costado D", new Vector3(0.55f, 0.05f, 0), new Vector3(0.07f, 0.45f, 3.3f), m.Wood, new Vector3(0, 0, 14f), true, 1f);
            K.Box(boat, "Proa", new Vector3(0, 0.05f, 1.7f), new Vector3(1.1f, 0.45f, 0.07f), m.Wood, new Vector3(-20f, 0, 0), true, 1f);
            K.Box(boat, "Popa", new Vector3(0, 0.05f, -1.7f), new Vector3(1.1f, 0.45f, 0.07f), m.Wood, new Vector3(20f, 0, 0), true, 1f);
            K.Box(boat, "Banco", new Vector3(0, 0.08f, 0), new Vector3(1f, 0.06f, 0.3f), m.Wood, default, false, 1f);
            K.Box(boat, "Remo", new Vector3(0.2f, 0.18f, -0.4f), new Vector3(0.06f, 0.04f, 2.2f), m.WoodDark, new Vector3(0, 25f, 0), false, 1f);
            K.Box(dock, "Amarra", new Vector3(1.2f, 0.1f, 0f), new Vector3(1.2f, 0.03f, 0.03f), m.Rope, new Vector3(0, 0, -25f), false, 1f);
        }

        // ================================================================== brejo profundo e arena

        public static void DeepSwamp(List<Transform> content, FloodedMaterials m)
        {
            var rng = new System.Random(808);
            // Raízes retorcidas espalhadas (arcos de troncos meio afundados)
            for (var i = 0; i < 26; i++)
            {
                var a = (float)rng.NextDouble() * Mathf.PI * 2f;
                var r = 45f + (float)rng.NextDouble() * 160f;
                var x = G.Swamp.x + Mathf.Cos(a) * r;
                var z = G.Swamp.y + Mathf.Sin(a) * r;
                if (G.RoadDistance(x, z) < 6f || Vector2.Distance(new Vector2(x, z), G.Arena) < G.ArenaRadius + 6f) continue;
                var root = Group("Raízes Retorcidas", G.P(x, z, -0.2f), (float)rng.NextDouble() * 360f);
                content.Add(root);
                GnarledRoots(root, m, rng, 3 + rng.Next(3), 2.5f + (float)rng.NextDouble() * 2f);
            }

            // Arena: clareira redonda, anel de pilares arruinados e raízes, entrada a nordeste.
            var arena = Group("Arena do Ancião do Brejo", G.P(G.Arena.x, G.Arena.y));
            content.Add(arena);
            const float gapAngle = 45f;   // nordeste (de onde chega a trilha); ângulo medido de +X para +Z
            for (var i = 0; i < 16; i++)
            {
                var ang = i * 22.5f;
                if (Mathf.Abs(Mathf.DeltaAngle(ang, gapAngle)) < 20f) continue;   // abertura voltada para a trilha
                var dir = new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad), 0f, Mathf.Sin(ang * Mathf.Deg2Rad));
                var pos = dir * (G.ArenaRadius + 1.5f);
                if (i % 2 == 0)
                {
                    var hgt = 3.5f + (float)rng.NextDouble() * 5f;
                    var broken = rng.NextDouble() < 0.35;
                    K.Box(arena, "Pilar Arruinado", pos + Vector3.up * (hgt * 0.5f - 0.4f), new Vector3(1.7f, hgt, 1.7f), m.Stone,
                        new Vector3(((float)rng.NextDouble() - 0.5f) * 8f, ang, ((float)rng.NextDouble() - 0.5f) * 8f), true, 2f);
                    K.Box(arena, "Capitel", pos + Vector3.up * (hgt - 0.2f), new Vector3(2.1f, 0.5f, 2.1f), m.Stone, new Vector3(0, ang + 10f, 0), false, 1.5f);
                    if (broken)
                        K.Box(arena, "Pedaço Caído", pos * 0.88f + Vector3.up * 0.5f, new Vector3(1.6f, 1.6f, 3.2f), m.Stone,
                            new Vector3(0f, ang + 70f, 85f), true, 2f);
                }
                else
                {
                    var holder = Group("Raízes do Anel", Vector3.zero, ang, arena);
                    holder.localPosition = pos;
                    GnarledRoots(holder, m, rng, 4, 4f);
                }
            }
            // Pedestal musgoso no centro (onde o Ancião dorme)
            K.Box(arena, "Laje Afundada", new Vector3(0, -0.05f, 0), new Vector3(9f, 0.3f, 9f), m.Stone, new Vector3(0, 12f, 0), true, 2f);
            var boss = BossContentFactory.Place(null, G.P(G.Arena.x, G.Arena.y), 45f);   // olhando para a entrada
            if (boss != null) content.Add(boss.transform);
            Discoverable(arena, "ta_arena", "Clareira do Ancião", false, 60, G.P(G.Arena.x, G.Arena.y, 3f), new Vector3(G.ArenaRadius * 2f, 6f, G.ArenaRadius * 2f));

            // Espíritos do Musgo escondidos no brejo: ao morrer soltam o musgo que acorda o Ancião.
            for (var i = 0; i < 8; i++)
            {
                var a = i * 45f * Mathf.Deg2Rad + 0.3f;
                var r = 55f + (i % 3) * 30f;
                var x = G.Swamp.x + Mathf.Cos(a) * r;
                var z = G.Swamp.y + Mathf.Sin(a) * r;
                var spirit = MossSpiritContentFactory.Place(null, G.P(x, z), i * 47f);
                if (spirit != null) content.Add(spirit.transform);
            }
            var chest = PlaceChest(null, false, "ta_bau_brejo", G.P(G.Swamp.x + 95f, G.Swamp.y - 70f), 200f);
            if (chest != null) content.Add(chest.transform);

            var swamp = Group("Brejo Profundo (local)", G.P(G.Swamp.x, G.Swamp.y));
            content.Add(swamp);
            Discoverable(swamp, "ta_brejo", "Brejo Profundo", false, 30, G.P(G.Swamp.x, G.Swamp.y, 3f), new Vector3(200f, 10f, 200f));
        }

        private static void GnarledRoots(Transform parent, FloodedMaterials m, System.Random rng, int count, float span)
        {
            for (var i = 0; i < count; i++)
            {
                var yaw = (float)rng.NextDouble() * 360f;
                var thick = 0.25f + (float)rng.NextDouble() * 0.3f;
                // cada raiz é um arco de 3 segmentos que sai da lama, sobe e afunda de novo
                var a = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                var pts = new[] { -a * span * 0.5f, -a * span * 0.18f + Vector3.up * span * 0.35f, a * span * 0.18f + Vector3.up * span * 0.38f, a * span * 0.5f };
                for (var s = 0; s < 3; s++)
                {
                    var p0 = pts[s]; var p1 = pts[s + 1];
                    var c = (p0 + p1) * 0.5f;
                    var d = p1 - p0;
                    var seg = K.Box(parent, "Raiz", c, new Vector3(thick, thick, d.magnitude + thick), m.Bark, default, true, 1.5f);
                    seg.transform.localRotation = Quaternion.LookRotation(d) * Quaternion.Euler(0, 0, (float)rng.NextDouble() * 90f);
                }
            }
        }

        // ================================================================== valas de corpos

        public static void BodyDisposal(List<Transform> content, FloodedMaterials m)
        {
            var names = new[] { ("ta_valas_capital", "Valas da Capital"), ("ta_valas_leste", "Valas do Leste"), ("ta_valas_brejo", "Valas do Brejo") };
            var rng = new System.Random(66);
            for (var s = 0; s < G.Disposals.Length; s++)
            {
                var site = G.Disposals[s];
                var root = Group(names[s].Item2, G.P(site.C.x, site.C.y));
                content.Add(root);
                foreach (var pit in site.Pits)
                {
                    var lime = Mathf.Abs(pit.Size.x - pit.Size.y) < 0.6f;
                    var floor = G.Height(pit.C.x, pit.C.y);
                    var pitGroup = Group(lime ? "Poço de Cal" : "Vala Aberta", new Vector3(pit.C.x, floor, pit.C.y), pit.Yaw);   // mesmo giro da cova escavada no relevo
                    pitGroup.SetParent(root, true);
                    if (!lime)
                    {
                        // corpos embrulhados em juta no fundo da vala
                        var n = Mathf.FloorToInt(pit.Size.x / 1.1f);
                        for (var i = 0; i < n; i++)
                        {
                            var body = Part(pitGroup, "Corpo Embrulhado", PrimitiveType.Capsule,
                                new Vector3(-pit.Size.x * 0.5f + 0.7f + i * 1.05f, 0.22f, ((float)rng.NextDouble() - 0.5f) * 1.2f),
                                new Vector3(0.45f, 0.9f, 0.38f), m.Burlap, new Vector3(90f, (float)rng.NextDouble() * 30f - 15f, 90f), false);
                            body.isStatic = true;
                        }
                        // monte de terra ao lado da vala
                        var mound = Part(root, "Monte de Terra", PrimitiveType.Sphere,
                            root.InverseTransformPoint(new Vector3(pit.C.x, floor + 1.2f, pit.C.y) + Quaternion.Euler(0, pit.Yaw, 0) * new Vector3(0, 0, pit.Size.y + 1.5f)),
                            new Vector3(pit.Size.x * 0.8f, 1.6f, 2.6f), m.Dirt, new Vector3(0, pit.Yaw, 0), true);
                        mound.isStatic = true;
                    }
                    else
                    {
                        K.Box(pitGroup, "Cal", new Vector3(0, 0.15f, 0), new Vector3(pit.Size.x * 0.8f, 0.2f, pit.Size.y * 0.8f), m.Lime, default, false, 1.5f);
                        for (var i = 0; i < 4; i++)
                            K.Box(root, "Saco de Cal", root.InverseTransformPoint(new Vector3(pit.C.x + 3.2f, G.Height(pit.C.x + 3.2f, pit.C.y) + 0.25f, pit.C.y - 1.5f + i * 0.9f)),
                                new Vector3(0.7f, 0.45f, 0.5f), m.Cloth, new Vector3(0, i * 23f, 4f), true, 1f);
                    }
                }
                // Carrinhos de mão, pás, placas tortas de aviso, mais corpos esperando
                for (var i = 0; i < 2; i++)
                {
                    var a = (float)rng.NextDouble() * 360f;
                    var p = site.C + (Vector2)(Quaternion.Euler(0, 0, a) * Vector3.right) * (site.R * 0.6f);
                    var barrow = Group("Carrinho de Mão", G.P(p.x, p.y), a);
                    barrow.SetParent(root, true);
                    Wheelbarrow(barrow, m, rng.NextDouble() < 0.6);
                }
                for (var i = 0; i < 3; i++)
                {
                    var a = i * 120f + (float)rng.NextDouble() * 40f;
                    var p = site.C + (Vector2)(Quaternion.Euler(0, 0, a) * Vector3.right) * (site.R * 0.9f);
                    var sign = Group("Placa de Aviso", G.P(p.x, p.y), a + 90f);
                    sign.SetParent(root, true);
                    K.Box(sign, "Poste", new Vector3(0, 1f, 0), new Vector3(0.12f, 2f, 0.12f), m.WoodDark, new Vector3(0, 0, ((float)rng.NextDouble() - 0.5f) * 20f), true, 1f);
                    K.Box(sign, "Tábua", new Vector3(0, 1.7f, 0.07f), new Vector3(1f, 0.55f, 0.05f), m.Wood, new Vector3(0, 0, ((float)rng.NextDouble() - 0.5f) * 30f), false, 1f);
                    K.Box(sign, "Marca", new Vector3(0, 1.7f, 0.1f), new Vector3(0.75f, 0.08f, 0.02f), m.Ember, new Vector3(0, 0, 45f), false, 1f);
                    K.Box(sign, "Marca", new Vector3(0, 1.7f, 0.1f), new Vector3(0.75f, 0.08f, 0.02f), m.Ember, new Vector3(0, 0, -45f), false, 1f);
                }
                for (var i = 0; i < 2; i++)
                {
                    var shovel = Group("Pá", G.P(site.C.x + 2f + i, site.C.y - 3f), (float)rng.NextDouble() * 360f);
                    shovel.SetParent(root, true);
                    K.Box(shovel, "Cabo", new Vector3(0, 0.7f, 0), new Vector3(0.05f, 1.4f, 0.05f), m.WoodDark, new Vector3(12f, 0, 0), false, 1f);
                    K.Box(shovel, "Lâmina", new Vector3(0, 0.08f, -0.15f), new Vector3(0.25f, 0.3f, 0.03f), m.Iron, new Vector3(12f, 0, 0), false, 1f);
                }
                content.Add(Enemy(EnemyContentFactory.Skeleton, null, G.P(site.C.x + 4f, site.C.y + 3f), rng.Next(360))?.transform);
                content.Add(Enemy(EnemyContentFactory.Skeleton, null, G.P(site.C.x - 5f, site.C.y - 2f), rng.Next(360))?.transform);
                Discoverable(root, names[s].Item1, names[s].Item2, false, 25, G.P(site.C.x, site.C.y, 2f), new Vector3(site.R * 2f, 5f, site.R * 2f));
            }
        }

        private static void Wheelbarrow(Transform t, FloodedMaterials m, bool loaded)
        {
            K.Box(t, "Caçamba", new Vector3(0, 0.55f, 0), new Vector3(0.8f, 0.35f, 1.1f), m.Wood, new Vector3(-6f, 0, 0), true, 1f);
            var wheel = Part(t, "Roda", PrimitiveType.Cylinder, new Vector3(0, 0.28f, 0.75f), new Vector3(0.55f, 0.04f, 0.55f), m.WoodDark, new Vector3(0, 0, 90), false);
            wheel.isStatic = true;
            K.Box(t, "Braço E", new Vector3(-0.3f, 0.5f, -0.6f), new Vector3(0.06f, 0.06f, 1.4f), m.WoodDark, new Vector3(-10f, 0, 0), false, 1f);
            K.Box(t, "Braço D", new Vector3(0.3f, 0.5f, -0.6f), new Vector3(0.06f, 0.06f, 1.4f), m.WoodDark, new Vector3(-10f, 0, 0), false, 1f);
            if (loaded)
            {
                var b = Part(t, "Corpo Embrulhado", PrimitiveType.Capsule, new Vector3(0, 0.85f, 0), new Vector3(0.42f, 0.85f, 0.36f), m.Burlap, new Vector3(90f, 0, 0), false);
                b.isStatic = true;
            }
        }

        // ================================================================== peste

        public static void PlagueZones(List<Transform> content, FloodedMaterials m)
        {
            var names = new[] { ("ta_peste_1", "Charco Negro"), ("ta_peste_2", "Poça da Peste"), ("ta_peste_3", "Lagoa Envenenada"), ("ta_peste_4", "Fosso Negro") };
            var rng = new System.Random(13);
            for (var i = 0; i < G.PlagueZones.Length; i++)
            {
                var z = G.PlagueZones[i];
                var zone = Group(names[i].Item2, new Vector3(z.x, G.WaterLevel, z.y));
                content.Add(zone);
                // Água negra, um pouco acima da água comum
                var water = new GameObject("Água da Peste");
                water.transform.SetParent(zone, false);
                water.transform.localPosition = Vector3.up * 0.035f;
                water.AddComponent<MeshFilter>().sharedMesh = K.Disc(z.z * 1.06f, 40, 8f);
                var wr = water.AddComponent<MeshRenderer>();
                wr.sharedMaterial = m.PlagueWater;
                wr.shadowCastingMode = ShadowCastingMode.Off;
                zone.gameObject.AddComponent<PlagueWater>().EditorSetRadius(z.z * 0.95f);
                Miasma(zone, m, z.z);

                // Árvores mortas e ossos em volta
                for (var k = 0; k < 7; k++)
                {
                    var a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    var r = z.z * (1.05f + (float)rng.NextDouble() * 0.35f);
                    var x = z.x + Mathf.Cos(a) * r;
                    var zz = z.y + Mathf.Sin(a) * r;
                    var tree = Group("Árvore Morta", G.P(x, zz), (float)rng.NextDouble() * 360f);
                    tree.SetParent(zone, true);
                    var h = 3f + (float)rng.NextDouble() * 3f;
                    K.Box(tree, "Tronco", new Vector3(0, h * 0.5f, 0), new Vector3(0.35f, h, 0.35f), m.Bark, new Vector3(((float)rng.NextDouble() - 0.5f) * 14f, 0, ((float)rng.NextDouble() - 0.5f) * 14f), true, 1.5f);
                    K.Box(tree, "Galho", new Vector3(0.4f, h * 0.75f, 0), new Vector3(0.14f, h * 0.45f, 0.14f), m.Bark, new Vector3(0, 0, -45f), false, 1.5f);
                    K.Box(tree, "Galho", new Vector3(-0.3f, h * 0.85f, 0.2f), new Vector3(0.12f, h * 0.35f, 0.12f), m.Bark, new Vector3(25f, 0, 50f), false, 1.5f);
                }
                for (var k = 0; k < 10; k++)
                {
                    var a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    var r = z.z * (0.95f + (float)rng.NextDouble() * 0.25f);
                    var bone = Part(zone, "Osso", PrimitiveType.Cylinder,
                        zone.InverseTransformPoint(G.P(z.x + Mathf.Cos(a) * r, z.y + Mathf.Sin(a) * r, 0.05f)),
                        new Vector3(0.06f, 0.22f, 0.06f), m.Bone, new Vector3(90f, (float)rng.NextDouble() * 180f, 0f), false);
                    bone.isStatic = true;
                }
                content.Add(Enemy(EnemyContentFactory.Skeleton, null, G.P(z.x + z.z * 1.15f, z.y), rng.Next(360))?.transform);
                Discoverable(zone, names[i].Item1, names[i].Item2, false, 20, new Vector3(z.x, G.WaterLevel + 1.5f, z.y), new Vector3(z.z * 2.2f, 5f, z.z * 2.2f));
            }
        }

        private static void Miasma(Transform parent, FloodedMaterials m, float radius)
        {
            var go = new GameObject("Miasma");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * 0.1f;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.06f, 0.05f, 0.07f, 0.35f), new Color(0.14f, 0.12f, 0.1f, 0.25f));
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 180;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.01f;
            var em = ps.emission;
            em.rateOverTime = Mathf.Clamp(radius * 0.9f, 10f, 30f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * 0.85f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.6f, 0.65f, 0.5f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0.7f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.25f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = m.Miasma;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.sortingFudge = 10f;
        }

        // ================================================================== andarilhos

        public static void Wanderers(List<Transform> content)
        {
            var packs = new[] { new Vector2(250f, -120f), new Vector2(60f, 40f), new Vector2(-30f, 250f), new Vector2(330f, 250f), new Vector2(-380f, -60f) };
            var rng = new System.Random(5);
            foreach (var p in packs)
                for (var i = 0; i < 3; i++)
                    content.Add(Enemy(EnemyContentFactory.Wolf, null, G.P(p.x + i * 2.5f, p.y + (i % 2) * 2f), rng.Next(360))?.transform);
        }
    }
}
