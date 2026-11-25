using UnityEngine;

namespace Athoria.Gameplay.AI
{
    /// <summary>
    /// Animação procedural para modelos provisórios feitos de primitivas:
    /// pernas balançam ao andar, braço da arma faz preparação/golpe, braço do
    /// escudo levanta no bloqueio, corpo inclina no atordoamento e tomba na morte.
    /// Cada junta é girada a partir da sua rotação de repouso.
    /// </summary>
    public sealed class EnemyAnimator : EnemyAnimatorBase
    {

        [Header("Juntas")]
        [SerializeField, Tooltip("Corpo inteiro (inclina, balança e tomba). Pivô no chão.")]
        private Transform body;
        [SerializeField] private Transform head;
        [SerializeField] private Transform weaponArm;
        [SerializeField] private Transform shieldArm;
        [SerializeField] private Transform[] legs = System.Array.Empty<Transform>();
        [SerializeField, Tooltip("Sentido do balanço de cada perna (1 ou -1).")]
        private float[] legSigns = System.Array.Empty<float>();

        [Header("Andar")]
        [SerializeField] private float strideAngle = 28f;
        [SerializeField, Tooltip("Passos por metro percorrido.")] private float stepsPerMeter = 1.3f;
        [SerializeField] private float bobHeight = 0.04f;

        [Header("Morte")]
        [SerializeField, Tooltip("Eixo local em que o corpo tomba (X = para trás, Z = de lado).")]
        private Vector3 deathAxis = Vector3.right;
        [SerializeField] private float deathAngle = -85f;

        private Quaternion _bodyRest, _headRest, _weaponRest, _shieldRest;
        private Quaternion[] _legRest;
        private Vector3 _bodyRestPos;

        private Vector3 _bodyAngles, _headAngles, _weaponAngles, _shieldAngles;
        private float _deathBlend;
        private float _phase;
        private float _speed;
        private Pose _pose;
        private EnemyAttackPose _attackPose;
        private float _poseSpeed = 14f;

        private void Awake()
        {
            if (body == null) body = transform;
            _bodyRest = body.localRotation;
            _bodyRestPos = body.localPosition;
            if (head != null) _headRest = head.localRotation;
            if (weaponArm != null) _weaponRest = weaponArm.localRotation;
            if (shieldArm != null) _shieldRest = shieldArm.localRotation;
            _legRest = new Quaternion[legs.Length];
            for (var i = 0; i < legs.Length; i++) if (legs[i] != null) _legRest[i] = legs[i].localRotation;
        }

        /// <summary>Velocidade horizontal atual em m/s (controla o passo).</summary>
        public override void SetSpeed(float metersPerSecond) => _speed = metersPerSecond;

        public override void SetPose(Pose pose, EnemyAttackPose attackPose = EnemyAttackPose.Slash, float duration = 0f)
        {
            _pose = pose;
            _attackPose = attackPose;
            // O golpe é rápido; a preparação e a volta são mais lentas, para o jogador ler.
            _poseSpeed = pose switch
            {
                Pose.Strike => 30f,
                Pose.Windup => 9f,
                Pose.Stagger => 20f,
                Pose.Block => 16f,
                _ => 8f
            };
        }

        /// <summary>Volta tudo ao repouso na hora (reaparecer).</summary>
        public override void ResetPose()
        {
            _pose = Pose.None;
            _deathBlend = 0f;
            _bodyAngles = _headAngles = _weaponAngles = _shieldAngles = Vector3.zero;
            Apply(0f);
        }

        private void LateUpdate()
        {
            var dt = Time.deltaTime;
            _phase += _speed * stepsPerMeter * dt * Mathf.PI;

            Targets(out var bodyT, out var headT, out var weaponT, out var shieldT);
            var k = 1f - Mathf.Exp(-_poseSpeed * dt);
            _bodyAngles = Vector3.Lerp(_bodyAngles, bodyT, k);
            _headAngles = Vector3.Lerp(_headAngles, headT, k);
            _weaponAngles = Vector3.Lerp(_weaponAngles, weaponT, k);
            _shieldAngles = Vector3.Lerp(_shieldAngles, shieldT, k);
            _deathBlend = Mathf.MoveTowards(_deathBlend, _pose == Pose.Dead ? 1f : 0f, dt * 2.2f);

            Apply(dt);
        }

