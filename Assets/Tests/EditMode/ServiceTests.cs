using System.Collections.Generic;
using NUnit.Framework;
using RuneArena.Combat;
using RuneArena.Content;
using RuneArena.Core;
using RuneArena.Items;
using RuneArena.Loot;
using RuneArena.Match;
using RuneArena.Runes;

namespace RuneArena.Tests.EditMode
{
    public sealed class ServiceTests
    {
        private UnitTestHelper _units;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            GameServices.Reset();
            GameServices.Rng = new Rng(99);
            GameServices.World = new CombatWorld();
            GameServices.Economy = new Economy();
            _units = new UnitTestHelper();
        }

        [TearDown]
        public void TearDown()
        {
            _units.Cleanup();
            GameServices.Reset();
            EventBus.Clear();
        }

        [Test]
        public void Draft_OffersThreeDistinctRunes()
        {
            Unit unit = _units.Create("blaze", Team.Blue);
            var draft = new RuneDraftService(new Rng(3), ContentCatalog.Runes);
            draft.BeginDraft(1, new[] { unit });
            IReadOnlyList<RuneDefinition> offer = draft.GetOffer(unit);
            Assert.AreEqual(GameConstants.DraftCardCount, offer.Count);
            Assert.AreNotEqual(offer[0].Id, offer[1].Id);
            Assert.AreNotEqual(offer[1].Id, offer[2].Id);
            Assert.AreNotEqual(offer[0].Id, offer[2].Id);
            Assert.IsFalse(draft.AllPicked);
            Assert.IsTrue(draft.Pick(unit, 1));
            Assert.IsTrue(draft.AllPicked);
            Assert.AreEqual(1, unit.Runes.Count(offer[1].Id));
            Assert.IsFalse(draft.Pick(unit, 0), "second pick must be rejected");
        }

        [Test]
        public void Draft_ExcludesOwnedUniqueRunes()
        {
            Unit unit = _units.Create("shade", Team.Blue);
            RuneDefinition unique = ContentCatalog.GetRune(RuneIds.TripleStrike);
            Assert.IsTrue(unit.Runes.Add(unique));
            Assert.IsFalse(unit.Runes.CanAdd(unique));
            var draft = new RuneDraftService(new Rng(11), ContentCatalog.Runes);
            for (int round = 1; round <= 30; round++)
            {
                draft.BeginDraft(round, new[] { unit });
                foreach (RuneDefinition rune in draft.GetOffer(unit)) Assert.AreNotEqual(unique.Id, rune.Id);
                draft.EndDraft();
            }
        }

        [Test]
        public void Draft_PityGuaranteesEpicAfterThreeDryDrafts()
        {
            Unit unit = _units.Create("vanguard", Team.Blue);
            var draft = new RuneDraftService(new Rng(21), ContentCatalog.Runes);
            bool verified = false;
            for (int round = 1; round <= 200 && !verified; round++)
            {
                bool pityDue = draft.DraftsWithoutEpic(unit) >= GameConstants.DraftPityWindow;
                draft.BeginDraft(round, new[] { unit });
                if (pityDue)
                {
                    bool hasEpic = false;
                    foreach (RuneDefinition rune in draft.GetOffer(unit)) hasEpic |= rune.Rarity >= RuneRarity.Epic;
                    Assert.IsTrue(hasEpic, "pity draft must contain an Epic+ card");
                    Assert.AreEqual(0, draft.DraftsWithoutEpic(unit));
                    verified = true;
                }
                draft.EndDraft();
            }
            Assert.IsTrue(verified, "pity never became due in 200 drafts");
        }

        [Test]
        public void Draft_BaseWeightsWithoutMatch()
        {
            Unit unit = _units.Create("blaze", Team.Red);
            var draft = new RuneDraftService(new Rng(1), ContentCatalog.Runes);
            CollectionAssert.AreEqual(GameConstants.DraftWeights, new List<float>(draft.GetWeights(unit)));
        }

