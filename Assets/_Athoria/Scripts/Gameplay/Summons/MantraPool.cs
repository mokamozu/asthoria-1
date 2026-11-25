using System.Collections.Generic;
using Athoria.Core.Events;
using UnityEngine;

namespace Athoria.Gameplay.Summons
{
    public readonly struct MantrasChanged : IGameEvent
    {
        public readonly int Used;
        public readonly int Capacity;
        public MantrasChanged(int used, int capacity) { Used = used; Capacity = capacity; }
    }

    /// <summary>
    /// Mantras Ativos: quantas manifestações espirituais a personagem consegue sustentar.
    /// A base vem da CharacterData (Akane: 4); habilidades, itens e equipamentos somam
    /// bônus com SetBonus(fonte, valor). Cada espírito reserva o seu custo enquanto existe.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MantraPool : MonoBehaviour
    {
        private int _base;
        private readonly Dictionary<object, int> _bonus = new();
        private readonly Dictionary<object, int> _reserved = new();

        public int Capacity
        {
            get
            {
                var total = _base;
                foreach (var b in _bonus.Values) total += b;
                return Mathf.Max(0, total);
            }
        }

        public int Used
        {
            get
            {
                var total = 0;
                foreach (var r in _reserved.Values) total += r;
                return total;
            }
        }

        public int Free => Mathf.Max(0, Capacity - Used);

        private void Start() => Publish();

        public void SetBase(int slots)
        {
            _base = Mathf.Max(0, slots);
            Publish();
        }

        /// <summary>Bônus de uma fonte (item, habilidade...). 0 remove o bônus dessa fonte.</summary>
        public void SetBonus(object source, int amount)
        {
            if (source == null) return;
            if (amount == 0) _bonus.Remove(source); else _bonus[source] = amount;
            Publish();
        }

        public bool CanReserve(int cost) => cost <= Free;

        public bool Reserve(object holder, int cost)
        {
            if (holder == null || !CanReserve(cost)) return false;
            _reserved[holder] = Mathf.Max(0, cost);
            Publish();
            return true;
        }

        public void Release(object holder)
        {
            if (holder != null && _reserved.Remove(holder)) Publish();
        }

        private void Publish() => EventBus.Publish(new MantrasChanged(Used, Capacity));
    }
}