        private void Apply(float dt)
        {
            var moving = Mathf.Clamp01(_speed / 2f);
            var bob = Mathf.Abs(Mathf.Sin(_phase)) * bobHeight * moving * (1f - _deathBlend);
            var fall = Quaternion.AngleAxis(deathAngle * EaseIn(_deathBlend), deathAxis);
            body.localPosition = _bodyRestPos + Vector3.up * bob;
            body.localRotation = _bodyRest * fall * Quaternion.Euler(_bodyAngles);
            if (head != null) head.localRotation = _headRest * Quaternion.Euler(_headAngles);
            if (weaponArm != null) weaponArm.localRotation = _weaponRest * Quaternion.Euler(_weaponAngles);
            if (shieldArm != null) shieldArm.localRotation = _shieldRest * Quaternion.Euler(_shieldAngles);

            var swing = Mathf.Sin(_phase) * strideAngle * moving * (1f - _deathBlend);
            for (var i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                var sign = i < legSigns.Length ? legSigns[i] : (i % 2 == 0 ? 1f : -1f);
                legs[i].localRotation = _legRest[i] * Quaternion.Euler(swing * sign, 0f, 0f);
            }
        }

        private static float EaseIn(float t) => t * t;

        private void Targets(out Vector3 bodyT, out Vector3 headT, out Vector3 weaponT, out Vector3 shieldT)
        {
            bodyT = headT = weaponT = shieldT = Vector3.zero;
            // Braço da arma levemente à frente em repouso; o braço do escudo cobre o corpo ao andar.
            weaponT = new Vector3(-15f, 0f, 0f);
            shieldT = new Vector3(-20f, 0f, 0f);

            switch (_pose)
            {
                case Pose.Windup:
                    switch (_attackPose)
                    {
                        case EnemyAttackPose.Overhead:
                            weaponT = new Vector3(-165f, 0f, 0f);
                            bodyT = new Vector3(-10f, 0f, 0f);
                            break;
                        case EnemyAttackPose.Slash:
                            weaponT = new Vector3(-80f, 75f, 0f);
                            bodyT = new Vector3(-4f, 25f, 0f);
                            break;
                        case EnemyAttackPose.Thrust:
                            weaponT = new Vector3(-70f, 20f, 0f);
                            bodyT = new Vector3(-8f, 15f, 0f);
                            break;
                        case EnemyAttackPose.Bite:
                            bodyT = new Vector3(-14f, 0f, 0f);
                            headT = new Vector3(-25f, 0f, 0f);
                            break;
                        case EnemyAttackPose.Shoot:
                            weaponT = new Vector3(-90f, 0f, 0f);
                            shieldT = new Vector3(-90f, 25f, 0f);
                            headT = new Vector3(0f, 0f, 0f);
                            break;
                    }
                    break;

                case Pose.Strike:
                    switch (_attackPose)
                    {
                        case EnemyAttackPose.Overhead:
                            weaponT = new Vector3(-25f, 0f, 0f);
                            bodyT = new Vector3(14f, 0f, 0f);
                            break;
                        case EnemyAttackPose.Slash:
                            weaponT = new Vector3(-80f, -70f, 0f);
                            bodyT = new Vector3(6f, -25f, 0f);
                            break;
                        case EnemyAttackPose.Thrust:
                            weaponT = new Vector3(-95f, 0f, 0f);
                            bodyT = new Vector3(12f, -5f, 0f);
                            break;
                        case EnemyAttackPose.Bite:
                            bodyT = new Vector3(12f, 0f, 0f);
                            headT = new Vector3(20f, 0f, 0f);
                            break;
                        case EnemyAttackPose.Shoot:
                            weaponT = new Vector3(-90f, 0f, 0f);
                            shieldT = new Vector3(-60f, 10f, 0f);
                            bodyT = new Vector3(-4f, 0f, 0f);
                            break;
                    }
                    break;

                case Pose.Recover:
                    weaponT = new Vector3(-40f, 0f, 0f);
                    break;

                case Pose.Block:
                    if (shieldArm != null) shieldT = new Vector3(-35f, -45f, 0f);
                    else weaponT = new Vector3(-75f, -40f, 50f);
                    bodyT = new Vector3(4f, 0f, 0f);
                    break;

                case Pose.Stagger:
                    bodyT = new Vector3(-18f, 0f, 6f);
                    headT = new Vector3(-20f, 0f, 0f);
                    weaponT = new Vector3(20f, 0f, 30f);
                    shieldT = new Vector3(15f, 0f, -25f);
                    break;

                case Pose.Dead:
                    weaponT = new Vector3(-60f, 0f, 40f);
                    shieldT = new Vector3(-50f, 0f, -40f);
                    break;
            }
        }
    }
}
