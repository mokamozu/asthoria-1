using System;
using System.Collections.Generic;
using Athoria.Gameplay.Items;

namespace Athoria.Gameplay.Inventory
{
    /// <summary>Quantidade de um item ocupando um lugar (pilha). O Id é estável para o save.</summary>
    [Serializable]
    public sealed class ItemStack
    {
        public ItemData Item;
        public int Quantity;
        public string Id;

        public ItemStack(ItemData item, int quantity)
        {
            Item = item;
            Quantity = quantity;
            Id = Guid.NewGuid().ToString("N");
        }

        public int Width => Item.GridWidth;
        public int Height => Item.GridHeight;
        public float Weight => Item.Weight * Quantity;
    }

    /// <summary>Pilha posicionada na grade (canto superior esquerdo em X,Y).</summary>
    public sealed class GridEntry
    {
        public ItemStack Stack;
        public int X, Y;

        public int Width => Stack.Width;
        public int Height => Stack.Height;
        public bool Contains(int x, int y) => x >= X && x < X + Width && y >= Y && y < Y + Height;
        public bool Overlaps(int x, int y, int w, int h) => x < X + Width && X < x + w && y < Y + Height && Y < y + h;
    }

    /// <summary>
    /// Grade de inventário estilo Diablo II: cada item ocupa W×H células.
    /// Lógica pura (sem Unity), testável e usada pela interface e pelo save.
    /// </summary>
    public sealed class InventoryGrid
    {
        private readonly List<GridEntry> _entries = new();
        private readonly List<GridEntry> _scratch = new();

        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<GridEntry> Entries => _entries;

        public event Action Changed;

        public InventoryGrid(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public float TotalWeight
        {
            get
            {
                var w = 0f;
                foreach (var e in _entries) w += e.Stack.Weight;
                return w;
            }
        }

        public GridEntry At(int x, int y)
        {
            foreach (var e in _entries)
                if (e.Contains(x, y)) return e;
            return null;
        }

        public bool InBounds(int x, int y, int w, int h) => x >= 0 && y >= 0 && x + w <= Width && y + h <= Height;

        public void Overlapping(int x, int y, int w, int h, List<GridEntry> result, GridEntry ignore = null)
        {
            result.Clear();
            foreach (var e in _entries)
                if (e != ignore && e.Overlaps(x, y, w, h)) result.Add(e);
        }

        public bool IsFree(int x, int y, int w, int h, GridEntry ignore = null)
        {
            if (!InBounds(x, y, w, h)) return false;
            foreach (var e in _entries)
                if (e != ignore && e.Overlaps(x, y, w, h)) return false;
            return true;
        }

        /// <summary>Procura espaço livre coluna por coluna, de cima para baixo.</summary>
        public bool FindFree(int w, int h, out int fx, out int fy)
        {
            for (var x = 0; x + w <= Width; x++)
            for (var y = 0; y + h <= Height; y++)
            {
                if (!IsFree(x, y, w, h)) continue;
                fx = x;
                fy = y;
                return true;
            }
            fx = fy = -1;
            return false;
        }

        public bool Place(ItemStack stack, int x, int y)
        {
            if (!IsFree(x, y, stack.Width, stack.Height)) return false;
            _entries.Add(new GridEntry { Stack = stack, X = x, Y = y });
            Changed?.Invoke();
            return true;
        }

        public void Remove(GridEntry entry)
        {
            if (_entries.Remove(entry)) Changed?.Invoke();
        }

        /// <summary>Adiciona itens: completa pilhas existentes e depois ocupa espaços livres. Retorna o que não coube.</summary>
        public int Add(ItemData item, int quantity)
        {
            if (item == null || quantity <= 0) return 0;
            var changed = false;

            if (item.Stackable)
            {
                foreach (var e in _entries)
                {
                    if (quantity <= 0) break;
                    if (e.Stack.Item != item || e.Stack.Quantity >= item.MaxStack) continue;
                    var move = Math.Min(quantity, item.MaxStack - e.Stack.Quantity);
                    e.Stack.Quantity += move;
                    quantity -= move;
                    changed = true;
                }
            }

            while (quantity > 0 && FindFree(item.GridWidth, item.GridHeight, out var x, out var y))
            {
                var take = Math.Min(quantity, item.MaxStack);
                _entries.Add(new GridEntry { Stack = new ItemStack(item, take), X = x, Y = y });
                quantity -= take;
                changed = true;
            }

            if (changed) Changed?.Invoke();
            return quantity;
        }

        /// <summary>Quantas unidades de um item existem no inventário.</summary>
        public int Count(ItemData item)
        {
            var total = 0;
            foreach (var e in _entries)
                if (e.Stack.Item == item) total += e.Stack.Quantity;
            return total;
        }

        /// <summary>
        /// Solta o item "na mão" em X,Y. Espaço livre: coloca. Um item por baixo:
        /// troca (ele vai para a mão) ou completa a pilha se for igual.
        /// </summary>
        public bool TryPlaceHeld(ItemStack held, int x, int y, out ItemStack nowHeld)
        {
            nowHeld = held;
            if (!InBounds(x, y, held.Width, held.Height)) return false;

            Overlapping(x, y, held.Width, held.Height, _scratch);
            if (_scratch.Count == 0)
            {
                Place(held, x, y);
                nowHeld = null;
                return true;
            }
            if (_scratch.Count > 1) return false;

            var under = _scratch[0];
            if (under.Stack.Item == held.Item && held.Item.Stackable)
            {
                var move = Math.Min(held.Quantity, held.Item.MaxStack - under.Stack.Quantity);
                if (move <= 0) return false;
                under.Stack.Quantity += move;
                held.Quantity -= move;
                nowHeld = held.Quantity > 0 ? held : null;
                Changed?.Invoke();
                return true;
            }

            _entries.Remove(under);
            _entries.Add(new GridEntry { Stack = held, X = x, Y = y });
            nowHeld = under.Stack;
            Changed?.Invoke();
            return true;
        }

        public void NotifyChanged() => Changed?.Invoke();
    }
}
