using System.Collections.Generic;
using Athoria.Core;
using Athoria.Core.Events;
using Athoria.Gameplay.Inventory;
using Athoria.Gameplay.Items;
using Athoria.Gameplay.Characters;
using Athoria.Gameplay.Player;
using Athoria.Gameplay.Stats;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Athoria.UI.Inventory
{
    /// <summary>
    /// Tela de inventário (I ou Tab). Pausa o jogo enquanto aberta.
    /// Clique: pega/solta o item (trocando se houver um por baixo).
    /// Botão direito: usa consumível ou equipa arma/escudo.
    /// Clicar fora do painel com um item na mão: larga no chão.
    /// </summary>
    public sealed class InventoryScreen : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private RectTransform grid;
        [SerializeField] private InventoryCell cellTemplate;
        [SerializeField] private RectTransform itemsLayer;
        [SerializeField] private InventoryItemView itemTemplate;
        [SerializeField] private InventoryItemView heldView;
        [SerializeField] private ItemTooltip tooltip;
        [SerializeField] private Text goldText;
        [SerializeField] private Text weightText;
        [SerializeField] private float cellSize = 56f;

        [Header("Equipamento e personagem")]
        [SerializeField] private EquipmentSlotView[] equipmentSlots;
        [SerializeField] private Image characterArt;
        [SerializeField] private Text characterName;
        [SerializeField] private Text statsText;

        [Header("Cores das células")]
        [SerializeField] private Color cellNormal = new(0.09f, 0.08f, 0.075f, 1f);
        [SerializeField] private Color cellFree = new(0.2f, 0.45f, 0.2f, 1f);
        [SerializeField] private Color cellSwap = new(0.5f, 0.42f, 0.15f, 1f);
        [SerializeField] private Color cellBlocked = new(0.5f, 0.15f, 0.12f, 1f);

        private PlayerInventory _inventory;
        private PlayerEquipment _equipment;
        private PlayerStats _stats;
        private PlayerExperience _experience;
        private EquipmentSlotView _hoverSlot;
        private readonly List<StatModifier> _mods = new();
        private PlayerInputReader _input;
        private InventoryCell[,] _cells;
        private readonly List<InventoryItemView> _views = new();
        private readonly List<GridEntry> _overlap = new();
        private ItemStack _held;
        private InventoryCell _hover;
        private bool _open;

        public bool IsOpen => _open;

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _inventory = player.GetComponent<PlayerInventory>();
                _input = player.GetComponent<PlayerInputReader>();
                _equipment = player.GetComponent<PlayerEquipment>();
                _stats = player.GetComponent<PlayerStats>();
                _experience = player.GetComponent<PlayerExperience>();
                var character = player.GetComponent<PlayerCharacter>();
                if (character != null && character.Current != null)
                {
                    if (characterArt != null)
                    {
                        characterArt.sprite = character.Current.FullBody != null ? character.Current.FullBody : character.Current.Portrait;
                        characterArt.enabled = characterArt.sprite != null;
                    }
                    if (characterName != null) characterName.text = $"{character.Current.DisplayName}\n<size=15><i>{character.Current.Title}</i></size>";
                }
            }
            if (_inventory == null)
            {
                Debug.LogWarning("[Athoria] InventoryScreen não encontrou o PlayerInventory.", this);
                enabled = false;
                return;
            }

            BuildCells();
            cellTemplate.gameObject.SetActive(false);
            itemTemplate.gameObject.SetActive(false);
            heldView.gameObject.SetActive(false);
            tooltip.Hide();
            SetVisible(false);

            if (_input != null)
            {
                _input.InventoryPressed += Toggle;
                _input.CharacterPressed += Toggle;
            }
            if (equipmentSlots != null) foreach (var s in equipmentSlots) s.Bind(this);
            EventBus.Subscribe<EquipmentChanged>(OnEquipmentChanged);
            EventBus.Subscribe<PlayerStatsChanged>(OnStatsChanged);
            EventBus.Subscribe<InventoryChanged>(OnInventoryChanged);
            EventBus.Subscribe<GamePausedChanged>(OnPauseChanged);
        }

        private void OnDestroy()
        {
            if (_input != null)
            {
                _input.InventoryPressed -= Toggle;
                _input.CharacterPressed -= Toggle;
            }
            EventBus.Unsubscribe<EquipmentChanged>(OnEquipmentChanged);
            EventBus.Unsubscribe<PlayerStatsChanged>(OnStatsChanged);
            EventBus.Unsubscribe<InventoryChanged>(OnInventoryChanged);
            EventBus.Unsubscribe<GamePausedChanged>(OnPauseChanged);
        }

        // ---------------- Abrir / fechar ----------------

        public void Toggle()
        {
            if (_open) Close();
            else Open();
        }

        public void Open()
        {
            if (_open) return;
            // Não abre por cima do menu de pausa.
            if (GameBootstrap.Instance != null && GameBootstrap.Instance.IsPaused) return;
            _open = true;
            SetVisible(true);
            Refresh();
            if (GameBootstrap.Instance != null) GameBootstrap.Instance.SetPaused(true);
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            ReturnHeld();
            tooltip.Hide();
            ResetCellColors();
            SetVisible(false);
            if (GameBootstrap.Instance != null) GameBootstrap.Instance.SetPaused(false);
        }

        private void OnPauseChanged(GamePausedChanged e)
        {
            // Esc despausa o jogo: o inventário fecha junto.
            if (!e.IsPaused && _open) Close();
        }

        private void SetVisible(bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;
        }

        private void ReturnHeld()
        {
            if (_held == null) return;
            var left = _inventory.Grid.Add(_held.Item, _held.Quantity);
            if (left > 0) _inventory.Drop(new ItemStack(_held.Item, left));
            _held = null;
            heldView.gameObject.SetActive(false);
        }

        // ---------------- Montagem ----------------

        private void BuildCells()
        {
            var g = _inventory.Grid;
            _cells = new InventoryCell[g.Width, g.Height];
            grid.sizeDelta = new Vector2(g.Width * cellSize, g.Height * cellSize);
            itemsLayer.sizeDelta = grid.sizeDelta;
            for (var x = 0; x < g.Width; x++)
            for (var y = 0; y < g.Height; y++)
            {
                var cell = Instantiate(cellTemplate, grid);
                cell.gameObject.SetActive(true);
                cell.name = $"Celula {x},{y}";
                var r = (RectTransform)cell.transform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0f, 1f);
                r.anchoredPosition = new Vector2(x * cellSize + 1f, -y * cellSize - 1f);
                r.sizeDelta = new Vector2(cellSize - 2f, cellSize - 2f);
                cell.Setup(this, x, y);
                cell.Image.color = cellNormal;
                _cells[x, y] = cell;
            }
        }

        private void OnInventoryChanged(InventoryChanged _)
        {
            if (_open) Refresh();
        }

        private void OnEquipmentChanged(EquipmentChanged _)
        {
            if (_open) Refresh();
        }

        private void OnStatsChanged(PlayerStatsChanged _)
        {
            if (_open) RefreshStats();
        }

        private void RefreshEquipment()
        {
            if (equipmentSlots == null || _equipment == null) return;
            foreach (var view in equipmentSlots)
            {
                view.Show(_equipment.Get(view.Slot));
                if (_held != null) view.Highlight(_held.Quantity == 1 && _equipment.CanEquip(_held.Item, view.Slot));
            }
        }

        private void RefreshStats()
        {
            if (statsText == null || _stats == null) return;
            var level = _experience != null ? $"Nível {_experience.Level}" : string.Empty;
            var mana = _stats.UsesMagic ? $"{_stats.MaxMana:0}" : "—";
            var sb = new System.Text.StringBuilder();
            sb.Append($"<b>{level}</b>\n");
            sb.Append($"Vida {_stats.MaxHealth:0}    Vigor {_stats.MaxStamina:0}    Mana {mana}\n");
            sb.Append($"Armadura {_stats.Armor:0} (-{_stats.ArmorReduction * 100f:0}% dano físico)    Velocidade {_stats.SpeedMultiplier * 100f:0}%\n");
            var phys = _stats.Get(StatType.PhysicalDamagePercent);
            var magic = _stats.Get(StatType.MagicDamagePercent);
            sb.Append($"Dano físico {(phys >= 0 ? "+" : "")}{phys:0}%    Dano mágico {(magic >= 0 ? "+" : "")}{magic:0}%");
            var resist = new System.Text.StringBuilder();
            for (var s = StatType.ResistPhysical; s <= StatType.ResistArcane; s++)
            {
                var v = _stats.Get(s);
                if (Mathf.Approximately(v, 0f)) continue;
                if (resist.Length > 0) resist.Append("   ");
                resist.Append($"{StatText.Name(s).Replace("Resistência ", "")} {v:0}%");
            }
            if (resist.Length > 0) sb.Append($"\nResistências: {resist}");
            statsText.text = sb.ToString();
        }

        // ---------------- Equipamento ----------------

        public void OnEquipmentClick(EquipmentSlotView view, PointerEventData.InputButton button)
        {
            if (_equipment == null) return;
            if (button == PointerEventData.InputButton.Left)
            {
                if (_held != null)
                {
                    if (_held.Quantity != 1 || !_equipment.CanEquip(_held.Item, view.Slot)) return;
                    var previous = _equipment.Equip(_held.Item, view.Slot);
                    SetHeld(previous != null ? new ItemStack(previous, 1) : null);
                }
                else
                {
                    var item = _equipment.Unequip(view.Slot);
                    if (item != null) SetHeld(new ItemStack(item, 1));
                }
            }
            else if (button == PointerEventData.InputButton.Right && _held == null)
            {
                _inventory.UnequipToGrid(view.Slot);
            }
            Refresh();
        }

        public void OnEquipmentEnter(EquipmentSlotView view)
        {
            _hoverSlot = view;
            var item = _equipment != null ? _equipment.Get(view.Slot) : null;
            if (item != null && _held == null) tooltip.Show(new ItemStack(item, 1), "Clique: pegar   •   Botão direito: guardar no inventário");
        }

        public void OnEquipmentExit(EquipmentSlotView view)
        {
            if (_hoverSlot == view) _hoverSlot = null;
            if (_hover == null) tooltip.Hide();
        }

        /// <summary>Texto "comparado ao equipado" para o item sob o mouse.</summary>
        private string BuildComparison(ItemData item)
        {
            if (_equipment == null || !_equipment.TryGetSlotFor(item, out var slot)) return null;
            var equipped = _equipment.Get(slot);
            var title = equipped != null ? $"Comparado a {equipped.DisplayName}:" : $"{PlayerEquipment.SlotName(slot)} vazio:";

            if (item is WeaponData w && !w.IsShield)
            {
                var ew = equipped as WeaponData;
                var dmg = w.Damage - (ew != null ? ew.Damage : 0f);
                var spd = w.AttackSpeed - (ew != null ? ew.AttackSpeed : 0f);
                return $"{title}\n{Delta("Dano", dmg, "0")}   {Delta("Ataques/s", spd, "0.0")}";
            }

            var totals = new Dictionary<StatType, float>();
            void Add(ItemData data, float sign)
            {
                switch (data)
                {
                    case ArmorData a:
                        totals[StatType.Armor] = totals.GetValueOrDefault(StatType.Armor) + a.Armor * sign;
                        foreach (var m in a.Modifiers) totals[m.stat] = totals.GetValueOrDefault(m.stat) + m.value * sign;
                        break;
                    case AccessoryData acc:
                        foreach (var m in acc.Modifiers) totals[m.stat] = totals.GetValueOrDefault(m.stat) + m.value * sign;
                        break;
                }
            }
            Add(item, 1f);
            if (equipped != null) Add(equipped, -1f);

            var sb = new System.Text.StringBuilder(title);
            var any = false;
            foreach (var kv in totals)
            {
                if (Mathf.Approximately(kv.Value, 0f)) continue;
                sb.Append('\n').Append(StatText.Line(kv.Key, kv.Value));
                any = true;
            }
            if (!any) sb.Append("\nSem diferença");
            return sb.ToString();
        }

        private static string Delta(string name, float v, string format)
        {
            var color = v > 0f ? "#8fdc7a" : v < 0f ? "#ff7a6a" : "#bbbbbb";
            return $"<color={color}>{(v >= 0 ? "+" : "")}{v.ToString(format)} {name}</color>";
        }

        private void Refresh()
        {
            foreach (var v in _views) Destroy(v.gameObject);
            _views.Clear();

            foreach (var e in _inventory.Grid.Entries)
            {
                var view = Instantiate(itemTemplate, itemsLayer);
                view.gameObject.SetActive(true);
                view.Show(e.Stack, cellSize);
                var r = view.Rect;
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0f, 1f);
                r.anchoredPosition = new Vector2(e.X * cellSize + 2f, -e.Y * cellSize - 2f);
                _views.Add(view);
            }

            goldText.text = $"Ouro: {_inventory.Gold}";
            weightText.text = $"Peso: {_inventory.Weight:0.#} / {_inventory.Capacity:0}" + (_inventory.IsOverweight ? "  (sobrecarregado!)" : string.Empty);
            weightText.color = _inventory.IsOverweight ? new Color(1f, 0.4f, 0.3f) : new Color(0.85f, 0.8f, 0.7f);
            RefreshEquipment();
            RefreshStats();
            UpdateHighlight();
        }

        // ---------------- Interação ----------------

        public void OnCellClick(InventoryCell cell, PointerEventData.InputButton button)
        {
            var g = _inventory.Grid;
            if (button == PointerEventData.InputButton.Left)
            {
                if (_held != null)
                {
                    TargetFor(_held, cell, out var tx, out var ty);
                    if (g.TryPlaceHeld(_held, tx, ty, out var nowHeld)) SetHeld(nowHeld);
                }
                else
                {
                    var entry = g.At(cell.X, cell.Y);
                    if (entry == null) return;
                    g.Remove(entry);
                    SetHeld(entry.Stack);
                }
            }
            else if (button == PointerEventData.InputButton.Right && _held == null)
            {
                _inventory.UseOrEquip(g.At(cell.X, cell.Y));
            }
            UpdateHighlight();
        }

        public void OnCellEnter(InventoryCell cell)
        {
            _hover = cell;
            UpdateHighlight();
        }

        public void OnCellExit(InventoryCell cell)
        {
            if (_hover == cell) _hover = null;
            UpdateHighlight();
        }

        public void OnBackdropClick()
        {
            if (_held == null) return;
            _inventory.Drop(_held);
            SetHeld(null);
        }

        private void SetHeld(ItemStack stack)
        {
            _held = stack;
            heldView.gameObject.SetActive(stack != null);
            if (stack != null) heldView.Show(stack, cellSize);
            tooltip.Hide();
            RefreshEquipment();
        }

        /// <summary>O item na mão fica centralizado no cursor: calcula o canto superior esquerdo.</summary>
        private void TargetFor(ItemStack stack, InventoryCell cell, out int x, out int y)
        {
            x = cell.X - (stack.Width - 1) / 2;
            y = cell.Y - (stack.Height - 1) / 2;
            var g = _inventory.Grid;
            x = Mathf.Clamp(x, 0, g.Width - stack.Width);
            y = Mathf.Clamp(y, 0, g.Height - stack.Height);
        }

        private void ResetCellColors()
        {
            if (_cells == null) return;
            foreach (var c in _cells) c.Image.color = cellNormal;
        }

        private void UpdateHighlight()
        {
            ResetCellColors();
            if (!_open) return;
            if (_hover == null)
            {
                if (_hoverSlot == null) tooltip.Hide();
                return;
            }
            var g = _inventory.Grid;

            if (_held != null)
            {
                TargetFor(_held, _hover, out var x, out var y);
                g.Overlapping(x, y, _held.Width, _held.Height, _overlap);
                var color = _overlap.Count == 0 ? cellFree : _overlap.Count == 1 ? cellSwap : cellBlocked;
                for (var i = x; i < x + _held.Width; i++)
                for (var j = y; j < y + _held.Height; j++)
                    _cells[i, j].Image.color = color;
                return;
            }

            var entry = g.At(_hover.X, _hover.Y);
            if (entry == null)
            {
                tooltip.Hide();
                return;
            }
            for (var i = entry.X; i < entry.X + entry.Width; i++)
            for (var j = entry.Y; j < entry.Y + entry.Height; j++)
                _cells[i, j].Image.color = new Color(0.16f, 0.14f, 0.11f);

            var item = entry.Stack.Item;
            var equippable = item is WeaponData || item is ArmorData || item is AccessoryData;
            var hint = item is ConsumableData ? "Botão direito: usar" : equippable ? "Botão direito: equipar" : string.Empty;
            tooltip.Show(entry.Stack, hint, equippable ? BuildComparison(item) : null);
        }

        private void Update()
        {
            if (!_open || Mouse.current == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Mouse.current.position.ReadValue(), null, out var local)) return;

            if (_held != null)
            {
                var r = heldView.Rect;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = local;
            }
            if (tooltip.isActiveAndEnabled) tooltip.MoveTo(local, canvasRect);
        }
    }
}
