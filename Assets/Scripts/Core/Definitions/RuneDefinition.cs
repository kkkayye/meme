using System;
using System.Collections.Generic;

namespace RuneArena.Core
{
    /// <summary>Immutable rune definition: rarity, set, stacking rules, stat modifiers and an optional combat hook factory.</summary>
    public sealed class RuneDefinition
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public RuneRarity Rarity { get; init; } = RuneRarity.Common;
        public RuneSet Set { get; init; } = RuneSet.None;
        /// <summary>Stackable stat runes may be drafted up to MaxStacks times; unique runes only once.</summary>
        public bool Stackable { get; init; }
        /// <summary>Max stacks when Stackable (default 3). Ignored (treated as 1) when not stackable.</summary>
        public int MaxStacks { get; init; } = GameConstants.StackableRuneMaxStacks;
        /// <summary>Stat modifiers applied per stack. SourceId is overridden by RuneInventory with the rune id.</summary>
        public IReadOnlyList<StatModifier> StatModifiers { get; init; } = Array.Empty<StatModifier>();
        /// <summary>Creates a fresh hook instance per owning unit, or null for pure stat runes.</summary>
        public Func<ICombatHook> HookFactory { get; init; }
        /// <summary>1-2 character label for HUD squares; defaults to the first two characters of Name.</summary>
        public string ShortLabel { get; init; }

        public int EffectiveMaxStacks => Stackable ? Math.Max(1, MaxStacks) : 1;
        public bool HasHook => HookFactory != null;

        public string GetShortLabel()
        {
            if (!string.IsNullOrEmpty(ShortLabel)) return ShortLabel;
            if (string.IsNullOrEmpty(Name)) return "?";
            return Name.Length <= 2 ? Name : Name.Substring(0, 2);
        }
    }
}