        [Test]
        public void RuneInventory_StacksAndSetBonus()
        {
            Unit unit = _units.Create("vanguard", Team.Blue);
            RuneDefinition iron = ContentCatalog.GetRune(RuneIds.IronBones);
            float before = unit.Stats.Get(StatType.MaxHealth);
            Assert.IsTrue(unit.Runes.Add(iron));
            Assert.IsTrue(unit.Runes.Add(iron));
            Assert.IsTrue(unit.Runes.Add(iron));
            Assert.IsFalse(unit.Runes.CanAdd(iron));
            Assert.AreEqual(before + 240f, unit.Stats.Get(StatType.MaxHealth), 1e-3f);
            Assert.AreEqual(1, unit.Runes.SetCount(RuneSet.Iron), "stacks count once toward a set");
            unit.Runes.Add(ContentCatalog.GetRune(RuneIds.Plating));
            Assert.IsFalse(unit.Runes.IsSetActive(RuneSet.Iron));
            unit.Runes.Add(ContentCatalog.GetRune(RuneIds.Tenacity));
            Assert.IsTrue(unit.Runes.IsSetActive(RuneSet.Iron));
            Assert.IsTrue(unit.Stats.HasSource(RuneTuning.SetSourceId(RuneSet.Iron)));
            unit.Runes.ClearAll();
            Assert.AreEqual(before, unit.Stats.Get(StatType.MaxHealth), 1e-3f);
        }

        [Test]
        public void Shop_TierGatingRerollCostAndBuy()
        {
            Unit unit = _units.Create("blaze", Team.Blue);
            GameServices.Economy.SetGold(unit, 2000);
            var shop = new ShopService(new Rng(8), ContentCatalog.Items);
            shop.OpenShop(1, new[] { unit });
            IReadOnlyList<ItemDefinition> offer = shop.GetOffer(unit);
            Assert.AreEqual(GameConstants.ShopOfferCount, offer.Count);
            foreach (ItemDefinition item in offer) Assert.AreEqual(ItemTier.T1, item.Tier, "round 1 offers T1 only");
            Assert.AreEqual(100, shop.RerollCost(unit));
            Assert.IsTrue(shop.Reroll(unit));
            Assert.AreEqual(150, shop.RerollCost(unit));
            Assert.AreEqual(1900, unit.Gold);
            Assert.IsTrue(shop.CanBuy(unit, 0));
            ItemDefinition bought = shop.GetOffer(unit)[0];
            Assert.IsTrue(shop.Buy(unit, 0));
            Assert.IsTrue(unit.Items.Has(bought.Id));
            Assert.AreEqual(1900 - bought.Cost, unit.Gold);
            Assert.IsNull(shop.GetOffer(unit)[0]);
            Assert.IsFalse(shop.CanBuy(unit, 0));
            Assert.IsTrue(shop.Sell(unit, bought));
            Assert.AreEqual(1900 - bought.Cost + bought.SellValue, unit.Gold);
            Assert.IsFalse(unit.Items.Has(bought.Id));
            shop.CloseShop();
            Assert.IsTrue(shop.AvailablePool(1).TrueForAll(i => i.Tier == ItemTier.T1));
            Assert.IsTrue(shop.AvailablePool(2).Exists(i => i.Tier == ItemTier.T2));
            Assert.IsFalse(shop.AvailablePool(2).Exists(i => i.Tier == ItemTier.T3));
            Assert.IsTrue(shop.AvailablePool(3).Exists(i => i.Tier == ItemTier.T3));
            Assert.AreEqual(0f, ShopService.TierWeight(ItemTier.T3, 2));
            Assert.AreEqual(GameConstants.ItemTierWeights[2], ShopService.TierWeight(ItemTier.T3, 3));
        }

