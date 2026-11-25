using System.Collections.Generic;
using Athoria.Core.Events;
using Athoria.Gameplay.World;
using UnityEngine;

namespace Athoria.Gameplay.Bosses
{
    /// <summary>
    /// Mecânica de descoberta: o chefe dorme disfarçado de pedra até que a jogadora jogue
    /// no chão, perto dele, a quantidade exigida do item (40 musgos). Consome exatamente
    /// essa quantidade (o que sobrar volta ao chão) e desperta o chefe.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MossAncientBrain))]
    public sealed class MossAwakener : MonoBehaviour
    {
        [SerializeField, Tooltip("Intervalo entre as verificações (s).")] private float checkInterval = 0.3f;

        private MossAncientBrain _brain;
        private float _nextCheck;
        private int _lastCount;
        private readonly List<WorldItem> _found = new();

        private void Awake() => _brain = GetComponent<MossAncientBrain>();

        private void Update()
        {
            if (Time.time < _nextCheck || _brain == null || !_brain.CanAwaken || _brain.Data == null || _brain.Data.wakeItem == null) return;
            _nextCheck = Time.time + checkInterval;

            var data = _brain.Data;
            _found.Clear();
            var total = 0;
            foreach (var wi in FindObjectsByType<WorldItem>(FindObjectsSortMode.None))
            {
                if (wi == null || wi.Item != data.wakeItem) continue;
                var d = wi.transform.position - transform.position; d.y = 0f;
                if (d.magnitude > data.activationRadius) continue;
                _found.Add(wi);
                total += wi.Quantity;
            }

            if (total != _lastCount && total > 0 && total < data.wakeItemAmount)
                EventBus.Publish(new GameMessage($"A pedra antiga parece farejar o {data.wakeItem.DisplayName.ToLowerInvariant()}… ({total}/{data.wakeItemAmount})"));
            _lastCount = total;
            if (total < data.wakeItemAmount) return;

            Consume(data.wakeItemAmount);
            _brain.Awaken();
            enabled = false;
        }

        private void Consume(int amount)
        {
            _found.Sort((a, b) => (a.transform.position - transform.position).sqrMagnitude
                .CompareTo((b.transform.position - transform.position).sqrMagnitude));
            foreach (var wi in _found)
            {
                if (amount <= 0) break;
                var take = Mathf.Min(amount, wi.Quantity);
                amount -= take;
                var left = wi.Quantity - take;
                if (left > 0) WorldItem.Spawn(wi.Item, left, wi.transform.position, wi.transform.rotation);   // o que sobrou fica no chão
                Destroy(wi.gameObject);
            }
        }
    }
}
