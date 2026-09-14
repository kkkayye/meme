using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneArena.Core
{
    /// <summary>Immutable hero definition: identity, base stats, basic attack and the four skills (Q, W, E, R).</summary>
    public sealed class HeroDefinition
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        /// <summary>Short role label for UI, e.g. "Ranged Mage".</summary>
        public string Role { get; init; } = "";
        public string Description { get; init; } = "";
        public HeroArchetype Archetype { get; init; } = HeroArchetype.Mage;
        public Color Color { get; init; } = Color.white;
        /// <summary>Base values for every StatType the hero has. Missing stats default to 0 (CritDamage defaults to 1.75 in StatBlock).</summary>
        public IReadOnlyDictionary<StatType, float> BaseStats { get; init; } = new Dictionary<StatType, float>();
        public SkillDefinition BasicAttack { get; init; }
        /// <summary>Exactly four skills in the order Q, W, E, R.</summary>
        public IReadOnlyList<SkillDefinition> Skills { get; init; } = Array.Empty<SkillDefinition>();

        /// <summary>Returns the skill for a key (Basic returns BasicAttack). Returns null if not authored.</summary>
        public SkillDefinition GetSkill(SkillKey key)
        {
            if (key == SkillKey.Basic) return BasicAttack;
            int index = (int)key - 1;
            if (index < 0 || index >= Skills.Count) return null;
            return Skills[index];
        }

        /// <summary>Base stat lookup with a default of 0.</summary>
        public float BaseStat(StatType stat)
        {
            return BaseStats.TryGetValue(stat, out float value) ? value : 0f;
        }

        /// <summary>Helper to build a base stat dictionary tersely in hero catalogs.</summary>
        public static IReadOnlyDictionary<StatType, float> Stats(
            float maxHealth, float healthRegen, float attackDamage, float abilityPower,
            float attackSpeed, float moveSpeed, float armor, float attackRange,
            float critChance = 0f, float critDamage = GameConstants.DefaultCritDamage)
        {
            return new Dictionary<StatType, float>
            {
                { StatType.MaxHealth, maxHealth },
                { StatType.HealthRegen, healthRegen },
                { StatType.AttackDamage, attackDamage },
                { StatType.AbilityPower, abilityPower },
                { StatType.AttackSpeed, attackSpeed },
                { StatType.MoveSpeed, moveSpeed },
                { StatType.Armor, armor },
                { StatType.AttackRange, attackRange },
                { StatType.CooldownReduction, 0f },
                { StatType.Lifesteal, 0f },
                { StatType.CritChance, critChance },
                { StatType.CritDamage, critDamage }
            };
        }
    }
}
