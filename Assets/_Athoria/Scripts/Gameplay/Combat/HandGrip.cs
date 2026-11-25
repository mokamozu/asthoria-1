using UnityEngine;

namespace Athoria.Gameplay.Combat
{
    /// <summary>
    /// Mão em primeira pessoa que segura a arma de verdade: dobra o pulso para o
    /// punho fechado ficar no mesmo eixo do cabo e expõe o ponto (centro do punho)
    /// onde a arma é presa. Fica no Transform do braço; o ponto é filho do osso da mão.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HandGrip : MonoBehaviour
    {
        [SerializeField, Tooltip("Osso do pulso (hand.R / hand.L).")] private Transform handBone;
        [SerializeField, Tooltip("Centro do punho fechado; o eixo Y (verde) segue o cabo.")] private Transform point;
        [SerializeField, Range(0f, 1f), Tooltip("Quanto o pulso acompanha a inclinação da arma.")] private float wristFollow = 1f;

        private Quaternion _rest;
        private bool _hasRest;

        public Transform Point => point;
        public bool IsValid => handBone != null && point != null;

        private void Awake() => CaptureRest();

        private void CaptureRest()
        {
            if (_hasRest || handBone == null) return;
            _rest = handBone.localRotation;
            _hasRest = true;
        }

        public void ResetPose()
        {
            CaptureRest();
            if (_hasRest) handBone.localRotation = _rest;
        }

        /// <summary>Dobra o pulso para o cabo (eixo Y de handleRotation, no espaço do braço) passar pelo punho.</summary>
        public void Align(Quaternion handleRotation, float follow = -1f)
        {
            if (!IsValid) return;
            ResetPose();
            var desired = transform.rotation * handleRotation * Vector3.up;
            var turn = Quaternion.FromToRotation(point.up, desired);
            handBone.rotation = Quaternion.Slerp(Quaternion.identity, turn, follow >= 0f ? follow : wristFollow) * handBone.rotation;
        }

#if UNITY_EDITOR
        public void EditorSetup(Transform hand, Transform gripPoint)
        {
            handBone = hand;
            point = gripPoint;
        }
#endif
    }
}
