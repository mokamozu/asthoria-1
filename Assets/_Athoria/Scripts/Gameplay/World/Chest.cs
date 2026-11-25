using System.Collections;
using Athoria.Core.Events;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Effects;
using Athoria.Gameplay.Interaction;
using Athoria.Gameplay.Inventory;
using Athoria.Gameplay.Items;
using Athoria.Gameplay.Loot;
using UnityEngine;

namespace Athoria.Gameplay.World
{
    /// <summary>Um baú foi aberto (para o save da Etapa 14 e para missões).</summary>
    public readonly struct ChestOpened : IGameEvent
    {
        public readonly string Id;
        public ChestOpened(string id) => Id = id;
    }

    /// <summary>
    /// Baú com tabela de loot. Pode estar trancado e exigir um item-chave.
    /// Ao abrir: a tampa gira, sai o brilho (PixelFX) e os itens saltam para a frente.
    /// </summary>
    public sealed class Chest : MonoBehaviour, IInteractable
    {
        [SerializeField, Tooltip("Identificador único na cena (para o save).")] private string id;
        [SerializeField] private LootTable loot;

        [Header("Tranca")]
        [SerializeField, Tooltip("Vazio = destrancado.")] private ItemData requiredKey;
        [SerializeField] private bool consumeKey = true;

        [Header("Visual")]
        [SerializeField, Tooltip("Pivô da tampa (na dobradiça, atrás).")] private Transform lid;
        [SerializeField] private float openAngle = -105f;
        [SerializeField] private float openTime = 0.45f;
        [SerializeField] private SpriteFxAnimation openFx;
        [SerializeField] private float fxScale = 1f;
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip lockedSound;

        private bool _opened;
        private bool _shaking;

        public string Id => id;
        public bool IsOpen => _opened;
        public bool IsLocked => requiredKey != null && !_opened;

        public string InteractionPrompt => requiredKey != null
            ? $"Abrir baú trancado ({requiredKey.DisplayName})"
            : "Abrir baú";

        public bool CanInteract(GameObject interactor) => !_opened;

        private void Start()
        {
            if (!string.IsNullOrEmpty(id) && WorldState.Has(WorldState.ChestKey(id))) SetOpenedSilently();
        }

        public void Interact(GameObject interactor)
        {
            if (_opened) return;

            if (requiredKey != null)
            {
                var inventory = interactor.GetComponentInParent<PlayerInventory>();
                if (inventory == null || inventory.CountOf(requiredKey) <= 0)
                {
                    EventBus.Publish(new GameMessage($"Trancado. Precisa de: {requiredKey.DisplayName}"));
                    CombatFeedback.Play(lockedSound, transform.position + Vector3.up * 0.5f);
                    if (!_shaking) StartCoroutine(Shake());
                    return;
                }
                if (consumeKey)
                {
                    inventory.TryConsume(requiredKey);
                    EventBus.Publish(new GameMessage($"Usou {requiredKey.DisplayName}"));
                }
            }

            _opened = true;
            var toPlayer = interactor.transform.position - transform.position;
            StartCoroutine(Open(toPlayer));
            EventBus.Publish(new ChestOpened(id));
        }

        /// <summary>Deixa aberto sem soltar loot (carregar save).</summary>
        public void SetOpenedSilently()
        {
            _opened = true;
            if (lid != null) lid.localRotation = Quaternion.Euler(openAngle, 0f, 0f);
        }

        private IEnumerator Open(Vector3 toPlayer)
        {
            CombatFeedback.Play(openSound, transform.position + Vector3.up * 0.5f);
            var start = lid != null ? lid.localRotation : Quaternion.identity;
            var end = Quaternion.Euler(openAngle, 0f, 0f);
            var fxDone = false;
            for (var t = 0f; t < 1f; t += Time.deltaTime / openTime)
            {
                if (lid != null) lid.localRotation = Quaternion.Slerp(start, end, 1f - (1f - t) * (1f - t));
                if (!fxDone && t > 0.4f)
                {
                    fxDone = true;
                    SpriteFx.Spawn(openFx, transform.position + Vector3.up * 0.7f, fxScale);
                }
                yield return null;
            }
            if (lid != null) lid.localRotation = end;

            // Os itens saltam em leque para o lado de quem abriu.
            var forward = toPlayer.sqrMagnitude > 0.01f ? toPlayer : transform.forward;
            LootSpawner.RollAndSpawn(loot, transform.position + Vector3.up * 0.8f, forward, 1.6f, 110f);
        }

        private IEnumerator Shake()
        {
            _shaking = true;
            var rest = transform.localRotation;
            for (var t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                transform.localRotation = rest * Quaternion.Euler(0f, Mathf.Sin(t * 60f) * 3f * (1f - t / 0.35f), 0f);
                yield return null;
            }
            transform.localRotation = rest;
            _shaking = false;
        }
    }
}
