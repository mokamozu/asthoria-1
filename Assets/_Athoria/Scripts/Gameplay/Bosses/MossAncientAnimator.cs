using System;
using UnityEngine;

namespace Athoria.Gameplay.Bosses
{
    public enum BossPose
    {
        Dormant, Awakening, Idle, Walk,
        SlamWindup, SlamHit, JumpCrouch, JumpAir, JumpLand,
        BelchInhale, Belch, QuickWindup, QuickStrike,
        Recover, Hit, Death
    }

    /// <summary>
    /// Animação procedural do Ancião do Brejo (provisória até haver um modelo animado):
    /// cada pose define ângulos das juntas e o corpo persegue a pose com inércia
    /// (pesado por padrão, rápido só no golpe rápido e nos impactos).
    /// Ângulos em graus, somados à pose de repouso de cada junta. O modelo olha para +Z.
    /// </summary>
    public sealed class MossAncientAnimator : MonoBehaviour
    {
        [Serializable]
        public struct Rig
        {
            public Transform hips, torso, chest, head, jaw;
            public Transform shoulderR, elbowR, wristR, shoulderL, elbowL, wristL;
            public Transform thighR, shinR, footR, thighL, shinL, footL;
            public Transform[] fingersR, fingersL;
            public Renderer[] eyes;
        }

        private struct Pose
        {
            public float hipsY, hipsZ, breath, chestScale, eyes, grip, jitter;
            public Vector3 torso, head, jaw, shR, elR, wrR, shL, elL, wrL, thR, shinR, footR, thL, shinL, footL;
        }

        [SerializeField] private Rig rig;
        [SerializeField, Tooltip("Rapidez com que o corpo alcança a pose (baixo = pesado).")] private float heaviness = 5f;

        public event Action Step;

        private Quaternion[] _rest;
        private Transform[] _joints;
        private Vector3 _hipsRest, _chestScaleRest;
        private Pose _current;
        private BossPose _pose = BossPose.Dormant;
        private float _poseTime, _poseDuration = 1f, _time;
        private float _eyeOpen = -1f, _lastStepPhase;
        private MaterialPropertyBlock _block;
        private Color[] _eyeBase = Array.Empty<Color>();
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        public BossPose Current => _pose;
        public float Progress => Mathf.Clamp01(_poseTime / _poseDuration);

        private void Awake()
        {
            _joints = new[]
            {
                rig.torso, rig.head, rig.jaw, rig.shoulderR, rig.elbowR, rig.wristR, rig.shoulderL, rig.elbowL, rig.wristL,
                rig.thighR, rig.shinR, rig.footR, rig.thighL, rig.shinL, rig.footL
            };
            _rest = new Quaternion[_joints.Length];
            for (var i = 0; i < _joints.Length; i++) _rest[i] = _joints[i] != null ? _joints[i].localRotation : Quaternion.identity;
            _hipsRest = rig.hips != null ? rig.hips.localPosition : Vector3.zero;
            _chestScaleRest = rig.chest != null ? rig.chest.localScale : Vector3.one;
            _block = new MaterialPropertyBlock();
            _eyeBase = new Color[rig.eyes?.Length ?? 0];
            for (var i = 0; i < _eyeBase.Length; i++)
            {
                var m = rig.eyes[i] != null ? rig.eyes[i].sharedMaterial : null;
                _eyeBase[i] = m != null && m.HasProperty(EmissionId) ? m.GetColor(EmissionId) : Color.white;
            }
            _current = Target(BossPose.Dormant, 0f, 0f);
            ApplyPose(_current);
        }

        /// <summary>Troca de pose. duration = tempo da pose (para as que avançam por progresso).</summary>
        public void Play(BossPose pose, float duration = 1f)
        {
            _pose = pose;
            _poseTime = 0f;
            _poseDuration = Mathf.Max(0.05f, duration);
        }

        /// <summary>Encaixa na pose sem transição (carregar o chefe já derrotado, por exemplo).</summary>
        public void Snap(BossPose pose)
        {
            Play(pose, 0.05f);
            _poseTime = 1f;
            _current = Target(pose, 1f, _time);
            ApplyPose(_current);
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f) return;
            _time += dt;
            _poseTime += dt;
            var target = Target(_pose, Progress, _time);
            var k = 1f - Mathf.Exp(-Rate(_pose) * dt);
            _current = Blend(_current, target, k);
            ApplyPose(_current);
            if (_pose == BossPose.Walk) CheckStep();
        }

