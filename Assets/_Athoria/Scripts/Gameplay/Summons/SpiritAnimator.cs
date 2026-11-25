using System;
using Athoria.Gameplay.AI;
using UnityEngine;

namespace Athoria.Gameplay.Summons
{
    /// <summary>
    /// Apresentação do espírito. Com um Animator (modelo com clipes Idle, Walk, Run,
    /// Attack, Hurt, Death, Summon) troca os estados pelo nome; sem ele, usa o
    /// EnemyAnimator procedural como substituto provisório. Também controla o brilho
    /// emissivo (olhos, lágrimas, chamas) que se apaga na morte.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpiritAnimator : MonoBehaviour
    {
        public enum Clip { Idle, Walk, Run, Attack, Hurt, Death, Summon }

        [SerializeField] private Animator animator;
        [SerializeField, Tooltip("Substituto procedural quando ainda não há clipes.")] private EnemyAnimator fallback;
        [SerializeField, Tooltip("Acima disso (m/s) anda; abaixo, parado.")] private float walkThreshold = 0.25f;
        [SerializeField, Tooltip("Acima disso (m/s) corre.")] private float runThreshold = 3.2f;
        [SerializeField, Min(0f)] private float crossFade = 0.1f;
        [SerializeField, Tooltip("Renderers com material emissivo (olhos, lágrimas, chamas) que se apaga na morte.")]
        private Renderer[] glowRenderers = Array.Empty<Renderer>();
        [SerializeField, Min(0f), Tooltip("Quadros por segundo da animação (0 = suave). Os jogos de PS1 animavam a uns 15.")]
        private float animationFps = 15f;
        [SerializeField, Min(1f), Tooltip("Brilho extra dos olhos durante o ataque.")] private float attackGlow = 1.8f;

        private struct GlowSlot { public Renderer Renderer; public int Index; public Color Base; }

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private Clip _locomotion = (Clip)(-1);
        private float _lockedUntil;
        private bool _dead;
        private float _glow = 1f;
        private float _boostUntil;
        private float _stepTime;
        private GlowSlot[] _glowSlots = Array.Empty<GlowSlot>();
        private MaterialPropertyBlock _block;
        private SpiritLiquid _liquid;

        private void Awake()
        {
            _liquid = GetComponent<SpiritLiquid>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (fallback == null) fallback = GetComponentInChildren<EnemyAnimator>();
            _block = new MaterialPropertyBlock();
            var slots = new System.Collections.Generic.List<GlowSlot>();
            foreach (var r in glowRenderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].HasProperty(EmissionId)) continue;
                    var c = mats[i].GetColor(EmissionId);
                    if (c.maxColorComponent > 0.01f) slots.Add(new GlowSlot { Renderer = r, Index = i, Base = c });
                }
            }
            _glowSlots = slots.ToArray();
            // Animação "travada" de PS1: o Animator é avaliado em passos fixos (Update abaixo).
            if (animator != null && animationFps > 0f) animator.enabled = false;
        }

        private void Update()
        {
            if (animator != null && animationFps > 0f && animator.runtimeAnimatorController != null)
            {
                _stepTime += Time.deltaTime;
                if (_stepTime >= 1f / animationFps)
                {
                    animator.Update(_stepTime);
                    _stepTime = 0f;
                }
            }
            if (_boostUntil > 0f && Time.time >= _boostUntil)
            {
                _boostUntil = 0f;
                ApplyGlow(_glow);
            }
        }

        public void SetSpeed(float metersPerSecond)
        {
            if (fallback != null) fallback.SetSpeed(metersPerSecond);
            if (_dead || Time.time < _lockedUntil) return;
            var clip = metersPerSecond < walkThreshold ? Clip.Idle : metersPerSecond < runThreshold ? Clip.Walk : Clip.Run;
            if (clip == _locomotion) return;
            _locomotion = clip;
            CrossFade(clip);
            if (fallback != null) fallback.SetPose(EnemyAnimator.Pose.None);
        }

        /// <summary>Animação de uma vez (ataque, dano, morte, invocação); a locomoção volta depois.</summary>
        public void PlayOneShot(Clip clip, float duration)
        {
            if (_dead) return;
            _dead = clip == Clip.Death;
            if (clip == Clip.Attack)
            {
                _boostUntil = Time.time + Mathf.Max(0.05f, duration);   // olhos brilham mais no bote
                ApplyGlow(_glow * attackGlow);
            }
            _lockedUntil = Time.time + Mathf.Max(0.05f, duration);
            _locomotion = (Clip)(-1);
            CrossFade(clip);
            if (fallback == null) return;
            switch (clip)
            {
                case Clip.Attack: fallback.SetPose(EnemyAnimator.Pose.Strike, EnemyAttackPose.Bite); break;
                case Clip.Hurt: fallback.SetPose(EnemyAnimator.Pose.Stagger); break;
                case Clip.Death: fallback.SetPose(EnemyAnimator.Pose.Dead); break;
                default: fallback.SetPose(EnemyAnimator.Pose.None); break;
            }
        }

        /// <summary>1 = brilho normal, 0 = apagado.</summary>
        public void SetGlow(float amount)
        {
            if (_liquid != null) _liquid.SetIntensity(amount);   // o líquido das lágrimas seca junto
            _glow = Mathf.Clamp01(amount);
            _boostUntil = 0f;
            ApplyGlow(_glow);
        }

        private void ApplyGlow(float amount)
        {
            foreach (var s in _glowSlots)
            {
                if (s.Renderer == null) continue;
                s.Renderer.GetPropertyBlock(_block, s.Index);
                _block.SetColor(EmissionId, s.Base * amount);
                s.Renderer.SetPropertyBlock(_block, s.Index);
            }
        }

        private void CrossFade(Clip clip)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            var hash = Animator.StringToHash(clip.ToString());
            if (animator.HasState(0, hash)) animator.CrossFadeInFixedTime(hash, crossFade, 0);
        }
    }
}
