using System;
using System.Collections.Generic;
using RuneArena.Core;
using RuneArena.Loot;
using RuneArena.Match;
using UnityEngine;

namespace RuneArena.Combat
{
    /// <summary>Registry of units and chests plus allocation-free spatial queries. Walkability delegates to the Arena.</summary>
    public sealed class CombatWorld
    {
        private readonly List<Unit> _units = new List<Unit>();
        private readonly List<Chest> _chests = new List<Chest>();

        /// <summary>Set by MatchController after Arena.Build(). Null-safe: without an arena everything is walkable.</summary>
        public Arena Arena { get; set; }

        public IReadOnlyList<Unit> Units => _units;
        public IReadOnlyList<Chest> Chests => _chests;

        public void Register(Unit unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (!_units.Contains(unit)) _units.Add(unit);
        }

        public void Unregister(Unit unit)
        {
            if (unit == null) return;
            _units.Remove(unit);
        }

        public void RegisterChest(Chest chest)
        {
            if (chest == null) throw new ArgumentNullException(nameof(chest));
            if (!_chests.Contains(chest)) _chests.Add(chest);
        }

        public void UnregisterChest(Chest chest)
        {
            if (chest == null) return;
            _chests.Remove(chest);
        }

        public void Clear()
        {
            _units.Clear();
            _chests.Clear();
        }