        private float Rate(BossPose pose) => pose switch
        {
            BossPose.QuickStrike => 28f,
            BossPose.QuickWindup => 14f,
            BossPose.SlamHit or BossPose.JumpLand => 20f,
            BossPose.JumpAir or BossPose.Hit => 12f,
            BossPose.Death or BossPose.Awakening or BossPose.Dormant => 3.5f,
            _ => heaviness
        };

        private void CheckStep()
        {
            var phase = Mathf.Repeat(_time * 1.1f, 1f);
            if ((_lastStepPhase < 0.25f && phase >= 0.25f) || (_lastStepPhase < 0.75f && phase >= 0.75f)) Step?.Invoke();
            _lastStepPhase = phase;
        }

        // ------------------------------------------------------------------ poses

        private static Pose Base() => new()
        {
            chestScale = 1f, eyes = 1f, grip = 0.4f,
            torso = new Vector3(18f, 0f, 0f), head = new Vector3(-14f, 0f, 0f),
            shR = new Vector3(-9f, 0f, 5f), elR = new Vector3(-20f, 0f, 0f), wrR = new Vector3(-11f, 0f, 0f),
            shL = new Vector3(-6f, 0f, -6f), elL = new Vector3(-12f, 0f, 0f), wrL = new Vector3(-8f, 0f, 0f),
            thR = new Vector3(-9f, 0f, 3f), shinR = new Vector3(14f, 0f, 0f), footR = new Vector3(-6f, 0f, 0f),
            thL = new Vector3(-9f, 0f, -3f), shinL = new Vector3(14f, 0f, 0f), footL = new Vector3(-6f, 0f, 0f),
        };

        private static Pose Sitting()
        {
            var p = Base();
            p.hipsY = -0.85f; p.eyes = 0f; p.grip = 0.2f;
            p.torso = new Vector3(42f, 0f, 0f); p.head = new Vector3(30f, 0f, 0f);
            p.thR = new Vector3(-80f, 0f, 14f); p.shinR = new Vector3(100f, 0f, 0f); p.footR = new Vector3(-20f, 0f, 0f);
            p.thL = new Vector3(-80f, 0f, -14f); p.shinL = new Vector3(100f, 0f, 0f); p.footL = new Vector3(-20f, 0f, 0f);
            p.shR = new Vector3(-25f, 0f, 8f); p.elR = new Vector3(-55f, 0f, 0f);
            p.shL = new Vector3(-25f, 0f, -8f); p.elL = new Vector3(-55f, 0f, 0f);
            return p;
        }

