using System.Collections;
using NUnit.Framework;
using RuneArena.Combat;
using RuneArena.Content;
using RuneArena.Core;
using RuneArena.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace RuneArena.Tests.PlayMode
{
    public sealed class MatchPlayTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (GameServices.Match != null) GameServices.Match.ReturnToMenu();
        }

        [UnityTest]
        public IEnumerator Bootstrap_CreatesGameRootAndMenuInEmptyScene()
        {
            GameRoot root = Bootstrap.EnsureRoot();
            yield return null;
            Assert.IsNotNull(GameRoot.Instance);
            Assert.IsNotNull(root.Ui);
            Assert.IsNotNull(root.Match);
            Assert.IsNotNull(Camera.main);
            Assert.AreEqual(MatchPhase.MainMenu, root.Match.Phase);
        }

        [UnityTest]
        [Timeout(200000)]
        public IEnumerator AllBotMatch_ReachesMatchEnd()
        {
            GameRoot root = Bootstrap.EnsureRoot();
            yield return null;
            var config = new MatchConfig { TeamSize = 3, Seed = 1234, HumanPlayer = false };
            root.Match.StartMatch(config);
            Time.timeScale = 8f;
            float started = Time.realtimeSinceStartup;
            int lastRound = 0;
            while (root.Match.Phase != MatchPhase.MatchEnd && Time.realtimeSinceStartup - started < 150f)
            {
                if (root.Match.Round != lastRound)
                {
                    lastRound = root.Match.Round;
                    Assert.AreEqual(6, root.Match.Units.Count);
                }
                yield return null;
            }
            Time.timeScale = 1f;
            Assert.AreEqual(MatchPhase.MatchEnd, root.Match.Phase, "match did not finish in time");
            Assert.IsTrue(root.Match.MatchWinner.HasValue);
            Assert.GreaterOrEqual(root.Match.Wins(root.Match.MatchWinner.Value), config.RoundsToWin);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator EveryHero_CanCastEverySkill()
        {
            Bootstrap.EnsureRoot();
            yield return null;
            GameServices.Reset();
            GameServices.Rng = new Rng(7);
            GameServices.World = new CombatWorld();
            foreach (HeroDefinition hero in ContentCatalog.Heroes)
            {
                yield return CastAll(hero);
            }
            GameServices.Reset();
        }

        private static IEnumerator CastAll(HeroDefinition hero)
        {
            var casterGo = new GameObject("Caster");
            var targetGo = new GameObject("Target");
            Unit caster = casterGo.AddComponent<Unit>();
            Unit target = targetGo.AddComponent<Unit>();
            caster.Setup(hero, Team.Blue, false, hero.Name);
            target.Setup(ContentCatalog.GetHero("vanguard"), Team.Red, false, "Dummy");
            SkillKey[] keys = { SkillKey.Basic, SkillKey.Q, SkillKey.W, SkillKey.E, SkillKey.R };
            for (int i = 0; i < keys.Length; i++)
            {
                SkillDefinition skill = hero.GetSkill(keys[i]);
                Assert.IsNotNull(skill, hero.Id + " " + keys[i]);
                caster.Motor.SetPosition(new Vector3(0f, 0f, 0f));
                target.Motor.SetPosition(new Vector3(0f, 0f, 1.5f));
                target.Motor.Face(Vector3.forward);
                target.SetHealth(target.MaxHealth);
                target.Status.Clear();
                caster.Caster.ResetCooldowns();
                float before = target.Health;
                Assert.IsTrue(caster.Caster.TryCast(keys[i], Vector3.forward, target.Position), hero.Id + " could not cast " + skill.Id);
                yield return new WaitForSeconds(skill.Windup + skill.Delay + 1.0f);
                if (skill.DealsDamage) Assert.Less(target.Health, before, hero.Id + " " + skill.Id + " dealt no damage");
            }
            Object.Destroy(casterGo);
            Object.Destroy(targetGo);
            yield return null;
        }
    }
}
