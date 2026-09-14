using RuneArena.Core;

namespace RuneArena.Runes
{
    /// <summary>Tuning numbers for rune mechanics and draft scoring that DESIGN.md leaves to the implementer (no magic numbers in hooks).</summary>
    public static class RuneTuning
    {
        /// <summary>Prefix for every rune-owned StatModifier / status / shield source id.</summary>
        public const string SourcePrefix = "rune:";

        // ---- Mechanic runes (Rare / Epic) ----
        public const int TripleStrikeEvery = 3;
        public const float TripleStrikeBonus = 0.60f;
        public const float BurningTouchMaxHealthFraction = 0.04f;
        public const float BurningTouchDuration = 2f;
        public const float DashShieldMaxHealthFraction = 0.10f;
        public const float DashShieldDuration = 2f;
        public const float HunterHealFraction = 0.20f;
        public const float CounterStoreFraction = 0.08f;
        public const float CounterMaxStoredMaxHealthFraction = 0.40f;
        public const float ThornsReflectFraction = 0.12f;
        public const float LastStandHealthFraction = 0.30f;
        public const float LastStandBonusPercent = 0.25f;
        public const float WarFrenzyAttackSpeedPerStack = 0.08f;
        public const float WarFrenzyDuration = 3f;
        public const int WarFrenzyMaxStacks = 5;
        public const float FrostSlowFraction = 0.20f;
        public const float FrostSlowDuration = 1f;
        public const float EchoBonusDamage = 30f;
        public const float EchoCooldown = 3f;
        public const float SwiftShadowSpeedBoost = 0.40f;
        public const float SwiftShadowDuration = 3f;
        public const float FirstStrikeBonus = 0.40f;
        /// <summary>How long after the first cast its hits still count (covers projectile travel and Circle delay).</summary>
        public const float FirstStrikeWindowSeconds = 3f;
        public const float TenacityCrowdControlMultiplier = 0.60f;
        public const float RevivalShield = 150f;
        public const float RevivalShieldDuration = 5f;
        public const float FrostArmorSlowFraction = 0.20f;
        public const float FrostArmorSlowDuration = 1f;
        public const float SiphonHealFraction = 0.08f;

        // ---- Legendary transforms ----
        public const int DoubleCastExtraCharges = 1;
        public const float FlameTrailSeconds = 3f;
        public const float FlameTrailTickInterval = 0.25f;
        public const float FlameTrailBaseDamage = 20f;
        public const float FlameTrailApRatio = 0.20f;
        public const float FlameTrailRadius = 0.9f;
        public const float FlameTrailSegmentSpacing = 1f;
        public const int SplitShotBoltCount = 2;
        public const float SplitShotRange = 6f;
        public const float SplitShotDamageFraction = 0.30f;
        public const float SplitShotBoltSpeed = 24f;
        public const float UndyingDamageReduction = 0.60f;
        public const float UndyingDamageReductionSeconds = 3f;

        // ---- Set bonuses (numbers live in GameConstants; ids here) ----
        public static string SetSourceId(RuneSet set)
        {
            return GameConstants.SetBonusSourcePrefix + set;
        }

        // ---- Draft scoring (bots) ----
        /// <summary>Score bonus per rarity (Common, Rare, Epic, Legendary).</summary>
        public static readonly float[] RarityBonus = { 0f, 3f, 6f, 10f };
        /// <summary>Fallback mechanic score by rarity for hooks without an explicit archetype entry.</summary>
        public static readonly float[] MechanicFallback = { 0f, 8f, 14f, 20f };
        public const float SetSynergyPerOwnedRune = 3f;
        public const float SetCompletionBonus = 8f;
        /// <summary>Percent modifiers are scored per whole percent (0.12 = 12 points before archetype weighting).</summary>
        public const float PercentPointScale = 100f;

        public static string SourceId(string runeId)
        {
            return SourcePrefix + (runeId ?? "");
        }
    }
}