        private static Pose Target(BossPose pose, float p, float t)
        {
            var o = Base();
            var breath = Mathf.Sin(t * 1.3f);
            switch (pose)
            {
                case BossPose.Dormant:
                    return Sitting();

                case BossPose.Awakening:
                {
                    // 0–15%: treme · 15–55%: levanta · 45%: abre os olhos · 55–85%: grito · 85–100%: assenta
                    var sit = Sitting();
                    var rise = Ease(Seg(p, 0.15f, 0.55f));
                    o = Lerp(sit, Base(), rise);
                    o.jitter = Seg(p, 0f, 0.15f) * (1f - rise) * 1.5f;
                    o.eyes = Seg(p, 0.42f, 0.5f);
                    var scream = Seg(p, 0.55f, 0.62f) * (1f - Seg(p, 0.8f, 0.92f));
                    o.torso += new Vector3(-30f, 0f, 0f) * scream;
                    o.head += new Vector3(-38f, 0f, 0f) * scream;
                    o.jaw = new Vector3(40f, 0f, 0f) * scream;
                    o.shR += new Vector3(-30f, 0f, 55f) * scream;
                    o.shL += new Vector3(-30f, 0f, -55f) * scream;
                    o.elR += new Vector3(-25f, 0f, 0f) * scream;
                    o.elL += new Vector3(-25f, 0f, 0f) * scream;
                    o.chestScale = 1f + 0.08f * scream;
                    o.grip = 0.9f * scream;
                    o.jitter += scream * 0.6f;
                    return o;
                }

                case BossPose.Idle:
                case BossPose.Recover:
                {
                    var heavy = pose == BossPose.Recover ? 2.2f : 1f;       // ofegante depois do golpe
                    var b = Mathf.Sin(t * (pose == BossPose.Recover ? 2.4f : 1.3f)) * heavy;
                    o.breath = b;
                    o.hipsY = b * 0.015f;
                    o.torso += new Vector3(b * 2f + (pose == BossPose.Recover ? 8f : 0f), Mathf.Sin(t * 0.4f) * 2f, 0f);
                    o.head += new Vector3(-b * 2f, Mathf.Sin(t * 0.55f) * 4f, 0f);
                    o.jaw = new Vector3(Mathf.Max(0f, b) * 4f, 0f, 0f);
                    o.shR += new Vector3(Mathf.Sin(t * 1.3f + 0.5f) * 3f, 0f, 0f);
                    o.shL += new Vector3(Mathf.Sin(t * 1.3f + 1.2f) * 3.5f, 0f, 0f);
                    return o;
                }

                case BossPose.Walk:
                {
                    var w = t * 1.1f * Mathf.PI * 2f;                       // passos lentos e pesados
                    var s = Mathf.Sin(w);
                    o.thR += new Vector3(-22f * s, 0f, 0f); o.shinR += new Vector3(Mathf.Max(0f, -Mathf.Cos(w)) * 30f, 0f, 0f);
                    o.thL += new Vector3(22f * s, 0f, 0f); o.shinL += new Vector3(Mathf.Max(0f, Mathf.Cos(w)) * 30f, 0f, 0f);
                    o.shR += new Vector3(14f * s, 0f, 0f); o.shL += new Vector3(-14f * s, 0f, 0f);
                    o.torso += new Vector3(4f, 0f, 5f * s);
                    o.hipsY = -Mathf.Abs(Mathf.Cos(w)) * 0.06f;
                    o.head += new Vector3(0f, 0f, -4f * s);
                    return o;
                }

                case BossPose.SlamWindup:
                {
                    var e = Ease(p);                                         // ergue o braço direito devagar
                    o.shR += new Vector3(-160f * e, 0f, 15f * e);
                    o.elR += new Vector3(-20f * e, 0f, 0f);
                    o.torso += new Vector3(-22f * e, -15f * e, 0f);
                    o.head += new Vector3(-12f * e, 0f, 0f);
                    o.jaw = new Vector3(18f * e, 0f, 0f);
                    o.shL += new Vector3(-20f * e, 0f, -15f * e);
                    o.grip = 0.4f + 0.5f * e;
                    o.jitter = p > 0.85f ? 0.3f : 0f;                        // segura no alto e treme antes de descer
                    return o;
                }

                case BossPose.SlamHit:
                    o.shR += new Vector3(-58f, 0f, 8f); o.elR += new Vector3(15f, 0f, 0f); o.wrR += new Vector3(35f, 0f, 0f);
                    o.torso += new Vector3(32f, 10f, 0f); o.head += new Vector3(14f, 0f, 0f);
                    o.hipsY = -0.25f; o.hipsZ = 0.15f;
                    o.thR += new Vector3(-20f, 0f, 0f); o.shinR += new Vector3(30f, 0f, 0f);
                    o.thL += new Vector3(-20f, 0f, 0f); o.shinL += new Vector3(30f, 0f, 0f);
                    o.grip = 1f;
                    return o;

                case BossPose.JumpCrouch:
                {
                    var e = Ease(p);
                    o.hipsY = -0.5f * e;
                    o.thR += new Vector3(-55f * e, 0f, 0f); o.shinR += new Vector3(95f * e, 0f, 0f); o.footR += new Vector3(-40f * e, 0f, 0f);
                    o.thL += new Vector3(-55f * e, 0f, 0f); o.shinL += new Vector3(95f * e, 0f, 0f); o.footL += new Vector3(-40f * e, 0f, 0f);
                    o.torso += new Vector3(24f * e, 0f, 0f);
                    o.shR += new Vector3(40f * e, 0f, 20f * e); o.shL += new Vector3(40f * e, 0f, -20f * e);
                    o.jitter = p > 0.7f ? 0.35f : 0f;
                    return o;
                }

                case BossPose.JumpAir:
                    o.thR += new Vector3(-30f, 0f, 0f); o.shinR += new Vector3(45f, 0f, 0f);
                    o.thL += new Vector3(-30f, 0f, 0f); o.shinL += new Vector3(45f, 0f, 0f);
                    o.shR += new Vector3(-140f, 0f, 30f); o.shL += new Vector3(-140f, 0f, -30f);
                    o.torso += new Vector3(-12f, 0f, 0f); o.jaw = new Vector3(22f, 0f, 0f);
                    o.grip = 1f;
                    return o;

                case BossPose.JumpLand:
                    o.hipsY = -0.55f;
                    o.thR += new Vector3(-65f, 0f, 0f); o.shinR += new Vector3(105f, 0f, 0f); o.footR += new Vector3(-35f, 0f, 0f);
                    o.thL += new Vector3(-65f, 0f, 0f); o.shinL += new Vector3(105f, 0f, 0f); o.footL += new Vector3(-35f, 0f, 0f);
                    o.torso += new Vector3(32f, 0f, 0f);
                    o.shR += new Vector3(-60f, 0f, 25f); o.shL += new Vector3(-60f, 0f, -25f);
                    o.grip = 1f;
                    return o;

                case BossPose.BelchInhale:
                {
                    var e = Ease(p);                                         // puxa o ar: peito estufa
                    o.torso += new Vector3(-18f * e, 0f, 0f); o.head += new Vector3(-24f * e, 0f, 0f);
                    o.chestScale = 1f + 0.14f * e;
                    o.jaw = new Vector3(8f * e, 0f, 0f);
                    o.shR += new Vector3(-10f * e, 0f, 25f * e); o.shL += new Vector3(-10f * e, 0f, -25f * e);
                    return o;
                }

                case BossPose.Belch:
                    o.torso += new Vector3(22f, 0f, 0f); o.head += new Vector3(14f, 0f, 0f);
                    o.jaw = new Vector3(46f + Mathf.Sin(t * 40f) * 5f, 0f, 0f);
                    o.chestScale = 0.94f;
                    o.shR += new Vector3(-15f, 0f, 30f); o.shL += new Vector3(-15f, 0f, -30f);
                    o.jitter = 0.25f;
                    return o;

                case BossPose.QuickWindup:
                    // braço esquerdo vai para trás num repente
                    o.shL += new Vector3(35f, 0f, -45f); o.elL += new Vector3(-30f, 0f, 0f);
                    o.torso += new Vector3(4f, 28f, 0f); o.head += new Vector3(6f, -10f, 0f);
                    o.jaw = new Vector3(14f, 0f, 0f);
                    return o;

                case BossPose.QuickStrike:
                    o.shL += new Vector3(-95f, 0f, 15f); o.elL += new Vector3(10f, 0f, 0f); o.wrL += new Vector3(-10f, 0f, 0f);
                    o.torso += new Vector3(14f, -30f, 0f); o.head += new Vector3(4f, 12f, 0f);
                    o.grip = 1f;
                    return o;

                case BossPose.Hit:
                {
                    var sway = Mathf.Sin(t * 14f) * (1f - p);
                    o.torso += new Vector3(-20f + sway * 4f, 0f, sway * 6f); o.head += new Vector3(-24f, 0f, 0f);
                    o.shR += new Vector3(-10f, 0f, 35f); o.shL += new Vector3(-10f, 0f, -35f);
                    o.hipsY = -0.1f; o.jaw = new Vector3(20f, 0f, 0f);
                    return o;
                }

                case BossPose.Death:
                {
                    var e = Ease(p);                                         // senta, desaba e vira pedra de novo
                    o = Lerp(Base(), Sitting(), Ease(Seg(p, 0f, 0.6f)));
                    o.torso += new Vector3(30f * e, 0f, 8f * e);
                    o.head += new Vector3(25f * e, 0f, 10f * e);
                    o.shR += new Vector3(20f * e, 0f, 10f * e); o.shL += new Vector3(20f * e, 0f, -10f * e);
                    o.hipsY -= 0.1f * e;
                    o.eyes = 1f - Seg(p, 0.2f, 0.6f);
                    return o;
                }
            }
            return o;
        }

