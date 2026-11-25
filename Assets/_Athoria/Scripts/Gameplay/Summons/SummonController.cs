using System;
using System.Collections.Generic;
using Athoria.Core.Events;
using UnityEngine;
using UnityEngine.AI;

namespace Athoria.Gameplay.Summons
{
    public readonly struct SpiritSummoned : IGameEvent
    {
        public readonly SpiritCompanion Spirit;
        public SpiritSummoned(SpiritCompanion spirit) => Spirit = spirit;
    }

    public readonly struct SpiritDismissed : IGameEvent
    {
        public readonly SpiritCompanion Spirit;
        public SpiritDismissed(SpiritCompanion spirit) => Spirit = spirit;
    }

    /// <summary>
    /// Invocações da jogadora: cria o espírito perto dela, reserva os Mantras e
    /// avisa quando ele deixa o mundo físico (o que libera os Mantras).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SummonController : MonoBehaviour
    {
        [SerializeField, Tooltip("Onde o espírito surge, relativo à invocadora (X = lado, Z = frente).")]
        private Vector3 spawnOffset = new(0.9f, 0f, 1.6f);

        private readonly List<SpiritCompanion> _active = new();
        private MantraPool _mantras;

        public IReadOnlyList<SpiritCompanion> Active => _active;
        public event Action<SpiritCompanion> SpiritEnded;

        private MantraPool Mantras => _mantras != null ? _mantras : (_mantras = GetComponent<MantraPool>());

        public bool CanSummon(SpiritData data, out string reason)
        {
            reason = null;
            if (data == null || data.Prefab == null || data.Prefab.GetComponent<SpiritCompanion>() == null)
            {
                reason = "Este espírito ainda não tem manifestação configurada";
                return false;
            }
            if (Mantras == null || !Mantras.CanReserve(data.MantraCost))
            {
                reason = "Mantras insuficientes";
                return false;
            }
            return true;
        }

        public SpiritCompanion Summon(SpiritData data, object source)
        {
            if (!CanSummon(data, out _)) return null;
            var pos = transform.TransformPoint(spawnOffset);
            if (NavMesh.SamplePosition(pos, out var hit, 3f, NavMesh.AllAreas)) pos = hit.position;
            else if (NavMesh.SamplePosition(transform.position, out hit, 3f, NavMesh.AllAreas)) pos = hit.position;

            var flat = transform.forward;
            flat.y = 0f;
            var go = Instantiate(data.Prefab, pos, Quaternion.LookRotation(flat.sqrMagnitude > 0.01f ? flat : Vector3.forward));
            var spirit = go.GetComponent<SpiritCompanion>();
            Mantras.Reserve(spirit, data.MantraCost);
            _active.Add(spirit);
            spirit.Ended += OnEnded;
            spirit.Manifest(data, transform, source);
            EventBus.Publish(new SpiritSummoned(spirit));
            return spirit;
        }

        /// <summary>Encerra todas as invocações (cada uma passa pelo desaparecimento normal).</summary>
        public void DismissAll()
        {
            foreach (var s in _active.ToArray()) if (s != null) s.Dismiss();
        }

        private void OnEnded(SpiritCompanion spirit)
        {
            spirit.Ended -= OnEnded;
            if (!this) return;   // a invocadora já saiu da cena (fim do jogo / troca de cena)
            _active.Remove(spirit);
            if (Mantras != null) Mantras.Release(spirit);
            EventBus.Publish(new SpiritDismissed(spirit));
            SpiritEnded?.Invoke(spirit);
        }
    }
}
