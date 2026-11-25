using System;
using Athoria.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Athoria.Gameplay.Player
{
    /// <summary>
    /// Única porta de entrada dos controles do jogador. Lê o asset
    /// Athoria_Controls e expõe valores simples para os outros componentes,
    /// que nunca falam com o Input System diretamente.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        [SerializeField, Tooltip("Desligado: segurar para agachar. Ligado: apertar alterna.")]
        private bool crouchIsToggle;

        private readonly InputAction[] _hotbar = new InputAction[4];
        private readonly Action<InputAction.CallbackContext>[] _hotbarHandlers = new Action<InputAction.CallbackContext>[4];
        private InputAction _move, _look, _sprint, _jump, _crouch, _interact, _pause;
        private InputAction _attack, _block, _dodge, _inventory, _character;
        private bool _crouchToggled;
        private bool _interactPressed;

        public Vector2 Move { get; private set; }
        public Vector2 LookDelta { get; private set; }
        /// <summary>True quando o último movimento de câmera veio do mouse (delta em pixels).</summary>
        public bool LookIsPointer { get; private set; } = true;
        public bool SprintHeld => _sprint != null && _sprint.IsPressed();
        public bool JumpHeld => _jump != null && _jump.IsPressed();
        public bool AttackHeld => _attack != null && _attack.IsPressed() && !IsPaused;
        public bool BlockHeld => _block != null && _block.IsPressed() && !IsPaused;
        /// <summary>Momento do último clique de ataque ainda não consumido.</summary>
        public float LastAttackPressedTime { get; private set; } = float.NegativeInfinity;
        /// <summary>Momento do último aperto de esquiva ainda não consumido.</summary>
        public float LastDodgePressedTime { get; private set; } = float.NegativeInfinity;
        public bool CrouchHeld => crouchIsToggle ? _crouchToggled : _crouch != null && _crouch.IsPressed();
        /// <summary>Momento (Time.time) do último aperto de pulo ainda não consumido.</summary>
        public float LastJumpPressedTime { get; private set; } = float.NegativeInfinity;

        public InputActionAsset Actions => actions;

        /// <summary>Atalho 0..3 apertado (teclas 1-4 / direcional).</summary>
        public event Action<int> HotbarPressed;
        /// <summary>Abrir/fechar inventário (funciona também com o jogo pausado).</summary>
        public event Action InventoryPressed;
        /// <summary>Tela de personagem/equipamento (K).</summary>
        public event Action CharacterPressed;

        private void Awake()
        {
            if (actions == null)
            {
                Debug.LogError("[Athoria] PlayerInputReader sem InputActionAsset atribuído.", this);
                enabled = false;
                return;
            }

            var player = actions.FindActionMap("Player", true);
            var menus = actions.FindActionMap("Menus", true);
            _move = player.FindAction("Move", true);
            _look = player.FindAction("Look", true);
            _sprint = player.FindAction("Sprint", true);
            _jump = player.FindAction("Jump", true);
            _crouch = player.FindAction("Crouch", true);
            _interact = player.FindAction("Interact", true);
            _pause = menus.FindAction("Pause", true);
            _attack = player.FindAction("Attack", true);
            _block = player.FindAction("Block", true);
            _dodge = player.FindAction("Dodge", true);
            _inventory = menus.FindAction("Inventory", true);
            _character = menus.FindAction("Character", true);

            for (var i = 0; i < _hotbar.Length; i++)
            {
                var slot = i;
                _hotbar[i] = player.FindAction($"Hotbar{i + 1}", true);
                _hotbarHandlers[i] = _ => { if (!IsPaused) HotbarPressed?.Invoke(slot); };
            }
        }

        private void OnEnable()
        {
            if (actions == null || _move == null) return;
            _jump.performed += OnJump;
            _crouch.performed += OnCrouch;
            _interact.performed += OnInteract;
            _pause.performed += OnPause;
            _attack.started += OnAttack;
            _dodge.performed += OnDodge;
            _inventory.performed += OnInventory;
            _character.performed += OnCharacter;
            for (var i = 0; i < _hotbar.Length; i++)
                if (_hotbar[i] != null) _hotbar[i].performed += _hotbarHandlers[i];
            actions.Enable();
        }

        private void OnDisable()
        {
            if (actions == null || _move == null) return;
            _jump.performed -= OnJump;
            _crouch.performed -= OnCrouch;
            _interact.performed -= OnInteract;
            _pause.performed -= OnPause;
            _attack.started -= OnAttack;
            _dodge.performed -= OnDodge;
            _inventory.performed -= OnInventory;
            _character.performed -= OnCharacter;
            for (var i = 0; i < _hotbar.Length; i++)
                if (_hotbar[i] != null) _hotbar[i].performed -= _hotbarHandlers[i];
            actions.Disable();
            Move = Vector2.zero;
            LookDelta = Vector2.zero;
        }

        private void Update()
        {
            var paused = GameBootstrap.Instance != null && GameBootstrap.Instance.IsPaused;
            Move = paused ? Vector2.zero : Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f);
            LookDelta = paused ? Vector2.zero : _look.ReadValue<Vector2>();

            var control = _look.activeControl;
            if (control != null) LookIsPointer = control.device is Pointer;
        }

        public void ConsumeJump() => LastJumpPressedTime = float.NegativeInfinity;
        public void ConsumeAttack() => LastAttackPressedTime = float.NegativeInfinity;
        public void ConsumeDodge() => LastDodgePressedTime = float.NegativeInfinity;

        private void OnAttack(InputAction.CallbackContext _)
        {
            if (!IsPaused) LastAttackPressedTime = Time.time;
        }

        private void OnInventory(InputAction.CallbackContext _) => InventoryPressed?.Invoke();
        private void OnCharacter(InputAction.CallbackContext _) => CharacterPressed?.Invoke();

        private void OnDodge(InputAction.CallbackContext _)
        {
            if (!IsPaused) LastDodgePressedTime = Time.time;
        }

        public bool ConsumeInteract()
        {
            var pressed = _interactPressed;
            _interactPressed = false;
            return pressed;
        }

        private static bool IsPaused => GameBootstrap.Instance != null && GameBootstrap.Instance.IsPaused;

        private void OnJump(InputAction.CallbackContext _)
        {
            if (!IsPaused) LastJumpPressedTime = Time.time;
        }

        private void OnCrouch(InputAction.CallbackContext _)
        {
            if (!IsPaused) _crouchToggled = !_crouchToggled;
        }

        private void OnInteract(InputAction.CallbackContext _)
        {
            if (!IsPaused) _interactPressed = true;
        }

        private void OnPause(InputAction.CallbackContext _)
        {
            if (GameBootstrap.BackRequested != null && GameBootstrap.BackRequested()) return;
            if (GameBootstrap.Instance != null) GameBootstrap.Instance.TogglePause();
        }
    }
}
