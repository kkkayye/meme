using NUnit.Framework;
using RuneArena.Core;

namespace RuneArena.Tests.EditMode
{
    public sealed class StatAndRngTests
    {
        [Test]
        public void StatBlock_ComputesFlatThenPercent()
        {
            var stats = new StatBlock();
            stats.SetBase(StatType.AttackDamage, 50f);
            stats.AddModifier(StatModifier.FlatOf(StatType.AttackDamage, 10f, "a"));
            stats.AddModifier(StatModifier.PercentOf(StatType.AttackDamage, 0.5f, "b"));
            Assert.AreEqual(90f, stats.Get(StatType.AttackDamage), 1e-4f);
        }

        [Test]
        public void StatBlock_ClampsCooldownReductionAndCrit()
        {
            var stats = new StatBlock();
            stats.AddModifier(StatModifier.FlatOf(StatType.CooldownReduction, 0.9f, "a"));
            stats.AddModifier(StatModifier.FlatOf(StatType.CritChance, 1.7f, "a"));
            Assert.AreEqual(GameConstants.MaxCooldownReduction, stats.Get(StatType.CooldownReduction), 1e-4f);
            Assert.AreEqual(GameConstants.MaxCritChance, stats.Get(StatType.CritChance), 1e-4f);
        }

        [Test]
        public void StatBlock_RemoveBySource_RemovesOnlyThatSource()
        {
            var stats = new StatBlock();
            stats.SetBase(StatType.Armor, 10f);
            stats.AddModifier(StatModifier.FlatOf(StatType.Armor, 5f, "x"));
            stats.AddModifier(StatModifier.FlatOf(StatType.Armor, 7f, "y"));
            Assert.AreEqual(1, stats.RemoveBySource("x"));
            Assert.AreEqual(17f, stats.Get(StatType.Armor), 1e-4f);
            Assert.IsFalse(stats.HasSource("x"));
            Assert.IsTrue(stats.HasSource("y"));
        }

        [Test]
        public void Rng_IsDeterministicForSameSeed()
        {
            var a = new Rng(42);
            var b = new Rng(42);
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.Range(0, 1000), b.Range(0, 1000));
        }

        [Test]
        public void Rng_ForkIsDeterministicAndDiffersFromParent()
        {
            var a = new Rng(7).Fork("bots");
            var b = new Rng(7).Fork("bots");
            var c = new Rng(7).Fork("loot");
            Assert.AreEqual(a.Seed, b.Seed);
            Assert.AreNotEqual(a.Seed, c.Seed);
        }

        [Test]
        public void Rng_WeightedPick_MatchesWeightsWithinTolerance()
        {
            var rng = new Rng(1234);
            string[] items = { "a", "b", "c" };
            float[] weights = { 60f, 30f, 10f };
            var counts = new int[3];
            const int samples = 20000;
            for (int i = 0; i < samples; i++)
            {
                string pick = rng.WeightedPick(items, s => weights[System.Array.IndexOf(items, s)]);
                counts[System.Array.IndexOf(items, pick)]++;
            }
            for (int i = 0; i < 3; i++)
            {
                float expected = weights[i] / 100f;
                float actual = counts[i] / (float)samples;
                Assert.AreEqual(expected, actual, 0.03f, "weight " + items[i]);
            }
        }

        [Test]
        public void Rng_WeightedIndex_SkipsZeroWeights()
        {
            var rng = new Rng(5);
            for (int i = 0; i < 200; i++) Assert.AreEqual(1, rng.WeightedIndex(new[] { 0f, 5f, 0f }));
            Assert.AreEqual(-1, rng.WeightedIndex(new[] { 0f, 0f }));
        }
    }
}
