using System.Collections.Generic;
using RuneArena.Combat;
using RuneArena.Core;
using UnityEngine;

namespace RuneArena.Match
{
    /// <summary>Phase timers and transitions of a match: RuneDraft → Shop → Countdown → Combat → RoundEnd → (repeat) → MatchEnd. Owned by MatchController.</summary>
    public sealed class MatchPhaseRunner
    {
        private readonly MatchController _match;
        private readonly Dictionary<Unit, float> _botDraftAt = new Dictionary<Unit, float>();
        private float _elapsed;
        private Team? _pendingWinner;

        public MatchPhaseRunner(MatchController match)
        {
            _match = match ?? throw new System.ArgumentNullException(nameof(match));
        }

        private IReadOnlyList<Unit> Units => _match.Units;
        private Unit Human => _match.HumanUnit;

        public void Tick(float dt)
        {
            _elapsed += dt;
            _match.PhaseTimeRemaining = Mathf.Max(0f, _match.PhaseTimeRemaining - dt);
            switch (_match.Phase)
            {
                case MatchPhase.RuneDraft: TickDraft(); break;
                case MatchPhase.Shop: TickShop(); break;
                case MatchPhase.Countdown: if (_match.PhaseTimeRemaining <= 0f) EnterCombat(); break;
                case MatchPhase.Combat: TickCombat(dt); break;
                case MatchPhase.RoundEnd: if (_match.PhaseTimeRemaining <= 0f) AfterRoundEnd(); break;
            }
        }

        /// <summary>Ends the current combat round in favour of a team at the next tick (tower destroyed).</summary>
        public void RequestRoundEnd(Team winner)
        {
            if (_match.Phase == MatchPhase.Combat) _pendingWinner = winner;
        }

        public void EnterRuneDraft()
        {
            _match.Round++;
            StartTimer(GameConstants.DraftSeconds);
            GameServices.Draft.BeginDraft(_match.Round);
            _botDraftAt.Clear();
            Rng rng = GameServices.Rng.Fork("draft" + _match.Round);
            for (int i = 0; i < Units.Count; i++)
            {
                if (!Units[i].IsHuman) _botDraftAt[Units[i]] = rng.Range(GameConstants.BotDraftDelayMin, GameConstants.BotDraftDelayMax);
            }
            _match.SetPhase(MatchPhase.RuneDraft);
            if (Human != null) _match.Ui?.ShowDraft(Human);
            else _match.Ui?.HideOverlays();
        }

        private void TickDraft()
        {
            RuneDraftServiceTick();
            if (GameServices.Draft.AllPicked || _match.PhaseTimeRemaining <= 0f)
            {
                GameServices.Draft.AutoPickRemaining();
                GameServices.Draft.EndDraft();
                EnterShop();
            }
        }

        private void RuneDraftServiceTick()
        {
            foreach (KeyValuePair<Unit, float> pair in _botDraftAt)
            {
                Unit bot = pair.Key;
                if (_elapsed < pair.Value || bot == null || GameServices.Draft.HasPicked(bot)) continue;
                int index = GameServices.Draft.BestIndexFor(bot);
                if (index >= 0) GameServices.Draft.Pick(bot, index);
            }
        }

        private void EnterShop()
        {
            StartTimer(GameConstants.ShopSeconds);
            GameServices.Shop.OpenShop(_match.Round);
            for (int i = 0; i < Units.Count; i++)
            {
                if (!Units[i].IsHuman) GameServices.Shop.AutoShop(Units[i]);
            }
            _match.SetPhase(MatchPhase.Shop);
            if (Human != null) _match.Ui?.ShowShop(Human);
            else _match.Ui?.HideOverlays();
        }

        private void TickShop()
        {
            if (GameServices.Shop.AllReady || _match.PhaseTimeRemaining <= 0f)
            {
                GameServices.Shop.CloseShop();
                EnterCountdown();
            }
        }

