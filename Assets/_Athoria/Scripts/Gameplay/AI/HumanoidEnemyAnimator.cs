using System;
using System.Collections.Generic;
using UnityEngine;

namespace Athoria.Gameplay.AI
{
    /// <summary>
    /// Animação de inimigos humanoides com os clipes da Universal Animation Library
    /// (Quaternius, CC0). Os estados do Animator Controller têm o mesmo nome dos clipes.
    /// A IA diz quanto dura cada fase do ataque (preparação, golpe, recuperação) e o
    /// clipe é acelerado/freado para o impacto cair exatamente no fim da preparação.
    /// O Animator avança em passos (15 q/s por padrão) para o visual de PS1.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HumanoidEnemyAnimator : EnemyAnimatorBase
    {
        [Serializable]
        public struct AttackClip
        {
            public EnemyAttackPose pose;
            [Tooltip("Estado/clipe do golpe (preparação + impacto).")] public string clip;
            [Range(0.05f, 0.95f), Tooltip("Momento do impacto no clipe (0-1).")] public float impact;
            [Tooltip("Clipe de recuperação (opcional; vazio = continua o mesmo clipe).")] public string recover;
        }

        [SerializeField] private Animator animator;
        [Header("Locomoção")]
        [SerializeField] private string idle = "Sword_Idle";
        [SerializeField] private string walk = "Walk_Loop";
        [SerializeField] private string run = "Jog_Fwd_Loop";
        [SerializeField, Tooltip("Velocidade (m/s) em que o clipe de andar não patina.")] private float walkClipSpeed = 1.4f;
        [SerializeField, Tooltip("Velocidade (m/s) em que o clipe de correr não patina.")] private float runClipSpeed = 3.6f;
        [SerializeField, Tooltip("Acima disso (m/s) usa o clipe de correr.")] private float runThreshold = 2.6f;
        [Header("Combate")]
        [SerializeField] private AttackClip[] attacks = Array.Empty<AttackClip>();
        [SerializeField] private string block = "Sword_Block";
        [SerializeField, Range(0f, 1f), Tooltip("Ponto do clipe de defesa em que ele congela com a guarda alta.")] private float blockHold = 0.3f;
        [SerializeField] private string stagger = "Hit_Chest";
        [SerializeField] private string death = "Death01";
        [Header("Estilo")]
        [SerializeField, Tooltip("Quadros por segundo da animação (0 = suave).")] private float animationFps = 15f;
        [SerializeField] private float crossFade = 0.12f;

        private readonly Dictionary<string, float> _lengths = new();
        private readonly Dictionary<EnemyAttackPose, int> _nextAttack = new();
        private Pose _pose = Pose.None;
        private AttackClip _current;
        private string _state;
        private float _speed, _clock;
        private bool _holdBlock;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator == null) return;
            animator.applyRootMotion = false;
            if (animator.runtimeAnimatorController != null)
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                    if (clip != null) _lengths[clip.name] = clip.length;
            if (animationFps > 0f) animator.enabled = false;           // avançado à mão, em passos
            Go(idle, 0f, 1f);
        }

        public override void SetSpeed(float metersPerSecond) => _speed = metersPerSecond;

        public override void SetPose(Pose pose, EnemyAttackPose attackPose = EnemyAttackPose.Slash, float duration = 0f)
        {
            if (animator == null) return;
            var previous = _pose;
            _pose = pose;
            _holdBlock = false;
            switch (pose)
            {
                case Pose.Windup:
                {
                    _current = PickAttack(attackPose);
                    if (string.IsNullOrEmpty(_current.clip)) { _pose = Pose.None; return; }
                    var len = Length(_current.clip);
                    Go(_current.clip, 0.08f, Rate(len * _current.impact * 0.85f, duration), restart: true);
                    break;
                }
                case Pose.Strike:
                    if (previous != Pose.Windup || string.IsNullOrEmpty(_current.clip)) { _current = PickAttack(attackPose); Go(_current.clip, 0.05f, 1f, restart: true); break; }
                    animator.speed = Rate(Length(_current.clip) * 0.25f, duration);
                    break;
                case Pose.Recover:
                    if (!string.IsNullOrEmpty(_current.recover) && _lengths.ContainsKey(_current.recover))
                        Go(_current.recover, 0.1f, Rate(Length(_current.recover), duration), restart: true);
                    else if (!string.IsNullOrEmpty(_current.clip))
                        animator.speed = Rate(Length(_current.clip) * Mathf.Max(0.1f, 1f - _current.impact - 0.15f), duration);
                    break;
                case Pose.Block:
                    Go(block, crossFade, 1.4f, restart: previous != Pose.Block);
                    _holdBlock = true;
                    break;
                case Pose.Stagger:
                    Go(stagger, 0.05f, duration > 0f ? Rate(Length(stagger), duration) : 1f, restart: true);
                    break;
                case Pose.Dead:
                    Go(death, 0.1f, 1f, restart: true);
                    break;
                default:
                    _state = null;                                        // a locomoção assume no próximo quadro
                    break;
            }
        }

        public override void ResetPose()
        {
            _pose = Pose.None;
            _holdBlock = false;
            _state = null;
            if (animator != null) Go(idle, 0f, 1f, restart: true);
        }

        private void Update()
        {
            if (animator == null) return;
            if (_pose == Pose.None) Locomotion();
            if (_holdBlock && _state == block)
            {
                var info = animator.GetCurrentAnimatorStateInfo(0);
                if (!animator.IsInTransition(0) && info.normalizedTime >= blockHold) animator.speed = 0f;
            }

            if (animationFps <= 0f) return;
            _clock += Time.deltaTime;
            var step = 1f / animationFps;
            if (_clock < step) return;
            var advance = Mathf.Floor(_clock / step) * step;
            _clock -= advance;
            animator.Update(advance);
        }

        private void Locomotion()
        {
            if (_speed < 0.15f) { Go(idle, 0.2f, 1f); return; }
            if (_speed < runThreshold) { Go(walk, 0.2f, Mathf.Clamp(_speed / walkClipSpeed, 0.6f, 1.7f)); return; }
            Go(run, 0.2f, Mathf.Clamp(_speed / runClipSpeed, 0.7f, 1.6f));
        }

        private AttackClip PickAttack(EnemyAttackPose pose)
        {
            var options = new List<AttackClip>();
            foreach (var a in attacks) if (a.pose == pose) options.Add(a);
            if (options.Count == 0) foreach (var a in attacks) if (a.pose == EnemyAttackPose.Slash) options.Add(a);
            if (options.Count == 0 && attacks.Length > 0) options.Add(attacks[0]);
            if (options.Count == 0) return default;
            _nextAttack.TryGetValue(pose, out var i);
            _nextAttack[pose] = i + 1;
            return options[i % options.Count];                         // alterna os golpes (A, B, C…)
        }

        private void Go(string state, float fade, float speed, bool restart = false)
        {
            if (string.IsNullOrEmpty(state)) return;
            animator.speed = speed;
            if (state == _state && !restart) return;
            _state = state;
            if (fade <= 0f || !animator.isActiveAndEnabled && animationFps <= 0f) animator.Play(state, 0, 0f);
            else animator.CrossFadeInFixedTime(state, fade, 0, 0f);
            if (animationFps > 0f) animator.Update(0f);
        }

        private float Length(string clip) => clip != null && _lengths.TryGetValue(clip, out var l) ? l : 1f;

        private static float Rate(float clipSeconds, float wantedSeconds) =>
            wantedSeconds > 0.01f ? Mathf.Clamp(clipSeconds / wantedSeconds, 0.15f, 4f) : 1f;
    }
}