        /// <summary>All units of a team, alive or dead.</summary>
        public IEnumerable<Unit> UnitsOfTeam(Team team)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                if (_units[i].Team == team) yield return _units[i];
            }
        }

        /// <summary>Living units NOT on the given team.</summary>
        public IEnumerable<Unit> EnemiesOf(Team team)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                Unit u = _units[i];
                if (u.Team != team && u.IsAlive) yield return u;
            }
        }

        /// <summary>Living units on the given team.</summary>
        public IEnumerable<Unit> AlliesOf(Team team)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                Unit u = _units[i];
                if (u.Team == team && u.IsAlive) yield return u;
            }
        }

        public int CountAlive(Team team)
        {
            int count = 0;
            for (int i = 0; i < _units.Count; i++)
            {
                if (_units[i].Team == team && _units[i].IsAlive) count++;
            }
            return count;
        }

        public bool AnyAlive(Team team)
        {
            return CountAlive(team) > 0;
        }

        /// <summary>Average current HP fraction of a team (dead units count as 0). Used for the timeout tie-break.</summary>
        public float AverageHealthFraction(Team team)
        {
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < _units.Count; i++)
            {
                if (_units[i].Team != team) continue;
                sum += _units[i].IsAlive ? _units[i].HealthFraction : 0f;
                count++;
            }
            return count > 0 ? sum / count : 0f;
        }

        /// <summary>Nearest living enemy of 'from' within maxRange (center distance). Invisible enemies are skipped when ignoreInvisible.</summary>
        public Unit NearestEnemy(Unit from, float maxRange, bool ignoreInvisible = true)
        {
            if (from == null) throw new ArgumentNullException(nameof(from));
            return NearestEnemyTo(from.Position, from.Team, maxRange, ignoreInvisible);
        }

        public Unit NearestEnemyTo(Vector3 position, Team enemyOf, float maxRange, bool ignoreInvisible = true)
        {
            Unit best = null;
            float bestSqr = maxRange * maxRange;
            for (int i = 0; i < _units.Count; i++)
            {
                Unit u = _units[i];
                if (u.Team == enemyOf || !u.IsAlive) continue;
                if (ignoreInvisible && u.IsInvisible) continue;
                float sqr = FlatSqrDistance(position, u.Position);
                if (sqr > bestSqr) continue;
                bestSqr = sqr;
                best = u;
            }
            return best;
        }

        /// <summary>Living enemies whose body (HeroRadius) overlaps the circle. Includes invisible units. Appends to results.</summary>
        public void EnemiesInRadius(Vector3 center, float radius, Team enemyOf, List<Unit> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            float reach = radius + GameConstants.HeroRadius;
            float reachSqr = reach * reach;
            for (int i = 0; i < _units.Count; i++)
            {
                Unit u = _units[i];
                if (u.Team == enemyOf || !u.IsAlive) continue;
                if (FlatSqrDistance(center, u.Position) <= reachSqr) results.Add(u);
            }
        }

        /// <summary>Living allies (including the unit at center, if any) within the circle. Appends to results.</summary>
        public void AlliesInRadius(Vector3 center, float radius, Team team, List<Unit> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            float reach = radius + GameConstants.HeroRadius;
            float reachSqr = reach * reach;
            for (int i = 0; i < _units.Count; i++)
            {
                Unit u = _units[i];
                if (u.Team != team || !u.IsAlive) continue;
                if (FlatSqrDistance(center, u.Position) <= reachSqr) results.Add(u);
            }
        }

        /// <summary>Living enemies inside a cone (total arc angleDeg) of the given range. Appends to results.</summary>
        public void EnemiesInCone(Vector3 origin, Vector3 dir, float range, float angleDeg, Team enemyOf, List<Unit> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            dir.Normalize();
            float reach = range + GameConstants.HeroRadius;
            float reachSqr = reach * reach;
            float cosHalf = Mathf.Cos(angleDeg * 0.5f * Mathf.Deg2Rad);
            for (int i = 0; i < _units.Count; i++)
            {
                Unit u = _units[i];
                if (u.Team == enemyOf || !u.IsAlive) continue;
                Vector3 to = u.Position - origin;
                to.y = 0f;
                float sqr = to.sqrMagnitude;
                if (sqr > reachSqr) continue;
                if (sqr < 1e-6f || Vector3.Dot(dir, to.normalized) >= cosHalf) results.Add(u);
            }
        }

        /// <summary>First living enemy whose body lies within hitRadius of the segment from→to, ordered by distance along the segment. Null if none.</summary>
        public Unit FirstEnemyAlongSegment(Vector3 from, Vector3 to, float hitRadius, Team enemyOf, bool ignoreInvisible = false)
        {
            Vector3 seg = to - from;
            seg.y = 0f;
            float length = seg.magnitude;
            if (length < 1e-6f) return null;
            Vector3 dir = seg / length;
            float reach = hitRadius + GameConstants.HeroRadius;
            Unit best = null;
            float bestT = float.MaxValue;
            for (int i = 0; i < _units.Count; i++)
            {
                Unit u = _units[i];
                if (u.Team == enemyOf || !u.IsAlive) continue;
                if (ignoreInvisible && u.IsInvisible) continue;
                Vector3 rel = u.Position - from;
                rel.y = 0f;
                float t = Mathf.Clamp(Vector3.Dot(rel, dir), 0f, length);
                Vector3 closest = dir * t;
                if ((rel - closest).sqrMagnitude > reach * reach || t >= bestT) continue;
                bestT = t;
                best = u;
            }
            return best;
        }

        /// <summary>Every living enemy whose body lies within hitRadius of the segment from→to (dash sweeps). Appends to results.</summary>
        public void EnemiesAlongSegment(Vector3 from, Vector3 to, float hitRadius, Team enemyOf, List<Unit> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            Vector3 seg = to - from;
            seg.y = 0f;
            float length = seg.magnitude;
            Vector3 dir = length > 1e-6f ? seg / length : Vector3.zero;
            float reach = hitRadius + GameConstants.HeroRadius;
            float reachSqr = reach * reach;
            for (int i = 0; i < _units.Count; i++)
            {
                Unit u = _units[i];
                if (u.Team == enemyOf || !u.IsAlive) continue;
                Vector3 rel = u.Position - from;
                rel.y = 0f;
                float t = length > 1e-6f ? Mathf.Clamp(Vector3.Dot(rel, dir), 0f, length) : 0f;
                if ((rel - dir * t).sqrMagnitude <= reachSqr) results.Add(u);
            }
        }

        /// <summary>Nearest unopened chest within maxRange, or null.</summary>
        public Chest NearestChest(Vector3 position, float maxRange)
        {
            Chest best = null;
            float bestSqr = maxRange * maxRange;
            for (int i = 0; i < _chests.Count; i++)
            {
                Chest c = _chests[i];
                if (c == null || c.IsOpened) continue;
                float sqr = FlatSqrDistance(position, c.Position);
                if (sqr > bestSqr) continue;
                bestSqr = sqr;
                best = c;
            }
            return best;
        }

        public bool IsWalkable(Vector3 position)
        {
            return Arena == null || Arena.IsWalkable(position);
        }

        public Vector3 ClampToArena(Vector3 position)
        {
            return Arena == null ? position : Arena.ClampToArena(position);
        }

        public static float FlatSqrDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
