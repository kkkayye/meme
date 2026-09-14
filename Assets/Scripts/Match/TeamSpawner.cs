using System;
using System.Collections.Generic;
using RuneArena.AI;
using RuneArena.Combat;
using RuneArena.Content;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Match
{
    /// <summary>Creates the units of both teams: the human (Blue index 0 when enabled) with the configured hero, bots with heroes chosen round-robin from a random start.</summary>
    public static class TeamSpawner
    {
        /// <summary>Spawns both teams under 'parent'. Returns every unit; 'human' is the human unit or null.</summary>
        public static List<Unit> Spawn(MatchConfig config, Arena arena, Rng rng, Transform parent, out Unit human)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (arena == null) throw new ArgumentNullException(nameof(arena));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            IReadOnlyList<HeroDefinition> heroes = ContentCatalog.Heroes;
            if (heroes.Count == 0) throw new InvalidOperationException("ContentCatalog has no heroes.");
            human = null;
            var units = new List<Unit>();
            int size = config.ClampedTeamSize;
            int heroCursor = rng.Range(0, heroes.Count);
            for (int t = 0; t < 2; t++)
            {
                Team team = (Team)t;
                for (int i = 0; i < size; i++)
                {
                    bool isHuman = config.HumanPlayer && team == Team.Blue && i == 0;
                    HeroDefinition hero = isHuman ? HumanHero(config, heroes) : heroes[heroCursor++ % heroes.Count];
                    Unit unit = Create(hero, team, isHuman, i, arena, parent);
                    if (isHuman) human = unit;
                    else BotBrain.Attach(unit);
                    units.Add(unit);
                }
            }
            return units;
        }

        private static HeroDefinition HumanHero(MatchConfig config, IReadOnlyList<HeroDefinition> heroes)
        {
            HeroDefinition hero = ContentCatalog.GetHero(config.PlayerHeroId);
            return hero ?? heroes[0];
        }

        private static Unit Create(HeroDefinition hero, Team team, bool isHuman, int index, Arena arena, Transform parent)
        {
            var go = new GameObject("Unit");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = arena.SpawnPoint(team, index);
            go.transform.rotation = Quaternion.LookRotation(team == Team.Blue ? Vector3.right : Vector3.left, Vector3.up);
            Unit unit = go.AddComponent<Unit>();
            string name = isHuman ? hero.Name : hero.Name + " " + (team == Team.Blue ? "B" : "R") + (index + 1);
            unit.Setup(hero, team, isHuman, name);
            return unit;
        }
    }
}
