using System;

namespace RuneArena.Core
{
    /// <summary>Kind of reward a chest can give (DESIGN.md section 8).</summary>
    public enum LootKind
    {
        Gold,
        CommonRune,
        RareItem,
        EpicRune,
        LegendaryItem
    }

    /// <summary>Immutable weighted entry of the chest loot table.</summary>
    public sealed class LootEntry
    {
        public LootKind Kind { get; init; }
        public float Weight { get; init; }

        public LootEntry() { }

        public LootEntry(LootKind kind, float weight)
        {
            Kind = kind;
            Weight = weight;
        }

        /// <summary>True for the "big" rewards that pity guarantees.</summary>
        public bool IsPityEligible => Kind == LootKind.EpicRune || Kind == LootKind.LegendaryItem;

        public static bool IsPityKind(LootKind kind)
        {
            return kind == LootKind.EpicRune || kind == LootKind.LegendaryItem;
        }
    }

    /// <summary>Immutable rolled chest reward. Exactly one of Rune / Item / Gold is meaningful depending on Kind (Gold may replace a full-inventory item).</summary>
    public sealed class LootResult
    {
        public LootKind Kind { get; init; }
        /// <summary>Display name for the reveal card (rune name, item name or "+200 Gold").</summary>
        public string RewardName { get; init; } = "";
        public RuneDefinition Rune { get; init; }
        public ItemDefinition Item { get; init; }
        public int Gold { get; init; }
        /// <summary>True when this roll was forced by pity.</summary>
        public bool FromPity { get; init; }

        public static LootResult OfGold(int gold, bool fromPity = false)
        {
            return new LootResult { Kind = LootKind.Gold, Gold = gold, RewardName = "+" + gold + " Gold", FromPity = fromPity };
        }

        public static LootResult OfRune(LootKind kind, RuneDefinition rune, bool fromPity = false)
        {
            if (rune == null) throw new ArgumentNullException(nameof(rune));
            return new LootResult { Kind = kind, Rune = rune, RewardName = rune.Name, FromPity = fromPity };
        }

        public static LootResult OfItem(LootKind kind, ItemDefinition item, bool fromPity = false)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            return new LootResult { Kind = kind, Item = item, RewardName = item.Name, FromPity = fromPity };
        }

        /// <summary>Rarity used for the reveal card color.</summary>
        public RuneRarity DisplayRarity
        {
            get
            {
                switch (Kind)
                {
                    case LootKind.CommonRune: return RuneRarity.Common;
                    case LootKind.RareItem: return RuneRarity.Rare;
                    case LootKind.EpicRune: return RuneRarity.Epic;
                    case LootKind.LegendaryItem: return RuneRarity.Legendary;
                    default: return RuneRarity.Common;
                }
            }
        }
    }
}
