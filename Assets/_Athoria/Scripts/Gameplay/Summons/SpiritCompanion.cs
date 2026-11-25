using System;
using System.Collections;
using Athoria.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Athoria.Gameplay.Summons
{
    /// <summary>
    /// Ciclo de vida de um espírito manifestado: surge, vive enquanto tem vida e,
    /// derrotado (ou dispensado), toca a morte, se dissipa e avisa UMA única vez
    /// que a invocação terminou (evento Ended) antes de sair da cena.
    /// A IA fica no SpiritBrain e a apresentação no SpiritAnimator.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(NavMeshAgent), typeof(PlayerAlly))]
    public sealed class SpiritCompanion : MonoBehaviour, IDamageModifier
    {
        [SerializeField, Tooltip("Segundos que leva para se dissipar depois da animação de morte.")]
        private float dissipateTime = 0.5f;

        private Health _health;
        private SpiritBrain _brain;
        private SpiritAnimator _animator;
        private bool _ending;
        private bool _ended;

        public SpiritData Data { get; private set; }
        public Transform Owner { get; private set; }
        public object Source { get; private set; }
        public Health Health => _health;
        public bool IsManifested => !_ending && _health != null && _health.IsAlive;

        /// <summary>Disparado uma única vez, quando o espírito deixa o mundo físico.</summary>
        public event Action<SpiritCompanion> Ended;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _brain = GetComponent<SpiritBrain>();
            _animator = GetComponent<SpiritAnimator>();
        }

        private void OnEnable() => _health.Died += OnDied;
        private void OnDisable() => _health.Died -= OnDied;

        public void Manifest(SpiritData data, Transform owner, object source)
        {
            Data = data;
            Owner = owner;
            Source = source;
            _health.SetMax(data.MaxHealth, true);
            if (data.ManifestFx != null) Effects.SpriteFx.SpawnOnGround(data.ManifestFx, transform.position);
            if (_animator != null) _animator.PlayOneShot(SpiritAnimator.Clip.Summon, data.SummonDuration);
            if (_brain != null) _brain.Begin(this);
        }

        /// <summary>Encerra a manifestação como se tivesse sido derrotado.</summary>
        public void Dismiss()
        {
            if (_ending) return;
            StartCoroutine(Vanish());
        }

        private void OnDied(DamageInfo _) => Dismiss();

        /// <summary>Golpes da própria invocadora atravessam o espírito (sem fogo amigo).</summary>
        public bool ModifyIncomingDamage(ref DamageInfo info) =>
            Owner == null || info.Source == null || !info.Source.transform.IsChildOf(Owner);

        private IEnumerator Vanish()
        {
            _ending = true;
            if (_brain != null) _brain.Stop();
            var ally = GetComponent<PlayerAlly>();
            if (ally != null) ally.enabled = false;   // inimigos param de mirar nele
            foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;

            var deathTime = Data != null ? Data.DeathDuration : 1f;
            if (_animator != null) _animator.PlayOneShot(SpiritAnimator.Clip.Death, deathTime);
            for (var t = 0f; t < deathTime; t += Time.deltaTime)
            {
                if (_animator != null) _animator.SetGlow(1f - t / Mathf.Max(0.01f, deathTime));
                yield return null;
            }

            // Dissipação: encolhe e some
            var start = transform.localScale;
            for (var t = 0f; t < dissipateTime; t += Time.deltaTime)
            {
                transform.localScale = start * (1f - t / dissipateTime);
                yield return null;
            }
            if (Data != null && Data.VanishFx != null) Effects.SpriteFx.SpawnOnGround(Data.VanishFx, transform.position);
            End();
            Destroy(gameObject);
        }

        private void End()
        {
            if (_ended) return;
            _ended = true;
            Ended?.Invoke(this);
        }

        // Saiu da cena sem passar pela morte (troca de área, cena descarregada): a invocação termina do mesmo jeito.
        private void OnDestroy() => End();
    }
}
