using System;
using System.Collections.Generic;
using RuneArena.Combat;
using RuneArena.Content;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Items
{
    /// <summary>Per-unit item inventory: 6 slots, unique items, applies stat modifiers and attaches hooks on add, removes them on remove/sell.</summary>
    [DisallowMultipleComponent]
    public sealed class Inventory : MonoBehaviour
    {
        private readonly ItemDefinition[] _slots = new ItemDefinition[GameConstants.InventorySlots];
        private readonly List<ItemDefinition> _items = new List<ItemDefinition>();
        private readonly Dictionary<string, ICombatHook> _hooks = new Dictionary<string, ICombatHook>();

        public Unit Owner { get; private set; }
        public int Capacity => GameConstants.InventorySlots;
        /// <summary>Fixed-length (6) view; null = empty slot. Preserves slot positions for the HUD.</summary>
        public IReadOnlyList<ItemDefinition> Slots => _slots;
        /// <summary>Compact list of owned items (no nulls).</summary>
        public IReadOnlyList<ItemDefinition> Items => _items;
        public int Count => _items.Count;
        public bool IsFull => _items.Count >= Capacity;

        private void Awake()
        {
            Owner = GetComponent<Unit>();
        }

        public bool Has(string itemId)
        {
            if (itemId == null) return false;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Id == itemId) return true;
            }
            return false;
        }

        /// <summary>True if not full and not already owned.</summary>
        public bool CanAdd(ItemDefinition item)
        {
            return item != null && !IsFull && !Has(item.Id);
        }

        /// <summary>Adds to the first empty slot, applies StatModifiers (SourceId = item id), attaches the hook, publishes ItemAcquired.</summary>
        public bool TryAdd(ItemDefinition item)
        {
            if (!CanAdd(item)) return false;
            if (Owner == null) Owner = GetComponent<Unit>();
            int slot = Array.IndexOf(_slots, null);
            if (slot < 0) return false;
            _slots[slot] = item;
            _items.Add(item);
            Owner.Stats.AddModifiers(item.StatModifiers, ItemSource.Of(item.Id));
            if (item.HookFactory != null)
            {
                ICombatHook hook = item.HookFactory();
                if (hook != null)
                {
                    hook.Attach(Owner);
                    _hooks[item.Id] = hook;
                }
            }
            EventBus.Publish(new ItemAcquired(Owner, item));
            return true;
        }

        /// <summary>Removes the item, its modifiers and hook. Does NOT refund gold (ShopService.Sell does) and does not publish ItemSold.</summary>
        public bool Remove(ItemDefinition item)
        {
            if (item == null || !Has(item.Id)) return false;
            int slot = Array.IndexOf(_slots, item);
            if (slot >= 0) _slots[slot] = null;
            _items.Remove(item);
            if (Owner != null) Owner.Stats.RemoveBySource(ItemSource.Of(item.Id));
            if (_hooks.TryGetValue(item.Id, out ICombatHook hook))
            {
                hook.Detach();
                _hooks.Remove(item.Id);
            }
            return true;
        }

        /// <summary>Removes everything (detaching hooks and modifiers).</summary>
        public void ClearAll()
        {
            for (int i = _items.Count - 1; i >= 0; i--) Remove(_items[i]);
        }

        private void OnDestroy()
        {
            ClearAll();
        }
    }
}
