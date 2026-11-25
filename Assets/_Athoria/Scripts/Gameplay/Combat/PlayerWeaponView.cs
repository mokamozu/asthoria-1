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
        [SerializeField, Tooltip("Braço esquerdo em repouso com arma de uma mão: baixo, no canto esquerdo, punho meio fechado (guarda natural).")]
        private ViewPose leftRest = new(new Vector3(-0.27f, -0.5f, 0.26f), new Vector3(-6f, 14f, -18f));
        [SerializeField] private float armPoseSmoothing = 14f;

        [Header("Defesa")]
        [SerializeField, Tooltip("Braço esquerdo erguendo o escudo.")]
        private ViewPose shieldBlockPose = new(new Vector3(-0.14f, -0.32f, 0.36f), new Vector3(-6f, 4f, 0f));
        [SerializeField, Tooltip("Braço direito com a arma atravessada na frente (sem escudo).")]
        private ViewPose weaponGuardPose = new(new Vector3(0.12f, -0.3f, 0.34f), new Vector3(-10f, -30f, 65f));
        [SerializeField, Tooltip("Braço esquerdo apoiando a arma de duas mãos na defesa.")]
        private ViewPose twoHandGuardLeft = new(new Vector3(-0.08f, -0.36f, 0.36f), new Vector3(-10f, 20f, 0f));

        [Header("Enquadramento")]
        [SerializeField, Tooltip("Desloca braços e armas juntos na tela (sobe as mãos acima da barra de atalhos).")]
        private Vector3 viewOffset = new(0f, 0.07f, -0.02f);

        [Header("Estilo Daggerfall")]
        [SerializeField, Tooltip("Desligado: só a arma aparece (sem braços nem mãos), como em Daggerfall.")]
        private bool showArms = true;
        [SerializeField, Tooltip("Sem braços: desce e afasta a arma para o canto da tela, com o punho escondido embaixo.")]
        private Vector3 weaponOnlyOffset = new(0.05f, -0.13f, 0.02f);

        [Header("Recuo (impacto em parede/bloqueio)")]
        [SerializeField] private float recoilStiffness = 260f;
        [SerializeField] private float recoilDamping = 18f;

        [Header("Trocar arma")]
        [SerializeField, Tooltip("Material do rastro da lâmina (aditivo, usa a cor dos vértices).")] private Material trailMaterial;
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

        private FirstPersonMovementAnimation _movement;
        private ViewModelAnimator _actions;
        private WeaponTrail _trail;

        /// <summary>Liga/desliga o rastro da arma principal (a cor já vem com o alfa).</summary>
        public void SetTrail(bool on, Color color, float lifetime, float start = 0.35f)
        {
            if (_trail != null) _trail.SetEmitting(on && _mainModel != null, color, lifetime, start);
        }
        private FirstPersonArmsIK _armsIK;

        /// <summary>Ações dos braços (sacar, inspecionar, pegar, beber…). Pode ser nulo.</summary>
        public ViewModelAnimator Actions => _actions;

        /// <summary>Toca uma ação dos braços; onEvent roda no momento do efeito (ou na hora, se não houver animação).</summary>
        public bool PlayAction(ViewModelActionId id, bool inCombat = false, System.Action onEvent = null)
        {
            if (_actions == null) { onEvent?.Invoke(); return false; }
            return _actions.Play(id, inCombat, onEvent);
        }
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
        private HandGrip _leftGrip;

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

            _leftGrip = leftArm.GetComponent<HandGrip>();
            _actions = GetComponent<ViewModelAnimator>();
            _armsIK = motionRoot.GetComponentInChildren<FirstPersonArmsIK>(true);
            var trailGo = new GameObject("Rastro da arma", typeof(MeshFilter), typeof(MeshRenderer));
            trailGo.transform.SetParent(motionRoot, false);
            _trail = trailGo.AddComponent<WeaponTrail>();
            _trail.SetMaterial(trailMaterial);
            if (!showArms) HideArms();
            _viewModelLayer = LayerMask.NameToLayer(ViewModelLayerName);
            if (_viewModelLayer < 0)
                Debug.LogWarning($"[Athoria] Layer '{ViewModelLayerName}' não existe; a arma pode atravessar paredes.", this);
            else
                SetLayerRecursively(motionRoot.gameObject, _viewModelLayer);
        }

        /// <summary>
        /// Esconde braços e mãos (os antigos e os WRAD). Os braços continuam existindo,
        /// invisíveis, porque é neles que a arma fica presa e que os golpes se movem.
        /// As armas são criadas depois, nos soquetes, então continuam visíveis.
        /// </summary>
        private void HideArms()
        {
            foreach (var arm in new[] { rightArm, leftArm })
                foreach (var r in arm.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            var wrad = motionRoot.GetComponentInChildren<FirstPersonArmsIK>(true);
            if (wrad != null) wrad.gameObject.SetActive(false);
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
            if (_actions != null) _actions.UseSet(_shownMain != null ? _shownMain.ViewModelActions : null);
            if (_shownMain != null || _shownOff != null) PlayAction(ViewModelActionId.Draw, true);
        }

        private void OnLoadoutChanged()
        {
            if (loadout.MainHand == _shownMain && loadout.EffectiveOffHand == _shownOff)
            {
                if (_state == EquipState.Lowering) _state = EquipState.Raising;
                return;
            }
            _state = EquipState.Lowering;
            if (_shownMain != null || _shownOff != null) PlayAction(ViewModelActionId.Sheathe, true);
        }

        private ViewPose _shownMainPose, _shownOffPose;

        /// <summary>Mudou a pose da arma no Inspector com o jogo rodando: recoloca a arma na mão na hora.</summary>
        private void RefreshEditedPoses()
        {
            static bool Changed(WeaponData w, ViewPose shown) =>
                w != null && (w.ModelPose.position != shown.position || w.ModelPose.rotation != shown.rotation);
            if (_state == EquipState.Ready && (Changed(_shownMain, _shownMainPose) || Changed(_shownOff, _shownOffPose)))
                SwapModels(_shownMain, _shownOff);
        }

        private void LateUpdate()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f) return;
