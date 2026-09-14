namespace RuneArena.Core
{
    /// <summary>Team identifier. Blue contains the human player (index 0) when HumanPlayer is enabled.</summary>
    public enum Team
    {
        Blue = 0,
        Red = 1
    }

    /// <summary>All unit stats. Final value = (base + sum flat) * (1 + sum percent).</summary>
    public enum StatType
    {
        MaxHealth,
        HealthRegen,
        AttackDamage,
        AbilityPower,
        AttackSpeed,
        MoveSpeed,
        Armor,
        CooldownReduction,
        Lifesteal,
        CritChance,
        CritDamage,
        AttackRange
    }

    /// <summary>Damage school. True ignores armor (shields still absorb it).</summary>
    public enum DamageType
    {
        Physical,
        Magical,
        True
    }

    /// <summary>Origin of a damage instance; drives lifesteal (Basic only), hooks and juice.</summary>
    public enum DamageTag
    {
        Basic,
        Skill,
        Item,
        Rune,
        Burn,
        Reflect
    }

    /// <summary>Skill slot on a hero.</summary>
    public enum SkillKey
    {
        Basic = 0,
        Q = 1,
        W = 2,
        E = 3,
        R = 4
    }

    /// <summary>Geometric resolution shape of a skill (see DESIGN.md section 5).</summary>
    public enum SkillShape
    {
        Projectile,
        Fan,
        Cone,
        Circle,
        SelfCircle,
        Dash,
        Blink,
        Buff,
        Channel,
        Melee
    }

    /// <summary>Secondary effects a skill can apply on hit (or on self for Buff shapes).</summary>
    public enum SkillEffectType
    {
        Knockback,
        Stun,
        Slow,
        Shield,
        Heal,
        Invisible,
        DamageReduction,
        SpeedBoost,
        Pull,
        /// <summary>Self: outgoing basic/skill damage multiplied by (1 + Value) for Duration (燃血).</summary>
        DamageAmp
    }

    /// <summary>Hint for the bot brain on when to use a skill.</summary>
    public enum AiHint
    {
        None,
        Engage,
        Poke,
        Escape,
        Burst,
        Defensive,
        Ult
    }

    /// <summary>Rune rarity; drives draft weights, card colors and loot.</summary>
    public enum RuneRarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    /// <summary>Rune set. Owning 3 runes of one set triggers its set bonus once.</summary>
    public enum RuneSet
    {
        None,
        Ember,
        Iron,
        Shadow,
        Storm
    }

    /// <summary>Item tier; tier T unlocks from round T.</summary>
    public enum ItemTier
    {
        T1 = 1,
        T2 = 2,
        T3 = 3
    }

    /// <summary>Match state machine phases (see DESIGN.md section 2).</summary>
    public enum MatchPhase
    {
        MainMenu,
        HeroSelect,
        RuneDraft,
        Shop,
        Countdown,
        Combat,
        RoundEnd,
        MatchEnd
    }

    /// <summary>Hero archetype; used by bot scoring tables.</summary>
    public enum HeroArchetype
    {
        Mage,
        Bruiser,
        Assassin
    }

    /// <summary>What a Unit is: a hero (drafts, shops, scores), a lane minion, or a static tower.</summary>
    public enum UnitKind
    {
        Hero,
        Minion,
        Tower
    }

    /// <summary>Runtime status effect types tracked by StatusEffects.</summary>
    public enum StatusType
    {
        Stun,
        Slow,
        SpeedBoost,
        DamageReduction,
        Invisible,
        Burn,
        Root,
        /// <summary>Outgoing damage amplification (magnitude = bonus fraction).</summary>
        DamageAmp
    }
}