        private void EnterCountdown()
        {
            StartTimer(GameConstants.CountdownSeconds);
            Arena arena = _match.Arena;
            int blueIndex = 0;
            int redIndex = 0;
            for (int i = 0; i < Units.Count; i++)
            {
                Unit unit = Units[i];
                int index = unit.Team == Team.Blue ? blueIndex++ : redIndex++;
                unit.ResetForRound(arena.SpawnPoint(unit.Team, index));
                unit.Motor.Face(unit.Team == Team.Blue ? Vector3.right : Vector3.left);
                unit.Motor.Locked = true;
            }
            _match.ControlPoint.ResetPoint();
            _match.Chests.ClearAll();
            _match.Chests.ResetTimer();
            GameServices.Scoring.Reset();
            if (CombatFx.Exists) CombatFx.Instance.ClearAll();
            _pendingWinner = null;
            _match.Lane?.BeginRound(_match.Round);
            _match.SetPhase(MatchPhase.Countdown);
            _match.Ui?.HideOverlays();
            _match.Ui?.ShowHud(_match.SpectatedUnit);
            _match.Camera?.Follow(_match.SpectatedUnit);
            _match.Camera?.SnapToTarget();
        }

        private void EnterCombat()
        {
            StartTimer(_match.Config.RoundSeconds);
            for (int i = 0; i < Units.Count; i++) Units[i].Motor.Locked = false;
            _match.SetPhase(MatchPhase.Combat);
            EventBus.Publish(new RoundStarted(_match.Round));
        }

        private void TickCombat(float dt)
        {
            if (_pendingWinner.HasValue)
            {
                Team winner = _pendingWinner.Value;
                _pendingWinner = null;
                EnterRoundEnd(winner);
                return;
            }
            GameServices.Economy.TickPassive(dt);
            _match.ControlPoint.Tick(dt);
            _match.Chests.Tick(dt);
            _match.Lane?.Tick(dt);
            CombatWorld world = GameServices.World;
            bool blueAlive = world.AnyAlive(Team.Blue);
            bool redAlive = world.AnyAlive(Team.Red);
            if (blueAlive && !redAlive) EnterRoundEnd(Team.Blue);
            else if (redAlive && !blueAlive) EnterRoundEnd(Team.Red);
            else if (!blueAlive && !redAlive) EnterRoundEnd(GameServices.Scoring.DecideTimeoutWinner(Units));
            else if (_match.PhaseTimeRemaining <= 0f) EnterRoundEnd(GameServices.Scoring.DecideTimeoutWinner(Units));
        }

        private void EnterRoundEnd(Team winner)
        {
            StartTimer(GameConstants.RoundEndSeconds);
            _match.AddWin(winner);
            for (int i = 0; i < Units.Count; i++)
            {
                Unit unit = Units[i];
                unit.AddGold(unit.Team == winner ? GameConstants.RoundWinGold : GameConstants.RoundLossGold);
                unit.Motor.Locked = true;
                unit.Caster.CancelCast();
            }
            _match.SetPhase(MatchPhase.RoundEnd);
            EventBus.Publish(new RoundEnded(_match.Round, winner));
            _match.Ui?.ShowRoundEnd(winner, _match.Round);
        }

        private void AfterRoundEnd()
        {
            Team? winner = null;
            if (_match.Wins(Team.Blue) >= _match.Config.RoundsToWin) winner = Team.Blue;
            else if (_match.Wins(Team.Red) >= _match.Config.RoundsToWin) winner = Team.Red;
            if (winner == null)
            {
                EnterRuneDraft();
                return;
            }
            _match.MatchWinner = winner;
            _match.PhaseTimeRemaining = 0f;
            _match.SetPhase(MatchPhase.MatchEnd);
            EventBus.Publish(new MatchEnded(winner.Value));
            _match.Ui?.ShowMatchEnd(winner.Value);
        }

        private void StartTimer(float seconds)
        {
            _match.PhaseTimeRemaining = seconds;
            _elapsed = 0f;
        }
    }
}
