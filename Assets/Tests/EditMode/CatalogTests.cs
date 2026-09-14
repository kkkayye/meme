using System.Collections.Generic;
using NUnit.Framework;
using RuneArena.Content;
using RuneArena.Core;

namespace RuneArena.Tests.EditMode
{
    public sealed class CatalogTests
    {
        [Test]
        public void Heroes_ThreeHeroesWithFiveSkillsEach()
        {
            Assert.AreEqual(3, ContentCatalog.Heroes.Count);
            foreach (HeroDefinition hero in ContentCatalog.Heroes)
            {
                Assert.IsNotNull(hero.BasicAttack, hero.Id + " basic");
                Assert.AreEqual(4, hero.Skills.Count, hero.Id + " skills");
                Assert.AreEqual(SkillKey.Q, hero.Skills[0].Key);
                Assert.AreEqual(SkillKey.R, hero.Skills[3].Key);
                Assert.Greater(hero.BaseStat(StatType.MaxHealth), 0f);
            }
            Assert.IsNotNull(ContentCatalog.GetHero("blaze"));
            Assert.IsNotNull(ContentCatalog.GetHero("vanguard"));
            Assert.IsNotNull(ContentCatalog.GetHero("shade"));
        }

        [Test]
        public void Runes_AtLeastThirtyWithUniqueIdsAndWorkingHooks()
        {
            IReadOnlyList<RuneDefinition> runes = ContentCatalog.Runes;
            Assert.GreaterOrEqual(runes.Count, 30);
            var ids = new HashSet<string>();
            int legendary = 0;
            int stackable = 0;
            foreach (RuneDefinition rune in runes)
            {
                Assert.IsTrue(ids.Add(rune.Id), "duplicate rune id " + rune.Id);
                if (rune.Rarity == RuneRarity.Legendary) legendary++;
                if (rune.Stackable) stackable++;
                if (rune.HookFactory != null) Assert.IsNotNull(rune.HookFactory(), rune.Id + " hook");
                Assert.IsTrue(rune.StatModifiers.Count > 0 || rune.HookFactory != null, rune.Id + " does nothing");
            }
            Assert.GreaterOrEqual(legendary, 4);
            Assert.GreaterOrEqual(stackable, 12);
        }

        [Test]
        public void Items_AtLeastSixteenWithUniqueIdsAndTiers()
        {
            IReadOnlyList<ItemDefinition> items = ContentCatalog.Items;
            Assert.GreaterOrEqual(items.Count, 16);
            var ids = new HashSet<string>();
            var tiers = new HashSet<ItemTier>();
            foreach (ItemDefinition item in items)
            {
                Assert.IsTrue(ids.Add(item.Id), "duplicate item id " + item.Id);
                Assert.Greater(item.Cost, 0);
                tiers.Add(item.Tier);
                if (item.HookFactory != null) Assert.IsNotNull(item.HookFactory(), item.Id + " hook");
                Assert.AreEqual((int)(item.Cost * GameConstants.ItemSellRefundFraction), item.SellValue);
            }
            Assert.AreEqual(3, tiers.Count);
        }

        [Test]
        public void LootTable_WeightsMatchDesign()
        {
            Assert.AreEqual(100f, LootTables.TotalWeight(LootTables.Chest), 1e-3f);
            Assert.AreEqual(0.4f, LootTables.ProbabilityOf(LootTables.Chest, LootKind.Gold), 1e-4f);
            Assert.AreEqual(2, LootTables.ChestPity.Count);
            foreach (LootEntry entry in LootTables.ChestPity) Assert.IsTrue(entry.IsPityEligible);
        }
    }
}
