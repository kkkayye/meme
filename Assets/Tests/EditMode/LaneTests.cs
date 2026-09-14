using NUnit.Framework;
using RuneArena.Combat;
using RuneArena.Content;
using RuneArena.Core;
using RuneArena.Match;
using UnityEngine;

namespace RuneArena.Tests.EditMode
{
    public sealed class LaneTests
    {
        private UnitTestHelper _units;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            GameServices.Reset();
            GameServices.Rng = new Rng(5);
            GameServices.World = new CombatWorld();
            GameServices.Economy = new Economy();
            GameServices.Scoring = new Scoring();
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
        public void MinionCatalog_DefinitionsAreConsistent()
        {
            Assert.AreEqual(UnitKind.Minion, MinionCatalog.Melee.Kind);
            Assert.AreEqual(UnitKind.Minion, MinionCatalog.Ranged.Kind);
            Assert.AreEqual(UnitKind.Tower, MinionCatalog.Tower.Kind);
            Assert.IsNotNull(MinionCatalog.Melee.BasicAttack);
            Assert.IsNotNull(MinionCatalog.Ranged.BasicAttack);
            Assert.IsNotNull(MinionCatalog.Tower.BasicAttack);
            Assert.AreEqual(0, MinionCatalog.Tower.Skills.Count);
            Assert.IsNull(MinionCatalog.Tower.GetSkill(SkillKey.Q));
            Assert.AreEqual(GameConstants.MinionGoldMelee, MinionCatalog.GoldFor(MinionCatalog.Melee));
            Assert.AreEqual(GameConstants.MinionGoldRanged, MinionCatalog.GoldFor(MinionCatalog.Ranged));
            Assert.AreEqual(GameConstants.TowerHealth, MinionCatalog.Tower.BaseStat(StatType.MaxHealth));
        }

        [Test]
        public void Tower_OnlyTakesBasicAttackDamageAndIgnoresStatuses()
        {
            Unit attacker = _units.Create("blaze", Team.Blue);
            Unit tower = _units.CreateDefinition(MinionCatalog.Tower, Team.Red);
            Assert.IsTrue(tower.IsTower);
            Assert.IsTrue(tower.IsStatic);
            Assert.AreEqual(GameConstants.ObstacleLayer, tower.gameObject.layer);
            DamageResult skill = DamagePipeline.Deal(attacker, tower, 500f, DamageType.Magical, DamageTag.Skill);
            Assert.AreEqual(0f, skill.Total, 1e-3f, "skills must not damage towers");
            DamageResult basic = DamagePipeline.Deal(attacker, tower, 100f, DamageType.Physical, DamageTag.Basic);
            Assert.Greater(basic.Dealt, 0f);
            Assert.AreEqual(basic.Total, GameServices.Scoring.TowerDamage(Team.Blue), 1e-3f);
            Assert.IsNull(tower.Status.Apply(StatusType.Stun, 1f, 2f));
            Assert.IsFalse(tower.Status.IsStunned);
            tower.Motor.BeginKnockback(Vector3.right, 3f);
            Assert.IsFalse(tower.Motor.IsDashing);
        }

        [Test]
        public void Scoring_CountsMinionKillsAndTowerDamage()
        {
            var scoring = new Scoring();
            scoring.RecordMinionKill(Team.Red);
            scoring.RecordMinionKill(Team.Red);
            scoring.RecordTowerDamage(Team.Blue, 1000f);
            Assert.AreEqual(2 * GameConstants.PointsPerMinionKill, scoring.RoundPoints(Team.Red), 1e-3f);
            Assert.AreEqual(1000f * GameConstants.PointsPerTowerDamage, scoring.RoundPoints(Team.Blue), 1e-3f);
            scoring.Reset();
            Assert.AreEqual(0f, scoring.RoundPoints(Team.Red), 1e-3f);
        }

        [Test]
        public void World_WipeChecksIgnoreMinionsAndTowers()
        {
            Unit hero = _units.Create("shade", Team.Blue);
            _units.CreateDefinition(MinionCatalog.Melee, Team.Blue);
            _units.CreateDefinition(MinionCatalog.Tower, Team.Blue);
            Assert.AreEqual(1, GameServices.World.CountAlive(Team.Blue));
            Assert.AreEqual(1, GameServices.World.CountAliveOfKind(Team.Blue, UnitKind.Minion));
            Assert.IsNotNull(GameServices.World.TowerOf(Team.Blue));
            hero.Kill(null);
            Assert.IsFalse(GameServices.World.AnyAlive(Team.Blue), "a dead hero with living minions is a wipe");
            Assert.AreEqual(1, GameServices.World.AllHeroes().Count);
        }

        [Test]
        public void MinionKill_DoesNotCountAsHeroKill()
        {
            Unit hero = _units.Create("vanguard", Team.Blue);
            Unit minion = _units.CreateDefinition(MinionCatalog.Melee, Team.Red);
            minion.Kill(hero);
            Assert.AreEqual(0, hero.Kills);
            Unit enemy = _units.Create("blaze", Team.Red);
            enemy.Kill(hero);
            Assert.AreEqual(1, hero.Kills);
        }
    }
}
