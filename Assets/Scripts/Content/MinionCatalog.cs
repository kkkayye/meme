using System;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Content
{
    /// <summary>Definitions of the lane units (melee minion, ranged minion, tower). They reuse HeroDefinition with Kind set, no Q/W/E/R and a basic attack only.</summary>
    public static class MinionCatalog
    {
        public const string MeleeId = "minion_melee";
        public const string RangedId = "minion_ranged";
        public const string TowerId = "tower";

        private static readonly SkillDefinition MeleeBasic = new SkillDefinition
        {
            Id = "minion_melee_basic", Name = "Slash", Key = SkillKey.Basic, Shape = SkillShape.Melee,
            Windup = 0.15f, Recovery = 0.15f, Range = 1.6f, Angle = 60f, AdRatio = 1f, DamageType = DamageType.Physical
        };

        private static readonly SkillDefinition RangedBasic = new SkillDefinition
        {
            Id = "minion_ranged_basic", Name = "Bolt", Key = SkillKey.Basic, Shape = SkillShape.Projectile,
            Windup = 0.2f, Recovery = 0.15f, Range = 5.5f, Radius = 0.25f, Speed = 18f, AdRatio = 1f, DamageType = DamageType.Physical
        };

        private static readonly SkillDefinition TowerBasic = new SkillDefinition
        {
            Id = "tower_basic", Name = "Tower Shot", Key = SkillKey.Basic, Shape = SkillShape.Projectile,
            Windup = 0.15f, Recovery = 0.1f, Range = GameConstants.TowerRange, Radius = 0.35f,
            Speed = GameConstants.TowerProjectileSpeed, AdRatio = 1f, DamageType = DamageType.Physical
        };

        /// <summary>小兵 melee minion: HP 300, AD 22, AS 1.0, MS 4.5, Armor 10, range 1.6.</summary>
        public static HeroDefinition Melee { get; } = new HeroDefinition
        {
            Id = MeleeId, Name = "小兵", Role = "Melee Minion", Kind = UnitKind.Minion, Archetype = HeroArchetype.Bruiser,
            Color = new Color(0.85f, 0.85f, 0.85f), BodyRadius = GameConstants.MinionRadius, BodyHeight = GameConstants.MinionHeight,
            BaseStats = HeroDefinition.Stats(maxHealth: 300f, healthRegen: 0f, attackDamage: 22f, abilityPower: 0f,
                attackSpeed: 1.0f, moveSpeed: 4.5f, armor: 10f, attackRange: 1.6f),
            BasicAttack = MeleeBasic, Skills = Array.Empty<SkillDefinition>()
        };

        /// <summary>弓兵 ranged minion: HP 220, AD 30, AS 0.8, MS 4.5, Armor 5, range 5.5.</summary>
        public static HeroDefinition Ranged { get; } = new HeroDefinition
        {
            Id = RangedId, Name = "弓兵", Role = "Ranged Minion", Kind = UnitKind.Minion, Archetype = HeroArchetype.Mage,
            Color = new Color(0.95f, 0.9f, 0.6f), BodyRadius = GameConstants.MinionRadius, BodyHeight = GameConstants.MinionHeight,
            BaseStats = HeroDefinition.Stats(maxHealth: 220f, healthRegen: 0f, attackDamage: 30f, abilityPower: 0f,
                attackSpeed: 0.8f, moveSpeed: 4.5f, armor: 5f, attackRange: 5.5f),
            BasicAttack = RangedBasic, Skills = Array.Empty<SkillDefinition>()
        };

        /// <summary>防御塔 tower: HP 2500, Armor 60, AD 100, AS 0.8, range 7.5; static, only basic attacks damage it.</summary>
        public static HeroDefinition Tower { get; } = new HeroDefinition
        {
            Id = TowerId, Name = "防御塔", Role = "Tower", Kind = UnitKind.Tower, Archetype = HeroArchetype.Bruiser,
            Color = new Color(0.75f, 0.75f, 0.8f), BodyRadius = GameConstants.TowerRadius, BodyHeight = GameConstants.TowerHeight,
            BaseStats = HeroDefinition.Stats(maxHealth: GameConstants.TowerHealth, healthRegen: 0f, attackDamage: GameConstants.TowerAttackDamage,
                abilityPower: 0f, attackSpeed: GameConstants.TowerAttackSpeed, moveSpeed: 0f, armor: GameConstants.TowerArmor, attackRange: GameConstants.TowerRange),
            BasicAttack = TowerBasic, Skills = Array.Empty<SkillDefinition>()
        };

        /// <summary>Gold a hero earns for last-hitting a minion.</summary>
        public static int GoldFor(HeroDefinition minion)
        {
            if (minion == null) return 0;
            return minion.Id == RangedId ? GameConstants.MinionGoldRanged : GameConstants.MinionGoldMelee;
        }
    }
}