        // ------------------------------------------------------------------ aplicar

        private void ApplyPose(Pose p)
        {
            var jitter = p.jitter > 0f ? UnityEngine.Random.insideUnitSphere * p.jitter : Vector3.zero;
            Set(0, p.torso + jitter); Set(1, p.head + jitter * 1.5f); Set(2, p.jaw);
            Set(3, p.shR); Set(4, p.elR); Set(5, p.wrR); Set(6, p.shL); Set(7, p.elL); Set(8, p.wrL);
            Set(9, p.thR); Set(10, p.shinR); Set(11, p.footR); Set(12, p.thL); Set(13, p.shinL); Set(14, p.footL);
            if (rig.hips != null) rig.hips.localPosition = _hipsRest + new Vector3(jitter.x * 0.004f, p.hipsY, p.hipsZ);
            if (rig.chest != null)
            {
                var s = p.chestScale + p.breath * 0.025f;
                rig.chest.localScale = new Vector3(_chestScaleRest.x * s, _chestScaleRest.y * (1f + p.breath * 0.015f), _chestScaleRest.z * s);
            }
            Curl(rig.fingersR, p.grip); Curl(rig.fingersL, p.grip);
            SetEyes(p.eyes);
        }

        private void Set(int i, Vector3 euler)
        {
            var j = _joints[i];
            if (j != null) j.localRotation = _rest[i] * Quaternion.Euler(euler);
        }

