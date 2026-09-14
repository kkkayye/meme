using System.Collections.Generic;
using RuneArena.AI;
using RuneArena.Combat;
using RuneArena.Content;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Match
{
    /// <summary>Owns the lane objectives of a round: one tower per team and periodic minion waves (scaled by round). MatchController calls BeginRound / Tick / ClearAll.</summary>
    public sealed class LaneController : MonoBehaviour
    {
        private const string ScalingSource = "wave_scaling";

        private readonly List<Unit> _minions = new List<Unit>();
        private readonly Unit[] _towers = new Unit[2];
        private Transform _root;
        private int _round;

        public IReadOnlyList<Unit> Minions => _minions;
        public int Wave { get; private set; }
        public float TimeToNextWave { get; private set; }
        public bool IsActive { get; private set; }

        public Unit Tower(Team team)
        {
            Unit tower = _towers[(int)team];
            return tower != null && tower.IsAlive ? tower : null;
        }

        /// <summary>Clears the previous round's lane, spawns fresh towers and arms the first wave.</summary>
        public void BeginRound(int round)
        {
            ClearAll();
            _round = Mathf.Max(1, round);
            Wave = 0;
            TimeToNextWave = GameConstants.MinionFirstWaveDelay;
            IsActive = true;
            EnsureRoot();
            SpawnTower(Team.Blue);
            SpawnTower(Team.Red);
        }

        /// <summary>Advances the wave timer; MatchController calls this only during Combat.</summary>
        public void Tick(float dt)
        {
            if (!IsActive || dt <= 0f) return;
            _minions.RemoveAll(m => m == null);
            TimeToNextWave -= dt;
            if (TimeToNextWave > 0f) return;
            TimeToNextWave = GameConstants.MinionWaveInterval;
            SpawnWave();
        }

        /// <summary>Destroys every minion and tower (round end / match teardown).</summary>
        public void ClearAll()
        {
            IsActive = false;
            for (int i = 0; i < _minions.Count; i++)
            {
                if (_minions[i] != null) Destroy(_minions[i].gameObject);
            }
            _minions.Clear();
            for (int i = 0; i < _towers.Length; i++)
            {
                if (_towers[i] != null) Destroy(_towers[i].gameObject);
                _towers[i] = null;
            }
        }

        private void EnsureRoot()
        {
            if (_root != null) return;
            _root = new GameObject("Lane").transform;
            _root.SetParent(transform, false);
        }

        private void SpawnTower(Team team)
        {
            Arena arena = GameServices.Arena;
            Vector3 position = arena != null ? arena.TowerPosition(team) : new Vector3(team == Team.Blue ? -GameConstants.TowerX : GameConstants.TowerX, 0f, 0f);
            Unit tower = Create(MinionCatalog.Tower, team, position, "防御塔 " + (team == Team.Blue ? "Blue" : "Red"));
            tower.Motor.Locked = true;
            tower.Motor.Face(Arena.LaneDirection(team));
            TowerBrain.Attach(tower);
            _towers[(int)team] = tower;
        }

        private void SpawnWave()
        {
            Wave++;
            int perTeam = GameConstants.MinionMeleePerWave + GameConstants.MinionRangedPerWave;
            for (int t = 0; t < 2; t++)
            {
                Team team = (Team)t;
                for (int i = 0; i < perTeam; i++)
                {
                    HeroDefinition def = i < GameConstants.MinionMeleePerWave ? MinionCatalog.Melee : MinionCatalog.Ranged;
                    Vector3 position = GameServices.Arena != null ? GameServices.Arena.MinionSpawnPoint(team, i, perTeam) : Vector3.zero;
                    Unit minion = Create(def, team, position, def.Name);
                    ApplyScaling(minion);
                    minion.Motor.Face(Arena.LaneDirection(team));
                    MinionBrain.Attach(minion);
                    _minions.Add(minion);
                }
            }
            EventBus.Publish(new MinionWaveSpawned(_round, Wave));
        }

        private void ApplyScaling(Unit minion)
        {
            float bonus = GameConstants.MinionScalingPerRound * (_round - 1);
            if (bonus <= 0f) return;
            minion.Stats.AddModifier(StatModifier.PercentOf(StatType.MaxHealth, bonus, ScalingSource));
            minion.Stats.AddModifier(StatModifier.PercentOf(StatType.AttackDamage, bonus, ScalingSource));
            minion.SetHealth(minion.MaxHealth);
        }

        private Unit Create(HeroDefinition def, Team team, Vector3 position, string name)
        {
            EnsureRoot();
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.position = position;
            Unit unit = go.AddComponent<Unit>();
            unit.Setup(def, team, false, name);
            return unit;
        }

        private void OnDestroy()
        {
            ClearAll();
        }
    }
}
