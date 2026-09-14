using System.Collections.Generic;
using RuneArena.Combat;
using RuneArena.Content;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Tests.EditMode
{
    /// <summary>Creates throwaway units for service tests and destroys them afterwards.</summary>
    public sealed class UnitTestHelper
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        public Unit Create(string heroId, Team team, bool human = false)
        {
            HeroDefinition hero = ContentCatalog.GetHero(heroId) ?? ContentCatalog.Heroes[0];
            var go = new GameObject("TestUnit_" + heroId);
            _objects.Add(go);
            Unit unit = go.AddComponent<Unit>();
            unit.Setup(hero, team, human, heroId);
            return unit;
        }

        /// <summary>Creates a unit from an explicit definition (minions, towers).</summary>
        public Unit CreateDefinition(HeroDefinition definition, Team team)
        {
            var go = new GameObject("TestUnit_" + definition.Id);
            _objects.Add(go);
            Unit unit = go.AddComponent<Unit>();
            unit.Setup(definition, team, false, definition.Name);
            return unit;
        }

        public void Cleanup()
        {
            for (int i = 0; i < _objects.Count; i++)
            {
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            }
            _objects.Clear();
        }
    }
}
