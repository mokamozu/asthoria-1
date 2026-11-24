using Athoria.Core.Events;
using Athoria.Gameplay.Interaction;
using Athoria.Gameplay.Inventory;
using Athoria.Gameplay.Items;
using UnityEngine;
using UnityEngine.Rendering;

namespace Athoria.Gameplay.World
{
    /// <summary>
    /// Item largado no mundo (loot, recompensa, item colocado na cena). Pode
    /// ser um item ou ouro. O visual é montado a partir do modelo do item;
    /// sem modelo, aparece uma bolsinha na cor da raridade.
    /// </summary>
    public sealed class WorldItem : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemData item;
        [SerializeField, Min(1)] private int quantity = 1;
        [SerializeField, Min(0), Tooltip("Se maior que 0, é um monte de ouro.")] private int gold;

        private bool _built;

        public ItemData Item => item;
        public int Quantity => quantity;

        public string InteractionPrompt =>
            gold > 0 ? $"Pegar {gold} de ouro"
            : item == null ? "Pegar"
            : quantity > 1 ? $"Pegar {item.DisplayName} (x{quantity})"
            : $"Pegar {item.DisplayName}";

        public bool CanInteract(GameObject interactor) => item != null || gold > 0;

        public void Interact(GameObject interactor)
        {
            var inventory = interactor.GetComponentInParent<PlayerInventory>();
            if (inventory == null) return;

            if (gold > 0)
            {
                inventory.AddGold(gold);
                Destroy(gameObject);
                return;
            }

            var left = inventory.AddItem(item, quantity);
            if (left <= 0) Destroy(gameObject);
            else quantity = left;
        }

        public static WorldItem Spawn(ItemData item, int quantity, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Item - " + (item != null ? item.DisplayName : "?"));
            go.transform.SetPositionAndRotation(position, rotation);
            var wi = go.AddComponent<WorldItem>();
            wi.item = item;
            wi.quantity = Mathf.Max(1, quantity);
            wi.Build();
            return wi;
        }

        public static WorldItem SpawnGold(int amount, Vector3 position)
        {
            var go = new GameObject("Ouro");
            go.transform.position = position;
            var wi = go.AddComponent<WorldItem>();
            wi.gold = amount;
            wi.Build();
            return wi;
        }

        private void Start() => Build();

        private void Build()
        {
            if (_built) return;
            _built = true;

            var rarity = item != null ? item.Rarity : ItemRarity.Common;
            var color = gold > 0 ? new Color(1f, 0.82f, 0.3f) : ItemText.RarityColor(rarity);
            var model = item != null ? item.WorldModel : null;

            if (GetComponentInChildren<Renderer>() == null)
            {
                if (model != null)
                {
                    var visual = Instantiate(model, transform);
                    visual.transform.localPosition = new Vector3(0f, 0.06f, 0f);
                    visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // armas (+Y) deitadas
                    foreach (var c in visual.GetComponentsInChildren<Collider>()) Destroy(c);
                }
                else
                {
                    var bag = GameObject.CreatePrimitive(gold > 0 ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                    bag.name = gold > 0 ? "Moedas" : "Bolsa";
                    Destroy(bag.GetComponent<Collider>());
                    bag.transform.SetParent(transform, false);
                    bag.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                    bag.transform.localScale = gold > 0 ? new Vector3(0.25f, 0.04f, 0.25f) : new Vector3(0.22f, 0.2f, 0.22f);
                    var r = bag.GetComponent<Renderer>();
                    var block = new MaterialPropertyBlock();
                    block.SetColor("_BaseColor", color);
                    r.SetPropertyBlock(block);
                }
            }

            foreach (var r in GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;

            // Colisor gatilho para a mira encontrar o item (não bloqueia o andar).
            var bounds = new Bounds(transform.position + Vector3.up * 0.1f, Vector3.one * 0.3f);
            foreach (var r in GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            if (!TryGetComponent<BoxCollider>(out var box)) box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = transform.InverseTransformPoint(bounds.center);
            box.size = Vector3.Max(transform.InverseTransformVector(bounds.size).Abs(), Vector3.one * 0.3f);

            // Brilho para itens acima de comum e ouro.
            if (gold > 0 || rarity > ItemRarity.Common)
            {
                var lightGo = new GameObject("Brilho");
                lightGo.transform.SetParent(transform, false);
                lightGo.transform.localPosition = Vector3.up * 0.3f;
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = color;
                light.range = 1.6f;
                light.intensity = 1.2f;
                light.shadows = LightShadows.None;
            }
        }
    }

    internal static class VectorExtensions
    {
        public static Vector3 Abs(this Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
