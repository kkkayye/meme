using System;
using System.Collections.Generic;

namespace RuneArena.Core
{
    /// <summary>Immutable item definition: tier, cost, stat modifiers and an optional combat hook factory. Items are unique per unit.</summary>
    public sealed class ItemDefinition
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public ItemTier Tier { get; init; } = ItemTier.T1;
        public int Cost { get; init; } = 500;
        /// <summary>Stat modifiers granted while owned. SourceId is overridden by Inventory with the item id.</summary>
        public IReadOnlyList<StatModifier> StatModifiers { get; init; } = Array.Empty<StatModifier>();
        /// <summary>Creates a fresh hook instance per owning unit, or null for pure stat items.</summary>
        public Func<ICombatHook> HookFactory { get; init; }

        public bool HasHook => HookFactory != null;

        /// <summary>Gold refunded when sold (70% of Cost, rounded down).</summary>
        public int SellValue => (int)Math.Floor(Cost * GameConstants.ItemSellRefundFraction);
    }
}
