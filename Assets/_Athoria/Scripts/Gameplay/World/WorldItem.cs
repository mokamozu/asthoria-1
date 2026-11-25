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
            : quantity > 1 ? $"Pegar {ColoredName} (x{quantity})"
            : $"Pegar {ColoredName}";

        /// <summary>Nome na cor da raridade (o aviso de interação aceita rich text).</summary>
        private string ColoredName =>
            $"<color=#{ColorUtility.ToHtmlStringRGB(ItemText.RarityColor(item.Rarity))}>{item.DisplayName}</color>";

        // Salto ao cair de um inimigo/baú
        private Vector3 _velocity;
        private bool _airborne;
        private float _airTime;
        private static Material s_beamMaterial;

        /// <summary>Lança o item no ar; ele cai e para no chão.</summary>
        public void Pop(Vector3 velocity)
        {
            _velocity = velocity;
            _airborne = true;
            _airTime = 0f;
        }

        private void Update()
        {
            if (!_airborne) return;
            var dt = Time.deltaTime;
            if (dt <= 0f) return;
            _airTime += dt;
            _velocity.y -= 12f * dt;
            var step = _velocity * dt;
            var pos = transform.position;

            if (Physics.Raycast(pos + Vector3.up * 0.05f, step.normalized, out var hit, step.magnitude + 0.05f, ~0, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform)
                && hit.collider.GetComponentInParent<Player.PlayerController>() == null)
            {
                if (hit.normal.y > 0.5f)
                {
                    transform.position = hit.point;
                    _airborne = false;
                    return;
                }
                // Bateu numa parede: perde força e escorrega para baixo.
                _velocity = Vector3.Reflect(_velocity, hit.normal) * 0.3f;
                return;
            }

            transform.position = pos + step;
            if (_airTime < 3f) return;
            _airborne = false;
            if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out var ground, 20f, ~0, QueryTriggerInteraction.Ignore))
                transform.position = ground.point;
        }

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
                light.range = rarity >= ItemRarity.Epic ? 2.4f : 1.6f;
                light.intensity = rarity >= ItemRarity.Epic ? 1.8f : 1.2f;
                light.shadows = LightShadows.None;
            }

            // Feixe de luz para Raro ou melhor: dá para ver de longe que caiu algo bom.
            if (gold <= 0 && rarity >= ItemRarity.Rare) AddBeam(color, rarity);
        }

        private void AddBeam(Color color, ItemRarity rarity)
        {
            if (s_beamMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) return;
                s_beamMaterial = new Material(shader) { name = "FeixeDeLoot" };
            }
            var beamGo = new GameObject("Feixe");
            beamGo.transform.SetParent(transform, false);
            var line = beamGo.AddComponent<LineRenderer>();
            line.sharedMaterial = s_beamMaterial;
            line.useWorldSpace = false;
            line.positionCount = 2;
            var height = rarity switch { ItemRarity.Legendary => 4f, ItemRarity.Epic => 3f, _ => 2.2f };
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, Vector3.up * height);
            line.widthCurve = new AnimationCurve(new Keyframe(0f, 0.09f), new Keyframe(1f, 0.01f));
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.Lerp(color, Color.white, 0.4f), 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0f, 1f) });
            line.colorGradient = gradient;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
        }
    }

    internal static class VectorExtensions
    {
        public static Vector3 Abs(this Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
