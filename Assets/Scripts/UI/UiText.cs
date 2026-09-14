using System.Collections.Generic;
using System.Text;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.UI
{
    /// <summary>String formatting helpers for the UI: clocks, cooldowns, names, stat modifier summaries and skill / hero summaries.</summary>
    public static class UiText
    {
        private static readonly StringBuilder Builder = new StringBuilder(160);

        /// <summary>"m:ss" from seconds (rounded up, clamped at 0).</summary>
        public static string FormatClock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        /// <summary>Whole seconds rounded up ("12"), clamped at 0.</summary>
        public static string FormatWhole(float seconds)
        {
            return Mathf.CeilToInt(Mathf.Max(0f, seconds)).ToString();
        }

        /// <summary>Cooldown text: whole seconds above 1 s, one decimal below.</summary>
        public static string FormatCooldown(float seconds)
        {
            if (seconds >= 1f) return Mathf.CeilToInt(seconds).ToString();
            return Mathf.Max(0f, seconds).ToString("0.0");
        }

        public static string TeamName(Team team)
        {
            return team == Team.Blue ? "Blue" : "Red";
        }

        public static string RarityName(RuneRarity rarity)
        {
            return rarity.ToString();
        }

        public static string SetName(RuneSet set)
        {
            return set == RuneSet.None ? "" : set.ToString();
        }

        public static string TierName(ItemTier tier)
        {
            return "T" + (int)tier;
        }

        public static string LootKindLabel(LootKind kind)
        {
            switch (kind)
            {
                case LootKind.CommonRune: return "Common Rune";
                case LootKind.RareItem: return "Rare Item";
                case LootKind.EpicRune: return "Epic Rune";
                case LootKind.LegendaryItem: return "Legendary Item";
                default: return "Gold";
            }
        }

        public static string StatShort(StatType stat)
        {
            switch (stat)
            {
                case StatType.MaxHealth: return "HP";
                case StatType.HealthRegen: return "HP/s";
                case StatType.AttackDamage: return "AD";
                case StatType.AbilityPower: return "AP";
                case StatType.AttackSpeed: return "AS";
                case StatType.MoveSpeed: return "MS";
                case StatType.Armor: return "Armor";
                case StatType.CooldownReduction: return "CDR";
                case StatType.Lifesteal: return "Lifesteal";
                case StatType.CritChance: return "Crit";
                case StatType.CritDamage: return "Crit Dmg";
                case StatType.AttackRange: return "Range";
                default: return stat.ToString();
            }
        }

        /// <summary>"+40 AP" / "+12% AS" / "+25 AD +20% Crit".</summary>
        public static string FormatModifier(StatModifier modifier)
        {
            if (modifier == null) return "";
            string name = StatShort(modifier.Stat);
            string flat = modifier.Flat != 0f ? Signed(modifier.Flat) + " " + name : "";
            string percent = modifier.Percent != 0f ? Signed(modifier.Percent * 100f) + "% " + name : "";
            if (flat.Length > 0 && percent.Length > 0) return flat + " " + percent;
            return flat.Length > 0 ? flat : percent;
        }

        /// <summary>Joins every modifier with the separator; empty string for none.</summary>
        public static string FormatModifiers(IReadOnlyList<StatModifier> modifiers, string separator = ", ")
        {
            if (modifiers == null || modifiers.Count == 0) return "";
            Builder.Length = 0;
            for (int i = 0; i < modifiers.Count; i++)
            {
                string part = FormatModifier(modifiers[i]);
                if (part.Length == 0) continue;
                if (Builder.Length > 0) Builder.Append(separator);
                Builder.Append(part);
            }
            return Builder.ToString();
        }

        /// <summary>First maxChars characters of a name ("?" when empty).</summary>
        public static string Abbreviate(string name, int maxChars)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            return name.Length <= maxChars ? name : name.Substring(0, maxChars);
        }

        /// <summary>One-line skill summary for hero cards: "Q Flame Wave - Cone, 4s cd".</summary>
        public static string SkillSummary(SkillDefinition skill)
        {
            if (skill == null) return "";
            string key = skill.Key == SkillKey.Basic ? "LMB" : skill.Key.ToString();
            string cd = skill.Cooldown > 0f ? ", " + skill.Cooldown.ToString("0.#") + "s cd" : "";
            return key + "  " + skill.Name + "  -  " + skill.Shape + cd;
        }

        /// <summary>Compact base stat line for hero cards.</summary>
        public static string HeroStatLine(HeroDefinition hero)
        {
            if (hero == null) return "";
            return "HP " + hero.BaseStat(StatType.MaxHealth).ToString("0")
                + "   AD " + hero.BaseStat(StatType.AttackDamage).ToString("0")
                + "   AP " + hero.BaseStat(StatType.AbilityPower).ToString("0")
                + "   Armor " + hero.BaseStat(StatType.Armor).ToString("0")
                + "   MS " + hero.BaseStat(StatType.MoveSpeed).ToString("0.0")
                + "   Range " + hero.BaseStat(StatType.AttackRange).ToString("0.0");
        }

        private static string Signed(float value)
        {
            return (value >= 0f ? "+" : "") + value.ToString("0.#");
        }
    }
}
