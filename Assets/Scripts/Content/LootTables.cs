using System;
using System.Collections.Generic;
using RuneArena.Core;

namespace RuneArena.Content
{
    /// <summary>Weighted chest reward tables (DESIGN.md section 8): the normal table and the pity table (Epic rune / Legendary item only).</summary>
    public static class LootTables
    {
        /// <summary>Gold 40 / CommonRune 30 / RareItem 20 / EpicRune 8 / LegendaryItem 2.</summary>
        public static IReadOnlyList<LootEntry> Chest { get; } = new LootEntry[]
        {
            new LootEntry(LootKind.Gold, GameConstants.LootWeightGold),
            new LootEntry(LootKind.CommonRune, GameConstants.LootWeightCommonRune),
            new LootEntry(LootKind.RareItem, GameConstants.LootWeightRareItem),
            new LootEntry(LootKind.EpicRune, GameConstants.LootWeightEpicRune),
            new LootEntry(LootKind.LegendaryItem, GameConstants.LootWeightLegendaryItem)
        };

        /// <summary>Subset of Chest used on a pity roll (every 4th chest per unit): EpicRune 8 / LegendaryItem 2, i.e. 80% / 20%.</summary>
        public static IReadOnlyList<LootEntry> ChestPity { get; } = FilterPity(Chest);

        /// <summary>Sum of the (non-negative) weights of a table.</summary>
        public static float TotalWeight(IReadOnlyList<LootEntry> table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            float total = 0f;
            for (int i = 0; i < table.Count; i++)
            {
                total += Math.Max(0f, table[i].Weight);
            }
            return total;
        }

        /// <summary>Weight of a kind in the normal chest table (0 if absent).</summary>
        public static float WeightOf(LootKind kind)
        {
            LootEntry entry = Find(Chest, kind);
            return entry != null ? entry.Weight : 0f;
        }

        /// <summary>Probability (0..1) of a kind in the given table.</summary>
        public static float ProbabilityOf(IReadOnlyList<LootEntry> table, LootKind kind)
        {
            float total = TotalWeight(table);
            if (total <= 0f) return 0f;
            LootEntry entry = Find(table, kind);
            return entry != null ? Math.Max(0f, entry.Weight) / total : 0f;
        }

        /// <summary>First entry of the given kind, or null.</summary>
        public static LootEntry Find(IReadOnlyList<LootEntry> table, LootKind kind)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            for (int i = 0; i < table.Count; i++)
            {
                if (table[i] != null && table[i].Kind == kind) return table[i];
            }
            return null;
        }

        private static IReadOnlyList<LootEntry> FilterPity(IReadOnlyList<LootEntry> table)
        {
            var list = new List<LootEntry>();
            for (int i = 0; i < table.Count; i++)
            {
                if (table[i].IsPityEligible) list.Add(table[i]);
            }
            return list.ToArray();
        }
    }
}
