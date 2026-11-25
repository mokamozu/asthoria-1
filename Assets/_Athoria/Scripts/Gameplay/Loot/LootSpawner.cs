using System.Collections.Generic;
using Athoria.Gameplay.World;
using UnityEngine;

namespace Athoria.Gameplay.Loot
{
    /// <summary>Faz o loot "saltar" para fora (de um inimigo ou de um baú) e cair no chão.</summary>
    public static class LootSpawner
    {
        private static readonly List<LootDrop> Buffer = new();

        /// <summary>Sorteia a tabela e espalha os itens. Devolve quantos itens/montes caíram.</summary>
        public static int RollAndSpawn(LootTable table, Vector3 origin, Vector3 forward, float spread = 1.6f, float arc = 0f)
        {
            LootRoller.Roll(table, Buffer);
            Spawn(Buffer, origin, forward, spread, arc);
            return Buffer.Count;
        }

        /// <param name="arc">0 = espalha em volta; maior que 0 = leque à frente (graus).</param>
        public static void Spawn(List<LootDrop> drops, Vector3 origin, Vector3 forward, float spread = 1.6f, float arc = 0f)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            for (var i = 0; i < drops.Count; i++)
            {
                var d = drops[i];
                var angle = arc > 0f
                    ? Mathf.Lerp(-arc * 0.5f, arc * 0.5f, drops.Count == 1 ? 0.5f : i / (float)(drops.Count - 1)) + Random.Range(-8f, 8f)
                    : i * (360f / Mathf.Max(1, drops.Count)) + Random.Range(-20f, 20f);
                var dir = Quaternion.Euler(0f, angle, 0f) * forward;
                var distance = spread * Random.Range(0.6f, 1f);
                // Salto: tempo de voo ~0.55 s com gravidade 12 → velocidade horizontal = distância / tempo.
                var velocity = dir * (distance / 0.55f) + Vector3.up * Random.Range(3f, 4f);
                var rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                var item = d.IsGold
                    ? WorldItem.SpawnGold(d.Gold, origin)
                    : WorldItem.Spawn(d.Item, d.Quantity, origin, rotation);
                item.Pop(velocity);
            }
        }
    }
}
