using Athoria.Gameplay.Interaction;
using UnityEngine;

namespace Athoria.Gameplay.World
{
    /// <summary>
    /// Porta que gira na dobradiça (o transform deste componente). Abre para o
    /// lado oposto de quem interagiu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimpleDoor : MonoBehaviour, IInteractable
    {
        [SerializeField] private float openAngle = 100f;
        [SerializeField, Tooltip("Graus por segundo.")] private float speed = 220f;
        [SerializeField] private bool locked;
        [SerializeField] private string openPrompt = "Abrir porta";
        [SerializeField] private string closePrompt = "Fechar porta";
        [SerializeField] private string lockedPrompt = "Trancada";

        private Quaternion _closedRotation;
        private float _currentAngle;
        private float _targetAngle;

        public bool IsOpen => !Mathf.Approximately(_targetAngle, 0f);
        public bool IsLocked { get => locked; set => locked = value; }

        public string InteractionPrompt => locked ? lockedPrompt : IsOpen ? closePrompt : openPrompt;

        private void Awake() => _closedRotation = transform.localRotation;

        public bool CanInteract(GameObject interactor) => true;

        public void Interact(GameObject interactor)
        {
            if (locked) return;

            if (IsOpen)
            {
                _targetAngle = 0f;
                return;
            }

            // Abre para longe de quem interagiu.
            var toInteractor = interactor.transform.position - transform.position;
            // Girar +Y leva a folha (+X) para -Z; então o sinal acompanha o lado do jogador.
            var side = Vector3.Dot(transform.forward, toInteractor) >= 0f ? 1f : -1f;
            _targetAngle = openAngle * side;
        }

        private void Update()
        {
            if (Mathf.Approximately(_currentAngle, _targetAngle)) return;
            _currentAngle = Mathf.MoveTowards(_currentAngle, _targetAngle, speed * Time.deltaTime);
            transform.localRotation = _closedRotation * Quaternion.Euler(0f, _currentAngle, 0f);
        }
    }
}
