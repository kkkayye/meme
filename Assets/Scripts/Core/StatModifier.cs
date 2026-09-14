using System;

namespace RuneArena.Core
{
    /// <summary>Immutable stat modifier: a flat and/or percent bonus to one stat, tagged with a source id for removal.</summary>
    public sealed class StatModifier
    {
        public StatType Stat { get; }
        public float Flat { get; }
        /// <summary>Fractional bonus, e.g. 0.12 for +12%.</summary>
        public float Percent { get; }
        public string SourceId { get; }

        public StatModifier(StatType stat, float flat, float percent, string sourceId)
        {
            if (sourceId == null) throw new ArgumentNullException(nameof(sourceId));
            Stat = stat;
            Flat = flat;
            Percent = percent;
            SourceId = sourceId;
        }

        /// <summary>Creates a flat modifier (e.g. +40 AP).</summary>
        public static StatModifier FlatOf(StatType stat, float flat, string sourceId)
        {
            return new StatModifier(stat, flat, 0f, sourceId);
        }

        /// <summary>Creates a percent modifier (e.g. 0.12 for +12%).</summary>
        public static StatModifier PercentOf(StatType stat, float percent, string sourceId)
        {
            return new StatModifier(stat, 0f, percent, sourceId);
        }

        /// <summary>Returns a copy of this modifier with a different source id (used when a definition is instanced on a unit).</summary>
        public StatModifier WithSource(string sourceId)
        {
            return new StatModifier(Stat, Flat, Percent, sourceId);
        }

        /// <summary>Returns a copy scaled by a factor (used for stacking runes).</summary>
        public StatModifier Scaled(float factor)
        {
            return new StatModifier(Stat, Flat * factor, Percent * factor, SourceId);
        }

        public override string ToString()
        {
            if (Flat != 0f && Percent != 0f) return Stat + " +" + Flat + " +" + (Percent * 100f) + "%";
            if (Percent != 0f) return Stat + " +" + (Percent * 100f) + "%";
            return Stat + " +" + Flat;
        }
    }
}
