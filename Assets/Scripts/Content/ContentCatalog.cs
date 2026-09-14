using System;
using System.Collections.Generic;
using RuneArena.Core;

namespace RuneArena.Content
{
    /// <summary>Single entry point to all immutable content; delegates to HeroCatalog / RuneCatalog / ItemCatalog / LootTables and caches id lookups.</summary>
    public static class ContentCatalog
    {
        private static Dictionary<string, HeroDefinition> _heroById;
        private static Dictionary<string, RuneDefinition> _runeById;
        private static Dictionary<string, ItemDefinition> _itemById;

        public static IReadOnlyList<HeroDefinition> Heroes => HeroCatalog.All;
        public static IReadOnlyList<RuneDefinition> Runes => RuneCatalog.All;
        public static IReadOnlyList<ItemDefinition> Items => ItemCatalog.All;
        public static IReadOnlyList<LootEntry> LootTable => LootTables.Chest;

        /// <summary>Returns the hero with the id, or null.</summary>
        public static HeroDefinition GetHero(string id)
        {
            if (id == null) return null;
            EnsureIndex();
            return _heroById.TryGetValue(id, out HeroDefinition hero) ? hero : null;
        }

        /// <summary>Returns the rune with the id, or null.</summary>
        public static RuneDefinition GetRune(string id)
        {
            if (id == null) return null;
            EnsureIndex();
            return _runeById.TryGetValue(id, out RuneDefinition rune) ? rune : null;
        }

        /// <summary>Returns the item with the id, or null.</summary>
        public static ItemDefinition GetItem(string id)
        {
            if (id == null) return null;
            EnsureIndex();
            return _itemById.TryGetValue(id, out ItemDefinition item) ? item : null;
        }

        /// <summary>Runes of a given rarity (new list).</summary>
        public static List<RuneDefinition> RunesOfRarity(RuneRarity rarity)
        {
            var list = new List<RuneDefinition>();
            IReadOnlyList<RuneDefinition> all = Runes;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Rarity == rarity) list.Add(all[i]);
            }
            return list;
        }

        /// <summary>Items of a given tier (new list).</summary>
        public static List<ItemDefinition> ItemsOfTier(ItemTier tier)
        {
            var list = new List<ItemDefinition>();
            IReadOnlyList<ItemDefinition> all = Items;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Tier == tier) list.Add(all[i]);
            }
            return list;
        }

        /// <summary>Drops cached lookups (call if catalogs are rebuilt, e.g. in tests).</summary>
        public static void InvalidateIndex()
        {
            _heroById = null;
            _runeById = null;
            _itemById = null;
        }

        private static void EnsureIndex()
        {
            if (_heroById != null && _runeById != null && _itemById != null) return;
            _heroById = Index(Heroes, h => h.Id, "hero");
            _runeById = Index(Runes, r => r.Id, "rune");
            _itemById = Index(Items, i => i.Id, "item");
        }

        private static Dictionary<string, T> Index<T>(IReadOnlyList<T> items, Func<T, string> key, string label)
        {
            var map = new Dictionary<string, T>();
            if (items == null) return map;
            for (int i = 0; i < items.Count; i++)
            {
                T item = items[i];
                if (item == null) continue;
                string id = key(item);
                if (string.IsNullOrEmpty(id)) throw new InvalidOperationException("A " + label + " definition has an empty Id.");
                if (map.ContainsKey(id)) throw new InvalidOperationException("Duplicate " + label + " id: " + id);
                map[id] = item;
            }
            return map;
        }
    }
}
