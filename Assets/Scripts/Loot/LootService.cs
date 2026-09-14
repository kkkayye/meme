using System;
using System.Collections.Generic;
using RuneArena.Combat;
using RuneArena.Content;
using RuneArena.Core;

namespace RuneArena.Loot
{
    /// <summary>Chest reward rolling (weighted table + every-4th-chest pity per unit) and granting (gold / rune / item with full-inventory fallback to gold).</summary>
    public sealed class LootService
    {
        private readonly Dictionary<Unit, int> _opened = new Dictionary<Unit, int>();
        private readonly Rng _rng;

        public LootService() : this(null) { }

        /// <summary>Optional explicit rng (defaults to GameServices.Rng) for tests.</summary>
        public LootService(Rng rng)
        {
            _rng = rng;
        }

        private Rng Random => _rng ?? GameServices.RngOrDefault();

        /// <summary>True if the unit's NEXT chest is a pity chest (every 4th).</summary>
        public bool IsNextPity(Unit unit)
        {
            return (ChestsOpened(unit) + 1) % GameConstants.ChestPityEvery == 0;
        }

        /// <summary>Rolls a reward for the opener using the unit's pity counter. Does not grant it and does not count the chest.</summary>
        public LootResult RollReward(Unit opener)
        {
            if (opener == null) throw new ArgumentNullException(nameof(opener));
            bool pity = IsNextPity(opener);
            IReadOnlyList<LootEntry> table = pity ? LootTables.ChestPity : LootTables.Chest;
            LootEntry entry = Random.WeightedPick(table, e => e.Weight);
            return Resolve(opener, entry.Kind, pity);
        }

        /// <summary>Applies the reward (AddGold / Runes.Add / Items.TryAdd), increments the unit's chest count and publishes ChestOpened.</summary>
        public void GrantReward(Unit unit, LootResult result)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (result == null) throw new ArgumentNullException(nameof(result));
            _opened[unit] = ChestsOpened(unit) + 1;
            switch (result.Kind)
            {
                case LootKind.Gold:
                    unit.AddGold(result.Gold);
                    break;
                case LootKind.CommonRune:
                case LootKind.EpicRune:
                    if (unit.Runes == null || !unit.Runes.Add(result.Rune)) unit.AddGold(GameConstants.ChestGoldReward);
                    break;
                case LootKind.RareItem:
                case LootKind.LegendaryItem:
                    if (unit.Items == null || !unit.Items.TryAdd(result.Item)) unit.AddGold(GameConstants.ChestGoldReward);
                    break;
            }
            EventBus.Publish(new ChestOpened(unit, result.Kind, result.RewardName));
        }

        /// <summary>Roll + Grant in one call.</summary>
        public LootResult OpenChest(Unit opener)
        {
            LootResult result = RollReward(opener);
            GrantReward(opener, result);
            return result;
        }

        /// <summary>Chests opened by the unit this match.</summary>
        public int ChestsOpened(Unit unit)
        {
            return unit != null && _opened.TryGetValue(unit, out int count) ? count : 0;
        }

        /// <summary>Clears per-unit counters (match start).</summary>
        public void Reset()
        {
            _opened.Clear();
        }

        private LootResult Resolve(Unit unit, LootKind kind, bool pity)
        {
            switch (kind)
            {
                case LootKind.CommonRune: return RuneOrGold(unit, kind, RuneRarity.Common, RuneRarity.Rare, pity);
                case LootKind.EpicRune: return RuneOrGold(unit, kind, RuneRarity.Epic, RuneRarity.Legendary, pity);
                case LootKind.RareItem: return ItemOrGold(unit, kind, ItemTier.T1, ItemTier.T2, pity);
                case LootKind.LegendaryItem: return ItemOrGold(unit, kind, ItemTier.T3, ItemTier.T3, pity);
                default: return LootResult.OfGold(GameConstants.ChestGoldReward, pity);
            }
        }

        private LootResult RuneOrGold(Unit unit, LootKind kind, RuneRarity primary, RuneRarity fallback, bool pity)
        {
            List<RuneDefinition> options = EligibleRunes(unit, primary);
            if (options.Count == 0) options = EligibleRunes(unit, fallback);
            if (options.Count == 0) return LootResult.OfGold(GameConstants.ChestGoldReward, pity);
            return LootResult.OfRune(kind, Random.Pick(options), pity);
        }

        private LootResult ItemOrGold(Unit unit, LootKind kind, ItemTier minTier, ItemTier maxTier, bool pity)
        {
            if (unit.Items == null || unit.Items.IsFull) return LootResult.OfGold(GameConstants.ChestGoldReward, pity);
            var options = new List<ItemDefinition>();
            IReadOnlyList<ItemDefinition> items = ContentCatalog.Items;
            for (int i = 0; i < items.Count; i++)
            {
                ItemDefinition item = items[i];
                if (item.Tier < minTier || item.Tier > maxTier || unit.Items.Has(item.Id)) continue;
                options.Add(item);
            }
            if (options.Count == 0) return LootResult.OfGold(GameConstants.ChestGoldReward, pity);
            return LootResult.OfItem(kind, Random.Pick(options), pity);
        }

        private static List<RuneDefinition> EligibleRunes(Unit unit, RuneRarity rarity)
        {
            var options = new List<RuneDefinition>();
            IReadOnlyList<RuneDefinition> runes = ContentCatalog.Runes;
            for (int i = 0; i < runes.Count; i++)
            {
                RuneDefinition rune = runes[i];
                if (rune.Rarity != rarity) continue;
                if (unit.Runes != null && !unit.Runes.CanAdd(rune)) continue;
                options.Add(rune);
            }
            return options;
        }
    }
}
