namespace RuneArena.Content
{
    /// <summary>String ids of every item in ItemCatalog; also used as StatModifier / status / shield source ids by the item hooks.</summary>
    public static class ItemIds
    {
        // ---- Tier 1 ----
        public const string LongSword = "long_sword";
        public const string Staff = "staff";
        public const string LeatherArmor = "leather_armor";
        public const string Ruby = "ruby";
        public const string Boots = "boots";
        public const string Dagger = "dagger";
        public const string VampiricBlade = "vampiric_blade";
        public const string ChronoGem = "chrono_gem";

        // ---- Tier 2 ----
        public const string InfinityEdge = "infinity_edge";
        public const string FrostHammer = "frost_hammer";
        public const string Thornmail = "thornmail";
        public const string Bloodthirster = "bloodthirster";
        public const string PhantomDancer = "phantom_dancer";
        public const string Rabadon = "rabadon";

        // ---- Tier 3 ----
        public const string GuardianAngel = "guardian_angel";
        public const string DeathSentence = "death_sentence";
        public const string Archangel = "archangel";
        public const string WitchClaw = "witch_claw";
    }

    /// <summary>Tunable numbers of the item passives (DESIGN.md section 7). Kept here so hooks and catalog descriptions agree.</summary>
    public static class ItemPassiveConstants
    {
        /// <summary>冰霜之锤: basic attacks slow 25% for 1.5 s.</summary>
        public const float FrostHammerSlowFraction = 0.25f;
        public const float FrostHammerSlowSeconds = 1.5f;

        /// <summary>荆棘甲: 15% of post-mitigation damage taken is reflected as Magical.</summary>
        public const float ThornmailReflectFraction = 0.15f;

        /// <summary>血饮: on kill gain a 100 HP shield (duration not specified by the spec; 3 s chosen).</summary>
        public const float BloodthirsterShieldAmount = 100f;
        public const float BloodthirsterShieldSeconds = 3f;

        /// <summary>幽梦: on kill +30% move speed for 3 s.</summary>
        public const float PhantomDancerSpeedFraction = 0.30f;
        public const float PhantomDancerSpeedSeconds = 3f;

        /// <summary>守护天使: once per round, revive after 2 s with 40% max health.</summary>
        public const float GuardianAngelReviveDelaySeconds = 2f;
        public const float GuardianAngelReviveHealthFraction = 0.40f;

        /// <summary>死刑宣告: skill hits deal +8% of the target's missing health as True damage.</summary>
        public const float DeathSentenceMissingHealthFraction = 0.08f;

        /// <summary>大天使: skill hits heal 5% of the damage dealt.</summary>
        public const float ArchangelHealFraction = 0.05f;

        /// <summary>魔女之爪: skill hits apply a burn of 5% of the target's max health over 3 s.</summary>
        public const float WitchClawBurnMaxHealthFraction = 0.05f;
        public const float WitchClawBurnSeconds = 3f;
    }
}
