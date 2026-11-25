using System;
using UnityEngine;

namespace Athoria.Gameplay.Combat
{
    /// <summary>
    /// Braços inteiros em primeira pessoa (WRAD ARMS, CC0): do ombro até os dedos.
    /// O sistema de armas continua mexendo os "braços de referência" (invisíveis) com
    /// as poses, golpes, pegadas e ajustes de sempre; aqui um IK de dois ossos leva o
    /// pulso do braço WRAD até a mão de referência, com o cotovelo para baixo e para
    /// fora. Assim o braço nunca termina cortado no meio da tela.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class FirstPersonArmsIK : MonoBehaviour
    {
        [Serializable]
        public sealed class Side
        {
            public Transform upper, lower, hand, twist0, twist1;
            [Tooltip("Osso da mão de referência (invisível) que o pulso segue.")] public Transform target;
            [Tooltip("Onde a arma é segurada no braço de referência.")] public Transform anchor;
            [Tooltip("Soquete da arma no modelo WRAD (centro do punho).")] public Transform socket;
            public Quaternion rotationOffset = Quaternion.identity;
            [Tooltip("Direção do cotovelo, no espaço do rig (x = lado, y = cima, z = frente).")] public Vector3 elbowHint = new(0.6f, -1f, -0.2f);

            [NonSerialized] public Quaternion UpperRest, LowerRest, HandRest, Twist0Rest, Twist1Rest;
            [NonSerialized] public Vector3 SocketInHand, UpperPosRest;
            [NonSerialized] public float A, B;
            public bool Valid => upper != null && lower != null && hand != null && target != null;
        }

        [SerializeField] private Transform rigRoot;
        [SerializeField] private Side right = new();
        [SerializeField] private Side left = new();
        [SerializeField, Tooltip("Afasta os ombros para a frente quando a mão não alcança (m, máximo).")] private float maxShoulderPush = 0.25f;
        [Header("Dedos (0 aberta, 1 fechada)")]
        [SerializeField] private Transform[] fingersRight = Array.Empty<Transform>();
        [SerializeField] private Quaternion[] openRight = Array.Empty<Quaternion>(), closedRight = Array.Empty<Quaternion>();
        [SerializeField] private Transform[] fingersLeft = Array.Empty<Transform>();
        [SerializeField] private Quaternion[] openLeft = Array.Empty<Quaternion>(), closedLeft = Array.Empty<Quaternion>();
        [SerializeField] private float fingerSpeed = 14f;
        private float _gripR = 1f, _gripL = 1f, _wantR = 1f, _wantL = 1f;

        /// <summary>Quanto cada mão fecha (0 aberta, 1 punho). Vai até lá suavemente.</summary>
        public void SetFingers(float right, float left) { _wantR = Mathf.Clamp01(right); _wantL = Mathf.Clamp01(left); }
        [SerializeField] private float pushSmoothing = 12f;
        [SerializeField, Tooltip("Se mesmo assim a mão não alcança, o ombro (fora da tela) vai até ela — a mão nunca solta a arma (m, máximo).")]
        private float maxShoulderStretch = 0.4f;

        private Vector3 _rootRest;
        private float _push;

        private void Awake()
        {
            if (rigRoot == null) rigRoot = transform;
            _rootRest = rigRoot.localPosition;
            Capture(right);
            Capture(left);
        }

        private static void Capture(Side s)
        {
            if (!s.Valid) return;
            s.UpperRest = s.upper.localRotation;
            s.UpperPosRest = s.upper.localPosition;
            s.LowerRest = s.lower.localRotation;
            s.HandRest = s.hand.localRotation;
            if (s.twist0 != null) s.Twist0Rest = s.twist0.localRotation;
            if (s.twist1 != null) s.Twist1Rest = s.twist1.localRotation;
            s.A = Vector3.Distance(s.upper.position, s.lower.position) / Mathf.Max(1e-5f, s.upper.lossyScale.x);
            s.B = Vector3.Distance(s.lower.position, s.hand.position) / Mathf.Max(1e-5f, s.upper.lossyScale.x);
            s.SocketInHand = s.socket != null ? Quaternion.Inverse(s.hand.rotation) * (s.socket.position - s.hand.position) / Mathf.Max(1e-5f, s.hand.lossyScale.x) : Vector3.zero;
        }

        private void LateUpdate()
        {
            if (!right.Valid && !left.Valid) return;
            // Mão longe demais do ombro: os ombros chegam um pouco para a frente (fora da tela).
            var need = Mathf.Max(Overreach(right), Overreach(left));
            _push = Mathf.Lerp(_push, Mathf.Clamp(need, 0f, maxShoulderPush), 1f - Mathf.Exp(-pushSmoothing * Time.deltaTime));
            rigRoot.localPosition = _rootRest + Vector3.forward * _push;
            Solve(right);
            Solve(left);
            var k = 1f - Mathf.Exp(-fingerSpeed * Time.deltaTime);
            _gripR = Mathf.Lerp(_gripR, _wantR, k);
            _gripL = Mathf.Lerp(_gripL, _wantL, k);
            Fingers(fingersRight, openRight, closedRight, _gripR);
            Fingers(fingersLeft, openLeft, closedLeft, _gripL);
        }

        private static void Fingers(Transform[] bones, Quaternion[] open, Quaternion[] closed, float grip)
        {
            var n = Mathf.Min(bones.Length, Mathf.Min(open.Length, closed.Length));
            for (var i = 0; i < n; i++)
                if (bones[i] != null) bones[i].localRotation = Quaternion.Slerp(open[i], closed[i], grip);
        }

        private float Overreach(Side s)
        {
            if (!s.Valid) return 0f;
            var reach = (s.A + s.B) * s.upper.lossyScale.x * 0.96f;
            var shoulder = s.upper.position - rigRoot.forward * _push;     // ombro sem o empurrão atual
            return Mathf.Max(0f, Vector3.Distance(WristTarget(s), shoulder) - reach);
        }

        private Quaternion WristRotation(Side s) => s.target.rotation * s.rotationOffset;

        private Vector3 WristTarget(Side s)
        {
            var anchor = s.anchor != null ? s.anchor.position : s.target.position;
            return anchor - WristRotation(s) * (s.SocketInHand * s.hand.lossyScale.x);
        }

        private void Solve(Side s)
        {
            if (!s.Valid) return;
            s.upper.localPosition = s.UpperPosRest;
            s.upper.localRotation = s.UpperRest;
            s.lower.localRotation = s.LowerRest;
            s.hand.localRotation = s.HandRest;
            if (s.twist0 != null) s.twist0.localRotation = s.Twist0Rest;
            if (s.twist1 != null) s.twist1.localRotation = s.Twist1Rest;

            var scale = s.upper.lossyScale.x;
            float a = s.A * scale, b = s.B * scale;
            var wristRot = WristRotation(s);
            var target = WristTarget(s);
            var shoulder = s.upper.position;
            var toTarget = target - shoulder;
            // Fora do alcance (pose da arma muito à frente/ao centro, golpe rápido): o ombro, que fica
            // fora da tela, desliza na direção da mão em vez de o pulso parar longe do cabo.
            var reach = (a + b) * 0.97f;
            if (toTarget.magnitude > reach)
            {
                var shift = Mathf.Min(toTarget.magnitude - reach, maxShoulderStretch);
                s.upper.position = shoulder + toTarget.normalized * shift;
                shoulder = s.upper.position;
                toTarget = target - shoulder;
            }
            var d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(a - b) + 1e-4f, (a + b) * 0.999f);
            var dir = toTarget.sqrMagnitude > 1e-10f ? toTarget.normalized : rigRoot.forward;
            target = shoulder + dir * d;

            var cosA = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
            var sinA = Mathf.Sqrt(1f - cosA * cosA);
            var hint = rigRoot.TransformDirection(s.elbowHint);
            var pole = hint - Vector3.Dot(hint, dir) * dir;
            if (pole.sqrMagnitude < 1e-8f) pole = -rigRoot.up;
            pole.Normalize();
            var elbow = shoulder + dir * (a * cosA) + pole * (a * sinA);

            s.upper.rotation = Quaternion.FromToRotation(s.lower.position - shoulder, elbow - shoulder) * s.upper.rotation;
            s.lower.rotation = Quaternion.FromToRotation(s.hand.position - s.lower.position, target - s.lower.position) * s.lower.rotation;

            // Giro do pulso repartido pelo antebraço (ossos de torção), sem "papel de bala"
            var natural = s.lower.rotation * s.HandRest;
            var extra = wristRot * Quaternion.Inverse(natural);
            var axis = (s.hand.position - s.lower.position).normalized;
            var twist = new Quaternion(0f, 0f, 0f, extra.w);
            var proj = Vector3.Dot(new Vector3(extra.x, extra.y, extra.z), axis) * axis;
            twist.x = proj.x; twist.y = proj.y; twist.z = proj.z;
            if (twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w > 1e-8f)
            {
                twist = Normalize(twist);
                if (s.twist0 != null) s.twist0.rotation = Quaternion.Slerp(Quaternion.identity, twist, 1f / 3f) * s.twist0.rotation;
                if (s.twist1 != null) s.twist1.rotation = Quaternion.Slerp(Quaternion.identity, twist, 1f / 3f) * s.twist1.rotation;
            }
            s.hand.rotation = wristRot;
        }

        private static Quaternion Normalize(Quaternion q)
        {
            var m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m);
        }

#if UNITY_EDITOR
        public Side EditorRight => right;
        public Side EditorLeft => left;
        public void EditorSetRoot(Transform root) => rigRoot = root;
        public void EditorSetFingers(Transform[] r, Quaternion[] rOpen, Quaternion[] rClosed, Transform[] l, Quaternion[] lOpen, Quaternion[] lClosed)
        {
            fingersRight = r; openRight = rOpen; closedRight = rClosed;
            fingersLeft = l; openLeft = lOpen; closedLeft = lClosed;
        }
#endif
    }
}
