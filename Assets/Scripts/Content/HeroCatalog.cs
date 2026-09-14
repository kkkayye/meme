using System;
using System.Collections.Generic;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Content
{
    /// <summary>Authoring of the three heroes (Blaze, Vanguard, Shade): base stats, colours and kits assembled from SkillBook; built once, validated once, immutable.</summary>
    public static class HeroCatalog
    {
        public const string BlazeId = "blaze";
        public const string VanguardId = "vanguard";
        public const string ShadeId = "shade";

        /// <summary>Hero body colours from DESIGN.md section 5 (orange / steel blue / purple). Declared before All so static init order is safe.</summary>
        public static readonly Color BlazeColor = new Color(1f, 0.5f, 0.1f);
        public static readonly Color VanguardColor = new Color(0.35f, 0.55f, 0.85f);
        public static readonly Color ShadeColor = new Color(0.6f, 0.3f, 0.9f);

        private const int SkillsPerHero = 4;
        private static readonly SkillKey[] KitOrder = { SkillKey.Q, SkillKey.W, SkillKey.E, SkillKey.R };

        /// <summary>All heroes in catalog order: Blaze, Vanguard, Shade.</summary>
        public static IReadOnlyList<HeroDefinition> All { get; } = BuildAll();

        /// <summary>Returns the hero with the given id (ordinal match), or null when id is null or unknown.</summary>
        public static HeroDefinition Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            IReadOnlyList<HeroDefinition> heroes = All;
            for (int i = 0; i < heroes.Count; i++)
            {
                if (string.Equals(heroes[i].Id, id, StringComparison.Ordinal)) return heroes[i];
            }
            return null;
        }

        // ================================================================
        // Assembly
        // ================================================================

        private static IReadOnlyList<HeroDefinition> BuildAll()
        {
            HeroDefinition[] heroes = { BuildBlaze(), BuildVanguard(), BuildShade() };
            ValidateCatalog(heroes);
            return Array.AsReadOnly(heroes);
        }

        /// <summary>Blaze 烈焰 - Ranged Mage. HP 520 (+1.5/s), AD 45, AP 60, AS 1.0, MS 6.0, Armor 20, Range 7.</summary>
        private static HeroDefinition BuildBlaze()
        {
            return new HeroDefinition
            {
                Id = BlazeId,
                Name = "Blaze",
                Role = "Ranged Mage",
                Description = "烈焰 Blaze — 远程法师。火球消耗、闪现保命、陨石与炼狱提供爆发性范围魔法伤害。",
                Archetype = HeroArchetype.Mage,
                Color = BlazeColor,
                BaseStats = HeroDefinition.Stats(
                    maxHealth: 520f, healthRegen: 1.5f, attackDamage: 45f, abilityPower: 60f,
                    attackSpeed: 1.0f, moveSpeed: 6.0f, armor: 20f, attackRange: 7f),
                BasicAttack = SkillBook.BlazeBasic,
                Skills = Kit(SkillBook.BlazeQ, SkillBook.BlazeW, SkillBook.BlazeE, SkillBook.BlazeR)
            };
        }

        /// <summary>Vanguard 铁壁 - Melee Bruiser. HP 780 (+3/s), AD 62, AP 0, AS 0.9, MS 5.6, Armor 40, Range 2.2.</summary>
        private static HeroDefinition BuildVanguard()
        {
            return new HeroDefinition
            {
                Id = VanguardId,
                Name = "Vanguard",
                Role = "Melee Bruiser",
                Description = "铁壁 Vanguard — 近战战士。冲锋切入、盾击眩晕、铁甲护盾，地震控制全场。",
                Archetype = HeroArchetype.Bruiser,
                Color = VanguardColor,
                BaseStats = HeroDefinition.Stats(
                    maxHealth: 780f, healthRegen: 3f, attackDamage: 62f, abilityPower: 0f,
                    attackSpeed: 0.9f, moveSpeed: 5.6f, armor: 40f, attackRange: 2.2f),
                BasicAttack = SkillBook.VanguardBasic,
                Skills = Kit(SkillBook.VanguardQ, SkillBook.VanguardW, SkillBook.VanguardE, SkillBook.VanguardR)
            };
        }

        /// <summary>Shade 影刃 - Melee Assassin. HP 560 (+2/s), AD 70, AP 0, AS 1.3, MS 6.4, Armor 25, Range 1.8, Crit 15%.</summary>
        private static HeroDefinition BuildShade()
        {
            return new HeroDefinition
            {
                Id = ShadeId,
                Name = "Shade",
                Role = "Melee Assassin",
                Description = "影刃 Shade — 近战刺客。影步背刺、飞刀消耗、烟雾隐身，处决斩杀残血目标。",
                Archetype = HeroArchetype.Assassin,
                Color = ShadeColor,
                BaseStats = HeroDefinition.Stats(
                    maxHealth: 560f, healthRegen: 2f, attackDamage: 70f, abilityPower: 0f,
                    attackSpeed: 1.3f, moveSpeed: 6.4f, armor: 25f, attackRange: 1.8f,
                    critChance: 0.15f),
                BasicAttack = SkillBook.ShadeBasic,
                Skills = Kit(SkillBook.ShadeQ, SkillBook.ShadeW, SkillBook.ShadeE, SkillBook.ShadeR)
            };
        }

        /// <summary>Wraps the four skills in a read-only list (Q, W, E, R order).</summary>
        private static IReadOnlyList<SkillDefinition> Kit(SkillDefinition q, SkillDefinition w, SkillDefinition e, SkillDefinition r)
        {
            return Array.AsReadOnly(new[] { q, w, e, r });
        }

        // ================================================================
        // Authoring validation (runs once at static init; throws on any authoring mistake)
        // ================================================================

        private static void ValidateCatalog(HeroDefinition[] heroes)
        {
            var heroIds = new HashSet<string>(StringComparer.Ordinal);
            var skillIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < heroes.Length; i++)
            {
                HeroDefinition hero = heroes[i];
                if (hero == null) throw new InvalidOperationException("HeroCatalog: hero at index " + i + " is null.");
                if (string.IsNullOrEmpty(hero.Id)) throw new InvalidOperationException("HeroCatalog: hero at index " + i + " has an empty Id.");
                if (!heroIds.Add(hero.Id)) throw new InvalidOperationException("HeroCatalog: duplicate hero id '" + hero.Id + "'.");
                ValidateStats(hero);
                ValidateKit(hero, skillIds);
            }
        }

        private static void ValidateStats(HeroDefinition hero)
        {
            if (hero.BaseStats == null) throw new InvalidOperationException("HeroCatalog: hero '" + hero.Id + "' has no BaseStats.");
            if (hero.BaseStat(StatType.MaxHealth) <= 0f) throw new InvalidOperationException("HeroCatalog: hero '" + hero.Id + "' must have MaxHealth > 0.");
            if (hero.BaseStat(StatType.MoveSpeed) <= 0f) throw new InvalidOperationException("HeroCatalog: hero '" + hero.Id + "' must have MoveSpeed > 0.");
            if (hero.BaseStat(StatType.AttackSpeed) <= 0f) throw new InvalidOperationException("HeroCatalog: hero '" + hero.Id + "' must have AttackSpeed > 0.");
            if (hero.BaseStat(StatType.AttackRange) <= 0f) throw new InvalidOperationException("HeroCatalog: hero '" + hero.Id + "' must have AttackRange > 0.");
        }

        private static void ValidateKit(HeroDefinition hero, HashSet<string> skillIds)
        {
            if (hero.BasicAttack == null) throw new InvalidOperationException("HeroCatalog: hero '" + hero.Id + "' has no BasicAttack.");
            ValidateSkill(hero, hero.BasicAttack, SkillKey.Basic, skillIds);
            if (hero.Skills == null || hero.Skills.Count != SkillsPerHero)
            {
                throw new InvalidOperationException("HeroCatalog: hero '" + hero.Id + "' must have exactly " + SkillsPerHero + " skills (Q, W, E, R).");
            }
            for (int i = 0; i < KitOrder.Length; i++)
            {
                ValidateSkill(hero, hero.Skills[i], KitOrder[i], skillIds);
            }
        }

        private static void ValidateSkill(HeroDefinition hero, SkillDefinition skill, SkillKey expectedKey, HashSet<string> skillIds)
        {
            if (skill == null) throw new InvalidOperationException("HeroCatalog: hero '" + hero.Id + "' has a null skill for key " + expectedKey + ".");
            if (string.IsNullOrEmpty(skill.Id)) throw new InvalidOperationException("HeroCatalog: hero '" + hero.Id + "' skill " + expectedKey + " has an empty Id.");
            if (skill.Key != expectedKey) throw new InvalidOperationException("HeroCatalog: skill '" + skill.Id + "' is authored with Key " + skill.Key + " but sits in slot " + expectedKey + ".");
            if (!skill.Id.StartsWith(hero.Id + "_", StringComparison.Ordinal)) throw new InvalidOperationException("HeroCatalog: skill '" + skill.Id + "' must be prefixed with '" + hero.Id + "_'.");
            if (!skillIds.Add(skill.Id)) throw new InvalidOperationException("HeroCatalog: duplicate skill id '" + skill.Id + "'.");
            if (skill.Windup < 0f || skill.Recovery < 0f || skill.Cooldown < 0f) throw new InvalidOperationException("HeroCatalog: skill '" + skill.Id + "' has negative timing.");
            if (skill.Shape == SkillShape.Fan && skill.ProjectileCount < 1) throw new InvalidOperationException("HeroCatalog: fan skill '" + skill.Id + "' needs ProjectileCount >= 1.");
            if (skill.Shape == SkillShape.Channel && skill.Delay <= 0f) throw new InvalidOperationException("HeroCatalog: channel skill '" + skill.Id + "' needs Delay > 0 (channel duration).");
        }
    }
}
