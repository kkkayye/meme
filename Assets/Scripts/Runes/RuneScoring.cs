using System.Collections.Generic;
using RuneArena.Combat;
using RuneArena.Core;

namespace RuneArena.Runes
{
    /// <summary>Bot scoring for runes (and the shared per-stat value tables items reuse): stat value x archetype weight + mechanic affinity + set synergy + rarity.</summary>
    public static class RuneScoring
    {
        /// <summary>Points per unit of a stat (flat) so different stats are comparable: 1 AD ~ 1 point.</summary>
        public static float FlatPointValue(StatType stat)
        {
            switch (stat)
            {
                case StatType.MaxHealth: return 0.12f;
                case StatType.HealthRegen: return 3f;
                case StatType.AttackDamage: return 1f;
                case StatType.AbilityPower: return 0.6f;
                case StatType.Armor: return 0.6f;
                case StatType.AttackSpeed: return 60f;
                case StatType.MoveSpeed: return 6f;
                case StatType.CooldownReduction: return 100f;
                case StatType.Lifesteal: return 80f;
                case StatType.CritChance: return 70f;
                case StatType.CritDamage: return 30f;
                case StatType.AttackRange: return 8f;
                default: return 1f;
            }
        }

        /// <summary>Points per whole percent of a stat (0.12 = 12 points before weighting).</summary>
        public static float PercentPointValue(StatType stat)
        {
            switch (stat)
            {
                case StatType.MaxHealth: return 1.2f;
                case StatType.AttackDamage: return 0.8f;
                case StatType.AbilityPower: return 0.7f;
                case StatType.AttackSpeed: return 0.8f;
                case StatType.MoveSpeed: return 1.2f;
                default: return 0.6f;
            }
        }

        /// <summary>How much an archetype values a stat (1 = neutral).</summary>
        public static float ArchetypeWeight(HeroArchetype archetype, StatType stat)
        {
            switch (archetype)
            {
                case HeroArchetype.Mage: return MageWeight(stat);
                case HeroArchetype.Bruiser: return BruiserWeight(stat);
                default: return AssassinWeight(stat);
            }
        }

        /// <summary>Affinity of an archetype for a rune set's mechanics (1 = neutral).</summary>
        public static float SetAffinity(HeroArchetype archetype, RuneSet set)
        {
            switch (set)
            {
                case RuneSet.Ember: return archetype == HeroArchetype.Mage ? 1.5f : 0.7f;
                case RuneSet.Iron: return archetype == HeroArchetype.Bruiser ? 1.5f : 0.8f;
                case RuneSet.Shadow: return archetype == HeroArchetype.Assassin ? 1.5f : 0.7f;
                case RuneSet.Storm: return 1.1f;
                default: return 1f;
            }
        }

        /// <summary>Score of a rune for a unit (higher = better). Deterministic; no randomness.</summary>
        public static float ScoreRune(Unit unit, RuneDefinition rune)
        {
            if (unit == null || rune == null) return 0f;
            HeroArchetype archetype = unit.Hero != null ? unit.Hero.Archetype : HeroArchetype.Bruiser;
            float score = ScoreModifiers(archetype, rune.StatModifiers);
            if (rune.HasHook) score += RuneTuning.MechanicFallback[(int)rune.Rarity] * SetAffinity(archetype, rune.Set);
            score += RuneTuning.RarityBonus[(int)rune.Rarity];
            if (rune.Set != RuneSet.None && unit.Runes != null)
            {
                int owned = unit.Runes.SetCount(rune.Set);
                bool newInSet = unit.Runes.Count(rune.Id) == 0;
                if (newInSet)
                {
                    score += owned * RuneTuning.SetSynergyPerOwnedRune;
                    if (owned + 1 >= GameConstants.RuneSetBonusCount && !unit.Runes.IsSetActive(rune.Set)) score += RuneTuning.SetCompletionBonus;
                }
            }
            return score;
        }

        /// <summary>Sum of weighted stat points for a modifier list.</summary>
        public static float ScoreModifiers(HeroArchetype archetype, IReadOnlyList<StatModifier> modifiers)
        {
            if (modifiers == null) return 0f;
            float score = 0f;
            for (int i = 0; i < modifiers.Count; i++)
            {
                StatModifier m = modifiers[i];
                float points = m.Flat * FlatPointValue(m.Stat) + m.Percent * RuneTuning.PercentPointScale * PercentPointValue(m.Stat);
                score += points * ArchetypeWeight(archetype, m.Stat);
            }
            return score;
        }

        /// <summary>Index of the best-scoring option, or -1 for an empty list.</summary>
        public static int BestIndex(Unit unit, IReadOnlyList<RuneDefinition> options)
        {
            int best = -1;
            float bestScore = float.MinValue;
            for (int i = 0; i < options.Count; i++)
            {
                float s = ScoreRune(unit, options[i]);
                if (s > bestScore)
                {
                    bestScore = s;
                    best = i;
                }
            }
            return best;
        }

        private static float MageWeight(StatType stat)
        {
            switch (stat)
            {
                case StatType.AbilityPower: return 1.6f;
                case StatType.CooldownReduction: return 1.4f;
                case StatType.AttackDamage: return 0.3f;
                case StatType.AttackSpeed: return 0.4f;
                case StatType.CritChance: return 0.2f;
                case StatType.Lifesteal: return 0.4f;
                case StatType.MaxHealth: return 0.9f;
                case StatType.MoveSpeed: return 1.1f;
                default: return 0.8f;
            }
        }

        private static float BruiserWeight(StatType stat)
        {
            switch (stat)
            {
                case StatType.MaxHealth: return 1.4f;
                case StatType.Armor: return 1.4f;
                case StatType.HealthRegen: return 1.2f;
                case StatType.AttackDamage: return 1.0f;
                case StatType.AbilityPower: return 0.1f;
                case StatType.CritChance: return 0.5f;
                default: return 0.9f;
            }
        }

        private static float AssassinWeight(StatType stat)
        {
            switch (stat)
            {
                case StatType.AttackDamage: return 1.5f;
                case StatType.CritChance: return 1.3f;
                case StatType.AttackSpeed: return 1.2f;
                case StatType.Lifesteal: return 1.1f;
                case StatType.MoveSpeed: return 1.2f;
                case StatType.AbilityPower: return 0.1f;
                case StatType.Armor: return 0.6f;
                default: return 0.8f;
            }
        }
    }
}
