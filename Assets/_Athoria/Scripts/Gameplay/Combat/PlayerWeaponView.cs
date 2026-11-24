using Athoria.Gameplay.Items;
using Athoria.Gameplay.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace Athoria.Gameplay.Combat
{
    /// <summary>
    /// Mostra braços e armas em primeira pessoa (view model) e dá vida a eles
    /// com movimento procedural: sacar/guardar, balanço ao virar a câmera,
    /// balanço ao andar, pose de corrida, respiração e tranco ao aterrissar.
    /// Os modelos ficam na layer "ViewModel", desenhada por uma câmera própria
    /// para nunca atravessar paredes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerWeaponView : MonoBehaviour
    {
        public const string ViewModelLayerName = "ViewModel";

        private enum EquipState { Ready, Lowering, Raising }

        [Header("Referências")]
        [SerializeField] private WeaponLoadout loadout;
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerInputReader input;

        [Header("Rig (filhos da câmera)")]
        [SerializeField] private Transform motionRoot;
        [SerializeField] private Transform rightArm;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightSocket;
        [SerializeField] private Transform leftSocket;

        [Header("Poses sem arma")]
        [SerializeField] private ViewPose unarmedRight = new(new Vector3(0.24f, -0.4f, 0.36f), new Vector3(-8f, -10f, 0f));
        [SerializeField] private ViewPose unarmedLeft = new(new Vector3(-0.24f, -0.4f, 0.36f), new Vector3(-8f, 10f, 0f));
        [SerializeField, Tooltip("Braço esquerdo abaixado, fora da tela.")]
        private ViewPose leftRest = new(new Vector3(-0.3f, -0.78f, 0.15f), new Vector3(10f, 0f, 0f));
        [SerializeField] private float armPoseSmoothing = 14f;

        [Header("Defesa")]
        [SerializeField, Tooltip("Braço esquerdo erguendo o escudo.")]
        private ViewPose shieldBlockPose = new(new Vector3(-0.14f, -0.32f, 0.36f), new Vector3(-6f, 4f, 0f));
        [SerializeField, Tooltip("Braço direito com a arma atravessada na frente (sem escudo).")]
        private ViewPose weaponGuardPose = new(new Vector3(0.12f, -0.3f, 0.34f), new Vector3(-10f, -30f, 65f));
        [SerializeField, Tooltip("Braço esquerdo apoiando a arma de duas mãos na defesa.")]
        private ViewPose twoHandGuardLeft = new(new Vector3(-0.08f, -0.36f, 0.36f), new Vector3(-10f, 20f, 0f));

        [Header("Recuo (impacto em parede/bloqueio)")]
        [SerializeField] private float recoilStiffness = 260f;
        [SerializeField] private float recoilDamping = 18f;

        [Header("Trocar arma")]
        [SerializeField] private float lowerTime = 0.16f;
        [SerializeField] private float raiseTime = 0.24f;
        [SerializeField] private Vector3 loweredOffset = new(0f, -0.38f, -0.05f);
        [SerializeField] private float loweredPitch = 35f;

        [Header("Balanço ao virar (sway)")]
        [SerializeField, Tooltip("Graus por pixel do mouse.")] private float swayPerPixel = 0.035f;
        [SerializeField, Tooltip("Graus para o analógico no máximo.")] private float swayGamepad = 3f;
        [SerializeField] private float swayMax = 6f;
        [SerializeField] private float swaySmoothing = 9f;

        [Header("Balanço ao andar (bob)")]
        [SerializeField] private float bobFrequency = 1.9f;
        [SerializeField] private float bobHorizontal = 0.014f;
        [SerializeField] private float bobVertical = 0.011f;

        [Header("Corrida")]
        [SerializeField] private ViewPose sprintOffset = new(new Vector3(0.02f, -0.06f, -0.04f), new Vector3(12f, -22f, 8f));
        [SerializeField] private float sprintBlendSpeed = 8f;

        [Header("Respiração e aterrissagem")]
        [SerializeField] private float breathAmplitude = 0.004f;
        [SerializeField] private float breathFrequency = 0.8f;
        [SerializeField] private float landingKickPerSpeed = 0.006f;
        [SerializeField] private float landingRecoverTime = 0.2f;

        private EquipState _state = EquipState.Raising;
        private float _lowerBlend = 1f;           // 0 = arma em posição, 1 = abaixada
        private WeaponData _shownMain, _shownOff;
        private GameObject _mainModel, _offModel;
        private Vector2 _sway;
        private float _bobPhase;
        private Vector3 _bob;
        private float _sprintBlend;
        private float _landing, _landingVel;
        private int _viewModelLayer;
        private Vector3 _rPos, _lPos;
        private Quaternion _rRot = Quaternion.identity, _lRot = Quaternion.identity;
        private Vector3 _actionPos, _actionEuler;
        private bool _actionBothArms;
        private float _blockBlend;
        private Vector3 _recoil, _recoilVel;

        public WeaponData ShownMainHand => _shownMain;
        public WeaponData ShownOffHand => _shownOff;
        public bool IsReady => _state == EquipState.Ready;
        public Transform MotionRoot => motionRoot;
        public bool HasShield => _shownOff != null;

        /// <summary>Deslocamento de ação (ataque) somado à pose do braço, no espaço da câmera.</summary>
        public void SetAction(Vector3 position, Vector3 euler, bool bothArms)
        {
            _actionPos = position;
            _actionEuler = euler;
            _actionBothArms = bothArms;
        }

        public void ClearAction() => SetAction(Vector3.zero, Vector3.zero, false);

        /// <summary>0 = guarda baixa, 1 = defendendo.</summary>
        public void SetBlockBlend(float blend) => _blockBlend = Mathf.Clamp01(blend);

        /// <summary>Empurrão rápido na arma (bater em parede, aparar golpe).</summary>
        public void Recoil(Vector3 impulse) => _recoilVel += impulse;

        private void Reset()
        {
            loadout = GetComponent<WeaponLoadout>();
            controller = GetComponent<PlayerController>();
            input = GetComponent<PlayerInputReader>();
        }

        private void Awake()
        {
            if (loadout == null) loadout = GetComponent<WeaponLoadout>();
            if (controller == null) controller = GetComponent<PlayerController>();
            if (input == null) input = GetComponent<PlayerInputReader>();

            if (motionRoot == null || rightArm == null || leftArm == null || rightSocket == null || leftSocket == null)
            {
                Debug.LogError("[Athoria] PlayerWeaponView com referências do rig faltando.", this);
                enabled = false;
                return;
            }

            _viewModelLayer = LayerMask.NameToLayer(ViewModelLayerName);
            if (_viewModelLayer < 0)
                Debug.LogWarning($"[Athoria] Layer '{ViewModelLayerName}' não existe; a arma pode atravessar paredes.", this);
            else
                SetLayerRecursively(motionRoot.gameObject, _viewModelLayer);
        }

        private void OnEnable()
        {
            loadout.Changed += OnLoadoutChanged;
            controller.Landed += OnLanded;
        }

        private void OnDisable()
        {
            loadout.Changed -= OnLoadoutChanged;
            controller.Landed -= OnLanded;
        }

        private void Start()
        {
            // Começa abaixada e "saca" a arma inicial.
            SwapModels(loadout.MainHand, loadout.EffectiveOffHand);
            SnapArms();
            _state = EquipState.Raising;
            _lowerBlend = 1f;
        }

        private void OnLoadoutChanged()
        {
            if (loadout.MainHand == _shownMain && loadout.EffectiveOffHand == _shownOff)
            {
                if (_state == EquipState.Lowering) _state = EquipState.Raising;
                return;
            }
            _state = EquipState.Lowering;
        }

        private void LateUpdate()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f) return;

            UpdateEquip(dt);
            UpdateArmPoses(dt);
            UpdateMotion(dt);
        }

        private void UpdateEquip(float dt)
        {
            switch (_state)
            {
                case EquipState.Lowering:
                    _lowerBlend = Mathf.MoveTowards(_lowerBlend, 1f, dt / lowerTime);
                    if (_lowerBlend >= 1f)
                    {
                        SwapModels(loadout.MainHand, loadout.EffectiveOffHand);
                        SnapArms();
                        _state = EquipState.Raising;
                    }
                    break;
                case EquipState.Raising:
                    _lowerBlend = Mathf.MoveTowards(_lowerBlend, 0f, dt / raiseTime);
                    if (_lowerBlend <= 0f) _state = EquipState.Ready;
                    break;
            }
        }

        private void GetArmTargets(out Vector3 rPos, out Quaternion rRot, out Vector3 lPos, out Quaternion lRot)
        {
            var right = _shownMain != null ? _shownMain.HoldingArmPose : unarmedRight;
            ViewPose left;
            if (_shownMain != null && _shownMain.IsTwoHanded) left = _shownMain.SupportArmPose;
            else if (_shownOff != null) left = _shownOff.HoldingArmPose;
            else if (_shownMain == null) left = unarmedLeft;
            else left = leftRest;

            rPos = right.position; rRot = right.Rotation;
            lPos = left.position; lRot = left.Rotation;

            if (_blockBlend <= 0f) return;
            var b = Mathf.SmoothStep(0f, 1f, _blockBlend);
            if (_shownOff != null)
            {
                lPos = Vector3.Lerp(lPos, shieldBlockPose.position, b);
                lRot = Quaternion.Slerp(lRot, shieldBlockPose.Rotation, b);
            }
            else
            {
                rPos = Vector3.Lerp(rPos, weaponGuardPose.position, b);
                rRot = Quaternion.Slerp(rRot, weaponGuardPose.Rotation, b);
                if (_shownMain != null && _shownMain.IsTwoHanded)
                {
                    lPos = Vector3.Lerp(lPos, twoHandGuardLeft.position, b);
                    lRot = Quaternion.Slerp(lRot, twoHandGuardLeft.Rotation, b);
                }
            }
        }

        private void UpdateArmPoses(float dt)
        {
            GetArmTargets(out var rPos, out var rRot, out var lPos, out var lRot);
            var t = 1f - Mathf.Exp(-armPoseSmoothing * dt);
            _rPos = Vector3.Lerp(_rPos, rPos, t);
            _rRot = Quaternion.Slerp(_rRot, rRot, t);
            _lPos = Vector3.Lerp(_lPos, lPos, t);
            _lRot = Quaternion.Slerp(_lRot, lRot, t);

            // Mola do recuo
            var accel = -recoilStiffness * _recoil - recoilDamping * _recoilVel;
            _recoilVel += accel * dt;
            _recoil += _recoilVel * dt;

            var actionRot = Quaternion.Euler(_actionEuler);
            rightArm.localPosition = _rPos + _actionPos + _recoil;
            rightArm.localRotation = actionRot * _rRot;
            leftArm.localPosition = _lPos + (_actionBothArms ? _actionPos + _recoil : Vector3.zero);
            leftArm.localRotation = (_actionBothArms ? actionRot : Quaternion.identity) * _lRot;
        }

        private void SnapArms()
        {
            GetArmTargets(out _rPos, out _rRot, out _lPos, out _lRot);
            rightArm.localPosition = _rPos;
            rightArm.localRotation = _rRot;
            leftArm.localPosition = _lPos;
            leftArm.localRotation = _lRot;
        }

        private void UpdateMotion(float dt)
        {
            // Sway: a arma "atrasa" em relação ao movimento da câmera.
            var look = input != null ? input.LookDelta : Vector2.zero;
            var target = input != null && input.LookIsPointer ? look * swayPerPixel : look * swayGamepad;
            target = Vector2.ClampMagnitude(target, swayMax);
            _sway = Vector2.Lerp(_sway, target, 1f - Mathf.Exp(-swaySmoothing * dt));

            // Bob ao andar (figura de 8).
            var speed = controller.HorizontalVelocity.magnitude;
            Vector3 bobTarget;
            if (controller.IsGrounded && speed > 0.2f)
            {
                var factor = speed / controller.WalkSpeed;
                _bobPhase += dt * bobFrequency * factor * Mathf.PI * 2f;
                var amp = Mathf.Clamp01(factor) * (controller.IsSprinting ? 1.5f : 1f);
                bobTarget = new Vector3(Mathf.Cos(_bobPhase) * bobHorizontal, -Mathf.Abs(Mathf.Sin(_bobPhase)) * bobVertical, 0f) * amp;
            }
            else
            {
                bobTarget = Vector3.zero;
            }
            _bob = Vector3.Lerp(_bob, bobTarget, 1f - Mathf.Exp(-12f * dt));

            // Corrida
            var sprinting = controller.IsSprinting && speed > controller.WalkSpeed * 1.05f;
            _sprintBlend = Mathf.MoveTowards(_sprintBlend, sprinting ? 1f : 0f, sprintBlendSpeed * dt);
            var sprintEase = Mathf.SmoothStep(0f, 1f, _sprintBlend);

            // Respiração e aterrissagem
            var breath = Mathf.Sin(Time.time * breathFrequency * Mathf.PI * 2f) * breathAmplitude;
            _landing = Mathf.SmoothDamp(_landing, 0f, ref _landingVel, landingRecoverTime);

            var lowerEase = Mathf.SmoothStep(0f, 1f, _lowerBlend);

            var pos = _bob
                      + Vector3.up * (breath + _landing)
                      + sprintOffset.position * sprintEase
                      + loweredOffset * lowerEase;

            var rot = Quaternion.Euler(_sway.y, -_sway.x, _sway.x * 0.5f)
                      * Quaternion.Slerp(Quaternion.identity, sprintOffset.Rotation, sprintEase)
                      * Quaternion.Euler(loweredPitch * lowerEase, 0f, 0f);

            motionRoot.localPosition = pos;
            motionRoot.localRotation = rot;
        }

        private void OnLanded(float fallSpeed) => _landing = -Mathf.Min(fallSpeed * landingKickPerSpeed, 0.08f);

        private void SwapModels(WeaponData main, WeaponData off)
        {
            if (_mainModel != null) Destroy(_mainModel);
            if (_offModel != null) Destroy(_offModel);
            _mainModel = Spawn(main, rightSocket);
            _offModel = Spawn(off, leftSocket);
            _shownMain = main;
            _shownOff = off;
        }

        private GameObject Spawn(WeaponData data, Transform socket)
        {
            if (data == null || data.ViewModelPrefab == null) return null;

            var model = Instantiate(data.ViewModelPrefab, socket);
            model.transform.SetLocalPositionAndRotation(data.ModelPose.position, data.ModelPose.Rotation);

            // View model é só visual: sem colisão, sem sombra projetada no mundo.
            foreach (var col in model.GetComponentsInChildren<Collider>()) Destroy(col);
            foreach (var r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            if (_viewModelLayer >= 0) SetLayerRecursively(model, _viewModelLayer);
            return model;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }
    }
}
