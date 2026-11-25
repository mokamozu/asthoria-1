using Athoria.Core;
using Athoria.Core.Events;
using UnityEngine;

namespace Athoria.Gameplay.Player
{
    /// <summary>
    /// Câmera em primeira pessoa: o corpo gira no eixo Y e o pivô da câmera no
    /// eixo X (olhar para cima/baixo). Trava o cursor durante o jogo e libera
    /// quando o jogo é pausado.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerLook : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private Transform cameraPivot;

        [Header("Sensibilidade")]
        [SerializeField, Tooltip("Graus por pixel de movimento do mouse.")]
        private float mouseSensitivity = 0.1f;
        [SerializeField, Tooltip("Graus por segundo com o analógico no máximo.")]
        private float gamepadSensitivity = 170f;
        [SerializeField] private bool invertY;
        [SerializeField, Range(45f, 89f)] private float pitchLimit = 85f;

        private float _pitch;
        // Ao travar o cursor o Unity costuma entregar um salto grande de delta;
        // ignoramos alguns frames para a câmera não "pular".
        private int _ignoreLookFrames;
        private const int FramesToIgnoreAfterLock = 3;

        public Transform CameraPivot => cameraPivot;
        public float Pitch => _pitch;

        private void Reset() => input = GetComponent<PlayerInputReader>();

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (cameraPivot == null)
            {
                Debug.LogError("[Athoria] PlayerLook sem cameraPivot atribuído.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GamePausedChanged>(OnPauseChanged);
            SetCursorLocked(!IsPaused);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GamePausedChanged>(OnPauseChanged);
            SetCursorLocked(false);
        }

        private void OnApplicationFocus(bool focus)
        {
            if (focus && isActiveAndEnabled && !IsPaused) SetCursorLocked(true);
        }

        private void Update()
        {
            if (IsPaused) return;
            if (_ignoreLookFrames > 0)
            {
                _ignoreLookFrames--;
                return;
            }

            var delta = input.LookDelta;
            if (input.LookIsPointer) delta *= mouseSensitivity * Athoria.Core.GameSettings.MouseSensitivity;
            else delta *= gamepadSensitivity * Time.deltaTime;

            transform.Rotate(0f, delta.x, 0f, Space.Self);

            _pitch -= invertY ? -delta.y : delta.y;
            _pitch = Mathf.Clamp(_pitch, -pitchLimit, pitchLimit);
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private static bool IsPaused => GameBootstrap.Instance != null && GameBootstrap.Instance.IsPaused;

        private void OnPauseChanged(GamePausedChanged e) => SetCursorLocked(!e.IsPaused);

        private void SetCursorLocked(bool locked)
        {
            if (locked) _ignoreLookFrames = FramesToIgnoreAfterLock;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