#if UNITY_EDITOR
            RefreshEditedPoses();
#endif

            UpdateEquip(dt);
            if (_actions != null) _actions.Tick(dt);
            UpdateArmPoses(dt);
            UpdateMotion(dt);
            SnapSupportHand();
        }

        /// <summary>
        /// Armas de duas mãos: a mão esquerda segura o cabo de verdade (à frente da direita
        /// em lanças e armas de fogo; abaixo dela em espadas e martelos), com o pulso alinhado ao cabo. Corrige só a posição do braço esquerdo
        /// depois da pose/animação do quadro, então acompanha golpes, defesa e balanço.
        /// </summary>
        private void SnapSupportHand()
        {
            if (_mainModel == null || _shownMain == null || !_shownMain.IsTwoHanded || _leftGrip == null || !_leftGrip.IsValid) return;
            var weapon = _mainModel.transform;
            // Peso da mão de apoio no cabo: as ações (sacar, beber, pegar) soltam a mão de propósito.
            var weight = _actions != null ? _actions.Pose.supportGrip : 1f;
            if (weight <= 0.001f) return;
            var target = weapon.position + weapon.up * (_shownMain.SupportGripDistance ?? SupportDistance(_shownMain.Category));
            // Armas de fogo e de haste: o cano/haste cruza a palma, o pulso dobra só um pouco (pose natural).
            _leftGrip.Align(Quaternion.Inverse(leftArm.rotation) * weapon.rotation,
                (_shownMain.SupportWristFollowOverride ?? SupportWristFollow(_shownMain.Category)) * weight);
            leftArm.position += (target - _leftGrip.Point.position) * weight;
        }

        private static float SupportWristFollow(WeaponCategory category) => category switch
        {
            WeaponCategory.Firearm => 0.3f,
            WeaponCategory.Spear => 0.5f,
            WeaponCategory.Magic => 0.5f,
            _ => 1f
        };

        private static float SupportDistance(WeaponCategory category) => category switch
        {
            WeaponCategory.Spear => 0.42f,      // lança: mão de apoio à frente, no cabo
            WeaponCategory.Firearm => 0.3f,     // arma de fogo: no guarda-mão
            WeaponCategory.Magic => 0.36f,
            WeaponCategory.Hammer => -0.2f,     // martelo: mão de apoio abaixo, perto do fim do cabo
            _ => -0.12f                         // espada de duas mãos: logo abaixo da mão direita, no punho
        };

        private void UpdateEquip(float dt)
        {
            switch (_state)
            {
                case EquipState.Lowering:
                    _lowerBlend = Mathf.MoveTowards(_lowerBlend, 1f, dt / LowerTime);
                    if (_lowerBlend >= 1f)
                    {
                        SwapModels(loadout.MainHand, loadout.EffectiveOffHand);
                        SnapArms();
                        _state = EquipState.Raising;
                        if (_actions != null) _actions.UseSet(_shownMain != null ? _shownMain.ViewModelActions : null);
                        if (_shownMain != null || _shownOff != null) PlayAction(ViewModelActionId.Draw, true);
                    }
                    break;
                case EquipState.Raising:
                    _lowerBlend = Mathf.MoveTowards(_lowerBlend, 0f, dt / RaiseTime);
                    if (_lowerBlend <= 0f) _state = EquipState.Ready;
                    break;
            }
        }

        // Trocar arma dura o mesmo que as animações de guardar/sacar (se forem mais longas).
        private float LowerTime => Mathf.Max(lowerTime, _actions != null ? _actions.DurationOf(ViewModelActionId.Sheathe) * 0.8f : 0f);
        private float RaiseTime => Mathf.Max(raiseTime, _actions != null ? _actions.DurationOf(ViewModelActionId.Draw) * 0.7f : 0f);

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
            var k = _actions != null ? _actions.Pose : ViewModelAction.Neutral(0f);
            rightArm.localPosition = _rPos + _actionPos + _recoil + k.rightPosition;
            rightArm.localRotation = Quaternion.Euler(k.rightEuler) * actionRot * _rRot;
            leftArm.localPosition = _lPos + (_actionBothArms ? _actionPos + _recoil : Vector3.zero) + k.leftPosition;
            leftArm.localRotation = Quaternion.Euler(k.leftEuler) * (_actionBothArms ? actionRot : Quaternion.identity) * _lRot;

            // Dedos: mão direita fechada no cabo (aberta sem arma só se a ação pedir); esquerda meio
            // fechada em repouso, fechada segurando escudo/cabo.
            if (_armsIK != null)
            {
                var leftHolding = _shownOff != null || _shownMain != null && _shownMain.IsTwoHanded;
                _armsIK.SetFingers(k.rightFingers, Mathf.Min(k.leftFingers, leftHolding ? 1f : 0.55f));
            }
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

            // Camadas com molas (olhar, andar/correr, pulo, aterrissagem, respiração) quando o
            // FirstPersonMovementAnimation existe; senão, o balanço antigo.
            Vector3 layerPos;
            Quaternion layerRot;
            if (_movement == null) _movement = GetComponent<FirstPersonMovementAnimation>();
            if (_movement != null && _movement.enabled)
            {
                _movement.Tick(dt);
                layerPos = _movement.Offset;
                layerRot = _movement.Rotation;
            }
            else
            {
                layerPos = _bob + Vector3.up * (breath + _landing);
                layerRot = Quaternion.Euler(_sway.y, -_sway.x, _sway.x * 0.5f);
            }

            var pos = viewOffset + (showArms ? Vector3.zero : weaponOnlyOffset) + layerPos
                      + sprintOffset.position * sprintEase
                      + loweredOffset * lowerEase;

            var rot = layerRot
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
            if (_trail != null)
            {
                if (_mainModel != null && BladeAxis(_mainModel.transform, out var tip)) _trail.SetWeapon(_mainModel.transform, tip * 0.35f, tip);
                else _trail.SetWeapon(null, Vector3.zero, Vector3.zero);
            }
            _shownMainPose = main != null ? main.ModelPose : default;
            _shownOffPose = off != null ? off.ModelPose : default;
        }

        /// <summary>
        /// Acha a lâmina no espaço do modelo: o eixo "para cima" da arma (o mesmo usado para a mão
        /// de apoio), do lado em que ela é mais comprida. Devolve a ponta.
        /// </summary>
        private static bool BladeAxis(Transform model, out Vector3 tip)
        {
            tip = Vector3.zero;
            float min = 0f, max = 0f;
            var any = false;
            foreach (var r in model.GetComponentsInChildren<MeshFilter>())
            {
                if (r.sharedMesh == null) continue;
                var bounds = r.sharedMesh.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var y = model.InverseTransformPoint(r.transform.TransformPoint(corner)).y;
                    min = Mathf.Min(min, y);
                    max = Mathf.Max(max, y);
                    any = true;
                }
            }
            if (!any) return false;
            var length = Mathf.Max(max, -min);
            if (length < 0.1f) return false;                       // punhos, objetos pequenos
            var dir = max >= -min ? Vector3.up : Vector3.down;
            tip = dir * length;
            return true;
        }

        private GameObject Spawn(WeaponData data, Transform socket)
        {
            var grip = socket.GetComponentInParent<HandGrip>();
            if (grip != null) grip.ResetPose();
            if (data == null || data.ViewModelPrefab == null) return null;

            GameObject model;
            if (grip != null && grip.IsValid)
            {
                // Mão de verdade: o pulso se inclina para o cabo e a arma vai para o centro do punho.
                grip.Align(data.ModelPose.Rotation);
                model = Instantiate(data.ViewModelPrefab, grip.Point);
                model.transform.SetPositionAndRotation(
                    grip.Point.position + grip.transform.rotation * data.ModelPose.position,
                    grip.transform.rotation * data.ModelPose.Rotation);
            }
            else
            {
                model = Instantiate(data.ViewModelPrefab, socket);
                model.transform.SetLocalPositionAndRotation(data.ModelPose.position, data.ModelPose.Rotation);
            }

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
