using System;
using System.Collections.Generic;

namespace RuneArena.Core
{
    /// <summary>Deterministic gameplay random source wrapping System.Random. All gameplay randomness must go through this class.</summary>
    public sealed class Rng
    {
        private readonly Random _random;

        public int Seed { get; }

        public Rng(int seed)
        {
            Seed = seed;
            _random = new Random(seed);
        }

        /// <summary>Integer in [minInclusive, maxExclusive). Returns minInclusive if the range is empty.</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return _random.Next(minInclusive, maxExclusive);
        }

        /// <summary>Float in [0, 1).</summary>
        public float Value()
        {
            return (float)_random.NextDouble();
        }

        /// <summary>Float in [min, max).</summary>
        public float Range(float min, float max)
        {
            return min + (max - min) * Value();
        }

        /// <summary>True with the given probability (0..1).</summary>
        public bool Chance(float probability)
        {
            if (probability <= 0f) return false;
            if (probability >= 1f) return true;
            return Value() < probability;
        }

        /// <summary>Uniform pick. Throws on empty list.</summary>
        public T Pick<T>(IReadOnlyList<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (items.Count == 0) throw new ArgumentException("Cannot pick from an empty list.", nameof(items));
            return items[Range(0, items.Count)];
        }

        /// <summary>Weighted pick. Items with weight &lt;= 0 are never chosen. Throws if the list is empty or total weight is 0.</summary>
        public T WeightedPick<T>(IReadOnlyList<T> items, Func<T, float> weight)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (weight == null) throw new ArgumentNullException(nameof(weight));
            if (items.Count == 0) throw new ArgumentException("Cannot pick from an empty list.", nameof(items));
            float total = 0f;
            for (int i = 0; i < items.Count; i++) total += Math.Max(0f, weight(items[i]));
            if (total <= 0f) throw new ArgumentException("Total weight must be positive.", nameof(weight));
            float roll = Value() * total;
            for (int i = 0; i < items.Count; i++)
            {
                float w = Math.Max(0f, weight(items[i]));
                if (w <= 0f) continue;
                if (roll < w) return items[i];
                roll -= w;
            }
            return LastPositive(items, weight);
        }

        /// <summary>Weighted index into a weight list. Returns -1 if all weights are non-positive.</summary>
        public int WeightedIndex(IReadOnlyList<float> weights)
        {
            if (weights == null) throw new ArgumentNullException(nameof(weights));
            float total = 0f;
            for (int i = 0; i < weights.Count; i++) total += Math.Max(0f, weights[i]);
            if (total <= 0f) return -1;
            float roll = Value() * total;
            int last = -1;
            for (int i = 0; i < weights.Count; i++)
            {
                float w = Math.Max(0f, weights[i]);
                if (w <= 0f) continue;
                last = i;
                if (roll < w) return i;
                roll -= w;
            }
            return last;
        }

        /// <summary>In-place Fisher-Yates shuffle.</summary>
        public void Shuffle<T>(IList<T> list)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        /// <summary>Derives a child Rng deterministically from this seed and a salt (same seed + salt always yields the same child).</summary>
        public Rng Fork(string salt)
        {
            return new Rng(DeriveSeed(Seed, salt ?? ""));
        }

        /// <summary>Stable FNV-1a style combination of a seed and a string (string.GetHashCode is not stable across runtimes).</summary>
        public static int DeriveSeed(int seed, string salt)
        {
            unchecked
            {
                uint hash = 2166136261u ^ (uint)seed;
                hash *= 16777619u;
                for (int i = 0; i < salt.Length; i++)
                {
                    hash ^= salt[i];
                    hash *= 16777619u;
                }
                return (int)hash;
            }
        }

        private static T LastPositive<T>(IReadOnlyList<T> items, Func<T, float> weight)
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (weight(items[i]) > 0f) return items[i];
            }
            return items[items.Count - 1];
        }
    }
}
