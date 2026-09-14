using RuneArena.Combat;
using RuneArena.Core;
using RuneArena.Match;
using UnityEngine;

namespace RuneArena.AI
{
    /// <summary>Lane minion AI: marches toward the enemy tower/base, fights enemy minions first, then the tower, then nearby heroes.</summary>
    public sealed class MinionBrain : MonoBehaviour
    {
        private const float AttackSlack = 0.25f;
        private const float LeashFactor = 1.6f;

        private float _tick;
        private Vector3 _moveDir;
        private Vector3 _faceDir;

        public Unit Owner { get; private set; }
        public Unit Target { get; private set; }

        public static MinionBrain Attach(Unit unit)
        {
            if (unit == null) throw new System.ArgumentNullException(nameof(unit));
            MinionBrain brain = unit.GetComponent<MinionBrain>();
            if (brain == null) brain = unit.gameObject.AddComponent<MinionBrain>();
            brain.Owner = unit;
            return brain;
        }

        private void Update()
        {
            if (Owner == null || !Owner.IsAlive) return;
            MatchController match = GameServices.Match;
            if (match == null || match.Phase != MatchPhase.Combat || match.IsPaused) return;
            _tick -= Time.deltaTime;
            if (_tick <= 0f)
            {
                _tick = GameConstants.MinionTickInterval;
                Decide();
            }
            if (!Owner.CanAct)
            {
                Owner.Motor.Move(Vector3.zero);
                return;
            }
            Owner.Motor.Move(_moveDir);
            if (_faceDir.sqrMagnitude > 1e-4f && !Owner.Motor.IsDashing) Owner.Motor.Face(_faceDir);
        }

        private void Decide()
        {
            CombatWorld world = GameServices.World;
            if (world == null) return;
            Target = PickTarget(world);
            if (Target == null)
            {
                March(world);
                return;
            }
            float range = Owner.Stats.Get(StatType.AttackRange);
            float dist = Steering.FlatDistance(Owner.Position, Target.Position) - Target.BodyRadius;
            Vector3 to = Target.Position - Owner.Position;
            _faceDir = to;
            if (dist <= range + AttackSlack)
            {
                _moveDir = Vector3.zero;
                if (!Owner.Caster.IsCasting) Owner.Caster.TryCast(SkillKey.Basic, to, Target.Position);
                return;
            }
            _moveDir = Steering.Avoid(Owner, to);
        }

        private Unit PickTarget(CombatWorld world)
        {
            if (Target != null && Target.IsAlive && !Target.IsInvisible
                && Steering.FlatDistance(Owner.Position, Target.Position) <= GameConstants.MinionAggroRange * LeashFactor)
            {
                return Target;
            }
            Unit minion = world.NearestEnemyOfKind(Owner.Position, Owner.Team, UnitKind.Minion, GameConstants.MinionAggroRange);
            if (minion != null) return minion;
            float range = Owner.Stats.Get(StatType.AttackRange);
            Unit tower = world.NearestEnemyOfKind(Owner.Position, Owner.Team, UnitKind.Tower, range + GameConstants.TowerRadius + 1.5f, false);
            if (tower != null) return tower;
            return world.NearestEnemyOfKind(Owner.Position, Owner.Team, UnitKind.Hero, GameConstants.MinionHeroAggroRange);
        }

        private void March(CombatWorld world)
        {
            Team enemy = Owner.Team == Team.Blue ? Team.Red : Team.Blue;
            Unit tower = world.TowerOf(enemy);
            Vector3 goal = tower != null ? tower.Position : (GameServices.Arena != null ? GameServices.Arena.BasePosition(enemy) : Vector3.zero);
            Vector3 dir = Steering.Toward(Owner, goal, 1.5f);
            _faceDir = dir;
            _moveDir = Steering.Avoid(Owner, dir);
        }
    }
}