        private static void Curl(Transform[] fingers, float grip)
        {
            if (fingers == null) return;
            for (var i = 0; i < fingers.Length; i++)
                if (fingers[i] != null) fingers[i].localRotation = Quaternion.Euler(15f + grip * 55f, 0f, 0f);
        }

        private void SetEyes(float open)
        {
            if (Mathf.Abs(open - _eyeOpen) < 0.001f || rig.eyes == null) return;
            _eyeOpen = open;
            for (var i = 0; i < rig.eyes.Length; i++)
            {
                var r = rig.eyes[i];
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(EmissionId, _eyeBase[i] * open);
                r.SetPropertyBlock(_block);
                r.transform.localScale = new Vector3(1f, Mathf.Lerp(0.1f, 1f, open), 1f);   // pálpebra de pedra
            }
        }

        // ------------------------------------------------------------------ util

        private static float Seg(float t, float a, float b) => Mathf.Clamp01((t - a) / (b - a));
        private static float Ease(float t) => t * t * (3f - 2f * t);

        private static Pose Lerp(Pose a, Pose b, float t) => new()
        {
            hipsY = Mathf.Lerp(a.hipsY, b.hipsY, t), hipsZ = Mathf.Lerp(a.hipsZ, b.hipsZ, t),
            breath = Mathf.Lerp(a.breath, b.breath, t), chestScale = Mathf.Lerp(a.chestScale, b.chestScale, t),
            eyes = Mathf.Lerp(a.eyes, b.eyes, t), grip = Mathf.Lerp(a.grip, b.grip, t), jitter = Mathf.Lerp(a.jitter, b.jitter, t),
            torso = Vector3.Lerp(a.torso, b.torso, t), head = Vector3.Lerp(a.head, b.head, t), jaw = Vector3.Lerp(a.jaw, b.jaw, t),
            shR = Vector3.Lerp(a.shR, b.shR, t), elR = Vector3.Lerp(a.elR, b.elR, t), wrR = Vector3.Lerp(a.wrR, b.wrR, t),
            shL = Vector3.Lerp(a.shL, b.shL, t), elL = Vector3.Lerp(a.elL, b.elL, t), wrL = Vector3.Lerp(a.wrL, b.wrL, t),
            thR = Vector3.Lerp(a.thR, b.thR, t), shinR = Vector3.Lerp(a.shinR, b.shinR, t), footR = Vector3.Lerp(a.footR, b.footR, t),
            thL = Vector3.Lerp(a.thL, b.thL, t), shinL = Vector3.Lerp(a.shinL, b.shinL, t), footL = Vector3.Lerp(a.footL, b.footL, t),
        };

        private static Pose Blend(Pose a, Pose b, float k)
        {
            var r = Lerp(a, b, k);
            r.jitter = b.jitter;      // tremor não arrasta
            r.eyes = b.eyes < a.eyes ? Mathf.Lerp(a.eyes, b.eyes, k) : Mathf.MoveTowards(a.eyes, b.eyes, Time.deltaTime * 1.5f);
            return r;
        }
    }
}
