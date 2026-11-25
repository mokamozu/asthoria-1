using System.Collections.Generic;
using Athoria.Core.Events;
using Athoria.Gameplay.AI;
using UnityEngine;

namespace Athoria.Gameplay.World
{
    /// <summary>
    /// O que já aconteceu no mundo neste jogo: baús abertos, locais descobertos,
    /// chefes derrotados. Vai para o save; os objetos consultam ao aparecer
    /// (inclusive pedaços do mapa carregados depois).
    /// </summary>
    public static class WorldState
    {
        private static readonly HashSet<string> Flags = new();

        public static string ChestKey(string id) => "bau:" + id;
        public static string LocationKey(string id) => "local:" + id;
        public static string BossKey(string id) => "chefe:" + id;

        public static bool Has(string key) => !string.IsNullOrEmpty(key) && Flags.Contains(key);
        public static void Set(string key) { if (!string.IsNullOrEmpty(key)) Flags.Add(key); }
        public static IEnumerable<string> All => Flags;

        /// <summary>Novo jogo.</summary>
        public static void Clear() => Flags.Clear();

        /// <summary>Carregar save.</summary>
        public static void SetAll(IEnumerable<string> flags)
        {
            Flags.Clear();
            if (flags == null) return;
            foreach (var f in flags) Set(f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Flags.Clear();

        // O EventBus é limpo no início do Play; assina logo depois.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Listen()
        {
            EventBus.Subscribe<ChestOpened>(e => Set(ChestKey(e.Id)));
            EventBus.Subscribe<LocationDiscovered>(e => Set(LocationKey(e.Id)));
            EventBus.Subscribe<EnemyKilled>(e =>
            {
                if (e.Enemy != null && !string.IsNullOrEmpty(e.Enemy.UniqueId)) Set(BossKey(e.Enemy.UniqueId));
            });
        }
    }
}
