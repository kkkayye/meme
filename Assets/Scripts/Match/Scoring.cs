using System.Collections.Generic;
using RuneArena.Combat;
using RuneArena.Core;

namespace RuneArena.Match
{
    /// <summary>Per-round points (kills * 100 + capture seconds * 10) and the timeout tie-break (points → HP% → Red).</summary>
    public sealed class Scoring
    {
        private readonly int[] _kills = new int[2];
        private readonly float[] _capture = new float[2];

        public int Kills(Team team)
        {
            return _kills[(int)team];
        }

        public float CaptureSeconds(Team team)
        {
            return _capture[(int)team];
        }

        /// <summary>kills * PointsPerKill + captureSeconds * PointsPerCaptureSecond.</summary>
        public float RoundPoints(Team team)
        {
            return Kills(team) * GameConstants.PointsPerKill + CaptureSeconds(team) * GameConstants.PointsPerCaptureSecond;
        }

        public void RecordKill(Team team)
        {
            _kills[(int)team]++;
        }

        public void RecordCaptureSeconds(Team team, float seconds)
        {
            if (seconds > 0f) _capture[(int)team] += seconds;
        }

        /// <summary>Clears the current round's counters (round start).</summary>
        public void Reset()
        {
            _kills[0] = 0;
            _kills[1] = 0;
            _capture[0] = 0f;
            _capture[1] = 0f;
        }

        /// <summary>Timeout winner: higher RoundPoints, then higher average current HP%, then GameConstants.TimeoutTieBreakWinner.</summary>
        public Team DecideTimeoutWinner(IReadOnlyList<Unit> units)
        {
            float blue = RoundPoints(Team.Blue);
            float red = RoundPoints(Team.Red);
            if (blue > red) return Team.Blue;
            if (red > blue) return Team.Red;
            float blueHp = AverageHealthFraction(units, Team.Blue);
            float redHp = AverageHealthFraction(units, Team.Red);
            if (blueHp > redHp + 1e-4f) return Team.Blue;
            if (redHp > blueHp + 1e-4f) return Team.Red;
            return GameConstants.TimeoutTieBreakWinner;
        }

        private static float AverageHealthFraction(IReadOnlyList<Unit> units, Team team)
        {
            if (units == null) return 0f;
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                if (u == null || u.Team != team) continue;
                sum += u.IsAlive ? u.HealthFraction : 0f;
                count++;
            }
            return count > 0 ? sum / count : 0f;
        }
    }
}
