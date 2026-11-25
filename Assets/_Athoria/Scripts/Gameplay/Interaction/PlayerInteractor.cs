using Athoria.Core.Events;
using Athoria.Gameplay.Player;
using UnityEngine;

namespace Athoria.Gameplay.Interaction
{
    /// <summary>
    /// Lança um raio a partir da câmera, encontra o IInteractable mirado e
    /// executa a interação quando o jogador aperta o botão.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private Transform viewOrigin;
        [SerializeField] private float range = 2.6f;
        [SerializeField] private LayerMask mask = ~0;

        private readonly RaycastHit[] _hits = new RaycastHit[8];
        private Athoria.Gameplay.Combat.PlayerWeaponView _view;
        private bool _pending;
        private IInteractable _current;
        private string _lastPrompt;

        public IInteractable Current => _current;
        public string CurrentPrompt => _lastPrompt;
        /// <summary>Último collider atingido pelo raio (diagnóstico).</summary>
        public Collider LastHit { get; private set; }
        public float LastHitDistance { get; private set; }

        private void Reset() => input = GetComponent<PlayerInputReader>();

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            _view = GetComponentInChildren<Athoria.Gameplay.Combat.PlayerWeaponView>(true);
            if (viewOrigin == null)
            {
                Debug.LogError("[Athoria] PlayerInteractor sem viewOrigin (câmera).", this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            _current = null;
            PublishIfChanged(null);
        }

        private void Update()
        {
            _current = FindTarget();
            var prompt = _current != null && _current.CanInteract(gameObject) ? _current.InteractionPrompt : null;
            PublishIfChanged(prompt);

            if (input.ConsumeInteract() && prompt != null && !_pending)
            {
                // A mão vai até o objeto e a interação acontece no meio do gesto.
                var target = _current;
                var id = target is Athoria.Gameplay.World.Chest ? Athoria.Gameplay.Combat.ViewModelActionId.OpenChest
                    : target is Athoria.Gameplay.World.WorldItem ? Athoria.Gameplay.Combat.ViewModelActionId.PickUp
                    : Athoria.Gameplay.Combat.ViewModelActionId.Interact;
                _pending = true;
                void Run()
                {
                    _pending = false;
                    if (target == null || target is Object o && o == null || !target.CanInteract(gameObject)) return;
                    target.Interact(gameObject);
                    // O texto costuma mudar após interagir (Abrir -> Fechar).
                    if (_current == target) PublishIfChanged(target.CanInteract(gameObject) ? target.InteractionPrompt : null);
                }
                if (_view == null || !_view.PlayAction(id, false, Run)) { if (_pending) Run(); }
            }
        }

        private IInteractable FindTarget()
        {
            var ray = new Ray(viewOrigin.position, viewOrigin.forward);
            var count = Physics.RaycastNonAlloc(ray, _hits, range, mask, QueryTriggerInteraction.Collide);

            // Pega o acerto mais próximo que não seja o próprio jogador.
            RaycastHit? closest = null;
            for (var i = 0; i < count; i++)
            {
                var h = _hits[i];
                if (h.collider.transform.IsChildOf(transform)) continue;
                // Gatilhos invisíveis (volumes de local, áreas) só contam se forem interagíveis.
                if (h.collider.isTrigger && h.collider.GetComponentInParent<IInteractable>() == null) continue;
                if (closest == null || h.distance < closest.Value.distance) closest = h;
            }

            if (closest == null)
            {
                LastHit = null;
                return null;
            }

            LastHit = closest.Value.collider;
            LastHitDistance = closest.Value.distance;
            return LastHit.GetComponentInParent<IInteractable>();
        }

        private void PublishIfChanged(string prompt)
        {
            if (prompt == _lastPrompt) return;
            _lastPrompt = prompt;
            EventBus.Publish(new InteractionTargetChanged(prompt));
        }
    }
}
