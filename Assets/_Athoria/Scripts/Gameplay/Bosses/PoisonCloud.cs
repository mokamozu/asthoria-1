using System.Collections.Generic;
using Athoria.Gameplay.Combat;
using UnityEngine;

namespace Athoria.Gameplay.Bosses
{
    /// <summary>
    /// Nuvem de gás venenoso que fica no lugar por um tempo: quem está dentro (jogadora
    /// ou aliados) leva dano periódico e a tela da jogadora fica intoxicada.
    /// </summary>
    public sealed class PoisonCloud : MonoBehaviour
    {
        [SerializeField, Tooltip("Partículas da nuvem (param de emitir no fim).")] private ParticleSystem particles;

        private GameObject _source;
        private float _radius, _duration, _dps, _tick;
        private float _age, _nextTick;
        private bool _ending;
        private static readonly Collider[] Buffer = new Collider[24];
        private readonly HashSet<Health> _hit = new();

        public void Begin(GameObject source, float radius, float duration, float dps, float tick)
        {
            _source = source; _radius = radius; _duration = duration; _dps = dps; _tick = Mathf.Max(0.1f, tick);
            _nextTick = _tick * 0.5f;
            transform.localScale = Vector3.one * Mathf.Max(0.2f, radius / 3f);   // partículas feitas para raio 3
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_ending) return;
            if (_age >= _duration)
            {
                _ending = true;
                if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(gameObject, 2.5f);
                return;
            }
            if (_age < _nextTick) return;
            _nextTick += _tick;
            _hit.Clear();
            var center = transform.position + Vector3.up;
            var count = Physics.OverlapSphereNonAlloc(center, _radius, Buffer, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                if (_source != null && Buffer[i].transform.IsChildOf(_source.transform)) continue;
                if (!BossHitbox.IsHostile(Buffer[i], out var health) || !_hit.Add(health)) continue;
                health.ApplyDamage(new DamageInfo
                {
                    Amount = _dps * _tick, Type = DamageType.Poison, Source = _source != null ? _source : gameObject,
                    Point = health.transform.position + Vector3.up, Normal = Vector3.up, Direction = Vector3.down, StaggerPower = 0f
                });
                if (health.IsPlayer) PoisonScreenEffect.For(health.gameObject).Pulse(_tick + 0.3f);
            }
        }
    }
}