        [Test]
        public void Shop_CannotBuyWhenBroke()
        {
            Unit unit = _units.Create("shade", Team.Blue);
            GameServices.Economy.SetGold(unit, 10);
            var shop = new ShopService(new Rng(2), ContentCatalog.Items);
            shop.OpenShop(1, new[] { unit });
            Assert.IsFalse(shop.CanBuy(unit, 0));
            Assert.IsFalse(shop.Buy(unit, 0));
            Assert.IsFalse(shop.Reroll(unit));
            Assert.AreEqual(10, unit.Gold);
        }

        [Test]
        public void Loot_PityEveryFourthChest()
        {
            Unit unit = _units.Create("blaze", Team.Blue);
            GameServices.Economy.SetGold(unit, 0);
            var loot = new LootService(new Rng(4));
            for (int i = 0; i < 3; i++)
            {
                Assert.IsFalse(loot.IsNextPity(unit));
                loot.OpenChest(unit);
            }
            Assert.IsTrue(loot.IsNextPity(unit));
            LootResult result = loot.RollReward(unit);
            Assert.IsTrue(LootEntry.IsPityKind(result.Kind), "4th chest must be Epic rune or Legendary item");
            Assert.IsTrue(result.FromPity);
            loot.GrantReward(unit, result);
            Assert.AreEqual(4, loot.ChestsOpened(unit));
            Assert.IsFalse(loot.IsNextPity(unit));
        }

        [Test]
        public void Scoring_TieBreaks()
        {
            var scoring = new Scoring();
            Unit blue = _units.Create("blaze", Team.Blue);
            Unit red = _units.Create("blaze", Team.Red);
            var units = new[] { blue, red };
            Assert.AreEqual(GameConstants.TimeoutTieBreakWinner, scoring.DecideTimeoutWinner(units));
            scoring.RecordKill(Team.Blue);
            Assert.AreEqual(Team.Blue, scoring.DecideTimeoutWinner(units));
            scoring.Reset();
            scoring.RecordCaptureSeconds(Team.Red, 5f);
            Assert.AreEqual(50f, scoring.RoundPoints(Team.Red), 1e-3f);
            Assert.AreEqual(Team.Red, scoring.DecideTimeoutWinner(units));
            scoring.Reset();
            red.SetHealth(red.MaxHealth * 0.5f);
            Assert.AreEqual(Team.Blue, scoring.DecideTimeoutWinner(units));
        }

        [Test]
        public void Damage_ArmorShieldsAndTrueDamage()
        {
            Unit attacker = _units.Create("shade", Team.Blue);
            Unit target = _units.Create("vanguard", Team.Red);
            float armor = target.Stats.Get(StatType.Armor);
            float expected = 100f * 100f / (100f + armor);
            DamageResult physical = DamagePipeline.Deal(attacker, target, 100f, DamageType.Physical, DamageTag.Skill);
            Assert.AreEqual(expected, physical.Dealt, 1e-3f);
            target.AddShield(50f, 5f, "test");
            DamageResult trueDamage = DamagePipeline.Deal(attacker, target, 80f, DamageType.True, DamageTag.Skill);
            Assert.AreEqual(50f, trueDamage.Absorbed, 1e-3f);
            Assert.AreEqual(30f, trueDamage.Dealt, 1e-3f);
            target.AddLethalGuard("undying");
            DamageResult lethal = DamagePipeline.Deal(attacker, target, 100000f, DamageType.True, DamageTag.Skill);
            Assert.IsFalse(lethal.Killed);
            Assert.IsTrue(target.IsAlive);
            Assert.AreEqual(1f, target.Health, 1e-3f);
            DamageResult finalHit = DamagePipeline.Deal(attacker, target, 100000f, DamageType.True, DamageTag.Skill);
            Assert.IsTrue(finalHit.Killed);
            Assert.IsFalse(target.IsAlive);
            Assert.AreEqual(1, attacker.Kills);
        }
    }
}
